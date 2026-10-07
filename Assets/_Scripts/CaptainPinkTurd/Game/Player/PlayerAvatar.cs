using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Extensions;
using UnityEngine;

namespace CaptainPinkTurd.Game.Player
{
    public class PlayerAvatar : MonoBehaviour
    {
        [SerializeField] private SerializeKeyValuePair<EColor, GameObject>[] colorProfiles;
        
        public void OnPlayerColorChangeEvent(EColor newColor)
        {
            if (TryGetComponent<PlayerAvatarHealthBar>(out var healthBar)) healthBar.OnPlayerColorChangeEvent(newColor);
            if(colorProfiles.TryGetValue(newColor, out var profile))
            {
                foreach (Transform child in transform)
                {
                    child.gameObject.SetActive(false);
                }
                profile.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogError("No profile found for color: " + newColor);
            }
        }
    }
}
