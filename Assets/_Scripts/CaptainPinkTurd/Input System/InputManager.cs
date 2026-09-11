using CaptainPinkTurd.Core.DesignPattern.Singleton;

namespace CaptainPinkTurd.Input
{
    //TODO: Slowly adjust other scripts to use this new manager in the future
    //TODO: Might need to make this script execution order slower than all the scripts that uses this
    public class InputManager : Singleton<InputManager>
    {
        public InputSystemActions InputSystemActions { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            InputSystemActions = new InputSystemActions();
        }

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