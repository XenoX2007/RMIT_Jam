using UnityEngine;
using UnityEngine.InputSystem;

// Swimming / floating, kept apart from Movement.
// In deep water this turns Movement off and moves the player itself; on land it gives control back.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Swimming : MonoBehaviour
{
    public enum SwimState { None, Floating, Swimming }

    [Header("Water")]
    [Tooltip("Water surface renderers. The player is over water when inside their bounds (X/Z).")]
    [SerializeField] private Renderer[] waterSurfaces;
    [Tooltip("How deep the feet must be under the surface to start swimming.")]
    [SerializeField] private float enterDepth = 1.1f;
    [Tooltip("Back to walking when the feet are shallower than this.")]
    [SerializeField] private float exitDepth = 0.8f;

    [Header("Floating")]
    [Tooltip("How far the body centre sits under the surface while floating (head stays above).")]
    [SerializeField] private float floatDepth = 0.35f;
    [SerializeField] private float buoyancy = 4f;
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 1.6f;

    [Header("Swimming")]
    [SerializeField] private float swimSpeed = 3f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float acceleration = 6f;
    [SerializeField] private float waterDrag = 3f;
    [SerializeField] private float rotationSpeed = 5f;
    [Tooltip("Jump in water = a push up, to climb out onto the bank.")]
    [SerializeField] private float climbOutForce = 6f;

    [Header("References")]
    [SerializeField] private Movement movement;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [Tooltip("RMB look. Movement turns it off when it is disabled, so it is kept on here while swimming.")]
    [SerializeField] private InputActionReference rightClickAction;
    [Tooltip("Same as Movement: while RMB is held the swimmer turns to face the camera.")]
    [SerializeField] private bool faceCameraWhileLooking = false;
    [Tooltip("Optional. Gets bools IsSwimming / IsFloating when the model is added.")]
    [SerializeField] private Animator animator;

    public SwimState State { get; private set; }
    public bool InWater => State != SwimState.None;

    private Rigidbody rb;
    private Collider body;
    private float defaultDamping;
    private bool jumpQueued;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        body = GetComponent<Collider>();
        if (movement == null) movement = GetComponent<Movement>();
    }

    private void OnEnable()
    {
        jumpAction.action.performed += OnJump;
    }

    private void OnDisable()
    {
        jumpAction.action.performed -= OnJump;
        if (InWater) ExitWater();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (InWater) jumpQueued = true;
    }

    private void FixedUpdate()
    {
        bool overWater = WaterLevel(out float level);
        float depth = overWater ? level - body.bounds.min.y : 0f;

        if (!InWater && overWater && depth > enterDepth) EnterWater();
        else if (InWater && (!overWater || depth < exitDepth)) ExitWater();

        if (InWater) Swim(level);
    }

    private bool WaterLevel(out float level)
    {
        level = 0f;
        Vector3 p = rb.position;
        foreach (Renderer water in waterSurfaces)
        {
            if (water == null) continue;
            Bounds b = water.bounds;
            if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
            level = b.max.y;
            return true;
        }
        return false;
    }

    private void EnterWater()
    {
        // Cancel the held move input first so Movement doesn't keep it after we take over
        moveAction.action.Disable();
        if (movement != null) movement.enabled = false;
        moveAction.action.Enable();
        jumpAction.action.Enable();
        sprintAction.action.Enable();
        if (rightClickAction != null) rightClickAction.action.Enable();

        defaultDamping = rb.linearDamping;
        rb.linearDamping = 0f;
        rb.useGravity = false;
        jumpQueued = false;
        State = SwimState.Floating;
    }

    private void ExitWater()
    {
        rb.useGravity = true;
        rb.linearDamping = defaultDamping;

        // Re-enabling the action makes Movement read the key that is held right now
        moveAction.action.Disable();
        if (movement != null) movement.enabled = true;
        moveAction.action.Enable();

        State = SwimState.None;
        UpdateAnimator();
    }

    private void Swim(float level)
    {
        Vector2 move = moveAction.action.ReadValue<Vector2>();

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        Vector3 input = Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);

        State = input.sqrMagnitude > 0.01f ? SwimState.Swimming : SwimState.Floating;

        // Horizontal: swim toward the input, drift to a stop when floating
        Vector3 velocity = rb.linearVelocity;
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        float speed = swimSpeed * (sprintAction.action.IsPressed() ? sprintMultiplier : 1f);
        float rate = State == SwimState.Swimming ? acceleration : waterDrag;
        flat = Vector3.MoveTowards(flat, input * speed, rate * speed * Time.fixedDeltaTime);

        // Vertical: spring to the float height with a gentle bob
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        float targetY = level - floatDepth + bob;
        float vertical = Mathf.Lerp(velocity.y, (targetY - rb.position.y) * buoyancy, 5f * Time.fixedDeltaTime);
        if (jumpQueued)
        {
            vertical = climbOutForce;
            jumpQueued = false;
        }

        rb.linearVelocity = new Vector3(flat.x, vertical, flat.z);

        // Face the camera while looking (optional), otherwise the swim direction; stay upright
        bool looking = faceCameraWhileLooking && rightClickAction != null && rightClickAction.action.IsPressed();
        Vector3 facing = looking ? forward : State == SwimState.Swimming ? input : transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(facing), rotationSpeed * Time.fixedDeltaTime));
        rb.angularVelocity = Vector3.zero;

        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetBool("IsSwimming", State == SwimState.Swimming);
        animator.SetBool("IsFloating", State == SwimState.Floating);
    }
}
