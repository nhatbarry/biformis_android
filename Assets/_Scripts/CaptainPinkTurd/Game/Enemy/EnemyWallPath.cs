using System.Collections.Generic;
using UnityEngine;
using CaptainPinkTurd.Game.Player;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>Small, bounded local search for approaching around room walls and corners.</summary>
    internal sealed class EnemyWallPath
    {
        private const float Cell = 0.6f;
        private readonly List<Vector2Int> open = new();
        private readonly Dictionary<Vector2Int,float> costs = new();
        private readonly Dictionary<Vector2Int,Vector2Int> parents = new();
        private readonly HashSet<Vector2Int> closed = new();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        private readonly List<Vector2> route = new();
        private ContactFilter2D filter;
        private float radius;
        private Vector2 footOffset;
        private int waypoint;
        private float nextSearch;
        private Rigidbody2D playerBody;
        private static readonly Vector2Int[] Steps = {
            new(1,0),new(-1,0),new(0,1),new(0,-1),new(1,1),new(1,-1),new(-1,1),new(-1,-1)
        };

        public EnemyWallPath(CircleCollider2D feet,LayerMask blockers)
        {
            radius=Mathf.Max(0.05f,feet.radius*Mathf.Max(feet.transform.lossyScale.x,feet.transform.lossyScale.y)-0.01f);
            footOffset=feet.offset;
            filter=new ContactFilter2D {useTriggers=false};filter.SetLayerMask(blockers);
            var player=Object.FindAnyObjectByType<PlayerUnit>();
            playerBody=player ? player.GetComponent<Rigidbody2D>() : null;
        }
        public void Reset(){route.Clear();nextSearch=0f;waypoint=0;}
        public void Bind(Rigidbody2D player){playerBody=player;Reset();}
        public bool Clear(Vector2 from,Vector2 to)
        {
            Vector2 delta=to-from;
            int count=Physics2D.CircleCast(from+footOffset,radius,delta.normalized,filter,hits,delta.magnitude);
            for(int i=0;i<count;i++)
                if(hits[i].collider.attachedRigidbody!=playerBody) return false;
            return true;
        }
        public Vector2 Direction(Vector2 from,Vector2 target,float reach)
        {
            if(Clear(from,target)) {route.Clear();return (target-from).normalized;}
            if(Time.time>=nextSearch || waypoint>=route.Count)
            {
                if(Time.time<nextSearch && route.Count==0) return Vector2.zero;
                Build(from,target,reach);
                nextSearch=Time.time+0.8f;
            }
            while(waypoint<route.Count && Vector2.Distance(from,route[waypoint])<0.18f)waypoint++;
            return waypoint<route.Count ? (route[waypoint]-from).normalized : Vector2.zero;
        }
        private void Build(Vector2 origin,Vector2 target,float reach)
        {
            route.Clear();waypoint=0;open.Clear();costs.Clear();parents.Clear();closed.Clear();
            Vector2Int goal=Vector2Int.RoundToInt((target-origin)/Cell);
            var start=Vector2Int.zero;open.Add(start);costs[start]=0;
            int minX=Mathf.Min(0,goal.x)-6,maxX=Mathf.Max(0,goal.x)+6;
            int minY=Mathf.Min(0,goal.y)-6,maxY=Mathf.Max(0,goal.y)+6;
            Vector2 World(Vector2Int node)=>origin+(Vector2)node*Cell;
            for(int visited=0;open.Count>0 && visited<384;visited++)
            {
                int best=0;float score=float.MaxValue;
                for(int i=0;i<open.Count;i++)
                {
                    float value=costs[open[i]]+Vector2.Distance(open[i],goal)*Cell;
                    if(value<score){score=value;best=i;}
                }
                var node=open[best];open.RemoveAt(best);closed.Add(node);
                var position=World(node);
                if(Vector2.Distance(position,target)<=reach*0.75f && Clear(position,target))
                {
                    while(node!=start){route.Add(World(node));node=parents[node];}
                    route.Reverse();return;
                }
                foreach(var step in Steps)
                {
                    var next=node+step;
                    if(next.x<minX || next.x>maxX || next.y<minY || next.y>maxY || closed.Contains(next))continue;
                    float cost=costs[node]+step.magnitude*Cell;
                    if(costs.TryGetValue(next,out float previous) && cost>=previous)continue;
                    if(!Clear(position,World(next)))continue;
                    if(!costs.ContainsKey(next))open.Add(next);
                    costs[next]=cost;parents[next]=node;
                }
            }
        }
    }
}
