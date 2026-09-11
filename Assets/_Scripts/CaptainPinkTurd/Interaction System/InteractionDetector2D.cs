using CaptainPinkTurd.Core.DesignPattern.SOAP.Variables;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaptainPinkTurd.Interaction
{
    [RequireComponent(typeof(Ray2DDetector))]
    public class InteractionDetector2D : MonoBehaviour
    {
        [SerializeField] private Vector2VariableSO playerInput;
        
        private Ray2DDetector rayDetector;
        private IInteractable closestInteractable;
        private Vector2 currentFaceDirection = Vector2.down;

        private void Awake()
        {
            rayDetector = GetComponent<Ray2DDetector>();
        }
        private void OnEnable()
        {
            //use started instead of performed to prevent overlapping with DialogueManager when end dialogue 
            InputManager.Instance.InputSystemActions.Player.Interact.started += OnInteract; 
            
            playerInput.OnValueChanged += OnPlayerInput;
        }

        private void OnDisable()
        {
            playerInput.OnValueChanged -= OnPlayerInput;

            if (!InputManager.HasInstance) return;
            
            InputManager.Instance.InputSystemActions.Player.Interact.started -= OnInteract;
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            if (!CheckForInteractable()) return;
            
            closestInteractable?.Interact();
        }
        private void OnPlayerInput(Vector2 input)
        {
            if (input == Vector2.zero) return;
            
            currentFaceDirection = input;
        }
        private bool CheckForInteractable()
        {
            var hit = rayDetector.GetRaycastHit2D(currentFaceDirection);
            
            if (!hit || !hit.transform.TryGetComponentInHierarchy(out IInteractable interactable) 
                || !interactable.CanInteract) return false;
            
            interactable.OnTriggerRangeEnter();
            closestInteractable = interactable;

            return true;
        }
    }
}