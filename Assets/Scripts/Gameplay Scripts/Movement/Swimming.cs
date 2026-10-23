using System.Collections;
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
    [Tooltip("Back to walking when the water under the player is shallower than this.")]
    [SerializeField] private float exitDepth = 0.8f;

    [Header("Floating")]
    [Tooltip("How far the body centre sits under the surface while floating (head stays above).")]
    [SerializeField] private float floatDepth = 0.35f;
    [Tooltip("Same while swimming. The swim animation lies flat, so the body rides higher to keep the head out.")]
    [SerializeField] private float swimDepth = -0.3f;
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

    [Header("Climb out (boats, low banks)")]
    [Tooltip("Highest edge above the water the player can pull up onto with Jump.")]
    [SerializeField] private float maxClimbHeight = 1.8f;
    [Tooltip("How far ahead to look for something to climb onto.")]
    [SerializeField] private float climbReach = 1.1f;
    [SerializeField] private float climbTime = 0.4f;

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
    private bool climbing;

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
        // Frozen (e.g. hiding inside a jar in the water): nothing to swim
        if (rb.isKinematic) return;

        bool overWater = WaterLevel(out float level);
        float depth = overWater ? level - body.bounds.min.y : 0f;

        // Leaving uses the real water depth under the player (surface to ground), not the feet,
        // so floating higher for the swim pose never counts as reaching the shore
        if (!InWater && overWater && depth > enterDepth) EnterWater();
        else if (InWater && (!overWater || level - GroundBelow() < exitDepth)) ExitWater();

        if (InWater) Swim(level);
    }

    private bool WaterLevel(out float level)
    {
        return WaterLevelAt(rb.position, out level);
    }

    // Height of the water surface (all surfaces are flat at the same level)
    public bool SurfaceHeight(out float level)
    {
        level = 0f;
        foreach (Renderer water in waterSurfaces)
        {
            if (water == null) continue;
            level = water.bounds.max.y;
            return true;
        }
        return false;
    }

    // Is this point over water, and how high is the surface there
    public bool WaterLevelAt(Vector3 p, out float level)
    {
        level = 0f;
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

    private float GroundBelow()
    {
        // Starts inside the player's own collider, so the ray doesn't hit it
        if (Physics.Raycast(rb.position, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return float.NegativeInfinity;
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
        float targetY = level - (State == SwimState.Swimming ? swimDepth : floatDepth) + bob;
        float vertical = Mathf.Lerp(velocity.y, (targetY - rb.position.y) * buoyancy, 5f * Time.fixedDeltaTime);
        if (jumpQueued && TryClimb(level))
        {
            jumpQueued = false;
            return;
        }
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

    // Jump in the water next to a boat / low bank: pull up onto it
    private bool TryClimb(float level)
    {
        if (climbing) return false;

        var capsule = body as CapsuleCollider;
        float radius = capsule != null ? capsule.radius : 0.3f;
        float halfHeight = capsule != null ? capsule.height * 0.5f : 1f;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

        // Straight ahead first, then fan out around the player
        float[] angles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 180f };
        foreach (float angle in angles)
        {
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            for (float reach = 0.4f; reach <= climbReach; reach += 0.35f)
            {
                Vector3 probe = rb.position + dir * (radius + reach);
                probe.y = level + maxClimbHeight + 0.5f;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, maxClimbHeight + 1.5f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (hit.transform.IsChildOf(transform) || hit.normal.y < 0.6f)
                    continue;

                float height = hit.point.y - level;
                if (height < 0.15f || height > maxClimbHeight)
                    continue;

                Vector3 stand = hit.point + Vector3.up * (halfHeight + 0.05f);
                if (!HasRoom(stand, radius, halfHeight))
                    continue;

                StartCoroutine(Climb(stand, hit.collider.transform, dir));
                return true;
            }
        }
        return false;
    }

    private bool HasRoom(Vector3 centre, float radius, float halfHeight)
    {
        Vector3 bottom = centre + Vector3.down * (halfHeight - radius - 0.05f);
        Vector3 top = centre + Vector3.up * (halfHeight - radius);
        foreach (Collider c in Physics.OverlapCapsule(bottom, top, radius * 0.9f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.transform.IsChildOf(transform)) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody.isKinematic && c.GetComponent<ThrowableObject>() != null) continue;   // coconuts on deck don't block
            return false;
        }
        return true;
    }

    private IEnumerator Climb(Vector3 stand, Transform onto, Vector3 dir)
    {
        climbing = true;
        rb.isKinematic = true;   // Swimming pauses while kinematic

        // Follow the boat while climbing (it bobs)
        Vector3 localStand = onto.InverseTransformPoint(stand);
        Vector3 start = rb.position;
        Quaternion startRot = rb.rotation;
        Quaternion endRot = Quaternion.LookRotation(dir);

        for (float t = 0f; t < climbTime; t += Time.deltaTime)
        {
            float k = t / climbTime;
            Vector3 target = onto != null ? onto.TransformPoint(localStand) : stand;
            // Up first, then over the edge
            float up = 1f - (1f - k) * (1f - k);
            float over = k * k * (3f - 2f * k);
            Vector3 p = new Vector3(Mathf.Lerp(start.x, target.x, over), Mathf.Lerp(start.y, target.y, up), Mathf.Lerp(start.z, target.z, over));
            rb.position = p;
            transform.position = p;
            transform.rotation = Quaternion.Slerp(startRot, endRot, over);
            yield return null;
        }

        Vector3 end = onto != null ? onto.TransformPoint(localStand) : stand;
        rb.position = end;
        transform.position = end;
        rb.rotation = endRot;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        climbing = false;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetBool("IsSwimming", State == SwimState.Swimming);
        animator.SetBool("IsFloating", State == SwimState.Floating);
    }
}
