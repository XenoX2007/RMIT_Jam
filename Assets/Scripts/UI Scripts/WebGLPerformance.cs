using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Tối ưu riêng cho bản WebGL (trình duyệt). Không ảnh hưởng tới Editor hay bản PC.
// Chạy tự động mỗi khi load scene:
// - Giảm tầm nhìn camera, ẩn vật nhỏ ở xa (sương mù che chỗ bị cắt)
// - Chỉ bật đèn lồng gần camera
// - Bloom nhẹ hơn
// - Sương mù dạng hạt: tắt "soft particles" (cần depth texture mà bản WebGL không có -> sương bị tàng hình), giảm số hạt
public class WebGLPerformance : MonoBehaviour
{
    const float FarClip = 450f;
    const float PropCullDistance = 260f;
    const float LightDistance = 45f;
    const float FogParticleScale = 0.5f;

    static readonly HashSet<Material> fixedMaterials = new HashSet<Material>();

    readonly List<Light> lights = new List<Light>();
    float nextLightCheck;

#if UNITY_WEBGL && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 0.6f);   // trees / props switch to simpler LODs sooner
        var go = new GameObject("WebGL Performance");
        DontDestroyOnLoad(go);
        go.AddComponent<WebGLPerformance>();
    }
#endif

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.farClipPlane = Mathf.Min(cam.farClipPlane, FarClip);
            var distances = new float[32];
            distances[0] = PropCullDistance;   // Default layer: houses, props, plants
            cam.layerCullDistances = distances;
            cam.layerCullSpherical = true;
        }

        // Terrain: full mesh trees only nearby, billboards further out
        foreach (Terrain t in Terrain.activeTerrains)
        {
            t.treeDistance = Mathf.Min(t.treeDistance, 400f);
            t.treeBillboardDistance = Mathf.Min(t.treeBillboardDistance, 120f);
            t.treeCrossFadeLength = 0f;
            t.detailObjectDistance = Mathf.Min(t.detailObjectDistance, 45f);
            t.heightmapPixelError = Mathf.Max(t.heightmapPixelError, 10f);
            t.basemapDistance = Mathf.Min(t.basemapDistance, 150f);
        }

        // Fewer lights per object, shorter shadows
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.maxAdditionalLightsCount = Mathf.Min(urp.maxAdditionalLightsCount, 2);
            urp.shadowDistance = Mathf.Min(urp.shadowDistance, 40f);
        }

        // Lighter bloom
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v.profile != null && v.profile.TryGet(out Bloom bloom))
            {
                bloom.highQualityFiltering.Override(false);
                bloom.downscale.Override(BloomDownscaleMode.Quarter);
                bloom.maxIterations.Override(4);
            }

        // Fog particles: visible without a depth texture, and fewer of them
        foreach (ParticleSystem ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            Material m = r != null ? r.sharedMaterial : null;
            if (m == null || !m.IsKeywordEnabled("_SOFTPARTICLES_ON")) { if (m == null || !fixedMaterials.Contains(m)) continue; }
            if (!fixedMaterials.Contains(m))
            {
                m.DisableKeyword("_SOFTPARTICLES_ON");
                m.SetFloat("_SoftParticlesEnabled", 0f);
                fixedMaterials.Add(m);
            }
            var main = ps.main;
            main.maxParticles = Mathf.Max(8, Mathf.RoundToInt(main.maxParticles * FogParticleScale));
            var em = ps.emission;
            em.rateOverTimeMultiplier *= FogParticleScale;
        }

        // Lanterns (not the player's own hip lantern, which switches itself)
        lights.Clear();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type != LightType.Directional && (player == null || !l.transform.IsChildOf(player.transform)))
                lights.Add(l);
        nextLightCheck = 0f;
    }

    void Update()
    {
        if (Time.unscaledTime < nextLightCheck || lights.Count == 0) return;
        nextLightCheck = Time.unscaledTime + 0.25f;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        float maxSqr = LightDistance * LightDistance;
        for (int i = lights.Count - 1; i >= 0; i--)
        {
            Light l = lights[i];
            if (l == null) { lights.RemoveAt(i); continue; }
            bool near = (l.transform.position - c).sqrMagnitude < maxSqr + l.range * l.range;
            if (l.enabled != near) l.enabled = near;
        }
    }
}
