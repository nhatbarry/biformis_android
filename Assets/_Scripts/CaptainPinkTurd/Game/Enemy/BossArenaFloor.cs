using CaptainPinkTurd.Game.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>A fixed-size floor window follows a freely moving player; there is no hidden arena perimeter.</summary>
    [RequireComponent(typeof(Tilemap))]
    public class BossArenaFloor : MonoBehaviour
    {
        [SerializeField] private TileBase floorTile;
        private Tilemap floor;
        private PlayerUnit player;
        private TileBase[] tiles;
        private Vector3Int centre;

        private void Start()
        {
            floor = GetComponent<Tilemap>();
            player = FindAnyObjectByType<PlayerUnit>();
            tiles = new TileBase[64 * 48];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = floorTile;
            centre = new Vector3Int(8, 16, 0);
        }

        private void Update()
        {
            if (!player || !floorTile) return;
            var cell = floor.WorldToCell(player.transform.position);
            if (Mathf.Abs(cell.x - centre.x) < 16 && Mathf.Abs(cell.y - centre.y) < 12) return;
            centre = new Vector3Int(Mathf.FloorToInt(cell.x / 16f) * 16 + 8,
                Mathf.FloorToInt(cell.y / 12f) * 12 + 4, 0);
            floor.ClearAllTiles();
            floor.SetTilesBlock(new BoundsInt(centre.x - 32, centre.y - 24, 0, 64, 48, 1), tiles);
            floor.CompressBounds();
            var collider = floor.GetComponent<TilemapCollider2D>();
            if (collider) collider.ProcessTilemapChanges();
        }
    }
}
