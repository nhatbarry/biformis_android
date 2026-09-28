using System.Collections;
using System.Linq;
using CaptainPinkTurd.Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// The story levels were painted by script, and tilemap colliders don't follow tile edits made outside the
    /// editor loop: once, every level kept the base scene's baked wall shape and the player walked through the new
    /// walls. This checks every drawn wall face actually blocks, and every barrier / gate cell is solid.
    /// </summary>
    public class StoryLevelCollisionTests
    {
        private static readonly string[] Levels =
        {
            "Level Story 1", "Level Story 2", "Level Story 3", "Level Story 4", "Level Story Corridor", "Level Story 5",
        };

        private static readonly Vector3Int[] Neighbours = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };

        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [UnityTest]
        public IEnumerator DrawnWallsAndBarriersCollide([ValueSource(nameof(Levels))] string level)
        {
            yield return StoryTestLoading.LoadLevelThroughCore(level);
            yield return new WaitForFixedUpdate();

            var maps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            var walls = maps.First(t => t.name.Trim() == "Collision");
            var solidMaps = maps.Where(t => t.name.StartsWith("Gate") || t.name.EndsWith("Barrier")).ToArray();

            bool IsSolidCell(Vector3Int cell) => walls.HasTile(cell) || solidMaps.Any(m => m.HasTile(cell));

            int faces = 0, open = 0;
            string firstOpen = null;
            foreach (var cell in walls.cellBounds.allPositionsWithin)
            {
                if (!walls.HasTile(cell)) continue;
                foreach (var offset in Neighbours)
                {
                    var outside = cell + offset;
                    if (IsSolidCell(outside)) continue;

                    faces++;
                    //the wall composite is outline-only, so test by crossing its edge rather than by point overlap
                    var hits = Physics2D.LinecastAll(walls.GetCellCenterWorld(outside), walls.GetCellCenterWorld(cell));
                    if (hits.Any(h => h.collider.gameObject == walls.gameObject)) continue;

                    open++;
                    firstOpen ??= $"{cell} from {outside}";
                }
            }
            Assert.Greater(faces, 0, $"{level}: no wall faces found");
            Assert.AreEqual(0, open, $"{level}: {open}/{faces} wall faces let the player through (first: {firstOpen})");

            foreach (var map in solidMaps)
            {
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    if (!map.HasTile(cell)) continue;
                    var overlaps = Physics2D.OverlapPointAll(map.GetCellCenterWorld(cell));
                    Assert.IsTrue(overlaps.Any(c => c.gameObject == map.gameObject), $"{level}: {map.name} cell {cell} has no collider");
                }
            }

            var player = Object.FindAnyObjectByType<PlayerUnit>();
            Assert.IsFalse(IsSolidCell(walls.WorldToCell(player.transform.position)), $"{level}: the player starts inside a wall");
        }
    }
}
