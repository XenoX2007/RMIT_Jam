using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Tối ưu riêng cho bản WebGL (trình duyệt), giữ nguyên hình ảnh như trong Editor.
// - Tắt những thứ tốn mà không nhìn thấy khác biệt: opaque texture (không shader nào dùng),
//   SSAO nửa độ phân giải, bóng mềm Medium, 2 cascade bóng, terrain vẽ instanced
// - Cây cỏ / bụi nhỏ ở xa bị ẩn (CullingGroup), cây lớn, nhà, bầu trời, cực quang luôn được vẽ
// - Đèn lồng ở xa camera tắt ánh sáng (mô hình đèn vẫn sáng)
// - Máy yếu: tự giảm độ phân giải cảnh 3D (UI vẫn nét 1920x1080), máy mạnh giữ nguyên 100%
// Thêm ?fps vào URL để hiện FPS, ?bench để đo từng bước tối ưu ở cảnh InGame vào ban đêm.
// Không ảnh hưởng tới Editor hay bản PC.
public class WebGLPerformance : MonoBehaviour
{
    const float LightDistance = 70f;
    const float SmallPropSize = 3f;        // bụi, cỏ, lá súng, đá nhỏ, dừa
    const float SmallPropDistance = 140f;
    const float MinRenderScale = 0.7f;

    readonly List<Light> lights = new List<Light>();
    float nextLightCheck;

    CullingGroup cullingGroup;
    readonly List<Renderer> props = new List<Renderer>();

    UniversalRenderPipelineAsset urp;
    bool showFps, bench;
    float fpsTimer, fpsMs, slowTime, fastTime;
    int fpsFrames;
    string report = "";

    static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

#if UNITY_WEBGL && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        var go = new GameObject("WebGL Performance");
        DontDestroyOnLoad(go);
        go.AddComponent<WebGLPerformance>();
    }
#endif

    void Awake()
    {
        string url = Application.absoluteURL ?? "";
        bench = url.Contains("bench");
        showFps = bench || url.Contains("fps");
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (!bench) ApplyPipelineSettings();
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    void OnDestroy() { DisposeCulling(); }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisposeCulling();
        if (bench)
        {
            if (scene.name != "InGame") SceneManager.LoadScene("InGame");
            else StartCoroutine(RunBench());
            return;
        }
        ApplySceneSettings();
    }

    // ---------- Các bước tối ưu ----------

    void ApplyPipelineSettings()
    {
        OpaqueTextureOff();
        SsaoHalfResolution();
        SoftShadowsMedium();
        TwoCascades();
    }

    void ApplySceneSettings()
    {
        EnableInstancing();
        TerrainSettings();
        CullSmallProps();
        CollectLights();
    }

    void OpaqueTextureOff() { if (urp != null) urp.supportsCameraOpaqueTexture = false; }

    void TwoCascades()
    {
        if (urp == null) return;
        urp.shadowCascadeCount = 2;
        urp.cascade2Split = 0.25f;
    }

    void SoftShadowsMedium()
    {
        if (urp == null) return;
        FieldInfo f = typeof(UniversalRenderPipelineAsset).GetField("m_SoftShadowQuality", Any);
        if (f != null) f.SetValue(urp, System.Enum.ToObject(f.FieldType, 2));   // 2 = Medium
    }

    void SsaoHalfResolution()
    {
        ScriptableRendererFeature ssao = FindSsao();
        if (ssao == null) return;
        object settings = ssao.GetType().GetField("m_Settings", Any)?.GetValue(ssao);
        FieldInfo down = settings?.GetType().GetField("Downsample", Any);
        if (down == null) return;
        down.SetValue(settings, true);
        ssao.Create();
    }

    ScriptableRendererFeature FindSsao()
    {
        if (urp == null) return null;
        var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", Any)?.GetValue(urp) as ScriptableRendererData[];
        if (list == null) return null;
        foreach (ScriptableRendererData data in list)
            if (data != null)
                foreach (ScriptableRendererFeature f in data.rendererFeatures)
                    if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") return f;
        return null;
    }

    // Hàng trăm cây dừa / bụi giống nhau: vẽ chung một lần bằng GPU instancing thay vì từng cây một
    void EnableInstancing()
    {
        var done = new HashSet<Material>();
        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>())
            foreach (Material m in r.sharedMaterials)
                if (m != null && done.Add(m) && !m.enableInstancing) m.enableInstancing = true;
    }

    void SrpBatcherOff() { if (urp != null) urp.useSRPBatcher = false; }

    // Ở xa terrain dùng ảnh nền đã trộn sẵn thay vì trộn 4 lớp texture (sương mù che đi)
    void TerrainSettings()
    {
        foreach (Terrain t in FindObjectsByType<Terrain>())
        {
            t.drawInstanced = true;
            t.basemapDistance = Mathf.Min(t.basemapDistance, 250f);
        }
    }

    // Cây cỏ / bụi nhỏ đứng yên: chỉ vẽ khi ở gần camera
    void CullSmallProps()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        props.Clear();
        var spheres = new List<BoundingSphere>();
        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>())
        {
            if (!r.enabled || r.gameObject.layer != 0) continue;
            if (player != null && r.transform.IsChildOf(player.transform)) continue;
            // Chỉ vật đứng yên: không có script / vật lý / animation trên nó hoặc cha trực tiếp
            if (r.GetComponentInParent<Rigidbody>() != null || r.GetComponentInParent<Animator>() != null) continue;
            if (r.GetComponent<MonoBehaviour>() != null || (r.transform.parent != null && r.transform.parent.GetComponent<MonoBehaviour>() != null)) continue;
            Bounds b = r.bounds;
            if (Mathf.Max(b.size.x, b.size.y, b.size.z) > SmallPropSize) continue;
            props.Add(r);
            spheres.Add(new BoundingSphere(b.center, b.extents.magnitude));
        }
        if (props.Count == 0) return;

        cullingGroup = new CullingGroup { targetCamera = cam };
        cullingGroup.SetBoundingSpheres(spheres.ToArray());
        cullingGroup.SetBoundingSphereCount(spheres.Count);
        cullingGroup.SetBoundingDistances(new[] { SmallPropDistance });
        cullingGroup.SetDistanceReferencePoint(cam.transform);
        cullingGroup.onStateChanged = e =>
        {
            if (e.index < props.Count && props[e.index] != null)
                props[e.index].enabled = e.currentDistance == 0;
        };
        // Áp dụng ngay cho khung hình đầu
        for (int i = 0; i < props.Count; i++)
            props[i].enabled = (props[i].bounds.center - cam.transform.position).sqrMagnitude < SmallPropDistance * SmallPropDistance;
    }

    void DisposeCulling()
    {
        if (cullingGroup == null) return;
        cullingGroup.Dispose();
        cullingGroup = null;
        props.Clear();
    }

    // Lantern lights (not the player's own hip lantern, which switches itself)
    void CollectLights()
    {
        lights.Clear();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        foreach (Light l in FindObjectsByType<Light>())
            if (l.type != LightType.Directional && (player == null || !l.transform.IsChildOf(player.transform)))
                lights.Add(l);
        nextLightCheck = 0f;
    }

    void UpdateLights()
    {
        if (Time.unscaledTime < nextLightCheck || lights.Count == 0) return;
        nextLightCheck = Time.unscaledTime + 0.25f;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        for (int i = lights.Count - 1; i >= 0; i--)
        {
            Light l = lights[i];
            if (l == null) { lights.RemoveAt(i); continue; }
            float reach = LightDistance + l.range;
            bool near = (l.transform.position - c).sqrMagnitude < reach * reach;
            if (l.enabled != near) l.enabled = near;
        }
    }

    // Dưới ~40 FPS thì giảm độ phân giải cảnh 3D từng bước, nhanh lại thì tăng lên
    void AdaptRenderScale(float dt)
    {
        if (urp == null) return;
        if (dt > 0.026f) { slowTime += dt; fastTime = 0f; }
        else if (dt < 0.0175f) { fastTime += dt; slowTime = 0f; }
        else { slowTime = 0f; fastTime = 0f; }

        if (slowTime > 2f && urp.renderScale > MinRenderScale)
        {
            urp.renderScale = Mathf.Max(MinRenderScale, urp.renderScale - 0.1f);
            slowTime = 0f;
        }
        else if (fastTime > 6f && urp.renderScale < 1f)
        {
            urp.renderScale = Mathf.Min(1f, urp.renderScale + 0.1f);
            fastTime = 0f;
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (!bench)
        {
            UpdateLights();
            if (dt < 0.25f) AdaptRenderScale(dt);   // bỏ qua khung hình lúc tải scene
        }

        fpsTimer += dt;
        fpsFrames++;
        if (fpsTimer >= 0.5f)
        {
            fpsMs = fpsTimer * 1000f / fpsFrames;
            fpsTimer = 0f;
            fpsFrames = 0;
        }
    }

    // ---------- Đo hiệu năng (?bench) ----------

    // Nhảy tới giữa đêm để có cực quang, đèn lồng và đom đóm
    void SetNight()
    {
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>())
            if (mb.GetType().Name == "DayNightCycle")
                mb.GetType().GetField("clock", Any)?.SetValue(mb, 150f);
    }

    IEnumerator RunBench()
    {
        yield return null;
        SetNight();

        var stages = new List<KeyValuePair<string, System.Action>>
        {
            new KeyValuePair<string, System.Action>("PC quality, no tweaks", null),
            new KeyValuePair<string, System.Action>("+ far lights off", () => { CollectLights(); UpdateLights(); }),
            new KeyValuePair<string, System.Action>("+ opaque texture off", OpaqueTextureOff),
            new KeyValuePair<string, System.Action>("+ SSAO half res", SsaoHalfResolution),
            new KeyValuePair<string, System.Action>("+ soft shadows medium", SoftShadowsMedium),
            new KeyValuePair<string, System.Action>("+ 2 cascades", TwoCascades),
            new KeyValuePair<string, System.Action>("+ terrain instanced", TerrainSettings),
            new KeyValuePair<string, System.Action>("+ small props culled", CullSmallProps),
            new KeyValuePair<string, System.Action>("+ GPU instancing", EnableInstancing),
            new KeyValuePair<string, System.Action>("+ SRP batcher off", SrpBatcherOff),
            new KeyValuePair<string, System.Action>("(info) render scale 0.8", () => urp.renderScale = 0.8f),
            new KeyValuePair<string, System.Action>("(info) render scale 0.5", () => urp.renderScale = 0.5f),
            new KeyValuePair<string, System.Action>("(info) + SSAO off", () => { urp.renderScale = 1f; FindSsao()?.SetActive(false); }),
            new KeyValuePair<string, System.Action>("(info) + shadows off", () => urp.shadowDistance = 0f),
            new KeyValuePair<string, System.Action>("(info) + particles off", () => { foreach (var p in FindObjectsByType<ParticleSystemRenderer>()) p.enabled = false; }),
        };
        report = "BENCH (night, InGame)\n";
        float benchStart = Time.realtimeSinceStartup;
        var samples = new List<float>();
        foreach (var stage in stages)
        {
            stage.Value?.Invoke();
            SetNight();
            // Chờ trình duyệt biên dịch xong shader mới (khung hình bị khựng), tối đa 25 giây
            float settleStart = Time.realtimeSinceStartup, stalls = 0f;
            int smooth = 0;
            while (Time.realtimeSinceStartup - settleStart < 25f && (Time.realtimeSinceStartup - settleStart < 6f || smooth < 60))
            {
                yield return null;
                if (Time.unscaledDeltaTime > 0.3f) { smooth = 0; stalls += Time.unscaledDeltaTime; }
                else smooth++;
            }
            samples.Clear();
            float end = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                samples.Add(Time.unscaledDeltaTime * 1000f);
            }
            samples.Sort();
            float median = samples.Count > 0 ? samples[samples.Count / 2] : 0f;
            float p90 = samples.Count > 0 ? samples[samples.Count * 9 / 10] : 0f;
            report += stage.Key + ": " + median.ToString("F1") + " ms (" + (1000f / Mathf.Max(0.1f, median)).ToString("F0") + " fps), p90 " +
                      p90.ToString("F0") + ", compile stall " + stalls.ToString("F1") + " s\n";
        }
        report += "DONE in " + (Time.realtimeSinceStartup - benchStart).ToString("F0") + " s";
        report += "DONE";
    }

    void OnGUI()
    {
        if (!showFps) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        style.normal.textColor = Color.yellow;
        string text = (1000f / Mathf.Max(0.01f, fpsMs)).ToString("F0") + " fps  " + fpsMs.ToString("F1") + " ms  scale " +
                      (urp != null ? urp.renderScale.ToString("F1") : "-") + (props.Count > 0 ? "  props " + props.Count : "");
        if (bench) text += "\n" + report;
        GUI.Box(new Rect(10, 10, 860, bench ? 540 : 40), GUIContent.none);
        GUI.Label(new Rect(20, 14, 850, 530), text, style);
    }
}
