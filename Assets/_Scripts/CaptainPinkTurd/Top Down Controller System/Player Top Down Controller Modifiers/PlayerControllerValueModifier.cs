using System;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Utilities;
using UnityEngine;

namespace CaptainPinkTurd.TopDownControllerSystem.Modifier
{
    public abstract class PlayerControllerValueModifier : AbstractValueModifier<PlayerFreeMovementTopDownController2D>
    {
        public abstract override void Modify(PlayerFreeMovementTopDownController2D player);
        public override void Modify(object target)
        {
            switch (target)
            {
                case GameObject targetGo:
                    if (targetGo.transform.TryGetComponentInHierarchy(out PlayerFreeMovementTopDownController2D topdownController))
                    {
                        Modify(topdownController);
                    }
                    else
                    {
                        throw new ArgumentException("Target GO must have a PlayerTopDownController2D component!");
                    }
                    break;
                default:
                    throw new Exception($"Invalid Modify({target}), target was of type: {target.GetType()}");
            }
        }
    }
}