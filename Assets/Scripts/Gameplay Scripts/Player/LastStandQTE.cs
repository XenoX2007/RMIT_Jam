using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Last stand: when a crocodile's bite takes the last HP, the player isn't dead yet.
// The crocodile holds the player in its open jaws and a timing ring appears:
// press Space while the needle is inside the pink zone.
// Hit it [hitsToWin] times -> MMA kick: the crocodile is kicked out of the map and the player gets back up.
// Miss (or wait too long) -> the crocodile finishes its Zombie Neck Bite and eats the player (game over).
[RequireComponent(typeof(PlayerHealth))]
public class LastStandQTE : MonoBehaviour
{
    [Header("Timing ring")]
    [SerializeField] private int hitsToWin = 2;
    [Tooltip("Needle speed (degrees per second) for each round; the last value is used for any extra rounds.")]
    [SerializeField] private float[] needleSpeeds = { 260f, 340f };
    [Tooltip("Size of the pink zone (degrees) for each round.")]
    [SerializeField] private float[] zoneSizes = { 70f, 55f };
    [Tooltip("Full turns of the needle without a press before it counts as a miss.")]
    [SerializeField] private float turnsBeforeMiss = 2.5f;
    [SerializeField] private float ringSize = 250f;
    [SerializeField] private Vector2 ringScreenOffset = new Vector2(560f, -230f);
    [SerializeField] private Color ringColor = Color.white;
    [SerializeField] private Color zoneColor = new Color(1f, 0.36f, 0.5f);
    [SerializeField] private Color needleColor = new Color(0.35f, 0.82f, 0.95f);
    [SerializeField] private Color successColor = new Color(0.45f, 1f, 0.55f);
    [SerializeField] private Color failColor = new Color(1f, 0.25f, 0.2f);

    [Header("Grab")]
    [Tooltip("How close (metres) the crocodile pulls in while holding the player.")]
    [SerializeField] private float grabDistance = 1.9f;
    [Tooltip("How far below the player's feet the crocodile's feet stay (it rises out of the water to this height).")]
    [SerializeField] private float grabSink = -0.3f;
    [Tooltip("Degrees the crocodile tilts up towards the player while holding them (jaws at kick height).")]
    [SerializeField] private float grabPitch = 25f;
    [SerializeField] private string biteState = "Bite";
    [Tooltip("Point of the bite (0-1) the crocodile freezes at during the QTE: jaws wide open.")]
    [SerializeField, Range(0f, 1f)] private float biteHoldTime = 0.22f;
    [Tooltip("Point of the bite (0-1) where the jaws close.")]
    [SerializeField, Range(0f, 1f)] private float biteCloseTime = 0.34f;

    [Header("Win: MMA kick")]
    [SerializeField] private string kickState = "Kick";
    [Tooltip("Point of the kick (0-1) where the foot connects.")]
    [SerializeField, Range(0f, 1f)] private float kickHitTime = 0.33f;
    [SerializeField] private string flyState = "Flying";
    [Tooltip("Launch speed of the crocodile: x = away from the player, y = up (m/s).")]
    [SerializeField] private Vector2 launchSpeed = new Vector2(38f, 32f);
    [SerializeField] private float flightGravity = 9f;
    [SerializeField] private float flightTime = 3f;
    [SerializeField] private float spinSpeed = 600f;
    [SerializeField] private int reviveHealth = 1;
    [Tooltip("Extra seconds the player can't be bitten after getting back up.")]
    [SerializeField] private float reviveSafeTime = 2f;
    [SerializeField] private AudioClip kickSound;
    [SerializeField] private AudioClip roarSound;

    [Header("Lose: eaten")]
    [SerializeField] private AudioClip biteSound;

    [Header("Camera")]
    [SerializeField] private float cameraDistance = 4.5f;
    [SerializeField] private float cameraHeight = 0.8f;
    [Tooltip("How far (0-1) the camera turns to follow the kicked crocodile.")]
    [SerializeField, Range(0f, 1f)] private float followFlight = 0.3f;

    public static bool Active { get; private set; }

    private PlayerHealth health;
    private Rigidbody rb;
    private Movement movement;
    private Animator animator;
    private Behaviour[] paused;
    private AudioSource voice;

    private CinemachineCamera cutCam;
    private Vector3 camPos;
    private Quaternion camRot;
    private float shake;

    private CanvasGroup ui;
    private RectTransform ringRoot;
    private RectTransform needlePivot;
    private RectTransform zone;
    private Image zoneImage;
    private Image ringImage;
    private Image[] dots;

    private bool pressQueued;
    private bool aborted;

    private void Awake()
    {
        Active = false;
        health = GetComponent<PlayerHealth>();
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<Movement>();
        animator = GetComponentInChildren<Animator>();
        paused = new Behaviour[]
        {
            GetComponent<PlayerAnimator>(), GetComponent<PlayerThrower>(),
            GetComponent<PlayerEmotes>(), GetComponent<InteractionController>()
        };
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;

        var camGo = new GameObject("Last Stand Camera");
        cutCam = camGo.AddComponent<CinemachineCamera>();
        cutCam.Priority = 200;
        cutCam.Lens.FieldOfView = 50f;
        cutCam.enabled = false;

        BuildUI();
    }

    private void OnDestroy()
    {
        Active = false;
    }

    // Same as pressing Space (for an on-screen button)
    public void Press()
    {
        pressQueued = true;
    }

    // Called by PlayerHealth on the killing bite. False = can't start, the player just dies.
    public bool Begin(Enemy crocodile)
    {
        if (Active || crocodile == null || crocodile.IsDead || crocodile.Animator == null) return false;
        StartCoroutine(Run(crocodile));
        return true;
    }

    private void Update()
    {
        if (!cutCam.enabled) return;
        Vector3 offset = Vector3.zero;
        if (shake > 0f)
        {
            float t = Time.unscaledTime * 25f;
            offset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * 2f * shake;
        }
        cutCam.transform.SetPositionAndRotation(camPos + camRot * offset, camRot);
    }

    // ---------- Sequence ----------

    private IEnumerator Run(Enemy croc)
    {
        Active = true;
        aborted = false;
        pressQueued = false;

        // Player can't do anything while held
        if (ThrowableObject.HeldObject != null) ThrowableObject.HeldObject.Release(Vector3.zero);
        if (movement != null) movement.CanMove = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        foreach (Behaviour b in paused) if (b != null) b.enabled = false;

        // Crocodile pulls in with its jaws wide open and freezes there
        croc.Hold();
        Animator crocAnim = croc.Animator;
        crocAnim.speed = 1f;
        crocAnim.Play(biteState, 0, biteHoldTime);
        crocAnim.Update(0f);
        crocAnim.speed = 0f;

        Vector3 toPlayer = Flat(transform.position - croc.transform.position).normalized;
        if (toPlayer == Vector3.zero) toPlayer = -transform.forward;
        // It lunges up out of the water onto the player's level, so it isn't hidden behind the bank
        Vector3 grabPos = transform.position - toPlayer * grabDistance;
        grabPos.y = Mathf.Max(croc.transform.position.y, FeetHeight() - grabSink);
        // rears up towards the player, jaws at kick height
        Quaternion crocFacing = Quaternion.LookRotation(toPlayer) * Quaternion.Euler(-grabPitch, 0f, 0f);
        Quaternion playerFacing = Quaternion.LookRotation(-toPlayer);
        rb.rotation = playerFacing;
        transform.rotation = playerFacing;

        SetupCamera(grabPos, toPlayer);
        Vector3 crocFrom = croc.transform.position;
        Quaternion crocFromRot = croc.transform.rotation;
        yield return Over(0.3f, k =>
        {
            float e = Smooth(k);
            croc.transform.SetPositionAndRotation(Vector3.Lerp(crocFrom, grabPos, e), Quaternion.Slerp(crocFromRot, crocFacing, e));
        }, true);
        shake = 0.08f;
        StartCoroutine(Over(0.4f, k => shake = 0.08f * (1f - k), true));

        // Timing ring
        foreach (Image dot in dots) dot.color = new Color(1f, 1f, 1f, 0.35f);
        ringImage.color = ringColor;
        yield return FadeUI(1f);

        int hits = 0;
        bool failed = false;
        while (hits < hitsToWin)
        {
            float speed = needleSpeeds[Mathf.Min(hits, needleSpeeds.Length - 1)];
            float size = zoneSizes[Mathf.Min(hits, zoneSizes.Length - 1)];
            float angle = Random.Range(0f, 360f);
            float zoneStart = Mathf.Repeat(angle + Random.Range(120f, 240f), 360f);
            zoneImage.fillAmount = size / 360f;
            zone.localEulerAngles = new Vector3(0f, 0f, -zoneStart);
            needlePivot.localEulerAngles = new Vector3(0f, 0f, -angle);
            pressQueued = false;

            float travelled = 0f;
            bool ok = false;
            while (true)
            {
                if (!health.IsDead) { aborted = true; break; }   // respawned from the pause menu
                if (PauseMenu.IsPaused) { pressQueued = false; yield return null; continue; }

                float step = speed * Time.unscaledDeltaTime;
                angle = Mathf.Repeat(angle + step, 360f);
                travelled += step;
                needlePivot.localEulerAngles = new Vector3(0f, 0f, -angle);

                if (Pressed())
                {
                    ok = Mathf.Repeat(angle - zoneStart, 360f) <= size;
                    break;
                }
                if (travelled >= turnsBeforeMiss * 360f) break;
                yield return null;
            }
            if (aborted) break;

            if (!ok)
            {
                failed = true;
                yield return Feedback(false);
                break;
            }
            hits++;
            dots[Mathf.Min(hits, dots.Length) - 1].color = zoneColor;
            yield return Feedback(true);
        }

        yield return FadeUI(0f);

        if (aborted)
        {
            croc.Release();
            Restore();
            yield break;
        }

        if (failed) yield return Eaten(croc, toPlayer);
        else yield return Kick(croc, toPlayer);
    }

    private IEnumerator Kick(Enemy croc, Vector3 toPlayer)
    {
        animator.CrossFadeInFixedTime(kickState, 0.1f, 0);

        // Wait for the foot to connect
        float timeout = 3f;
        while (timeout > 0f)
        {
            AnimatorStateInfo s = animator.GetCurrentAnimatorStateInfo(0);
            if (s.IsName(kickState) && s.normalizedTime >= kickHitTime) break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        // Impact: slow motion, shake, the crocodile flies off out of the map
        croc.KnockedOut();
        Animator crocAnim = croc.Animator;
        crocAnim.speed = 1f;
        crocAnim.CrossFadeInFixedTime(flyState, 0.1f, 0);
        Play(kickSound);
        Play(roarSound);
        StartCoroutine(SlowMotion(0.2f, 0.35f));
        StartCoroutine(Over(0.7f, k => shake = 0.35f * (1f - k), true));
        StartCoroutine(Fly(croc, -toPlayer));

        // Camera leans after it as it flies (part of the way, the player stays in shot)
        Quaternion from = camRot;
        yield return Over(1.6f, k =>
        {
            if (croc != null)
                camRot = Quaternion.Slerp(from, Quaternion.LookRotation(croc.transform.position - camPos), followFlight * Smooth(Mathf.Clamp01(k * 1.5f)));
        });

        // Finish the kick, back on your feet
        timeout = 2f;
        while (timeout > 0f && animator.GetCurrentAnimatorStateInfo(0).IsName(kickState))
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        health.Revive(reviveHealth, reviveSafeTime);
        Restore();
    }

    private IEnumerator Fly(Enemy croc, Vector3 away)
    {
        Transform t = croc.transform;
        Vector3 start = t.position;
        Vector3 axis = Vector3.Cross(Vector3.up, away).normalized;   // tumble backwards
        Quaternion startRot = t.rotation;
        for (float time = 0f; time < flightTime && croc != null; time += Time.deltaTime)
        {
            t.position = start + away * launchSpeed.x * time + Vector3.up * (launchSpeed.y * time - 0.5f * flightGravity * time * time);
            t.rotation = Quaternion.AngleAxis(-spinSpeed * time, axis) * startRot;
            yield return null;
        }
        if (croc != null) Destroy(croc.gameObject);
    }

    private IEnumerator Eaten(Enemy croc, Vector3 toPlayer)
    {
        Animator crocAnim = croc.Animator;
        Play(roarSound);
        crocAnim.speed = 1f;   // the bite carries on from the open-jaw pose

        float timeout = 3f;
        while (timeout > 0f && crocAnim.GetCurrentAnimatorStateInfo(0).normalizedTime < biteCloseTime)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // Jaws close: pulled into the mouth and gone
        Play(biteSound);
        StartCoroutine(Over(0.5f, k => shake = 0.3f * (1f - k), true));
        Transform head = crocAnim.isHuman ? crocAnim.GetBoneTransform(HumanBodyBones.Head) : null;
        Vector3 from = transform.position;
        Vector3 mouth = head != null ? head.position : croc.transform.position + Vector3.up;
        yield return Over(0.25f, k =>
        {
            transform.position = Vector3.Lerp(from, mouth, k * k);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, k);
        });
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;

        // The crocodile sinks back under with its meal
        Vector3 crocFrom = croc.transform.position;
        StartCoroutine(Over(1.6f, k => { if (croc != null) croc.transform.position = crocFrom + Vector3.down * 3f * Smooth(k); }));
        yield return new WaitForSeconds(0.6f);

        Active = false;
        health.Kill();
    }

    // Everything back to normal play (won, or respawned mid-QTE)
    private void Restore()
    {
        rb.isKinematic = false;
        if (movement != null) movement.CanMove = true;
        foreach (Behaviour b in paused) if (b != null) b.enabled = true;
        cutCam.enabled = false;
        shake = 0f;
        Active = false;
    }

    // ---------- Helpers ----------

    private bool Pressed()
    {
        bool queued = pressQueued;
        pressQueued = false;
        if (queued) return true;
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
        return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
    }

    // Side-on shot of the player and the crocodile, from the side the camera already was
    private void SetupCamera(Vector3 crocPos, Vector3 toPlayer)
    {
        Vector3 mid = (transform.position + crocPos) * 0.5f + Vector3.up * 1f;
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer).normalized;
        Camera main = Camera.main;
        if (main != null && Vector3.Dot(main.transform.position - mid, side) < 0f) side = -side;

        Vector3 pos = mid + side * cameraDistance + Vector3.up * cameraHeight;
        Vector3 dir = pos - mid;
        if (Physics.Raycast(mid, dir.normalized, out RaycastHit hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
            pos = hit.point - dir.normalized * 0.3f;

        camPos = pos;
        camRot = Quaternion.LookRotation(mid - pos);
        cutCam.transform.SetPositionAndRotation(camPos, camRot);
        cutCam.enabled = true;
    }

    private IEnumerator Feedback(bool success)
    {
        Color flash = success ? successColor : failColor;
        if (!success) StartCoroutine(Over(0.35f, k => shake = 0.15f * (1f - k), true));
        yield return Over(0.35f, k =>
        {
            ringImage.color = Color.Lerp(flash, ringColor, k);
            float pop = success ? 1f + 0.15f * (1f - k) : 1f;
            Vector2 wobble = success ? Vector2.zero : new Vector2(Mathf.Sin(k * 60f) * 12f * (1f - k), 0f);
            ringRoot.localScale = Vector3.one * pop;
            ringRoot.anchoredPosition = ringScreenOffset + wobble;
        }, true);
    }

    private IEnumerator FadeUI(float to)
    {
        float from = ui.alpha;
        yield return Over(0.2f, k =>
        {
            ui.alpha = Mathf.Lerp(from, to, k);
            ringRoot.localScale = Vector3.one * Mathf.Lerp(to > 0f ? 0.7f : 1f, to > 0f ? 1f : 0.8f, Smooth(k));
        }, true);
    }

    private IEnumerator SlowMotion(float scale, float realSeconds)
    {
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(realSeconds);
        if (!PauseMenu.IsPaused && !GameOverScreen.IsShowing) Time.timeScale = 1f;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null) voice.PlayOneShot(clip, GameAudio.Sfx);
    }

    private static IEnumerator Over(float duration, System.Action<float> step, bool unscaled = false)
    {
        for (float t = 0f; t < duration; t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime)
        {
            step(t / duration);
            yield return null;
        }
        step(1f);
    }

    private float FeetHeight()
    {
        float y = float.MaxValue;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (r.enabled && !(r is ParticleSystemRenderer) && !(r is LineRenderer)) y = Mathf.Min(y, r.bounds.min.y);
        return y == float.MaxValue ? transform.position.y - 1f : y;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

    // ---------- UI (built at runtime) ----------

    private void BuildUI()
    {
        var canvasGo = new GameObject("Last Stand UI", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        ui = canvasGo.GetComponent<CanvasGroup>();
        ui.alpha = 0f;
        ui.blocksRaycasts = false;
        ui.interactable = false;

        ringRoot = NewRect("Ring", canvasGo.transform, new Vector2(ringSize, ringSize));
        ringRoot.anchoredPosition = ringScreenOffset;

        // dark outline + white ring
        Image shadow = NewImage("Ring Shadow", ringRoot, new Vector2(ringSize + 10f, ringSize + 10f), RingSprite(0.075f));
        shadow.color = new Color(0f, 0f, 0f, 0.45f);
        ringImage = NewImage("Ring Line", ringRoot, new Vector2(ringSize, ringSize), RingSprite(0.05f));
        ringImage.color = ringColor;

        // pink target zone, a thicker band on the ring
        zoneImage = NewImage("Zone", ringRoot, new Vector2(ringSize + 14f, ringSize + 14f), RingSprite(0.11f));
        zone = zoneImage.rectTransform;
        zoneImage.color = zoneColor;
        zoneImage.type = Image.Type.Filled;
        zoneImage.fillMethod = Image.FillMethod.Radial360;
        zoneImage.fillOrigin = (int)Image.Origin360.Top;
        zoneImage.fillClockwise = true;

        // needle
        needlePivot = NewRect("Needle Pivot", ringRoot, Vector2.zero);
        Image needle = NewImage("Needle", needlePivot, new Vector2(16f, 40f), RoundedSprite());
        needle.rectTransform.anchoredPosition = new Vector2(0f, ringSize * 0.5f - 6f);
        needle.color = needleColor;

        // "Space" key in the middle
        Image key = NewImage("Key", ringRoot, new Vector2(150f, 56f), RoundedSprite());
        key.type = Image.Type.Sliced;
        key.color = new Color(0.97f, 0.97f, 1f);
        var keyShadow = key.gameObject.AddComponent<Shadow>();
        keyShadow.effectDistance = new Vector2(0f, -4f);
        keyShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        var labelGo = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(key.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "Space";
        label.fontSize = 30f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.2f, 0.22f, 0.3f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        // one dot per hit needed
        dots = new Image[Mathf.Max(1, hitsToWin)];
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i] = NewImage("Dot " + (i + 1), ringRoot, new Vector2(18f, 18f), CircleSprite());
            dots[i].rectTransform.anchoredPosition = new Vector2((i - (dots.Length - 1) * 0.5f) * 30f, -58f);
        }
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        return rect;
    }

    private static Image NewImage(string name, Transform parent, Vector2 size, Sprite sprite)
    {
        RectTransform rect = NewRect(name, parent, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    // White ring, thickness as a fraction of the size, soft edges
    private static Sprite RingSprite(float thickness)
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        float outer = size * 0.5f - 1f, inner = outer - thickness * size;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CircleSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(size * 0.5f - 1f - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Rounded rectangle, 9-sliced
    private static Sprite RoundedSprite()
    {
        const int size = 48, radius = 14;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
