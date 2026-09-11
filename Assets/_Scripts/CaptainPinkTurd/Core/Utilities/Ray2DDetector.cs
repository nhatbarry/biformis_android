using CaptainPinkTurd.Core.Attributes;
using UnityEngine;

namespace CaptainPinkTurd.Core.Utilities
{
    public class Ray2DDetector : MonoBehaviour
    {
        [Header("Ray Detection Variables")]
        [SerializeField] private float rayLength = 5;
        [SerializeField] private LayerMask ignoreLayers;
        
        [Header("Debug Only")]
        [SerializeField] private bool useManualDirection;
        [ShowIf(nameof(useManualDirection))]
        [SerializeField] private Vector2 manualDirection;
        
        public RaycastHit2D GetRaycastHit2D(Vector2 direction)
        {
            return Physics2D.Raycast(transform.position, direction, 
                rayLength, ~ignoreLayers);
        }
        
        private void OnDrawGizmosSelected()
        {
            Vector2 origin = transform.position;
            Vector2 direction = useManualDirection ? manualDirection : transform.right;

            RaycastHit2D hit = Physics2D.Raycast(origin, direction, rayLength, ~ignoreLayers);
            Gizmos.color = hit.collider ? Color.green : Color.red;

            float length = hit.collider ? hit.distance : rayLength;

            Gizmos.DrawLine(origin, origin + direction * length);

            if (hit.collider)
            {
                Gizmos.DrawSphere(hit.point, 0.08f);
            }
        }
    }
}