using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Crocodile warning signs.
// Approaching a crocodile's detection zone: its warning sign fades in above it and pulses faster the closer you get.
// Once it spots you and starts chasing: the sign grows, shakes and flashes, and the screen edges glow red.
// A crocodile that is off screen or behind the camera keeps its sign pinned to the screen edge on its side.
// Hidden in a jar / on a boat: no warnings (the crocodiles can't get you).
public class EnemyWarningHUD : MonoBehaviour
{
    [SerializeField] private Sprite warningSprite;

    [Header("When it shows")]
    [Tooltip("Warn from this many times the crocodile's detection radius (1 = only inside the zone).")]
    [SerializeField] private float warnRange = 1.8f;

    [Header("Look")]
    [SerializeField] private float iconSize = 90f;
    [Tooltip("Metres above the top of the crocodile.")]
    [SerializeField] private float heightAboveCroc = 1.2f;
    [Tooltip("Gap (pixels) between an off-screen sign and the screen edge.")]
    [SerializeField] private float edgeMargin = 70f;
    [SerializeField] private float cautionPulseSpeed = 3f;
    [SerializeField] private float alertPulseSpeed = 11f;
    [SerializeField] private Color alertFlash = new Color(1f, 0.55f, 0.55f);
    [SerializeField] private Color vignetteColor = new Color(0.85f, 0.05f, 0.02f, 0.3f);

    [Header("Sound (optional)")]
    [Tooltip("Plays once when a crocodile spots you.")]
    [SerializeField] private AudioClip alertSound;
    [SerializeField, Range(0f, 1f)] private float alertVolume = 0.8f;

    private class Sign
    {
        public Enemy enemy;
        public StateMachine brain;
        public Renderer[] body;
        public RectTransform rect;
        public Image image;
        public float shown;      // 0-1 fade
        public bool wasAlert;
    }

    private readonly List<Sign> signs = new List<Sign>();
    private RectTransform canvasRect;
    private CanvasGroup vignette;
    private AudioSource voice;
    private Camera cam;

    private void Start()
    {
        BuildCanvas();
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;

        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsInactive.Exclude))
        {
            var go = new GameObject("Warning - " + enemy.name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            rect.sizeDelta = new Vector2(iconSize, iconSize * 0.88f);
            var image = go.GetComponent<Image>();
            image.sprite = warningSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            signs.Add(new Sign
            {
                enemy = enemy,
                brain = enemy.GetComponent<StateMachine>(),
                body = enemy.GetComponentsInChildren<Renderer>(),
                rect = rect,
                image = image
            });
        }
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        bool blocked = cam == null || PauseMenu.IsPaused || GameOverScreen.IsShowing || LastStandQTE.Active;
        float dt = Time.unscaledDeltaTime;
        bool anyAlert = false;

        foreach (Sign s in signs)
        {
            // crocodile gone (kicked out of the map / dead)
            if (s.enemy == null || s.enemy.IsDead)
            {
                if (s.rect != null) s.rect.gameObject.SetActive(false);
                continue;
            }

            float zone = s.enemy.detectionRadius;
            float dist = s.enemy.DistanceToPlayer();
            bool alert = !blocked && !s.enemy.PlayerHidden && s.brain != null && s.brain.activeState is AttackState;
            // 0 at the edge of the warning range, 1 at the detection zone
            float caution = blocked || s.enemy.PlayerHidden ? 0f : Mathf.Clamp01(Mathf.InverseLerp(zone * warnRange, zone, dist));
            float target = alert ? 1f : caution;
            s.shown = Mathf.MoveTowards(s.shown, target, dt * (target > s.shown ? 4f : 1.5f));
            anyAlert |= alert;

            if (alert && !s.wasAlert && alertSound != null) voice.PlayOneShot(alertSound, alertVolume * GameAudio.Sfx);
            s.wasAlert = alert;

            if (s.shown <= 0.01f)
            {
                s.rect.gameObject.SetActive(false);
                continue;
            }
            s.rect.gameObject.SetActive(true);

            // pulse: gentle while approaching, fast and big once spotted
            float t = Time.time;
            float pulse = alert ? 1.25f + 0.12f * Mathf.Sin(t * alertPulseSpeed) : 0.75f + 0.25f * caution + 0.06f * Mathf.Sin(t * cautionPulseSpeed * (1f + caution));
            Vector2 shake = alert ? new Vector2(Mathf.Sin(t * 47f), Mathf.Cos(t * 53f)) * 3f : Vector2.zero;
            s.rect.localScale = Vector3.one * pulse;
            Color c = alert ? Color.Lerp(Color.white, alertFlash, 0.5f + 0.5f * Mathf.Sin(t * alertPulseSpeed)) : Color.white;
            c.a = s.shown;
            s.image.color = c;

            s.rect.anchoredPosition = ScreenPosition(TopOf(s)) + shake;
        }

        if (vignette != null)
        {
            float pulse = anyAlert ? 0.75f + 0.25f * Mathf.Sin(Time.time * 4f) : 0f;
            vignette.alpha = Mathf.MoveTowards(vignette.alpha, pulse, dt * 2f);
        }
    }

    private Vector3 TopOf(Sign s)
    {
        float top = s.enemy.transform.position.y;
        bool any = false;
        foreach (Renderer r in s.body)
        {
            if (r == null || !r.enabled) continue;
            top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
            any = true;
        }
        Vector3 p = s.enemy.transform.position;
        return new Vector3(p.x, top + heightAboveCroc, p.z);
    }

    // On screen: right above the crocodile. Off screen / behind: pinned to the edge in its direction.
    private Vector2 ScreenPosition(Vector3 world)
    {
        Vector2 size = canvasRect.rect.size;
        Vector3 vp = cam.WorldToViewportPoint(world);
        bool behind = vp.z < 0f;
        Vector2 p = new Vector2(vp.x, vp.y);
        if (behind) p = Vector2.one - p;   // mirrored when behind the camera

        Vector2 local = new Vector2((p.x - 0.5f) * size.x, (p.y - 0.5f) * size.y);
        Vector2 half = size * 0.5f - Vector2.one * edgeMargin;
        bool inside = !behind && Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        if (inside) return local;

        if (behind && local.sqrMagnitude < 1f) local = Vector2.down;
        float k = Mathf.Min(half.x / Mathf.Max(0.001f, Mathf.Abs(local.x)), half.y / Mathf.Max(0.001f, Mathf.Abs(local.y)));
        return local * k;
    }

    private void BuildCanvas()
    {
        var go = new GameObject("Enemy Warning HUD", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;   // under the pause / game over menus
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)go.transform;

        // red glow around the screen edges while being chased
        var v = new GameObject("Danger Vignette", typeof(RectTransform), typeof(RawImage), typeof(CanvasGroup));
        var vr = (RectTransform)v.transform;
        vr.SetParent(canvasRect, false);
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = vr.offsetMax = Vector2.zero;
        var raw = v.GetComponent<RawImage>();
        raw.texture = VignetteTexture();
        raw.color = vignetteColor;
        raw.raycastTarget = false;
        vignette = v.GetComponent<CanvasGroup>();
        vignette.alpha = 0f;
        vignette.blocksRaycasts = false;
    }

    private static Texture2D VignetteTexture()
    {
        const int w = 256, h = 144;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * 0.55f + new Vector2(u, v).magnitude * 0.45f;
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1.08f, d)));
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
