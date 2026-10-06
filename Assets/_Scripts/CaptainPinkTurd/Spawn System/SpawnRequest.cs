using CaptainPinkTurd.Core.Base;

namespace CaptainPinkTurd.SpawnSystem
{
    /// <summary>
    /// One upcoming spawn, handed to PositionBasedSpawner.OnSpawning listeners. Listeners may only set Cancel to true:
    /// one "no" skips it, whatever order they run in.
    /// </summary>
    //a class, not a struct: every listener must see and set the same Cancel
    public class SpawnRequest
    {
        public GameObjectBase Prefab { get; }
        public bool Cancel { get; set; }
        
        public SpawnRequest(GameObjectBase prefab) => Prefab = prefab;
    }
}
