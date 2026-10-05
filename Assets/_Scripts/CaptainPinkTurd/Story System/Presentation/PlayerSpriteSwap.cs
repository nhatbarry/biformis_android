using System.Collections.Generic;
using CaptainPinkTurd.Game.Player;
using UnityEngine;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// Shows one of the player's forms with other art in this level only, frame for frame: the player prefab and its
    /// animations stay as they are, and every sprite the animator puts on that form is swapped for the replacement
    /// sprite of the same name. Used in Level 4, where A arrives already hurt by the villain (A_Bloody, which has
    /// exactly Blue Character's frames and tags).
    /// </summary>
    [DefaultExecutionOrder(10000)] //after the animator has set this frame's sprite
    public class PlayerSpriteSwap : MonoBehaviour
    {
        [Tooltip("The player's child SpriteRenderer to restyle (\"Blue\" is A, \"Red\" is B)")]
        [SerializeField] private string form = "Blue";
        [Tooltip("Sprites named like the form's own (Frame_0, Frame_1, ...)")]
        [SerializeField] private Sprite[] replacements;

        private readonly Dictionary<string, Sprite> byName = new();
        private SpriteRenderer target;

        private void Awake()
        {
            foreach (var sprite in replacements)
            {
                if (sprite) byName[sprite.name] = sprite;
            }
        }

        private void LateUpdate()
        {
            if (!target)
            {
                var player = FindAnyObjectByType<PlayerUnit>();
                if (!player) return;
                foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.name == form) target = renderer;
                }
                if (!target) return;
            }

            var current = target.sprite;
            if (current && byName.TryGetValue(current.name, out var swap) && swap != current) target.sprite = swap;
        }
    }
}
