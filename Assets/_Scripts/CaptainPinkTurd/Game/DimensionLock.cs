using CaptainPinkTurd.Core.Enum;
using UnityEngine;

namespace CaptainPinkTurd.Game
{
    /// <summary>
    /// Keeps the player in one dimension for as long as this object exists (e.g. a level where only A is present).
    /// </summary>
    public class DimensionLock : MonoBehaviour
    {
        [SerializeField] private EColor lockedDimension = EColor.Blue;

        private void Start()
        {
            GameManager.Instance.LockDimension(lockedDimension);
        }

        private void OnDestroy()
        {
            if (GameManager.HasInstance) GameManager.Instance.UnlockDimension();
        }
    }
}
