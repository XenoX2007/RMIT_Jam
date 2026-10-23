using UnityEngine;

// Cho vật nổi trên mặt nước (thuyền, chum) dập dềnh theo sóng:
// nhấp nhô lên xuống, chúi/ngóc theo con sóng, lắc qua lại và xoay nhẹ.
// Sóng chạy theo một hướng, nên các vật ở gần nhau chuyển động gần giống nhau.
// Chỉ chạy lúc Play, vị trí và góc nghiêng đặt trong scene được giữ làm gốc.
public class WaterBob : MonoBehaviour
{
    [Header("Bob (up / down)")]
    [SerializeField] float bobHeight = 0.08f;
    [SerializeField] float bobSpeed = 1.1f;          // radians per second

    [Header("Rock (tilt)")]
    [Tooltip("Tilt forward/back as the wave passes under.")]
    [SerializeField] float pitchAngle = 3f;
    [Tooltip("Tilt side to side.")]
    [SerializeField] float rollAngle = 4f;
    [SerializeField] float rockSpeed = 0.9f;

    [Header("Drift (slow turn)")]
    [SerializeField] float yawAngle = 5f;
    [SerializeField] float yawSpeed = 0.12f;

    [Header("Wave")]
    [Tooltip("Metres between two wave crests: objects this far apart move in step.")]
    [SerializeField] float waveLength = 14f;
    [SerializeField] Vector2 waveDirection = new Vector2(1f, 0.6f);

    Vector3 basePosition;
    Quaternion baseRotation;
    Vector3 waveDir;
    Vector3 pitchAxis;
    float phase;
    float seed;

    void Start()
    {
        basePosition = transform.position;
        baseRotation = transform.rotation;

        Vector2 dir = waveDirection.sqrMagnitude > 0f ? waveDirection.normalized : Vector2.right;
        waveDir = new Vector3(dir.x, 0f, dir.y);
        pitchAxis = Vector3.Cross(Vector3.up, waveDir);

        // Vị trí dọc theo hướng sóng quyết định pha, thêm chút ngẫu nhiên để không đều tăm tắp
        phase = Vector3.Dot(basePosition, waveDir) / waveLength * Mathf.PI * 2f;
        seed = Random.Range(0f, 100f);
    }

    void Update()
    {
        float t = Time.time;

        float bob = Mathf.Sin(t * bobSpeed - phase) * bobHeight
                  + Mathf.Sin(t * bobSpeed * 1.7f + seed) * bobHeight * 0.3f;

        // Sườn sóng: chúi theo độ dốc của con sóng (lệch pha 90° so với nhấp nhô)
        float pitch = Mathf.Cos(t * bobSpeed - phase) * pitchAngle;
        float roll = Mathf.Sin(t * rockSpeed + seed) * rollAngle;
        float yaw = (Mathf.PerlinNoise(seed, t * yawSpeed) - 0.5f) * 2f * yawAngle;

        transform.SetPositionAndRotation(
            basePosition + Vector3.up * bob,
            Quaternion.AngleAxis(yaw, Vector3.up)
                * Quaternion.AngleAxis(pitch, pitchAxis)
                * Quaternion.AngleAxis(roll, waveDir)
                * baseRotation);
    }
}
