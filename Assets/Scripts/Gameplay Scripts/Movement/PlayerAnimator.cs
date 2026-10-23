using UnityEngine;
using UnityEngine.InputSystem;

// Feeds the player's movement into the Animator (PlayerController).
// Swimming sets IsSwimming / IsFloating itself; this sets Speed, IsRunning, the walk/run playback speed and the jump.
// Jump: Movement does the physics; this plays the Jump clip, holds the falling pose while still in the air
// and plays the landing once the feet touch the ground.
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rb;
    [Tooltip("Ground speed (m/s) at which the walk clip plays at normal speed (the clip's own stride speed at this model size).")]
    [SerializeField] private float walkAnimSpeedRef = 2.03f;
    [SerializeField] private float minWalkAnimSpeed = 0.8f;
    [SerializeField] private float maxWalkAnimSpeed = 2f;
    [SerializeField] private float speedSmoothing = 10f;

    [Header("Run (hold Sprint)")]
    [SerializeField] private InputActionReference sprintAction;
    [Tooltip("Ground speed (m/s) at which the run clip plays at normal speed.")]
    [SerializeField] private float runAnimSpeedRef = 7f;
    [SerializeField] private float minRunAnimSpeed = 0.6f;
    [SerializeField] private float maxRunAnimSpeed = 1.5f;
    [Tooltip("Slower than this the player walks even while holding Sprint.")]
    [SerializeField] private float minRunSpeed = 1f;

    [Header("Jump (same input / ground check as Movement)")]
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private Swimming swimming;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Point of the Jump clip (0-1) where the feet come back down. The clip waits here until the player lands.")]
    [SerializeField, Range(0f, 1f)] private float landTime = 0.58f;
    [Tooltip("Never hold the falling pose longer than this (seconds), whatever the player landed on.")]
    [SerializeField] private float maxHoldTime = 1.5f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int WalkAnimSpeedId = Animator.StringToHash("WalkAnimSpeed");
    private static readonly int RunAnimSpeedId = Animator.StringToHash("RunAnimSpeed");
    private static readonly int IsRunningId = Animator.StringToHash("IsRunning");
    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int IsGroundedId = Animator.StringToHash("IsGrounded");
    private static readonly int JumpAnimSpeedId = Animator.StringToHash("JumpAnimSpeed");

    private float speed;
    private float holdTimer;
    private int jumpTriggerFrame = -1;
    private readonly Collider[] landHits = new Collider[8];

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (swimming == null) swimming = GetComponent<Swimming>();
    }

    private void OnEnable()
    {
        if (jumpAction != null) jumpAction.action.performed += OnJump;
    }

    private void OnDisable()
    {
        if (jumpAction != null) jumpAction.action.performed -= OnJump;
    }

    // Movement's own ground check: decides whether a jump really happens
    private bool Grounded => groundCheck != null && Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);

    // Landing: ground, or any solid thing under the feet (house floor, boat, rock... not only the Ground layer)
    private bool Landed()
    {
        if (Grounded) return true;
        if (groundCheck == null) return false;
        int count = Physics.OverlapSphereNonAlloc(groundCheck.position, groundCheckRadius * 1.5f, landHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!landHits[i].transform.IsChildOf(transform)) return true;
        return false;
    }

    // Same rule as Movement.OnJump: only from the ground (Space in the water is Swimming's climb)
    private void OnJump(InputAction.CallbackContext context)
    {
        if (animator == null || !Grounded) return;
        if (swimming != null && swimming.InWater) return;
        animator.SetTrigger(JumpId);
        jumpTriggerFrame = Time.frameCount;
    }

    private void Update()
    {
        if (animator == null || rb == null) return;

        Vector3 velocity = rb.isKinematic ? Vector3.zero : rb.linearVelocity;
        velocity.y = 0f;
        speed = Mathf.Lerp(speed, velocity.magnitude, speedSmoothing * Time.deltaTime);

        bool sprinting = sprintAction != null && sprintAction.action.IsPressed();
        animator.SetFloat(SpeedId, speed);
        animator.SetBool(IsRunningId, sprinting && speed > minRunSpeed);
        animator.SetFloat(WalkAnimSpeedId, Mathf.Clamp(speed / walkAnimSpeedRef, minWalkAnimSpeed, maxWalkAnimSpeed));
        animator.SetFloat(RunAnimSpeedId, Mathf.Clamp(speed / runAnimSpeedRef, minRunAnimSpeed, maxRunAnimSpeed));

        bool inWater = swimming != null && swimming.InWater;
        bool landed = Landed() || inWater || rb.isKinematic;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool inJump = state.shortNameHash == JumpId;

        // A Jump pressed while throwing / emoting must not fire later
        bool toJump = inJump || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == JumpId);
        if (jumpTriggerFrame >= 0 && (toJump || Time.frameCount - jumpTriggerFrame > 3))
        {
            if (!toJump) animator.ResetTrigger(JumpId);
            jumpTriggerFrame = -1;
        }

        // Still in the air at the landing part of the clip: hold the pose until the feet touch down
        if (!inJump) holdTimer = 0f;
        bool holding = inJump && !landed && state.normalizedTime >= landTime && holdTimer < maxHoldTime;
        if (holding) holdTimer += Time.deltaTime;   // safety: after maxHoldTime the clip plays on, never frozen
        if (holdTimer >= maxHoldTime) landed = true;

        animator.SetBool(IsGroundedId, landed || !inJump);
        animator.SetFloat(JumpAnimSpeedId, holding ? 0f : 1f);
    }
}
