using CaptainPinkTurd.Core;
using CaptainPinkTurd.Core.Attributes;
using UnityEngine;
using CaptainPinkTurd.Core.DesignPattern.Singleton;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Game.Player;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CaptainPinkTurd.Game
{
    public class GameManager : Singleton<GameManager>
    {
        [Header("GameManager Properties")]
        [SerializeField] private float gameFastForwardSpeed = 2;
        [SerializeField] private VoidEvent onGameOver;
        
        [Header("Game Specific Variables")]
        [SerializeField] private EnumColorEvent onDimensionChange;
        [SerializeField, ReadOnly] private EColor currentDimension;
            
        public GameEvent OnGameOver = new GameEvent();

        private float oldTimeScale;

        protected override void Awake()
        {
            base.Awake();
            
            switchDimensionAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/j");
            // Second binding so the mobile HUD's dimension button drives the exact same action as the J key.
            switchDimensionAction.AddBinding(MobileControlPaths.SwitchDimension);
        }

        private void Start()
        {
            currentDimension = EColor.Red;
            onDimensionChange.Raise(currentDimension);
        }

        private void OnEnable()
        {
            switchDimensionAction.Enable();
            switchDimensionAction.performed += DimensionSwitch;
            
            OnGameOver.Subscribe(OnGameOverEvents);
        }
        private void OnDisable()
        {
            switchDimensionAction.performed -= DimensionSwitch;
            switchDimensionAction.Disable();
            
            OnGameOver.Unsubscribe(OnGameOverEvents);
        }

        private void OnGameOverEvents()
        {
            onGameOver.Raise();
            switchDimensionAction.Disable();
        }
        public void SceneReset()
        {
            Debug.Log($"Reset scene: {SceneManager.GetActiveScene().name}");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            SetTimeScale(1); //in case Timescale got set to something else when scene reset
        }

        public void SetTimeScale(float timeScale)
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = timeScale;
        }
        public void ResetTimeScale()
        {
            Time.timeScale = oldTimeScale;
        }
        public void QuitGame()
        {
            #if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
                return;
            }
            #endif
            
            Application.Quit();
        }

        #region GAME SPECIFIC REGION

        private InputAction switchDimensionAction;
        private PlayerUnit playerUnit;
        private int playerCurrentHealth;
        private bool isLightDimension;
        private void DimensionSwitch(InputAction.CallbackContext obj)
        {
            currentDimension = currentDimension == EColor.Red ? EColor.Blue : EColor.Red;
            onDimensionChange.Raise(currentDimension);
        }

        public void OnLevelSceneLoaded()
        {
            onDimensionChange.Raise(currentDimension);
            
            playerUnit = FindAnyObjectByType<PlayerUnit>();

            if (!playerUnit || !playerUnit.TryGetComponent(out IDamageable playerDamageable)) return;
            
            playerUnit.OnDisableEvents.Subscribe(() =>
            {
                playerCurrentHealth = playerDamageable.CurrentHealth;
            }, rememberListener: false);

            if (playerCurrentHealth <= 0) return;
            
            var damageHasTaken = Mathf.Abs(playerCurrentHealth - playerDamageable.CurrentHealth);
            playerDamageable.TakeDamage(new SDamageData(damageHasTaken, gameObject));
        }
        public void OnGameRestartEvents()
        {
            switchDimensionAction.Enable();
            currentDimension = EColor.Red;
            
            if (!playerUnit || !playerUnit.TryGetComponent(out IDamageable playerDamageable)) return;
            
            playerUnit.OnDisableEvents.Clear();
            playerCurrentHealth = playerDamageable.MaxHealth;
        }

        #endregion
    }
}