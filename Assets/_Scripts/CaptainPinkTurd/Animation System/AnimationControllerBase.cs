using System;
using CaptainPinkTurd.Core.Base;
using CaptainPinkTurd.ImprovedTimers;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CaptainPinkTurd.AnimationSystem
{
    public abstract class AnimationControllerBase : GameObjectBase
    {
        protected Animator animator;
        protected CountdownTimer timer;
        protected float crossfadeTime = 0;
        
        protected SpriteRenderer spriteRenderer;
        
        private bool isAnimationPaused;
        private int pausedAnimationHash;
        private bool didntTriggerMidAnimation;
        
        public bool IsAnimationPaused => isAnimationPaused;
        public abstract int DefaultAnimationHash { get; set; }
        
        protected override void Awake()
        {
            base.Awake();
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            //stop the timer from ticking after this object is disabled/destroyed (e.g. scene unload),
            //otherwise the static TimerManager keeps ticking it and its callback touches a destroyed Animator
            timer?.Dispose();
        }
        public void PlayAnimation(string animationName)
        {
            Debug.Log("Playing animation: " + animationName);
            PlayAnimation(Animator.StringToHash(animationName));
        }
        
        public void PlayAnimation(int animationHash, float midEventTimer = 0f, 
            Action onMidAnimation = null, Action onAnimationEnd = null,
            bool playInReverse = false, bool isClamp = false, float speed = 1f)
        {
            if (!gameObject.activeInHierarchy || !gameObject.activeSelf) return;

            // Retire the timer this call is about to replace. Start() registers every timer with the static
            // TimerManager, so one that is simply overwritten stays in that list ticking forever, and its
            // OnTimerStop still fires later and cross-fades back to the default animation - fighting whatever
            // is playing by then. Dispose only deregisters, so no end-callback fires for the interrupted clip.
            timer?.Dispose();


            if (playInReverse)
            {
                var clip = GetAnimationClip(animationHash); // You must implement this
                var graph = PlayableGraph.Create();
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetTime(clip.length);
                playable.SetSpeed(-1 * speed);

                var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
                output.SetSourcePlayable(playable);
                graph.Evaluate(); // ← This fixes the first-frame flicker
                
                timer = new CountdownTimer(clip.length, midEventTimer); // Or shorter if crossfade still applies
                timer.OnTimerStart += () => graph.Play();
                timer.OnMidTimer += onMidAnimation;
                timer.OnTimerStop += () =>
                {
                    graph.Destroy();
                    animator.Rebind(); // Reset animator state
                    animator.Update(0f); // Force immediate update
                    if (animator && !isClamp && !isAnimationPaused)
                    {
                        animator.CrossFade(DefaultAnimationHash, crossfadeTime, 0, 0f);
                    }
                    timer.Dispose();
                    onAnimationEnd?.Invoke(); //this order matter
                };
                timer.Start();
            }
            else
            {
                timer = new CountdownTimer(GetAnimationClipLength(animationHash) - crossfadeTime, midEventTimer);
                timer.OnTimerStart += () =>
                {
                    animator.speed = speed;
                    animator.CrossFade(animationHash, crossfadeTime, 0, 0f);
                };
                timer.OnMidTimer += onMidAnimation;
                timer.OnTimerStop += () =>
                {
                    if (animator && !isClamp && !isAnimationPaused)
                    {
                        animator.CrossFade(DefaultAnimationHash, crossfadeTime);
                    }
                    timer.Dispose();
                    onAnimationEnd?.Invoke(); //this order matter
                };
                timer.Start();
            }
        }
        public void PauseCurrentAnimation(float exactPauseFrame)
        {
            if (timer is not { IsRunning: true }) return;
            
            timer.Pause();
            isAnimationPaused = true;
            
            animator.speed = 0;
            animator.Update(0f); 
            
            // Store the current animation state
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            pausedAnimationHash = stateInfo.shortNameHash;
            didntTriggerMidAnimation = !timer.HasTriggeredEvent;
            
            animator.CrossFade(pausedAnimationHash, 0f, 0, exactPauseFrame);
        }
        
        public void ResumeAnimation(Action onMidAnimation = null, Action onAnimationEnd = null)
        {
            if (!isAnimationPaused) return;
            
            // Resume timer
            if (timer != null)
            {
                // Update callbacks for the new turn
                if (didntTriggerMidAnimation)
                {
                    timer.OnMidTimer = onMidAnimation;
                }
                timer.OnTimerStop = () =>
                {
                    onAnimationEnd?.Invoke();
                    animator.CrossFade(DefaultAnimationHash, crossfadeTime);
                };
                
                animator.speed = 1;
                timer.Resume();
            }
            
            isAnimationPaused = false;
        }
        public float GetAnimationClipLength(int animationHash)
        {
            if (!animator || !animator.runtimeAnimatorController)
            {
                Debug.LogError("Animator or animator runtime controller is null, return 0 instead");
                return 0f;
            }
            
            foreach(var clip in animator.runtimeAnimatorController.animationClips)
            {
                if(Animator.StringToHash(clip.name) == animationHash)
                {
                    return clip.length;
                }
            }

            return 0f;
        }

        public AnimationClip GetAnimationClip(int animationHash)
        {
            foreach(var clip in animator.runtimeAnimatorController.animationClips)
            {
                if(Animator.StringToHash(clip.name) == animationHash)
                {
                    return clip;
                }
            }
            return null;
        }
        public AnimationClip GetCurrentAnimationClip() => GetAnimationClip(animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
    }
}
