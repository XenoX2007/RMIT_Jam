using UnityEngine;

// Nhân vật trong cutscene dùng Timeline.
// - Animation Track chỉ cho nhân vật diễn tại chỗ; Move Track di chuyển "rig" cha của nó.
//   Script này giữ nhân vật dính vào rig (bỏ root motion của clip) và đứng trên mặt đất.
// - Gesture Track điều khiển cử chỉ: lắc đầu (không), gật đầu (có), đặt tay lên vai người khác.
//   Các cử chỉ được cộng thêm lên animation đang chạy, nên vẫn đứng thở / nhảy bình thường.
[RequireComponent(typeof(Animator))]
public class CutsceneActor : MonoBehaviour
{
    [Header("Ground")]
    [SerializeField] bool snapToGround = true;
    [SerializeField] LayerMask groundLayers = 1 << 6;   // layer "Ground" (terrain)

    [Header("Shake head (no)")]
    [SerializeField] float shakeAngle = 25f;
    [Tooltip("Shakes per second.")]
    [SerializeField] float shakeSpeed = 2.2f;

    [Header("Nod (yes)")]
    [SerializeField] float nodAngle = 20f;
    [SerializeField] float nodSpeed = 1.6f;

    [Header("Hand on shoulder")]
    [SerializeField] bool useRightHand = true;
    [Tooltip("How far (degrees) the character may bend forward to reach a shorter person.")]
    [SerializeField, Range(0f, 80f)] float maxLean = 60f;
    [Tooltip("Height above the target bone where the hand rests (metres).")]
    [SerializeField] float reachHeightOffset = 0.08f;

    [Tooltip("Seconds this character is behind the others, so a crowd doesn't nod in perfect sync.")]
    [SerializeField] float gestureDelay = 0f;

    Animator animator;
    Vector3 localPosition;
    Quaternion localRotation;

    float shakeWeight, shakeTime, nodWeight, nodTime, reachWeight;
    Transform reachTarget;

    // Gesture Track gọi mỗi frame
    public void SetGestures(float shakeW, float shakeT, float nodW, float nodT, float reachW, Transform target)
    {
        shakeWeight = shakeW;
        shakeTime = shakeT;
        nodWeight = nodW;
        nodTime = nodT;
        reachWeight = reachW;
        reachTarget = target;
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        localPosition = transform.localPosition;
        localRotation = transform.localRotation;
    }

    void LateUpdate()
    {
        // Animation (đã chạy xong frame này) có thể đẩy nhân vật đi; kéo về đúng chỗ trong rig
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        if (snapToGround) SnapToGround();

        if (animator == null || !animator.isHuman) return;
        if (reachWeight > 0.001f && reachTarget != null) Reach();

        float shake = shakeWeight > 0.001f ? Mathf.Sin((shakeTime - gestureDelay) * Mathf.PI * 2f * shakeSpeed) * shakeAngle * shakeWeight : 0f;
        float nod = nodWeight > 0.001f ? (0.5f - 0.5f * Mathf.Cos(Mathf.Max(0f, nodTime - gestureDelay) * Mathf.PI * 2f * nodSpeed)) * nodAngle * nodWeight : 0f;
        if (shake != 0f) TurnHead(transform.up, shake);
        if (nod != 0f) TurnHead(transform.right, nod);
    }

    void SnapToGround()
    {
        Vector3 p = transform.position;
        if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 12f, groundLayers, QueryTriggerInteraction.Ignore))
            transform.position = new Vector3(p.x, hit.point.y, p.z);
    }

    // Cổ xoay một phần, đầu xoay phần còn lại cho tự nhiên
    void TurnHead(Vector3 axis, float angle)
    {
        Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (neck != null) neck.rotation = Quaternion.AngleAxis(angle * 0.35f, axis) * neck.rotation;
        if (head != null) head.rotation = Quaternion.AngleAxis(angle * (neck != null ? 0.65f : 1f), axis) * head.rotation;
    }

    // Cúi người (nếu cần) rồi đưa tay tới điểm cần chạm: IK 2 khớp (vai - khuỷu - bàn tay)
    void Reach()
    {
        Transform upper = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        Transform lower = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        Transform hand = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        if (upper == null || lower == null || hand == null) return;

        Vector3 target = reachTarget.position + Vector3.up * reachHeightOffset;
        float a = Vector3.Distance(upper.position, lower.position);
        float b = Vector3.Distance(lower.position, hand.position);
        float reach = (a + b) * 0.97f;

        // 1. Cúi về phía mục tiêu cho tới khi với tới
        Vector3 flat = target - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, flat.normalized);
            Transform[] spine =
            {
                animator.GetBoneTransform(HumanBodyBones.Spine),
                animator.GetBoneTransform(HumanBodyBones.Chest),
                animator.GetBoneTransform(HumanBodyBones.UpperChest)
            };
            float[] share = { 0.45f, 0.35f, 0.2f };
            Quaternion[] original = new Quaternion[spine.Length];
            for (int i = 0; i < spine.Length; i++) if (spine[i] != null) original[i] = spine[i].localRotation;

            float lean = 0f;
            while (lean < maxLean && Vector3.Distance(upper.position, target) > reach)
            {
                lean = Mathf.Min(maxLean, lean + 2.5f);
                ApplyLean(spine, original, share, axis, lean);
            }
            ApplyLean(spine, original, share, axis, lean * reachWeight);
        }

        // 2. IK cánh tay, khuỷu tay hướng xuống và ra ngoài
        Vector3 s = upper.position;
        Vector3 d = target - s;
        float c = Mathf.Clamp(d.magnitude, 0.01f, (a + b) * 0.999f);
        Vector3 dir = d.normalized;
        Vector3 side = useRightHand ? transform.right : -transform.right;
        Vector3 pole = (side * 0.6f - Vector3.up).normalized;
        Vector3 normal = Vector3.Cross(dir, pole);
        if (normal.sqrMagnitude < 0.0001f) normal = Vector3.Cross(dir, transform.forward);
        normal.Normalize();
        float angle = Mathf.Acos(Mathf.Clamp((a * a + c * c - b * b) / (2f * a * c), -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 elbowA = s + Quaternion.AngleAxis(angle, normal) * dir * a;
        Vector3 elbowB = s + Quaternion.AngleAxis(-angle, normal) * dir * a;
        Vector3 elbow = Vector3.Dot(elbowA - s, pole) > Vector3.Dot(elbowB - s, pole) ? elbowA : elbowB;

        Quaternion upperIK = Quaternion.FromToRotation(lower.position - s, elbow - s) * upper.rotation;
        upper.rotation = Quaternion.Slerp(upper.rotation, upperIK, reachWeight);
        Quaternion lowerIK = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
        lower.rotation = Quaternion.Slerp(lower.rotation, lowerIK, reachWeight);
    }

    static void ApplyLean(Transform[] spine, Quaternion[] original, float[] share, Vector3 axis, float lean)
    {
        // Trả về tư thế gốc rồi cúi lại từ đầu (xương dưới quay sẽ kéo xương trên theo)
        for (int i = 0; i < spine.Length; i++) if (spine[i] != null) spine[i].localRotation = original[i];
        for (int i = 0; i < spine.Length; i++)
            if (spine[i] != null) spine[i].rotation = Quaternion.AngleAxis(lean * share[i], axis) * spine[i].rotation;
    }
}
