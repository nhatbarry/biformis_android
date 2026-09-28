using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CaptainPinkTurd.Game;
using CaptainPinkTurd.Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// Can each story level actually be finished? Path-finds with the player's real colliders (a 0.75 x 1.175 body box
    /// that walls stop, plus a small hitbox circle that the colour barriers stop), switching colour wherever that is
    /// safe (never in Level 4, which locks A in blue). Rooms are played in order: each room's fight must be enterable and
    /// every point it can spawn an enemy at touchable while later gates are still shut, then its gate opens; finally
    /// the door must be reachable.
    /// </summary>
    public class StoryLevelFeasibilityTests
    {
        private const float Step = 0.25f;

        private static readonly string[] Levels =
        {
            "Level Story 1", "Level Story 2", "Level Story 3", "Level Story 4", "Level Story Corridor", "Level Story 5",
        };

        private Transform player;
        private Vector2 bodyOffset, bodySize;
        private float hitboxRadius;
        private int bodyLayer, bodyExcludes;

        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [UnityTest]
        public IEnumerator LevelCanBeFinished([ValueSource(nameof(Levels))] string level)
        {
            yield return StoryTestLoading.LoadLevelThroughCore(level);
            yield return new WaitForFixedUpdate();

            var unit = Object.FindAnyObjectByType<PlayerUnit>();
            player = unit.transform;
            var body = player.GetComponentsInChildren<BoxCollider2D>(true).First(c => !c.isTrigger);
            var hitbox = player.GetComponentsInChildren<CircleCollider2D>(true).First(c => !c.isTrigger);
            bodyOffset = (Vector2)(body.bounds.center - player.position);
            bodySize = body.bounds.size;
            hitboxRadius = hitbox.bounds.extents.x;
            bodyLayer = body.gameObject.layer;
            bodyExcludes = body.excludeLayers.value;

            bool blueOnly = Object.FindAnyObjectByType<DimensionLock>() != null;
            var colours = blueOnly ? new[] { LayerMask.NameToLayer("Blue") } : new[] { LayerMask.NameToLayer("Red"), LayerMask.NameToLayer("Blue") };
            var bound = GameObject.Find("Map Bound").GetComponent<BoxCollider2D>().bounds;
            Vector2 start = player.position;

            //freeze the level (not Core's managers, which outlive it) while it is being measured
            var levelScene = SceneManager.GetActiveScene();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                string ns = mb.GetType().Namespace ?? "";
                if (mb.gameObject.scene == levelScene && (ns.StartsWith("CaptainPinkTurd") || ns.StartsWith("BulletHell"))) mb.enabled = false;
            }
            Physics2D.SyncTransforms();

            var encounters = Object.FindObjectsByType<EncounterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(e => e.name == "Encounter Door" ? 1 : 0) //the door's fight is always the last room
                .ThenBy(e => e.name, System.StringComparer.Ordinal).ToList();
            var gates = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Gate ")).ToDictionary(t => t.name["Gate ".Length..]);

            HashSet<Vector2Int> reached = null;
            foreach (var encounter in encounters)
            {
                //gates of rooms not yet cleared are still shut here
                reached = Explore(start, bound, colours);
                var area = encounter.GetComponent<Collider2D>().bounds;
                if (!Touches(reached, area))
                {
                    SaveDebugImage(level, bound, reached, area.center);
                    Assert.Fail($"{level}: {encounter.name}'s room can't be entered");
                }
                //turrets don't move, so every point an enemy can appear at must be reachable to ram it
                foreach (var point in encounter.SpawnPoints)
                {
                    if (Touches(reached, new Bounds(point, Vector3.one))) continue;
                    SaveDebugImage(level, bound, reached, point);
                    Assert.Fail($"{level}: {encounter.name} can spawn an enemy at {point}, which can't be reached");
                }

                string id = encounter.name.Replace("Encounter ", "");
                if (gates.TryGetValue(id, out var gate))
                {
                    gate.gameObject.SetActive(false);
                    Physics2D.SyncTransforms();
                }
            }

            reached = Explore(start, bound, colours);
            var door = Object.FindAnyObjectByType<Door>(FindObjectsInactive.Include);
            var doorTrigger = door.GetComponents<BoxCollider2D>().First(c => c.isTrigger).bounds;
            if (!Touches(reached, doorTrigger))
            {
                SaveDebugImage(level, bound, reached, doorTrigger.center);
                Assert.Fail($"{level}: the door can't be reached");
            }

            TestContext.WriteLine($"{level}: {reached.Count} reachable positions, start→door ≈ {ShortestDistance(start, doorTrigger, bound, colours):0} units");
        }

        // ------------------------------------------------------------------ search

        private HashSet<Vector2Int> Explore(Vector2 start, Bounds bound, int[] colours) => Search(start, bound, colours, null, out _);

        private float ShortestDistance(Vector2 start, Bounds target, Bounds bound, int[] colours)
        {
            Search(start, bound, colours, target, out int steps);
            return steps * Step;
        }

        private HashSet<Vector2Int> Search(Vector2 start, Bounds bound, int[] colours, Bounds? target, out int targetSteps)
        {
            targetSteps = -1;
            var origin = new Vector2(bound.min.x, bound.min.y);
            Vector2 World(Vector2Int c) => origin + (Vector2)c * Step;
            var startCell = Vector2Int.RoundToInt((start - origin) / Step);
            int w = Mathf.CeilToInt(bound.size.x / Step), h = Mathf.CeilToInt(bound.size.y / Step);

            var free = new Dictionary<(Vector2Int, int), bool>();
            bool Free(Vector2Int c, int colour)
            {
                if (free.TryGetValue((c, colour), out var f)) return f;
                return free[(c, colour)] = BodyFree(World(c)) && HitboxFree(World(c), colour);
            }

            var reached = new HashSet<Vector2Int>();
            var seen = new HashSet<(Vector2Int, int)>();
            var queue = new Queue<(Vector2Int cell, int colour, int steps)>();
            foreach (var colour in colours)
            {
                if (Free(startCell, colour) && seen.Add((startCell, colour))) queue.Enqueue((startCell, colour, 0));
            }

            var dirs = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var (cell, colour, steps) = queue.Dequeue();
                reached.Add(cell);
                if (target.HasValue && targetSteps < 0 && BodyBounds(World(cell)).Intersects(target.Value)) targetSteps = steps;

                //switching colour is only safe where the player would be free in either colour
                foreach (var other in colours)
                {
                    if (other != colour && Free(cell, other) && seen.Add((cell, other))) queue.Enqueue((cell, other, steps));
                }
                foreach (var d in dirs)
                {
                    var next = cell + d;
                    if (next.x < 0 || next.y < 0 || next.x > w || next.y > h) continue;
                    if (!Free(next, colour) || !seen.Add((next, colour))) continue;
                    queue.Enqueue((next, colour, steps + 1));
                }
            }
            reachedOrigin = origin;
            return reached;
        }

        //grid origin of the last search, so Touches can turn reached cells back into positions
        private Vector2 reachedOrigin;

        private bool Touches(HashSet<Vector2Int> reached, Bounds target)
        {
            var grown = target;
            grown.Expand(0.3f); //contact, not overlap: enemies are solid to the hitbox
            return reached.Any(c => BodyBounds(reachedOrigin + (Vector2)c * Step).Intersects(grown));
        }

        //Logs/feasibility <level>.png: green = reachable, dark = blocked for the body, grey = open but unreached, red = target
        private void SaveDebugImage(string level, Bounds bound, HashSet<Vector2Int> reached, Vector2 target)
        {
            int w = Mathf.CeilToInt(bound.size.x / Step) + 1, h = Mathf.CeilToInt(bound.size.y / Step) + 1;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var p = reachedOrigin + new Vector2(x, y) * Step;
                    tex.SetPixel(x, y, reached.Contains(new Vector2Int(x, y)) ? Color.green : BodyFree(p) ? Color.gray : new Color(0.1f, 0.1f, 0.15f));
                }
            var t = Vector2Int.RoundToInt((target - reachedOrigin) / Step);
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    tex.SetPixel(t.x + dx, t.y + dy, Color.red);
            tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Logs", $"feasibility {level}.png"), tex.EncodeToPNG());
        }

        private Bounds BodyBounds(Vector2 position) => new(position + bodyOffset, bodySize);

        private bool BodyFree(Vector2 position)
        {
            foreach (var c in Physics2D.OverlapBoxAll(position + bodyOffset, bodySize * 0.98f, 0f))
            {
                if (Blocks(c, bodyLayer, bodyExcludes)) return false;
            }
            return true;
        }

        private bool HitboxFree(Vector2 position, int colourLayer)
        {
            foreach (var c in Physics2D.OverlapCircleAll(position, hitboxRadius * 0.98f))
            {
                if (Blocks(c, colourLayer, 0)) return false;
            }
            return true;
        }

        private bool Blocks(Collider2D other, int ourLayer, int ourExcludes)
        {
            if (other.isTrigger || other.transform.IsChildOf(player)) return false;
            int theirLayer = other.gameObject.layer;
            if (theirLayer == LayerMask.NameToLayer("Enemy") || theirLayer == LayerMask.NameToLayer("Projectile")) return false; //touching enemies is how they die
            if (Physics2D.GetIgnoreLayerCollision(ourLayer, theirLayer)) return false;
            if ((ourExcludes & (1 << theirLayer)) != 0) return false;
            if ((other.excludeLayers.value & (1 << ourLayer)) != 0) return false;
            return true;
        }
    }
}
