using System.Collections.Generic;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Game;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>
/// Dresses the story levels in the Endless levels' look: thick wall masses split into outlined panels with a front face
/// and a cast shadow, lab pods / consoles / server panels set into the walls, crimson bushes and trees, grass and floor
/// debris, light rays and dust. Everything goes on its own tilemaps ("Wall Art", "Prop Art", "Ground Dressing") and
/// objects ("-- Environment Dressing --"), none with a collider. The Collision tilemap keeps its tiles and collider; only
/// its renderer is switched off, because Wall Art draws those same cells (and the extra thickness around them).
/// Re-running replaces the previous dressing; each level has a fixed seed, so the result is stable.
/// </summary>
public static class StoryEnvironmentDressing
{
    private const string ScenePath = "Assets/Scenes/CaptainPinkTurd/Story/";
    private const string DressingRoot = "-- Environment Dressing --";
    private static readonly string[] OwnTilemaps = { "Wall Art", "Prop Art", "Ground Dressing" };
    private static readonly string[] Levels =
    {
        "Level Story 1", "Level Story 2", "Level Story 3", "Level Story 4", "Level Story Corridor", "Level Story 5", "Level Story 6",
    };

    //how far past the reachable floor the wall mass is drawn (the camera never sees much further)
    private const int WallThickness = 9;

    private static Dictionary<Vector2Int, TileBase> sheet;

    [MenuItem("Biformis/Environment/Dress Story Levels")]
    public static void DressAll()
    {
        sheet = LoadSheet();
        foreach (var level in Levels)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath + level + ".unity", OpenSceneMode.Single);
            Dress(level);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    [MenuItem("Biformis/Environment/Remove Story Level Dressing")]
    public static void RemoveAll()
    {
        foreach (var level in Levels)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath + level + ".unity", OpenSceneMode.Single);
            RemoveDressing();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    //batchmode entry points
    public static void DressAllBatch() => RunBatch(DressAll);
    public static void SnapshotsBatch() => RunBatch(() => RenderSnapshots(Path.GetFullPath("Logs/Environment Snapshots")));

    private static void RunBatch(System.Action action)
    {
        try
        {
            action();
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    // ---------------------------------------------------------------- tileset

    //every Environment tile by its cell in Tile.png, counted in 16 px cells from the top left
    private static Dictionary<Vector2Int, TileBase> LoadSheet()
    {
        const string tileset = "Assets/Sprites/Tilemap/Tillesets/Tile.png";
        //sprite.texture is the sprite atlas in the editor; the rects are in the source texture
        int height = AssetDatabase.LoadAssetAtPath<Texture2D>(tileset).height;
        var map = new Dictionary<Vector2Int, TileBase>();
        foreach (var guid in AssetDatabase.FindAssets("t:Tile", new[] { "Assets/Sprites/Tilemap/Tiles/Environment" }))
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
            if (!tile || !tile.sprite || tile.name.Contains(' ') || AssetDatabase.GetAssetPath(tile.sprite) != tileset) continue;
            var rect = tile.sprite.rect;
            map.TryAdd(new Vector2Int((int)rect.x / 16, (height - (int)rect.yMax) / 16), tile);
        }
        return map;
    }

    private static TileBase T(int column, int row) =>
        sheet.TryGetValue(new Vector2Int(column, row), out var tile) ? tile : throw new KeyNotFoundException($"no tile at Tile.png cell {column},{row}");

    //a prop drawn from a block of Tile.png cells; shadowRows are the bottom rows that fall on the floor below it
    private class Stamp
    {
        public readonly List<(Vector2Int offset, TileBase tile)> Body = new();
        public readonly List<(Vector2Int offset, TileBase tile)> Shadow = new();
        public int Width, Height;

        public Stamp(int column, int row, int width, int height, int shadowRows = 0)
        {
            Width = width;
            Height = height - shadowRows;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!sheet.TryGetValue(new Vector2Int(column + x, row + y), out var tile)) continue;
                var offset = new Vector2Int(x, Height - 1 - y);
                (y < Height ? Body : Shadow).Add((offset, tile));
            }
        }
    }

    // ---------------------------------------------------------------- dressing

    private static void RemoveDressing()
    {
        foreach (var map in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (OwnTilemaps.Contains(map.name)) Object.DestroyImmediate(map.gameObject);
        var root = GameObject.Find(DressingRoot);
        if (root) Object.DestroyImmediate(root);
        var walls = FindTilemap("Collision");
        if (walls) walls.GetComponent<TilemapRenderer>().enabled = true;
    }

    private static Tilemap FindTilemap(string name) =>
        Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(map => map.name.Trim() == name);

    private static void Dress(string level)
    {
        RemoveDressing();
        var random = new Random(level.Aggregate(17, (hash, c) => hash * 31 + c));
        var walls = FindTilemap("Collision");
        var ground = FindTilemap("Ground");
        var player = Object.FindAnyObjectByType<PlayerUnit>();
        var arena = Object.FindAnyObjectByType<BossArenaController>();

        // which cells the player can stand on (reach), and the wall mass around them
        var reach = new HashSet<Vector2Int>();
        var mass = new HashSet<Vector2Int>();
        var groundCells = new HashSet<Vector2Int>();
        foreach (var cell in ground.cellBounds.allPositionsWithin)
            if (ground.HasTile(cell)) groundCells.Add((Vector2Int)cell);
        bool IsWall(Vector2Int cell) => walls.HasTile((Vector3Int)cell);

        if (arena)
        {
            //the open boss floor: no walls, so frame the arena with a mass on either side of the bounded play area
            var settings = new SerializedObject(arena);
            var centre = settings.FindProperty("playAreaCentre").vector2Value;
            var size = settings.FindProperty("playAreaSize").vector2Value;
            var cameraCentre = settings.FindProperty("cameraCentre").vector2Value;
            var play = new Rect(centre - size * 0.5f, size);
            foreach (var cell in groundCells)
            {
                var world = (Vector2)ground.GetCellCenterWorld((Vector3Int)cell);
                if (play.Contains(world)) reach.Add(cell);
                bool side = world.x < play.xMin && world.x > play.xMin - WallThickness ||
                            world.x > play.xMax && world.x < play.xMax + WallThickness;
                if (side && world.y > play.yMin - 4f && world.y < cameraCentre.y + 8f) mass.Add(cell);
            }
        }
        else
        {
            var start = (Vector2Int)walls.WorldToCell(player.transform.position);
            var open = new Queue<Vector2Int>(new[] { start });
            reach.Add(start);
            while (open.Count > 0)
            {
                var cell = open.Dequeue();
                foreach (var next in Neighbours4(cell))
                {
                    if (reach.Contains(next) || IsWall(next)) continue;
                    if (!groundCells.Contains(next))
                        throw new System.Exception($"{level}: the floor around the player leaks out of the Ground tilemap at {next}");
                    reach.Add(next);
                    open.Enqueue(next);
                }
            }
            var distance = DistanceFrom(reach, WallThickness);
            foreach (var cell in groundCells)
                if (!reach.Contains(cell) && (IsWall(cell) || distance.TryGetValue(cell, out int d) && d <= WallThickness))
                    mass.Add(cell);
            foreach (var cell in walls.cellBounds.allPositionsWithin)
                if (walls.HasTile(cell)) mass.Add((Vector2Int)cell);
        }
        var near = DistanceFrom(reach, WallThickness + 2);
        int Near(Vector2Int cell) => near.TryGetValue(cell, out int d) ? d : 99;

        // own tilemaps beside the existing ones, with the walls' lit material
        var wallRenderer = walls.GetComponent<TilemapRenderer>();
        var wallArt = NewTilemap(walls, "Wall Art", "Collision", 0, wallRenderer.sharedMaterial);
        var groundArt = NewTilemap(walls, "Ground Dressing", "Ground Decor", 2, wallRenderer.sharedMaterial);
        //on the boss floor the props must not draw over the levitating boss, so they stay below the characters there
        var propArt = NewTilemap(walls, "Prop Art", arena ? "Collision" : "Decoration", 1, wallRenderer.sharedMaterial);
        wallRenderer.enabled = false;

        // walls: a front face where the mass meets the floor below it, outlined panels everywhere else
        var faces = new HashSet<Vector2Int>(mass.Where(c => reach.Contains(c + Vector2Int.down) && mass.Contains(c + Vector2Int.up)));
        foreach (var cell in faces)
        {
            bool left = faces.Contains(cell + Vector2Int.left), right = faces.Contains(cell + Vector2Int.right);
            wallArt.SetTile((Vector3Int)cell, !left && !right ? T(7, 6) : !left ? T(1, 6) : !right ? T(5, 6) : T(2, 6));
            groundArt.SetTile((Vector3Int)(cell + Vector2Int.down), T(2, 7));
        }
        PackPanels(mass.Where(c => !faces.Contains(c)).ToHashSet(), wallArt, random, Near);
        foreach (var cell in mass)
        {
            //the walls fade into the dark away from the rooms
            int d = Near(cell);
            if (d < 4) continue;
            wallArt.SetTileFlags((Vector3Int)cell, TileFlags.None);
            wallArt.SetColor((Vector3Int)cell, Color.Lerp(Color.white, new Color(0.55f, 0.55f, 0.6f), Mathf.InverseLerp(4, 9, d)));
        }
        //a half-cell drop shadow on the floor right of a wall, light coming from the top left like the tileset
        foreach (var cell in mass)
        {
            var east = cell + Vector2Int.right;
            if (reach.Contains(east) && !groundArt.HasTile((Vector3Int)east)) groundArt.SetTile((Vector3Int)east, T(6, 3));
        }

        // props set into the walls, never near the door or a hint
        var keepClear = Object.FindObjectsByType<Door>(FindObjectsSortMode.None).Select(d => d.transform.position)
            .Concat(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Hint")).Select(t => t.position))
            .Select(p => (Vector2Int)walls.WorldToCell(p)).ToList();
        bool ClearBy(Vector2Int cell, int cells) => keepClear.All(k => Mathf.Max(Mathf.Abs(k.x - cell.x), Mathf.Abs(k.y - cell.y)) > cells);
        bool Clear(Vector2Int cell) => ClearBy(cell, 2);

        int edge = mass.Count(c => Neighbours4(c).Any(reach.Contains));
        var used = new HashSet<Vector2Int>();
        var podLeft = new Stamp(1, 11, 3, 7, shadowRows: 1);
        var podRight = new Stamp(5, 11, 3, 7, shadowRows: 1);
        var podOffLeft = new Stamp(1, 18, 3, 5);
        var podOffRight = new Stamp(5, 18, 3, 5);
        var console = new Stamp(1, 23, 3, 4);
        var server = new Stamp(9, 12, 3, 5);
        int pods = Mathf.Max(2, edge / 16);
        pods -= PlaceStamps(random, pods * 2 / 3, new[] { podLeft, podRight }, mass, reach, used, Clear, propArt, groundArt, touchFloor: !arena);
        //the rest beside a room, so long side walls (the corridor) get their tubes too
        PlaceStamps(random, pods, new[] { podOffLeft, podOffRight, podLeft }, mass, reach, used, Clear, propArt, groundArt, touchFloor: false);
        PlaceStamps(random, Mathf.Max(1, edge / 32), new[] { podOffLeft, podOffRight }, mass, reach, used, Clear, propArt, groundArt, touchFloor: !arena);
        PlaceStamps(random, Mathf.Max(1, edge / 32), new[] { console }, mass, reach, used, Clear, propArt, groundArt, touchFloor: !arena);
        PlaceStamps(random, Mathf.Max(1, edge / 28), new[] { server }, mass, reach, used, Clear, propArt, groundArt, touchFloor: false);

        var propBodies = mass.Where(c => propArt.HasTile((Vector3Int)c)).ToHashSet();

        // grass on the wall tops near the rooms, mostly teal with some crimson
        var tealGrass = Cells(12, 20, 3, 2).Concat(Cells(12, 21, 3, 1)).ToArray();
        var redGrass = Cells(15, 20, 3, 2).Concat(Cells(15, 23, 3, 2)).ToArray();
        foreach (var cell in mass)
        {
            if (propArt.HasTile((Vector3Int)cell) || faces.Contains(cell) || Near(cell) > 3 || random.NextDouble() > 0.12) continue;
            var pick = random.NextDouble() < 0.35 ? redGrass : tealGrass;
            var choice = pick[random.Next(pick.Length)];
            propArt.SetTile((Vector3Int)cell, T(choice.x, choice.y));
        }

        // floor: dark debris and grass, denser along the walls, never on a gate or a colour barrier
        var barriers = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None)
            .Where(m => m.name.StartsWith("Gate") || m.name.EndsWith("Barrier")).ToArray();
        var debris = Cells(9, 18, 3, 3).Concat(Cells(12, 18, 3, 2)).ToArray();
        float floorDensity = arena ? 0.4f : 1f;
        foreach (var cell in reach)
        {
            if (groundArt.HasTile((Vector3Int)cell) || barriers.Any(m => m.HasTile((Vector3Int)cell))) continue;
            int toWall = mass.Count == 0 ? 99 : Neighbours8(cell).Any(mass.Contains) ? 1 : Neighbours8(cell).SelectMany(Neighbours8).Any(mass.Contains) ? 2 : 3;
            double chance = (toWall == 1 ? 0.10 : toWall == 2 ? 0.06 : 0.025) * floorDensity;
            if (random.NextDouble() > chance) continue;
            double kind = random.NextDouble();
            var pick = kind < 0.55 ? debris : kind < 0.85 ? tealGrass : redGrass;
            var choice = pick[random.Next(pick.Length)];
            groundArt.SetTile((Vector3Int)cell, T(choice.x, choice.y));
        }

        // crimson bushes and trees on the walls, light rays and dust over the rooms
        var root = new GameObject(DressingRoot).transform;
        var bush = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Polishing/Bush.prefab");
        var tree = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Polishing/Leaves.prefab");
        var ray = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Polishing/Ray.prefab");
        var dust = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Polishing/Particle.prefab");
        var planted = new List<Vector2Int>();
        void Plant(GameObject prefab, int count, int minNear, int maxNear, int spacing, System.Func<Vector2Int, bool> extra)
        {
            //bushes and trees spread well past their cell, so they keep further from the door and the hints
            var spots = mass.Where(c => Near(c) >= minNear && Near(c) <= maxNear && ClearBy(c, 4) && extra(c)).OrderBy(_ => random.Next()).ToList();
            foreach (var cell in spots)
            {
                if (count <= 0) break;
                if (planted.Any(p => Mathf.Max(Mathf.Abs(p.x - cell.x), Mathf.Abs(p.y - cell.y)) < spacing)) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                var jitter = new Vector3((float)random.NextDouble() * 0.6f - 0.3f, (float)random.NextDouble() * 0.4f - 0.2f);
                instance.transform.position = walls.GetCellCenterWorld((Vector3Int)cell) + jitter;
                if (random.Next(2) == 0) instance.transform.localScale = new Vector3(-1f, 1f, 1f);
                if (arena)
                    foreach (var sprite in instance.GetComponentsInChildren<SpriteRenderer>())
                    {
                        sprite.sortingLayerName = "Collision";
                        sprite.sortingOrder += 2;
                    }
                planted.Add(cell);
                count--;
            }
        }
        Plant(tree, Mathf.Max(1, edge / 45), 3, 4, 6, c => mass.Contains(c + Vector2Int.down) && mass.Contains(c + Vector2Int.down * 2));
        Plant(bush, Mathf.Max(3, edge / 7), 1, 2, 3, c => !propBodies.Contains(c));

        var toMass = DistanceFrom(mass, 3);
        var rooms = reach.Where(c => !toMass.ContainsKey(c)).ToList();
        if (!arena)
            foreach (var cell in rooms.OrderBy(_ => random.Next()).Take(2))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ray, root);
                instance.transform.position = walls.GetCellCenterWorld((Vector3Int)cell);
            }
        var bounds = new RectInt(reach.Min(c => c.x), reach.Min(c => c.y), reach.Max(c => c.x) - reach.Min(c => c.x) + 1, reach.Max(c => c.y) - reach.Min(c => c.y) + 1);
        //the dust prefab covers about 26 x 17 units; one per such block of the level
        for (int x = bounds.xMin + 13; x < bounds.xMax + 13; x += 26)
        for (int y = bounds.yMin + 8; y < bounds.yMax + 8; y += 17)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(dust, root);
            instance.transform.position = walls.GetCellCenterWorld(new Vector3Int(Mathf.Min(x, bounds.xMax - 1), Mathf.Min(y, bounds.yMax - 1), 0));
        }
        Debug.Log($"[Dressing] {level}: reach {reach.Count}, mass {mass.Count}, faces {faces.Count}, wall edge {edge}, props {used.Count} cells, plants {planted.Count}");
    }

    private static IEnumerable<Vector2Int> Cells(int column, int row, int width, int height)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (sheet.ContainsKey(new Vector2Int(column + x, row + y))) yield return new Vector2Int(column + x, row + y);
    }

    private static Tilemap NewTilemap(Tilemap beside, string name, string sortingLayer, int order, Material material)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(beside.transform.parent, false);
        go.transform.SetSiblingIndex(beside.transform.GetSiblingIndex() + 1);
        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = order;
        renderer.sharedMaterial = material;
        return go.GetComponent<Tilemap>();
    }

    //splits the wall mass into outlined rectangles of mixed sizes, like the hand-drawn Endless walls
    private static void PackPanels(HashSet<Vector2Int> cells, Tilemap map, Random random, System.Func<Vector2Int, int> near)
    {
        var done = new HashSet<Vector2Int>();
        foreach (var start in cells.OrderByDescending(c => c.y).ThenBy(c => c.x))
        {
            if (done.Contains(start)) continue;
            bool far = near(start) > 3;
            int maxWidth = random.Next(2, far ? 7 : 6), maxHeight = random.Next(2, far ? 6 : 5);
            int width = 1;
            while (width < maxWidth && Free(start + Vector2Int.right * width)) width++;
            int height = 1;
            while (height < maxHeight && Enumerable.Range(0, width).All(x => Free(start + new Vector2Int(x, -height)))) height++;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var cell = start + new Vector2Int(x, -y);
                done.Add(cell);
                map.SetTile((Vector3Int)cell, PanelTile(x, y, width, height, random));
            }
        }
        bool Free(Vector2Int cell) => cells.Contains(cell) && !done.Contains(cell);
    }

    //x from the left, y from the top of a width x height panel
    private static TileBase PanelTile(int x, int y, int width, int height, Random random)
    {
        bool left = x == 0, right = x == width - 1, top = y == 0, bottom = y == height - 1;
        if (width == 1 && height == 1) return T(7, 8);
        if (width == 1) return top ? T(7, 1) : bottom ? T(7, 5) : T(7, 2);
        if (height == 1) return left ? T(1, 8) : right ? T(5, 8) : T(2, 8);
        if (top) return left ? T(1, 1) : right ? T(5, 1) : random.Next(5) == 0 ? T(3, 1) : T(2, 1);
        if (bottom) return left ? T(1, 5) : right ? T(5, 5) : T(2, 5);
        if (left) return random.Next(5) == 0 ? T(1, 2) : T(1, 4);
        if (right) return random.Next(5) == 0 ? T(5, 2) : T(5, 4);
        return T(2, 4);
    }

    //returns how many it placed
    private static int PlaceStamps(Random random, int count, Stamp[] stamps, HashSet<Vector2Int> mass, HashSet<Vector2Int> reach,
        HashSet<Vector2Int> used, System.Func<Vector2Int, bool> clear, Tilemap props, Tilemap ground, bool touchFloor)
    {
        int placed = 0;
        foreach (var origin in mass.OrderBy(_ => random.Next()))
        {
            if (placed >= count) break;
            var stamp = stamps[random.Next(stamps.Length)];
            var body = stamp.Body.Select(b => origin + b.offset).ToList();
            if (!body.All(c => mass.Contains(c) && !used.Contains(c) && clear(c))) continue;
            bool touches = body.Any(c => Neighbours4(c).Any(reach.Contains));
            //a pod or console stands on the floor line below it; a wall panel only needs to be near a room
            if (touchFloor ? !Enumerable.Range(0, stamp.Width).Any(x => reach.Contains(origin + new Vector2Int(x, -1))) : !touches) continue;

            foreach (var (offset, tile) in stamp.Body) props.SetTile((Vector3Int)(origin + offset), tile);
            foreach (var (offset, tile) in stamp.Shadow)
            {
                var cell = origin + offset;
                if (reach.Contains(cell)) ground.SetTile((Vector3Int)cell, tile);
            }
            //keep two cells between props so they read as separate machines, not one long wall of tubes
            foreach (var c in body)
            for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
                used.Add(c + new Vector2Int(x, y));
            placed++;
        }
        return placed;
    }

    private static Dictionary<Vector2Int, int> DistanceFrom(HashSet<Vector2Int> sources, int limit)
    {
        var distance = sources.ToDictionary(c => c, _ => 0);
        var open = new Queue<Vector2Int>(sources);
        while (open.Count > 0)
        {
            var cell = open.Dequeue();
            int d = distance[cell];
            if (d >= limit) continue;
            foreach (var next in Neighbours8(cell))
            {
                if (distance.ContainsKey(next)) continue;
                distance[next] = d + 1;
                open.Enqueue(next);
            }
        }
        return distance;
    }

    private static IEnumerable<Vector2Int> Neighbours4(Vector2Int c)
    {
        yield return c + Vector2Int.up;
        yield return c + Vector2Int.down;
        yield return c + Vector2Int.left;
        yield return c + Vector2Int.right;
    }

    private static IEnumerable<Vector2Int> Neighbours8(Vector2Int c)
    {
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            if (x != 0 || y != 0) yield return c + new Vector2Int(x, y);
    }

    // ---------------------------------------------------------------- snapshots

    //renders each story level around the player's start (gameplay framing) and as a whole, for review
    private static void RenderSnapshots(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var level in Levels)
        {
            EditorSceneManager.OpenScene(ScenePath + level + ".unity", OpenSceneMode.Single);
            var player = Object.FindAnyObjectByType<PlayerUnit>();
            var walls = FindTilemap("Collision");
            var wallArt = FindTilemap("Wall Art");
            var area = wallArt ? wallArt.localBounds : walls.localBounds;
            Save(Render(player.transform.position, 6.5f, 1920, 1080, false), Path.Combine(folder, level + " start.png"));
            float ortho = Mathf.Max(area.size.y * 0.5f, area.size.x * 0.5f * 9f / 16f) * 0.75f;
            if (ortho > 1f) Save(Render(area.center, ortho, 1920, 1080, false), Path.Combine(folder, level + " whole.png"));
        }
    }

    //what the story cutscenes cut to: the level around the player's start, one art pixel per pixel, without the player
    public static void RenderCutsceneFrames()
    {
        foreach (var (level, file) in new[] { ("Level Story 1", "Next Scene - Level Story 1"), ("Level Story 3", "Next Scene - Level Story 3") })
        {
            EditorSceneManager.OpenScene(ScenePath + level + ".unity", OpenSceneMode.Single);
            var player = Object.FindAnyObjectByType<PlayerUnit>();
            Save(Render(player.transform.position, 90f / 32f, 224, 90, true), Path.GetFullPath($"Logs/Environment Snapshots/{file}.png"));
        }
    }
    public static void CutsceneFramesBatch() => RunBatch(RenderCutsceneFrames);

    private static Texture2D Render(Vector3 centre, float orthographicSize, int width, int height, bool hidePlayer)
    {
        var main = Camera.main;
        var hidden = new List<Renderer>();
        if (hidePlayer)
            foreach (var renderer in Object.FindAnyObjectByType<PlayerUnit>().GetComponentsInChildren<Renderer>())
                if (renderer.enabled)
                {
                    renderer.enabled = false;
                    hidden.Add(renderer);
                }

        var camera = new GameObject("Snapshot Camera").AddComponent<Camera>();
        camera.CopyFrom(main);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = main.GetUniversalAdditionalCameraData().renderPostProcessing;
        camera.transform.position = new Vector3(centre.x, centre.y, -10f);
        camera.orthographic = true;
        camera.orthographicSize = orthographicSize;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        camera.aspect = (float)width / height;
        var request = new RenderPipeline.StandardRequest { destination = target };
        RenderPipeline.SubmitRenderRequest(camera, request);

        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        Object.DestroyImmediate(camera.gameObject);
        target.Release();
        foreach (var renderer in hidden) renderer.enabled = true;
        return texture;
    }

    private static void Save(Texture2D texture, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }
}
