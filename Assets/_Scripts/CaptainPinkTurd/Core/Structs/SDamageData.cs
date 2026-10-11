using UnityEngine;

namespace CaptainPinkTurd.Core.Struct
{
    public struct SDamageData
    {
        public readonly int Amount;
        public readonly GameObject Source;  
        // Explicit cheat attacks still use normal health/death events, but cannot be lost to hit cooldowns.
        public readonly bool IgnoreInvincibility;

        public SDamageData(int amount, GameObject source, bool ignoreInvincibility = false)
        {
            Amount = amount;
            Source = source;
            IgnoreInvincibility = ignoreInvincibility;
        }
    }
}
