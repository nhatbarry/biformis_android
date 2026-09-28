using CaptainPinkTurd.Core.DesignPattern.Singleton;

namespace CaptainPinkTurd.Input
{
    //TODO: Slowly adjust other scripts to use this new manager in the future
    public class InputManager : Singleton<InputManager>
    {
        private InputSystemActions inputSystemActions;

        //created on first use rather than in Awake: a user's OnEnable (e.g. DialogueManager) can run before this Awake,
        //since Unity doesn't guarantee Awake/OnEnable order across components
        public InputSystemActions InputSystemActions => inputSystemActions ??= new InputSystemActions();

        private void OnEnable()
        {
            InputSystemActions.Enable();
        }

        private void OnDisable()
        {
            InputSystemActions.Disable();
        }
    }
}
