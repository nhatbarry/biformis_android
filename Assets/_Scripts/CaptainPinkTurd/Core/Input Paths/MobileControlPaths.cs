namespace CaptainPinkTurd.Core.InputPaths
{
    /// <summary>
    /// The control paths the on-screen (touch) controls feed input into.
    /// </summary>
    /// <remarks>
    /// The touch HUD does not talk to actions directly, it simulates a virtual <c>Gamepad</c> through
    /// Unity's <c>OnScreenControl</c>. That keeps every gameplay system untouched: they all keep reading
    /// the same actions they always did, and each of the paths below is already bound to the action that
    /// its desktop key is bound to, so touch and keyboard behave identically.
    ///
    /// Desktop equivalents:
    /// <list type="bullet">
    /// <item><see cref="Move"/> - WASD / arrows.</item>
    /// <item><see cref="RunAndDash"/> - left shift. Both Run and Dash are bound to it, so holding runs and
    /// tapping dashes, exactly like the key.</item>
    /// <item><see cref="Interact"/> - E.</item>
    /// <item><see cref="SwitchDimension"/> - J. Unlike the others this one is registered in code, see
    /// <c>GameManager.Awake</c>; it is deliberately a control nothing else binds.</item>
    /// </list>
    /// </remarks>
    public static class MobileControlPaths
    {
        public const string Move = "<Gamepad>/leftStick";
        public const string RunAndDash = "<Gamepad>/leftStickPress";
        public const string Interact = "<Gamepad>/buttonNorth";
        public const string SwitchDimension = "<Gamepad>/rightShoulder";

        /// <summary>
        /// Opens and closes the pause menu, exactly as the Escape key does on desktop.
        /// </summary>
        /// <remarks>
        /// The UI map's Cancel action is bound to <c>*/{Cancel}</c>, and a gamepad's buttonEast carries the
        /// Cancel usage, so pressing this drives the same PopupActivator that Escape drives - same popup,
        /// same time-scale freeze. Player/Crouch is also bound to buttonEast but nothing reads it.
        /// </remarks>
        public const string Pause = "<Gamepad>/buttonEast";
    }
}
