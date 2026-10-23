using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Aims and plays the throw for the held item.
// While holding: point the mouse where to throw. An arc and a landing ring show exactly where it lands
// (red when it locks onto an enemy near the aim point).
// Throw: the player stops and faces the target, the item follows both hands over the head,
// is let go at releaseTime and flies on an arc that lands on the target.
public class PlayerThrower : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Movement movement;
    [SerializeField] private Swimming swimming;
    [SerializeField] private Transform cameraTransform;

    [Header("Aim")]
    [SerializeField] private float maxRange = 20f;
    [Tooltip("An enemy this close (metres) to the aim point is locked onto, so the throw hits it.")]
    [SerializeField] private float assistRadius = 3f;
    [Tooltip("Horizontal speed of the throw (m/s). Lower = higher, slower arc.")]
    [SerializeField] private float throwSpeed = 12f;

    [Header("Animation")]
    [Tooltip("Point of the Throw clip (0-1) where the hands let go.")]
    [SerializeField, Range(0f, 1f)] private float releaseTime = 0.5f;
    [Tooltip("Point of the Throw clip (0-1) where the player can move again.")]
    [SerializeField, Range(0f, 1f)] private float recoverTime = 0.8f;
    [Tooltip("Where the item sits relative to the middle of both hands (in the player's space).")]
    [SerializeField] private Vector3 handOffset = new Vector3(0f, 0.05f, 0.08f);
    [Tooltip("Seconds to move the item from the carry hand into both hands.")]
    [SerializeField] private float graspTime = 0.2f;
    [Tooltip("Roughly where the hands let go, in the player's space (used for the preview arc).")]
    [SerializeField] private Vector3 releasePoint = new Vector3(0f, 0.65f, 0.05f);

    [Header("Preview")]
    [SerializeField] private Color previewColor = new Color(1f, 0.95f, 0.6f, 0.9f);
    [SerializeField] private Color lockedColor = new Color(1f, 0.25f, 0.2f, 0.95f);
    [SerializeField] private float previewWidth = 0.06f;
    [SerializeField] private float markerRadius = 0.5f;

    private static readonly int ThrowId = Animator.StringToHash("Throw");
    private const int ArcPoints = 32;
    private const int RingPoints = 28;

    private Rigidbody rb;
    private Camera cam;
    private Enemy[] enemies;
    private ThrowableObject heldItem;
    private bool followHands;
    private Vector3 carryLocal;     // where the item was carried, in the player's space
    private float graspTimer;

    private LineRenderer arc;
    private LineRenderer ring;

    // Current aim
    private Vector3 aimPoint;
    private Enemy aimEnemy;
    private bool hasAim;

    public bool IsThrowing { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (movement == null) movement = GetComponent<Movement>();
        if (swimming == null) swimming = GetComponent<Swimming>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        arc = CreateLine("Throw Arc", false);
        ring = CreateLine("Throw Landing", true);
        ShowPreview(false);
    }

    private void Start()
    {
        enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
    }

    private void Update()
    {
        bool aiming = ThrowableObject.HeldObject != null && !IsThrowing && (swimming == null || !swimming.InWater);
        if (!aiming)
        {
            if (!IsThrowing) hasAim = false;
            ShowPreview(false);
            return;
        }

        UpdateAim();
        Vector3 start = transform.TransformPoint(releasePoint);
        DrawPreview(start, Launch(start, AimTarget(), out float time), time);
    }

    public bool Throw(ThrowableObject item)
    {
        if (IsThrowing || animator == null) return false;
        if (swimming != null && swimming.InWater) return false;   // no throwing while swimming

        // Throw exactly where the preview was showing (aim is refreshed every frame while holding)
        if (!hasAim) UpdateAim();
        StartCoroutine(ThrowRoutine(item));
        return true;
    }

    private IEnumerator ThrowRoutine(ThrowableObject item)
    {
        IsThrowing = true;
        heldItem = item;
        ShowPreview(false);

        // Stand still and face the target
        if (movement != null) movement.CanMove = false;
        StopHorizontal();
        Vector3 look = AimTarget() - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
        {
            Quaternion facing = Quaternion.LookRotation(look);
            rb.rotation = facing;
            transform.rotation = facing;
        }

        animator.SetTrigger(ThrowId);

        // Wait for the Throw state to start
        float timeout = 0.5f;
        while (!InThrowState() && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            StopHorizontal();
            yield return null;
        }

        // Item moves from the carry hand into both hands and rides there until the release point
        carryLocal = transform.InverseTransformPoint(item.transform.position);
        item.TakeFromHolder();
        graspTimer = 0f;
        followHands = true;
        while (InThrowState() && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < releaseTime)
        {
            StopHorizontal();
            yield return null;
        }
        followHands = false;

        // Aim from where the item really is now (a locked enemy may have moved meanwhile)
        item.Release(Launch(item.transform.position, AimTarget(), out _));
        heldItem = null;

        // Finish the follow-through, then give control back
        while (InThrowState() && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < recoverTime)
        {
            StopHorizontal();
            yield return null;
        }

        animator.ResetTrigger(ThrowId);
        if (movement != null) movement.CanMove = true;
        IsThrowing = false;
    }

    // After the Animator has posed the bones this frame
    private void LateUpdate()
    {
        if (!followHands || heldItem == null) return;

        Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (left == null || right == null) return;

        Vector3 hands = (left.position + right.position) * 0.5f + transform.TransformDirection(handOffset);

        // Ease from the carry hand into both hands, then stay in them
        graspTimer += Time.deltaTime;
        float t = graspTime > 0f ? Mathf.Clamp01(graspTimer / graspTime) : 1f;
        t = t * t * (3f - 2f * t);
        heldItem.transform.position = Vector3.Lerp(transform.TransformPoint(carryLocal), hands, t);
    }

    // ---------- Aim ----------

    private void UpdateAim()
    {
        if (cam == null) cam = Camera.main;
        Mouse mouse = Mouse.current;
        if (cam == null || mouse == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        float best = maxRange * 3f;
        Vector3 point = ray.GetPoint(best);
        Enemy hitEnemy = null;

        // Nearest solid thing under the cursor (not the player or the held item)
        foreach (RaycastHit hit in Physics.RaycastAll(ray, best, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance >= best || hit.transform.IsChildOf(transform)) continue;
            if (ThrowableObject.HeldObject != null && hit.transform.IsChildOf(ThrowableObject.HeldObject.transform)) continue;
            best = hit.distance;
            point = hit.point;
            hitEnemy = hit.collider.GetComponentInParent<Enemy>();
        }

        // The water surface has no collider, so test it as a flat plane
        if (swimming != null && Mathf.Abs(ray.direction.y) > 0.001f && swimming.SurfaceHeight(out float level))
        {
            float d = (level - ray.origin.y) / ray.direction.y;
            if (d > 0f && d < best && swimming.WaterLevelAt(ray.GetPoint(d), out _))
            {
                best = d;
                point = ray.GetPoint(d);
                hitEnemy = null;
            }
        }

        // Keep it within throwing range
        Vector3 flat = point - transform.position;
        flat.y = 0f;
        if (flat.magnitude > maxRange)
            point = transform.position + flat.normalized * maxRange + Vector3.up * (point.y - transform.position.y);

        // Lock onto an enemy near the aim point
        aimEnemy = hitEnemy;
        if (aimEnemy == null && enemies != null)
        {
            float closest = assistRadius;
            foreach (Enemy enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                Vector3 e = enemy.transform.position - point;
                e.y = 0f;
                if (e.magnitude < closest && Flat(enemy.transform.position - transform.position) <= maxRange + assistRadius)
                {
                    closest = e.magnitude;
                    aimEnemy = enemy;
                }
            }
        }

        aimPoint = point;
        hasAim = true;
    }

    private Vector3 AimTarget()
    {
        return aimEnemy != null ? aimEnemy.transform.position : aimPoint;
    }

    // Velocity that carries the item from start to target (always lands on it)
    private Vector3 Launch(Vector3 start, Vector3 target, out float time)
    {
        float g = -Physics.gravity.y;
        Vector3 delta = target - start;
        time = Mathf.Max(Flat(delta) / throwSpeed, 0.3f);
        Vector3 velocity = delta / time;
        velocity.y += 0.5f * g * time;
        return velocity;
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }

    // ---------- Preview ----------

    private LineRenderer CreateLine(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = loop ? RingPoints : ArcPoints;
        line.widthMultiplier = previewWidth;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = new Material(Shader.Find("Sprites/Default"));
        return line;
    }

    private void ShowPreview(bool show)
    {
        if (arc != null) arc.enabled = show;
        if (ring != null) ring.enabled = show;
    }

    private void DrawPreview(Vector3 start, Vector3 velocity, float time)
    {
        Color color = aimEnemy != null ? lockedColor : previewColor;
        Color faded = color;
        faded.a *= 0.35f;
        arc.startColor = faded;
        arc.endColor = color;
        ring.startColor = ring.endColor = color;

        for (int i = 0; i < ArcPoints; i++)
        {
            float t = time * i / (ArcPoints - 1);
            arc.SetPosition(i, start + velocity * t + 0.5f * Physics.gravity * t * t);
        }

        Vector3 center = AimTarget();
        if (aimEnemy == null) center += Vector3.up * 0.05f;
        for (int i = 0; i < RingPoints; i++)
        {
            float a = i * Mathf.PI * 2f / RingPoints;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * markerRadius);
        }

        ShowPreview(true);
    }

    private bool InThrowState()
    {
        return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == ThrowId;
    }

    private void StopHorizontal()
    {
        if (rb.isKinematic) return;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }
}
