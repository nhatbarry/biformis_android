#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.Core.Utilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using System.IO;
using CaptainPinkTurd.Game;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.UnitSystem;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Scene;
using CaptainPinkTurd.SpawnSystem;
using CaptainPinkTurd.BulletHell;
using UnityEditor;

namespace CaptainPinkTurd.Story.Tests
{
    public class GameplayRepairTests
    {
        [UnityTest]
        public IEnumerator CorridorPlayerIsVisibleAndCanMove()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 4");
            yield return new WaitForSecondsRealtime(0.5f);
            var oldPlayer=Object.FindAnyObjectByType<PlayerUnit>();
            oldPlayer.GetComponent<UnitHealth>().TakeDamage(new SDamageData(4,oldPlayer.gameObject));
            GameManager.Instance.LockDimension(EColor.Blue);
            yield return new WaitForSecondsRealtime(1f);
            SceneController.Instance.NewTransition().Load(SceneDatabase.Slots.SessionContent,"Level Story Corridor",true).Perform();
            yield return StoryTestLoading.WaitFor(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Level Story Corridor",30f,"corridor transition");
            yield return new WaitForSecondsRealtime(2f);
            var p=Object.FindAnyObjectByType<PlayerUnit>();
            var cam=Camera.main;
            cam.aspect=2.4f; // Cinemachine uses its uncorrected lens when solving the confiner on wide phones.
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(6,p.GetComponent<UnitHealth>().CurrentHealth,"carried health");
            foreach(var colour in new[]{ EColor.Red,EColor.Blue })
            {
                GameManager.Instance.LockDimension(colour);
                var wallBox=p.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Player Sprite Collider");
                Assert.AreEqual(LayerMask.NameToLayer("Player Collider"),wallBox.gameObject.layer);
                Vector2 before=p.transform.position;
                var thumb=TouchHud.PushStick(Vector2.up);
                yield return new WaitForSeconds(1f);
                Capture($"corridor-moving-{colour}");
                TouchHud.LetGoOfStick(thumb);
                Assert.Greater(p.transform.position.y,before.y+1f,"touch movement in corridor");
            }
            Assert.IsTrue(p.GetComponentsInChildren<SpriteRenderer>().Any(s => (s.name=="Red" || s.name=="Blue") && s.enabled && s.sprite && s.color.a>0.9f));
            var v=cam.WorldToViewportPoint(p.transform.position);
            Assert.That(v.x,Is.InRange(0.1f,0.9f));Assert.That(v.y,Is.InRange(0.1f,0.9f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator ReaperEscapesAWallCornerAndRemainsDamageable()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 2");
            foreach(var e in Object.FindObjectsByType<EncounterSpawner>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
            foreach(var e in Object.FindObjectsByType<BiformisEmitterController>(FindObjectsSortMode.None))Object.Destroy(e.gameObject);
            yield return null;
            var p=Object.FindAnyObjectByType<PlayerUnit>();
            Vector2 target=p.transform.position;
            var wall=new GameObject("Corner regression wall",typeof(BoxCollider2D));
            wall.transform.position=target+Vector2.left*2.5f;wall.GetComponent<BoxCollider2D>().size=new Vector2(0.7f,3f);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Luneblade/Reaper.prefab");
            var enemy=Object.Instantiate(prefab,target+Vector2.left*4f,Quaternion.identity).GetComponent<LunebladeEnemy>();
            try
            {
                float closest=float.MaxValue;
                for(float end=Time.time+9f;Time.time<end;)
                {
                    closest=Mathf.Min(closest,Vector2.Distance(enemy.transform.position,p.transform.position));
                    if(closest<1.6f)break;
                    yield return null;
                }
                Assert.Less(closest,1.6f,"must detour around wall rather than push into its corner");
                Assert.IsTrue(enemy.Coll.enabled);
                int hp=enemy.CurrentHealth;
                var thumb=TouchHud.PushStick(((Vector2)enemy.transform.position-(Vector2)p.transform.position).normalized);
                yield return StoryTestLoading.WaitFor(()=>enemy.CurrentHealth<hp,4f,"ordinary player contact with recovered enemy");
                TouchHud.LetGoOfStick(thumb);
                Assert.Less(enemy.CurrentHealth,hp,"recovered enemy accepts normal ram collision");
            }
            finally {Object.Destroy(enemy.gameObject);Object.Destroy(wall);}
        }
        private static void Capture(string name)
        {
            Directory.CreateDirectory("Logs/art-diagnostics");
            var cam=Camera.main;var previous=cam.targetTexture;var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1280,720,24);var read=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            cam.targetTexture=target;cam.Render();RenderTexture.active=target;
            read.ReadPixels(new Rect(0,0,1280,720),0,0);read.Apply();
            File.WriteAllBytes($"Logs/art-diagnostics/{name}.png",read.EncodeToPNG());
            cam.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.Destroy(read);
        }
        [TearDown] public void Cleanup(){HitStop.Abort();Time.timeScale=1f;if(GameManager.HasInstance)GameManager.Instance.UnlockDimension();}
    }
}
#endif
