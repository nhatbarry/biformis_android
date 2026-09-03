using CaptainPinkTurd.Core.DesignPattern.SOAP.Variables;
using CaptainPinkTurd.Input;
using UnityEngine;

public class MoveEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoolVariableSO isPlayerDashing;
    [SerializeField] private ParticleSystem moveParticles;
    [SerializeField] private TrailRenderer runTrail;

    private Rigidbody2D rb;
    private InputSystemActions playerInputs;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInputs = new InputSystemActions();
    }

    private void OnEnable()
    {
        playerInputs.Enable();
        isPlayerDashing.OnValueChanged += OnDashingChanged;
    }

    private void OnDisable()
    {
        isPlayerDashing.OnValueChanged -= OnDashingChanged;
        playerInputs.Disable();

        if (runTrail) runTrail.emitting = false;
    }

    private void Update()
    {
        HandleParticles();
        HandleTrail();
    }

    private void HandleParticles()
    {
        // Play particles when moving, stop when idle
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            if (!moveParticles.isPlaying)
                moveParticles.Play();
        }
        else
        {
            if (moveParticles.isPlaying)
                moveParticles.Stop();
        }
    }

    /// <summary>
    /// The trail marks sprinting as well as dashing.
    /// </summary>
    /// <remarks>
    /// Sprint reads the Run action here rather than riding on <c>isPlayerDashing</c>, because that flag also
    /// swaps the player onto the invincibility layer in PlayerUnit - sprinting is meant to look like a
    /// prolonged dash, not to grant a dash's invulnerability. Movement is part of the condition so a player
    /// holding the sprint button while standing still does not sit inside a trail.
    /// </remarks>
    private void HandleTrail()
    {
        if (!runTrail) return;

        bool isSprinting = playerInputs.Player.Run.IsPressed() && rb.linearVelocity.magnitude > 0.1f;
        bool shouldEmit = isPlayerDashing.Value || isSprinting;

        if (runTrail.emitting != shouldEmit) runTrail.emitting = shouldEmit;
    }

    /// <summary>Starts the trail the instant a dash begins, without waiting for the next frame.</summary>
    private void OnDashingChanged(bool isDashing)
    {
        if (isDashing && runTrail && !runTrail.emitting) runTrail.emitting = true;
    }
}
