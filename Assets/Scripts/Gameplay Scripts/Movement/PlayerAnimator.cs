using UnityEngine;

// Feeds the player's movement into the Animator (PlayerController).
// Swimming sets IsSwimming / IsFloating itself; this sets Speed and the walk playback speed.
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rb;
    [Tooltip("Ground speed (m/s) at which the walk clip plays at normal speed.")]
    [SerializeField] private float walkAnimSpeedRef = 2.2f;
    [SerializeField] private float minWalkAnimSpeed = 0.8f;
    [SerializeField] private float maxWalkAnimSpeed = 2f;
    [SerializeField] private float speedSmoothing = 10f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int WalkAnimSpeedId = Animator.StringToHash("WalkAnimSpeed");

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

        animator.SetFloat(SpeedId, speed);
        animator.SetFloat(WalkAnimSpeedId, Mathf.Clamp(speed / walkAnimSpeedRef, minWalkAnimSpeed, maxWalkAnimSpeed));
    }
}
