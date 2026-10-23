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
    [Tooltip("Sky colour when the sun is low (morning / evening).")]
    [SerializeField] Color morningSkyTint = new Color(1f, 0.6f, 0.36f);
    [Tooltip("Sky colour around midday.")]
    [SerializeField] Color middaySkyTint = new Color(0.72f, 0.7f, 0.66f);
    [SerializeField] float morningAtmosphere = 1.7f;
    [SerializeField] float middayAtmosphere = 1.05f;

    [Header("Distance haze (fog)")]
    [SerializeField] bool useFog = true;
    [SerializeField] Color dayFog = new Color(0.88f, 0.74f, 0.58f);
    [SerializeField] Color nightFog = new Color(0.04f, 0.05f, 0.09f);
    [SerializeField] float fogDensity = 0.004f;

    [Header("Night objects")]
    [Tooltip("Every Light under this root is turned on at night.")]
    [SerializeField] Transform lanternRoot;
    [Tooltip("More roots whose Lights are lanterns too (e.g. lanterns on the boats).")]
    [SerializeField] Transform[] extraLanternRoots;
    [Tooltip("Every ParticleSystem under this root only emits at night.")]
    [SerializeField] Transform fireflyRoot;
    [Tooltip("Unlit effects (fog, rain) that should darken at night.")]
    [SerializeField] GameObject[] tintedObjects;
    [SerializeField] Color nightTint = new Color(0.22f, 0.25f, 0.35f);

    [Header("Night sky")]
    [Tooltip("Moon disc, kept in the sky where the moonlight comes from.")]
    [SerializeField] Transform moon;
    [SerializeField] float moonDistance = 600f;
    [Tooltip("Highest point of the moon in the sky (degrees), so it stays visible over the water.")]
    [SerializeField] float moonMaxElevation = 38f;
    [SerializeField] float moonGlow = 1.6f;
    [Tooltip("Aurora curtains, shown only at night.")]
    [SerializeField] Renderer[] auroras;
    [SerializeField] float auroraStrength = 1.3f;
    [Tooltip("Water gets glossy at night so the moon reflects on it.")]
    [SerializeField] Renderer[] waterSurfaces;
    [SerializeField] float nightWaterSmoothness = 0.78f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    readonly List<float> waterDaySmoothness = new List<float>();
    Renderer moonRenderer;
    static readonly int AtmosphereId = Shader.PropertyToID("_AtmosphereThickness");

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

        var roots = new List<Transform> { lanternRoot };
        if (extraLanternRoots != null) roots.AddRange(extraLanternRoots);
        foreach (Transform root in roots)
        {
            if (root == null) continue;
            foreach (Light l in root.GetComponentsInChildren<Light>())
            {
                if (lanterns.Contains(l)) continue;
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

        if (moon != null) moonRenderer = moon.GetComponentInChildren<Renderer>();
        if (waterSurfaces != null)
            foreach (Renderer w in waterSurfaces)
                waterDaySmoothness.Add(w != null && w.sharedMaterial != null && w.sharedMaterial.HasProperty(SmoothnessId) ? w.sharedMaterial.GetFloat(SmoothnessId) : 0.3f);

        Apply();
    }

    // Trăng, cực quang và mặt nước bóng vào ban đêm
    void ApplyNightSky(float night)
    {
        if (moon != null)
        {
            // Đèn đêm đóng vai trăng: trăng nằm ở hướng ánh sáng chiếu tới
            Camera cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            Vector3 toMoon = -sun.transform.forward;
            moon.position = eye + toMoon * moonDistance;
            moon.rotation = Quaternion.LookRotation(-toMoon);
            if (moonRenderer != null)
            {
                moonRenderer.enabled = night > 0.01f && toMoon.y > -0.05f;
                SetFloat(moonRenderer, IntensityId, night * moonGlow);
            }
        }

        if (auroras != null)
            foreach (Renderer r in auroras)
            {
                if (r == null) continue;
                r.enabled = night > 0.01f;
                SetFloat(r, IntensityId, night * auroraStrength);
            }

        if (waterSurfaces != null)
            for (int i = 0; i < waterSurfaces.Length && i < waterDaySmoothness.Count; i++)
                if (waterSurfaces[i] != null)
                    SetFloat(waterSurfaces[i], SmoothnessId, Mathf.Lerp(waterDaySmoothness[i], nightWaterSmoothness, night));
    }

    void SetFloat(Renderer r, int id, float value)
    {
        r.GetPropertyBlock(block);
        block.SetFloat(id, value);
        r.SetPropertyBlock(block);
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
        if (sunUp)
        {
            sun.transform.rotation = Quaternion.Euler(angle, sunYaw, 0f);
        }
        else
        {
            // Trăng đi thấp (tối đa moonMaxElevation) để thấy được trăng và bóng trăng trên mặt nước
            float phase = (angle - 180f) / 180f;
            float pitch = Mathf.Sin(phase * Mathf.PI) * moonMaxElevation;
            float yaw = sunYaw + Mathf.Lerp(-60f, 60f, phase);
            sun.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        Color sunCol = Color.Lerp(sunsetColor, dayColor, Smooth(elevation / 0.4f));
        sun.color = Color.Lerp(moonColor, sunCol, day);
        sun.intensity = Mathf.Lerp(moonIntensity, sunIntensity, day);

        RenderSettings.ambientSkyColor = Color.Lerp(nightSky, daySky, day);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightEquator, dayEquator, day);
        RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dayGround, day);
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.15f, 1f, day);
        if (skybox != null && skybox.HasProperty(ExposureId))
            skybox.SetFloat(ExposureId, Mathf.Lerp(nightExposure, dayExposure, day));

        // Bầu trời cam lúc sáng/chiều, trắng dịu buổi trưa
        float high = Smooth(Mathf.Abs(elevation) / 0.55f);
        if (skybox != null)
        {
            if (skybox.HasProperty(SkyTintId)) skybox.SetColor(SkyTintId, Color.Lerp(morningSkyTint, middaySkyTint, high));
            if (skybox.HasProperty(AtmosphereId)) skybox.SetFloat(AtmosphereId, Mathf.Lerp(morningAtmosphere, middayAtmosphere, high));
        }
        if (useFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, day);
        }

        float night = 1f - day;
        ApplyNightSky(night);
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
