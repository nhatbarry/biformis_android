using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// A character (or prop) that a cutscene can put on stage with the ink tag "cast:Id,Id".
    /// Characters without art yet use a placeholder frame; drop a sprite into it to replace it.
    /// </summary>
    public class StageActor : MonoBehaviour
    {
        private static readonly HashSet<string> CharacterActorIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "A", "A4", "A_FB", "B", "B4", "B_Bed", "B_Fall", "B_FB", "B_Floor", "BSit",
            "Captives", "Doctor", "Mom", "Teen", "TeenB", "Villain", "Villain_D", "Villain_Op"
        };

        [Tooltip("Matches the #speaker tag value in ink, or the name used in a cast: tag")]
        [SerializeField] private string actorId;
        [Tooltip("The #speaker value that lights this actor up, if not its actor id (e.g. B_Bed speaks as B)")]
        [SerializeField] private string speakerId;
        [Tooltip("Tinted to show whether this actor is the one speaking")]
        [SerializeField] private Graphic[] tintedGraphics;

        private Color[] baseColors;

        public string ActorId => actorId;
        public string SpeakerId => string.IsNullOrEmpty(speakerId) ? actorId : speakerId;

        private void Awake()
        {
            //an actor that starts off stage only wakes up when first cast, after the stage has already tinted it:
            //keep the colours cached by that first SetTint, not the tinted ones
            if (baseColors == null) CacheBaseColors();
            RemoveCharacterOutlines();
        }

        public void SetVisible(bool visible)
        {
            if (visible) RemoveCharacterOutlines();
            gameObject.SetActive(visible);
        }

        private void RemoveCharacterOutlines()
        {
            if (!CharacterActorIds.Contains(actorId) || tintedGraphics == null) return;

            foreach (var graphic in tintedGraphics)
            {
                if (!graphic) continue;

                var outline = graphic.GetComponent<Outline>();
                if (outline) outline.enabled = false;
            }
        }

        /// <summary>
        /// Multiplies each graphic's own colour, so a black prop stays black and a grey frame stays grey.
        /// </summary>
        public void SetTint(Color tint)
        {
            if (baseColors == null) CacheBaseColors();

            for (int i = 0; i < tintedGraphics.Length; i++)
            {
                if (tintedGraphics[i]) tintedGraphics[i].color = baseColors[i] * tint;
            }
        }

        private void CacheBaseColors()
        {
            baseColors = new Color[tintedGraphics.Length];
            for (int i = 0; i < tintedGraphics.Length; i++)
            {
                baseColors[i] = tintedGraphics[i] ? tintedGraphics[i].color : Color.white;
            }
        }
    }
}
