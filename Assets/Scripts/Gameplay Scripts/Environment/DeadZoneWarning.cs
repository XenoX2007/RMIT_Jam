using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

// Gắn vào Player. Khi chạm vào vùng cấm (con của deadZoneRoot), sương mù che màn hình
// và hiện cảnh báo. Chạm liên tục đủ blindTime giây thì sương phủ kín
// và người chơi bị đưa về chỗ an toàn gần nhất.
[RequireComponent(typeof(Rigidbody))]
public class DeadZoneWarning : MonoBehaviour
{
    [Header("Zone")]
    [Tooltip("Every collider under this object counts as a dead zone.")]
    [SerializeField] Transform deadZoneRoot;
    [SerializeField] float blindTime = 8f;
    [Tooltip("How far inside the map the player is put back (metres from the zone).")]
    [SerializeField] float safeDistance = 12f;
    [SerializeField] LayerMask groundMask = ~0;

    [Header("Screen fog")]
    [SerializeField] CanvasGroup fogGroup;
    [SerializeField] Image fogVignette;
    [SerializeField] Image fogSolid;
    [Tooltip("How much fog covers the screen as soon as the player touches the zone.")]
    [SerializeField, Range(0f, 1f)] float startFog = 0.55f;
    [SerializeField] float fogFadeSpeed = 3f;

    [Header("Warning")]
    [SerializeField] TMP_Text warningText;
    [SerializeField] string warningMessage = "You shouldn't go here...\nTurn back!";

    Rigidbody rb;
    float exposure;          // số giây đã ở trong vùng cấm
    float lastContactTime = -10f;
    float fog;               // độ phủ sương hiện tại 0..1
    bool teleporting;
    Collider lastZone;
    Vector3 lastSafePosition;

    bool Touching => Time.time - lastContactTime < 0.25f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastSafePosition = rb.position;
        if (warningText != null) warningText.text = warningMessage;
        ApplyFog(0f);
    }

    void Update()
    {
        if (teleporting) return;

        if (Touching)
            exposure += Time.deltaTime;
        else
            exposure = Mathf.Max(0f, exposure - Time.deltaTime * 2f);

        float target = exposure > 0f ? Mathf.Lerp(startFog, 1f, exposure / blindTime) : 0f;
        fog = Mathf.MoveTowards(fog, target, fogFadeSpeed * Time.deltaTime);
        ApplyFog(fog);

        if (exposure >= blindTime)
            StartCoroutine(TeleportBack());
        else if (!Touching && exposure <= 0f)
            RememberSafePosition();
    }

    void ApplyFog(float amount)
    {
        if (fogGroup != null) fogGroup.alpha = amount > 0f ? 1f : 0f;

        // Viền mờ trước, sau đó cả màn hình trắng xoá
        if (fogVignette != null) SetAlpha(fogVignette, Mathf.Clamp01(amount * 1.6f));
        if (fogSolid != null) SetAlpha(fogSolid, Mathf.Clamp01((amount - 0.2f) / 0.8f));
        if (warningText != null) SetAlpha(warningText, amount > 0.01f && !teleporting ? Mathf.Clamp01(amount * 2f) : 0f);
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }

    void RememberSafePosition()
    {
        if (Time.frameCount % 15 != 0) return;
        if (Physics.Raycast(rb.position + Vector3.up * 0.5f, Vector3.down, 3f, groundMask, QueryTriggerInteraction.Ignore))
            lastSafePosition = rb.position;
    }

    IEnumerator TeleportBack()
    {
        teleporting = true;

        // Sương phủ kín màn hình
        while (fog < 1f)
        {
            fog = Mathf.MoveTowards(fog, 1f, fogFadeSpeed * Time.deltaTime);
            ApplyFog(fog);
            yield return null;
        }
        yield return new WaitForSeconds(0.4f);

        Vector3 from = rb.position;
        Vector3 to = FindSafePoint(from);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = to;
        transform.position = to;
        CinemachineCore.OnTargetObjectWarped(transform, to - from);

        exposure = 0f;
        lastContactTime = -10f;
        yield return new WaitForSeconds(0.5f);

        // Tan sương
        teleporting = false;
        while (fog > 0f && !Touching)
        {
            fog = Mathf.MoveTowards(fog, 0f, Time.deltaTime / 1.5f);
            ApplyFog(fog);
            yield return null;
        }
    }

    // Điểm gần nhất bên trong bản đồ, cách vùng cấm safeDistance mét
    Vector3 FindSafePoint(Vector3 from)
    {
        Vector3 dir = Vector3.zero;
        if (lastZone != null)
        {
            Vector3 closest = lastZone.ClosestPoint(from);
            dir = from - closest;
        }
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = ZoneCenter() - from;
            dir.y = 0f;
        }
        dir.Normalize();

        Vector3 point = from;
        for (float d = safeDistance; d <= safeDistance * 4f; d += 4f)
        {
            point = from + dir * d;
            if (NearZone(point, 4f)) continue;
            if (!Physics.Raycast(point + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 200f, groundMask, QueryTriggerInteraction.Ignore)
                || IsZone(hit.collider))
                continue;

            float ground = hit.point.y;
            // Tia có thể chạm tán cây của terrain, nên lấy độ cao mặt đất thật
            if (hit.collider is TerrainCollider && hit.collider.TryGetComponent(out Terrain terrain))
                ground = terrain.SampleHeight(point) + terrain.transform.position.y;
            return new Vector3(point.x, ground + 1.2f, point.z);
        }
        return lastSafePosition;
    }

    Vector3 ZoneCenter()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        if (deadZoneRoot != null)
            foreach (Collider c in deadZoneRoot.GetComponentsInChildren<Collider>())
                b.Encapsulate(c.bounds);
        return b.center;
    }

    bool NearZone(Vector3 p, float radius)
    {
        foreach (Collider c in Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Collide))
            if (IsZone(c)) return true;
        return false;
    }

    bool IsZone(Collider c) => deadZoneRoot != null && c != null && c.transform.IsChildOf(deadZoneRoot);

    void Touch(Collider c)
    {
        if (teleporting || !IsZone(c)) return;
        lastContactTime = Time.time;
        lastZone = c;
    }

    void OnCollisionEnter(Collision collision) => Touch(collision.collider);
    void OnCollisionStay(Collision collision) => Touch(collision.collider);
    void OnTriggerEnter(Collider other) => Touch(other);
    void OnTriggerStay(Collider other) => Touch(other);
}
