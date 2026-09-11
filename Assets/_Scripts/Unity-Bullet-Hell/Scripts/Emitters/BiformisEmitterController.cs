using BulletHell;
using CaptainPinkTurd.AnimationSystem;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Core.Utils;
using CaptainPinkTurd.EffectSystem.ShakeEffect;
using CaptainPinkTurd.ScoreSystem;
using CaptainPinkTurd.UI.Popup;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace CaptainPinkTurd.BulletHell
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class BiformisEmitterController : AnimationControllerBase, IDamageable, IScorable
    {
        [Header("Biformis Entity Configs")]
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private float knockbackForce = 10f;
        [SerializeField] private PopupText scorePopup;
        [SerializeField] private float scoreTextTargetHeight = 1.5f;
        [SerializeField] private ScoreConfig scoreConfig;
        
        [Header("Projectile Emitter Configs")]
        [SerializeField] private ProjectileEmitterBiformis redEmitter;
        [SerializeField] private ProjectileEmitterBiformis blueEmitter;
        [SerializeField] private GameObject alertModel;
        [SerializeField] private SerializeKeyValuePair<EColor, GameObject>[] colorAlertModels;
        
        [Header("Swap Settings")]
        [SerializeField] private float colorSwapIntervalMin = 2.5f;
        [SerializeField] private float colorSwapIntervalMax = 5f;
        [SerializeField][Range(0f, 1f)] private float swapAlertTimePercentage = .85f;
        
        [Header("Impact Configs")]
        [SerializeField] private float hitStopDuration = 0.2f;
        [SerializeField] private GameObjectShakeProfile shakeProfile;
        [SerializeField] private ShockwaveScreen impactShockwavePrefab;
        [SerializeField] private BasicVfxAnimationController explosionVfx;
        [SerializeField] private UnityEvent OnDamagedTaken;
        
        [Header("SFXs")]
        [SerializeField] protected SoundData startUpSfx;
        [SerializeField] private SoundData damagedSfx;
        [SerializeField] private SoundData colorChangeAlertSfx;
        
        protected EColor currentColor;
        protected GameObject damageSource;
        
        private float colorSwapInterval;
        
        public Collider2D Coll { get; private set; }
        
        //IDamageable Variables
        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public GameEvent<SDamageData> OnTakeDamage { get; } = new GameEvent<SDamageData>();
        public GameEvent<SDamageData> OnDeath { get; } = new GameEvent<SDamageData>();

        //IScorable Variables
        public ScoreConfig ScoreConfig => scoreConfig;

        //AnimationControllerBase Variables
        public override int DefaultAnimationHash { get; set; }

        protected override void Awake()
        {
            base.Awake();
                
            Coll = GetComponent<Collider2D>();
            Coll.isTrigger = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            colorSwapInterval = Random.Range(colorSwapIntervalMin, colorSwapIntervalMax);
            alertModel.SetActive(false);
            
            CurrentHealth = maxHealth;

            ProjectileEmitterSetup(redEmitter);
            ProjectileEmitterSetup(blueEmitter);
            
            // Randomize starting state
            var probability = Random.value;
            currentColor = probability <= .5f ? EColor.Red : EColor.Blue;
            
            ApplyEmitterStates();
            StartColorCycle();
        }
        
        private void StartColorCycle()
        {
            // Trigger the alert just before the swap
            StartCoroutine(CoroutineUtils.WaitForSeconds(colorSwapInterval * swapAlertTimePercentage, OnProjectileColorChangeAlert));

            // Trigger the actual swap
            StartCoroutine(CoroutineUtils.WaitForSeconds(colorSwapInterval, () => 
            {
                currentColor = currentColor == EColor.Red ? EColor.Blue : EColor.Red;
                
                ApplyEmitterStates();
                StartColorCycle(); // Loop
            }));
        }

        private void ApplyEmitterStates()
        {
            if (currentColor == EColor.Red)
            {
                redEmitter.SyncStateFrom(blueEmitter);
        
                redEmitter.AutoFire = true;
                blueEmitter.AutoFire = false;
            }
            else
            {
                blueEmitter.SyncStateFrom(redEmitter);
        
                blueEmitter.AutoFire = true;
                redEmitter.AutoFire = false;
            }
            OnColorChangeEvent(currentColor);
        }

        protected virtual void OnColorChangeEvent(EColor color)
        {
            alertModel.SetActive(false);
            foreach (var colorModel in colorAlertModels)
            {
                colorModel.Value.SetActive(colorModel.Key == color);
            }
        }

        private void OnProjectileColorChangeAlert()
        {
            alertModel.SetActive(true);
            SoundManager.Instance.CreateSoundBuilder()
                .WithPosition(transform.position).WithRandomPitch().Play(colorChangeAlertSfx,
                    //the SoundEmitter outlives this object, so guard against alertModel being
                    //destroyed (e.g. scene unload) before the sound finishes and this callback fires
                    () => { if (alertModel) alertModel.SetActive(false); });
        }

        protected void ToggleEmitter(bool on)
        {
            if (!on)
            {
                redEmitter.enabled = false;
                redEmitter.AutoFire = false;
                blueEmitter.enabled = false;
                blueEmitter.AutoFire = false;
            }
            else
            {
                ApplyEmitterStates();
                redEmitter.enabled = true;
                blueEmitter.enabled = true;
            }
        }
        private void ProjectileEmitterSetup(ProjectileEmitterBiformis emitter)
        {
            if (!emitter.UseFollowTarget) return;
            
            var target = FindAnyObjectByType<Target>();
            if (!target)
            {
                Debug.LogError("No target found in scene");
                return;
            }
            emitter.Target = target.transform;
        }
        
        public void TakeDamage(SDamageData damageData)
        {
            if (damageSource) return;
            
            damageSource = damageData.Source;
            if(damageData.Source.transform.TryGetComponentInHierarchy(out IDamageable damageable))
            {
                var knockbackDir = (damageData.Source.transform.position - transform.position).normalized;
                damageable.WithKnockback(knockbackDir * knockbackForce, 0);
            }
            
            SoundManager.Instance.CreateSoundBuilder()
                .WithPosition(transform.position).WithRandomPitch().Play(damagedSfx);
            ObjectPoolManager.Instance.SpawnObject(impactShockwavePrefab.gameObject, transform.position, 
                Quaternion.identity, ObjectPoolManager.PoolType.VFX);
            
            //multiplier is equal to enemy current health before they died
            if(ScoreConfig.useMultiplier) ScoreConfig.runtimeMultiplier = CurrentHealth;
            CurrentHealth -= damageData.Amount;

            transform.DOShakePosition(shakeProfile.useWithHitStop ? hitStopDuration : shakeProfile.defaultShakeDuration,
                    shakeProfile.shakeStrength,
                    shakeProfile.vibration,
                    shakeProfile.randomness,
                    shakeProfile.snapping,
                    shakeProfile.fadeOut)
                .SetUpdate(UpdateType.Normal, true);
            
            HitStop.Stop(hitStopDuration, () =>
            {
                damageSource = null;
                OnDamagedTaken.Invoke();

                if (CurrentHealth > 0) return;
                
                ScoreManager.Instance.AddScore(this);
                ObjectPoolManager.Instance.SpawnObject(explosionVfx.gameObject, transform.position, Quaternion.identity,
                    ObjectPoolManager.PoolType.VFX);
                if (SpawnedFromPool)
                {
                    ObjectPoolManager.Instance.ReturnObjectToPool(gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }
            });
        }
        public Transform GetTransform() => transform;
        public void OnScored()
        {
            scorePopup.InitializeText(ScoreConfig.GetFinalScore().ToString(), transform.position, scoreTextTargetHeight);
        }
    }
}