using System.Collections.Generic;
using UnityEngine;

// Chu kỳ ngày/đêm: mặt trời đi từ đông sang tây trong dayDuration giây,
// sau đó là đêm trong nightDuration giây (đèn lồng + đom đóm bật lên).
public class DayNightCycle : MonoBehaviour
{
    [Header("Timing (seconds)")]
    [SerializeField] float dayDuration = 120f;
    [SerializeField] float nightDuration = 120f;
    [Tooltip("0 = sunrise, 0.5 = sunset, 1 = next sunrise")]
    [SerializeField, Range(0f, 1f)] float startTime = 0.08f;

    [Header("Sun / Moon")]
    [SerializeField] Light sun;
    [SerializeField] float sunIntensity = 1.2f;
    [SerializeField] Color dayColor = new Color(1f, 0.957f, 0.839f);
    [SerializeField] Color sunsetColor = new Color(1f, 0.55f, 0.3f);
    [SerializeField] float moonIntensity = 0.25f;
    [SerializeField] Color moonColor = new Color(0.55f, 0.65f, 1f);

    [Header("Ambient light")]
    [SerializeField] Color daySky = new Color(0.55f, 0.65f, 0.8f);
    [SerializeField] Color dayEquator = new Color(0.45f, 0.5f, 0.5f);
    [SerializeField] Color dayGround = new Color(0.25f, 0.25f, 0.2f);
    [SerializeField] Color nightSky = new Color(0.06f, 0.08f, 0.16f);
    [SerializeField] Color nightEquator = new Color(0.05f, 0.06f, 0.1f);
    [SerializeField] Color nightGround = new Color(0.02f, 0.02f, 0.03f);

    [Header("Skybox")]
    [SerializeField] float dayExposure = 1.3f;
    [SerializeField] float nightExposure = 0.12f;

    [Header("Night objects")]
    [Tooltip("Every Light under this root is turned on at night.")]
    [SerializeField] Transform lanternRoot;
    [Tooltip("Every ParticleSystem under this root only emits at night.")]
    [SerializeField] Transform fireflyRoot;
    [Tooltip("Unlit effects (fog, rain) that should darken at night.")]
    [SerializeField] GameObject[] tintedObjects;
    [SerializeField] Color nightTint = new Color(0.22f, 0.25f, 0.35f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ExposureId = Shader.PropertyToID("_Exposure");

    float clock;
    float sunYaw;
    Material skybox;
    readonly List<Light> lanterns = new List<Light>();
    readonly List<float> lanternIntensity = new List<float>();
    readonly List<ParticleSystem> fireflies = new List<ParticleSystem>();
    readonly List<float> fireflyRate = new List<float>();
    readonly List<Renderer> tinted = new List<Renderer>();
    MaterialPropertyBlock block;

    public bool IsNight => clock >= dayDuration;

    void Start()
    {
        clock = startTime * (dayDuration + nightDuration);
        sunYaw = sun.transform.eulerAngles.y;
        block = new MaterialPropertyBlock();

        // Sao chép skybox để không sửa material gốc
        if (RenderSettings.skybox != null)
        {
            skybox = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skybox;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

        if (lanternRoot != null)
        {
            foreach (Light l in lanternRoot.GetComponentsInChildren<Light>())
            {
                lanterns.Add(l);
                lanternIntensity.Add(l.intensity);
            }
        }

        if (fireflyRoot != null)
        {
            foreach (ParticleSystem ps in fireflyRoot.GetComponentsInChildren<ParticleSystem>())
            {
                fireflies.Add(ps);
                fireflyRate.Add(ps.emission.rateOverTimeMultiplier);
            }
        }

        // Lấy renderer ở Start vì mưa tạo ParticleSystem trong Awake
        foreach (GameObject go in tintedObjects)
            if (go != null)
                tinted.AddRange(go.GetComponentsInChildren<Renderer>());

        Apply();
    }

    void Update()
    {
        clock = (clock + Time.deltaTime) % (dayDuration + nightDuration);
        Apply();
    }

    void Apply()
    {
        // Góc mặt trời: 0° mọc, 90° trưa, 180° lặn, 270° nửa đêm
        float angle = clock < dayDuration
            ? clock / dayDuration * 180f
            : 180f + (clock - dayDuration) / nightDuration * 180f;

        float elevation = Mathf.Sin(angle * Mathf.Deg2Rad);
        float day = Smooth(Mathf.InverseLerp(-0.1f, 0.25f, elevation));

        // Ban đêm dùng chính đèn này làm mặt trăng (đối diện mặt trời)
        bool sunUp = elevation >= 0f;
        float lightAngle = sunUp ? angle : angle - 180f;
        sun.transform.rotation = Quaternion.Euler(lightAngle, sunYaw, 0f);

        Color sunCol = Color.Lerp(sunsetColor, dayColor, Smooth(elevation / 0.4f));
        sun.color = Color.Lerp(moonColor, sunCol, day);
        sun.intensity = Mathf.Lerp(moonIntensity, sunIntensity, day);

        RenderSettings.ambientSkyColor = Color.Lerp(nightSky, daySky, day);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightEquator, dayEquator, day);
        RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dayGround, day);
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.15f, 1f, day);
        if (skybox != null && skybox.HasProperty(ExposureId))
            skybox.SetFloat(ExposureId, Mathf.Lerp(nightExposure, dayExposure, day));

        float night = 1f - day;
        for (int i = 0; i < lanterns.Count; i++)
            lanterns[i].intensity = lanternIntensity[i] * night;
        for (int i = 0; i < fireflies.Count; i++)
        {
            var emission = fireflies[i].emission;
            emission.rateOverTimeMultiplier = fireflyRate[i] * night;
        }

        Color tint = Color.Lerp(nightTint, Color.white, day);
        foreach (Renderer r in tinted)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, tint);
            r.SetPropertyBlock(block);
        }
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
