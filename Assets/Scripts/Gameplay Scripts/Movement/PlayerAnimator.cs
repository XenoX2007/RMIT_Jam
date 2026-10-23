using UnityEngine;
using UnityEngine.InputSystem;

// Feeds the player's movement into the Animator (PlayerController).
// Swimming sets IsSwimming / IsFloating itself; this sets Speed, IsRunning and the walk/run playback speed.
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

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int WalkAnimSpeedId = Animator.StringToHash("WalkAnimSpeed");
    private static readonly int RunAnimSpeedId = Animator.StringToHash("RunAnimSpeed");
    private static readonly int IsRunningId = Animator.StringToHash("IsRunning");

    private float speed;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
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
    }
}
