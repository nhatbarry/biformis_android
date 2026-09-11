using System;
using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Extensions;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CaptainPinkTurd.Core.Movement
{
    public class GridBasedMovement : MonoBehaviour
    {
        [Header("Grid Based Movement Configs")] 
        [SerializeField] private float moveTimeBetweenGrids = 0.2f;
        [SerializeField] private List<Tilemap> groundTiles;
        [SerializeField] private List<Tilemap> collisionTiles;
        [SerializeField] private bool ignoreTimeScale = false;
        [SerializeField] private EDirectionMode directionMode = EDirectionMode.FourDirectional;
        
        [Header("Grid Based Obstacle Configs")]
        [SerializeField] private LayerMask obstacleLayers;
        [SerializeField] private Vector2 overlapSize = Vector2.one;
        
        private Tilemap currentGroundTilemap;
        private Tilemap currentCollisionTilemap;
        private Vector3 targetPos;
        private ContactFilter2D obstacleFilter;
            
        protected bool isMoving;

        private void Awake()
        {
            obstacleFilter.SetLayerMask(obstacleLayers);
            obstacleFilter.useLayerMask = true;
            obstacleFilter.useTriggers = false; 
        }

        protected virtual void OnEnable()
        {
            targetPos = transform.position;
            isMoving = false;
        }
        protected virtual void OnDisable()
        {
            transform.position = targetPos;
        }
        protected void Move(Vector2 direction)
        {
            if (isMoving) return;
            
            currentGroundTilemap = GetActiveTilemap(groundTiles);
            currentCollisionTilemap = GetActiveTilemap(collisionTiles);

            direction = direction.SnapDiagonal(directionMode);
            if (CanMove(direction))
            {
                StartCoroutine(MoveToGrid(direction));
            }
        }

        private IEnumerator MoveToGrid(Vector2 direction)
        {
            isMoving = true;
            float elapsedTime = 0;
            
            var originalPos = transform.position;
            var targetGridPos = currentGroundTilemap.WorldToCell(originalPos + (Vector3)direction);
            targetPos = currentCollisionTilemap.GetCellCenterWorld(targetGridPos);
            
            while (elapsedTime < moveTimeBetweenGrids)
            {
                elapsedTime += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                transform.position = Vector3.Lerp(originalPos, targetPos, elapsedTime / moveTimeBetweenGrids);
                yield return null;
            }
            
            transform.position = targetPos;
            isMoving = false;
        }
        private bool CanMove(Vector2 direction)
        {
            Vector3Int gridPosition = currentGroundTilemap.WorldToCell(transform.position + (Vector3)direction);
            
            if (!currentGroundTilemap.HasTile(gridPosition))
                return false;

            if (currentCollisionTilemap.HasTile(gridPosition))
                return false;

            Vector2 worldPos = currentGroundTilemap.GetCellCenterWorld(gridPosition);
            
            return IsAreaClear(worldPos, overlapSize);
        }

        private bool IsAreaClear(Vector2 worldPos, Vector2 overlapSize)
        {
            return Physics2D.OverlapBox(worldPos, overlapSize, 0f, obstacleFilter, 
                new Collider2D[1]) == 0;
        }
        private Tilemap GetActiveTilemap(List<Tilemap> tilemaps)
        {
            foreach (var tilemap in tilemaps)
            {
                if(tilemap.isActiveAndEnabled) return tilemap;
            }
            
            Debug.LogError("No tilemap is active");
            return null;
        }
    }
}