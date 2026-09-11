namespace CaptainPinkTurd.Core.Interfaces
{
    public interface IInteractable
    {
        bool CanInteract { get; }
        void Interact();
        void OnTriggerRangeEnter();
        void OnTriggerRangeExit();
    }
}