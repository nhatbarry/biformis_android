using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utils;
using CaptainPinkTurd.UI.LayoutUI;
using CaptainPinkTurd.UnitSystem;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CaptainPinkTurd.Game.Player
{
    public class PlayerHealthUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private LayoutGroupController hpGroup;
        [SerializeField] private float hpGroupFadeOutTimer = 1;
        [SerializeField] private SerializeKeyValuePair<EColor, Color>[] hpColors;

        private void Awake()
        {
            hpGroup.RemoveAllLayoutElements();
            hpGroup.AddLayoutElements(unitHealth.MaxHealth);
        }
        private void Start()
        {
            ToggleHpGroupUI(false);
        }

        private void OnEnable()
        {
            unitHealth.OnTakeDamage.Subscribe(OnUnitDamageTakenEvent);
        }

        private void OnDisable()
        {
            unitHealth.OnTakeDamage.Unsubscribe(OnUnitDamageTakenEvent);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ToggleHpGroupUI(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ToggleHpGroupUI(false);
        }
        
        internal void ToggleHpGroupUI(bool isHover, float fadeTime = .1f)
        {
            for(int i = 0; i < hpGroup.transform.childCount; i++)
            {
                if (hpGroup.transform.GetChild(i).TryGetComponent(out Image hpUiImage))
                {
                    hpUiImage.DOFade(isHover ? 1 : 0, fadeTime);
                }
            }
        }

        private void OnUnitDamageTakenEvent(SDamageData damageData)
        {
            hpGroup.RemoveLayoutElements(damageData.Amount);
            ToggleHpGroupUI(true);

            StartCoroutine(CoroutineUtils.WaitForSeconds(hpGroupFadeOutTimer,
                () => ToggleHpGroupUI(false)));
        }

        public void OnPlayerColorChangeEvent(EColor newColor)
        {
            // Serialized colour events may still call the old, disabled health widget.
            if (!isActiveAndEnabled) return;
            if (hpColors.TryGetValue(newColor, out var color))
            {
                for(int i = 0; i < hpGroup.transform.childCount; i++)
                {
                    if (hpGroup.transform.GetChild(i).TryGetComponent(out Image hpUiImage))
                    {
                        hpUiImage.color = color;
                    }
                }
                
                ToggleHpGroupUI(true);
                StartCoroutine(CoroutineUtils.WaitForSeconds(hpGroupFadeOutTimer,
                    () => ToggleHpGroupUI(false)));
            }
            else
            {
                Debug.LogError($"No color found for {newColor}");
            }
        }
    }
}
