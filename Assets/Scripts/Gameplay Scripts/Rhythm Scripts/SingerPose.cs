using UnityEngine;

// Tư thế "đang hát" cho nhân vật trong trận đấu nhịp điệu (cộng thêm lên animation đang chạy).
// Mỗi lần đánh trúng nốt (Sing): giơ một tay lên cao như ca sĩ tuồng, đổi tay luân phiên, ngẩng đầu.
// Khi đang hát còn lắc lư nhẹ theo nhịp. Bị trúng đòn (Stop) thì hạ tay xuống.
[RequireComponent(typeof(Animator))]
public class SingerPose : MonoBehaviour
{
    [Header("Raised hand")]
    [Tooltip("Where the raised hand goes, relative to its shoulder: x = out to the side, y = up, z = forward (times arm length).")]
    [SerializeField] Vector3 handGoal = new Vector3(0.45f, 0.75f, 0.35f);
    [Tooltip("Seconds the hand stays up after a hit.")]
    [SerializeField] float holdTime = 0.9f;
    [SerializeField] float raiseSpeed = 8f;
    [SerializeField] float lowerSpeed = 3f;
    [Tooltip("Hits in a row before the other hand joins in (both hands up).")]
    [SerializeField] int bothHandsCombo = 6;

    [Header("Singing")]
    [SerializeField] float chinUp = 12f;
    [SerializeField] float swayAngle = 4f;
    [SerializeField] float bpm = 110f;

    Animator animator;
    float leftWeight, rightWeight, singWeight;
    float leftTimer, rightTimer;
    bool nextRight = true;
    int combo;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // Đánh trúng một nốt
    public void Sing()
    {
        combo++;
        if (combo >= bothHandsCombo) { leftTimer = rightTimer = holdTime; }
        else if (nextRight) rightTimer = holdTime;
        else leftTimer = holdTime;
        nextRight = !nextRight;
    }

    // Trượt / bị đánh: hạ tay
    public void Stop()
    {
        combo = 0;
        leftTimer = rightTimer = 0f;
    }

    void LateUpdate()
    {
        if (animator == null || !animator.isHuman) return;
        float dt = Time.deltaTime;
        leftTimer -= dt;
        rightTimer -= dt;
        leftWeight = Approach(leftWeight, leftTimer > 0f ? 1f : 0f, dt);
        rightWeight = Approach(rightWeight, rightTimer > 0f ? 1f : 0f, dt);
        singWeight = Approach(singWeight, Mathf.Max(leftTimer, rightTimer) > 0f ? 1f : 0f, dt);

        // Lắc lư theo nhịp + ngẩng đầu khi hát
        if (singWeight > 0.001f)
        {
            float beat = Time.time * bpm / 60f * Mathf.PI;
            Rotate(HumanBodyBones.Spine, transform.forward, Mathf.Sin(beat) * swayAngle * singWeight);
            Rotate(HumanBodyBones.Head, transform.right, -chinUp * singWeight);
        }
        if (rightWeight > 0.001f) RaiseHand(true, rightWeight);
        if (leftWeight > 0.001f) RaiseHand(false, leftWeight);
    }

    float Approach(float value, float target, float dt)
    {
        return Mathf.MoveTowards(value, target, (target > value ? raiseSpeed : lowerSpeed) * dt);
    }

    void Rotate(HumanBodyBones bone, Vector3 axis, float angle)
    {
        Transform t = animator.GetBoneTransform(bone);
        if (t != null) t.rotation = Quaternion.AngleAxis(angle, axis) * t.rotation;
    }

    // IK 2 khớp: đưa bàn tay lên điểm phía trên - bên ngoài vai
    void RaiseHand(bool right, float weight)
    {
        Transform upper = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        Transform lower = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        Transform hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        if (upper == null || lower == null || hand == null) return;

        float a = Vector3.Distance(upper.position, lower.position);
        float b = Vector3.Distance(lower.position, hand.position);
        Vector3 side = right ? transform.right : -transform.right;
        Vector3 s = upper.position;
        Vector3 goal = s + (side * handGoal.x + transform.up * handGoal.y + transform.forward * handGoal.z) * (a + b);

        Vector3 d = goal - s;
        float c = Mathf.Clamp(d.magnitude, 0.01f, (a + b) * 0.999f);
        Vector3 dir = d.normalized;
        Vector3 pole = (side - transform.up * 0.3f).normalized;   // khuỷu tay hướng ra ngoài
        Vector3 normal = Vector3.Cross(dir, pole);
        if (normal.sqrMagnitude < 0.0001f) normal = Vector3.Cross(dir, transform.forward);
        normal.Normalize();
        float angle = Mathf.Acos(Mathf.Clamp((a * a + c * c - b * b) / (2f * a * c), -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 elbowA = s + Quaternion.AngleAxis(angle, normal) * dir * a;
        Vector3 elbowB = s + Quaternion.AngleAxis(-angle, normal) * dir * a;
        Vector3 elbow = Vector3.Dot(elbowA - s, pole) > Vector3.Dot(elbowB - s, pole) ? elbowA : elbowB;

        Quaternion upperIK = Quaternion.FromToRotation(lower.position - s, elbow - s) * upper.rotation;
        upper.rotation = Quaternion.Slerp(upper.rotation, upperIK, weight);
        Quaternion lowerIK = Quaternion.FromToRotation(hand.position - lower.position, goal - lower.position) * lower.rotation;
        lower.rotation = Quaternion.Slerp(lower.rotation, lowerIK, weight);
    }
}
