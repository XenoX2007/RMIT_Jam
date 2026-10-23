using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Cutscene khi nhặt đủ 3 mảnh:
// 1. Toàn cảnh góc bản đồ  2. Máy quay bay tới nhân vật chính
// 3. 3 mảnh bay vòng quanh nhân vật rồi nhập vào người (lóe sáng)
// 4. Mặt đất rung, máy quay ra sau lưng nhân vật
// 5. Cá sấu mặc vest trồi lên từ dưới nước rồi gầm  6. Mờ đen -> Boss Fight.
// Space / Enter / Esc để bỏ qua.
public class WinCutscene : MonoBehaviour
{
    [Header("Cast")]
    [SerializeField] Camera cam;
    [SerializeField] Animator character;
    [SerializeField] Transform[] pieces;
    [SerializeField] Transform crocodile;
    [SerializeField] Animator crocodileAnimator;
    [SerializeField] AudioClip roarSound;
    [SerializeField, Range(0f, 1f)] float roarVolume = 1f;

    [Header("Camera shots (move these points in the scene to change the angles)")]
    [SerializeField] Transform establishingShot;
    [SerializeField] Transform characterShot;
    [SerializeField] Transform crocodileShot;
    [Tooltip("Degrees the camera circles around the character while the pieces fly.")]
    [SerializeField] float orbitCameraAngle = 35f;
    [Tooltip("The camera flies in through a point this far behind the character shot (over the river) ...")]
    [SerializeField] float flyApproachDistance = 16f;
    [Tooltip("... and this high above it, so it never passes through the trees.")]
    [SerializeField] float flyArcHeight = 10f;

    [Header("Timing (seconds)")]
    [SerializeField] float fadeInTime = 1.2f;
    [SerializeField] float establishingTime = 3f;
    [SerializeField] float flyToCharacterTime = 3.5f;
    [SerializeField] float orbitTime = 5f;
    [SerializeField] float combineTime = 0.6f;
    [SerializeField] float rumbleTime = 1.8f;
    [SerializeField] float riseTime = 2.4f;
    [SerializeField] float roarTime = 3.4f;
    [SerializeField] float fadeOutTime = 1.2f;

    [Header("Pieces")]
    [SerializeField] float pieceStartDistance = 12f;
    [SerializeField] float pieceStartHeight = 7f;
    [SerializeField] float orbitRadius = 0.9f;
    [SerializeField] float orbitTurns = 3f;
    [SerializeField] float pieceScale = 0.45f;
    [SerializeField] Color flashColor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] float flashIntensity = 12f;

    [Header("Crocodile")]
    [Tooltip("How far below its final position the crocodile starts (hidden under the river bed).")]
    [SerializeField] float riseDepth = 7f;
    [SerializeField] float waterLevel = 3f;
    [SerializeField] string screamState = "Scream";
    [SerializeField] Material splashMaterial;

    [Header("End")]
    [SerializeField] string nextScene = "BossFight";
    [Tooltip("Height of the black cinematic bars (fraction of the screen).")]
    [SerializeField, Range(0f, 0.25f)] float letterbox = 0.1f;

    Vector3 camPos;
    Quaternion camRot;
    float shake;
    CanvasGroup fader;
    Vector3 crocHome;
    ParticleSystem splash;
    Light flash;
    AudioSource voice;
    bool ending;

    IEnumerator Start()
    {
        Setup();
        StartCoroutine(ScreenFader.Fade(fader, 0f, fadeInTime));

        // 1. Toàn cảnh, máy quay trôi nhẹ về phía trước
        Vector3 p0 = establishingShot.position;
        camRot = establishingShot.rotation;
        yield return Over(establishingTime, k => camPos = p0 + establishingShot.forward * (8f * Smooth(k)));

        // 2. Bay xuống nhân vật
        Vector3 fromPos = camPos;
        Quaternion fromRot = camRot;
        // bay vòng qua phía trên mặt sông (không có cây) rồi lướt vào nhân vật
        Vector3 via = characterShot.position - characterShot.forward * flyApproachDistance + Vector3.up * flyArcHeight;
        yield return Over(flyToCharacterTime, k =>
        {
            float e = Smooth(k);
            camPos = Vector3.Lerp(Vector3.Lerp(fromPos, via, e), Vector3.Lerp(via, characterShot.position, e), e);
            camRot = Quaternion.Slerp(fromRot, characterShot.rotation, e);
        });

        // 3. 3 mảnh bay vòng quanh, lại gần dần và nhanh dần; máy quay xoay quanh nhân vật
        foreach (Transform piece in pieces) piece.gameObject.SetActive(true);
        Vector3 shotOffset = characterShot.position - character.transform.position;
        yield return Over(orbitTime, k =>
        {
            Quaternion around = Quaternion.AngleAxis(orbitCameraAngle * Smooth(k), Vector3.up);
            camPos = character.transform.position + around * shotOffset;
            camRot = Quaternion.LookRotation(Chest() - camPos);
            PlacePieces(k);
        });

        // Nhập vào người + lóe sáng
        Vector3[] startPos = new Vector3[pieces.Length];
        for (int i = 0; i < pieces.Length; i++) startPos[i] = pieces[i].position;
        yield return Over(combineTime, k =>
        {
            float e = k * k;
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i].position = Vector3.Lerp(startPos[i], Chest(), e);
                pieces[i].localScale = Vector3.one * pieceScale * (1f - e);
            }
            flash.transform.position = Chest();
            flash.intensity = flashIntensity * e;
        });
        foreach (Transform piece in pieces) piece.gameObject.SetActive(false);
        StartCoroutine(Over(1f, k => flash.intensity = flashIntensity * (1f - Smooth(k))));

        // 4. Rung đất, máy quay ra sau lưng nhân vật
        fromPos = camPos;
        fromRot = camRot;
        yield return Over(rumbleTime, k =>
        {
            float e = Smooth(k);
            camPos = Vector3.Lerp(fromPos, crocodileShot.position, e);
            camRot = Quaternion.Slerp(fromRot, crocodileShot.rotation, e);
            shake = 0.06f + 0.06f * k;
        });

        // 5. Cá sấu trồi lên
        bool splashed = false;
        yield return Over(riseTime, k =>
        {
            float e = 1f - Mathf.Pow(1f - k, 3f);   // nhanh lúc đầu, chậm dần khi lên tới nơi
            crocodile.position = crocHome - Vector3.up * riseDepth * (1f - e);
            if (!splashed && crocodile.position.y + riseDepth * 0.55f > waterLevel)
            {
                splashed = true;
                Splash(140);
            }
            shake = 0.12f * (1f - k) + 0.04f;
        });
        Splash(50);

        // Gầm
        crocodileAnimator.speed = 1f;
        if (roarSound != null)
        {
            voice.clip = roarSound;
            voice.volume = roarVolume * GameAudio.Sfx;
            voice.Play();
        }
        Vector3 roarFrom = camPos;
        yield return Over(roarTime, k =>
        {
            camPos = roarFrom + camRot * Vector3.forward * (1.5f * Smooth(k));
            shake = k < 0.15f ? 0.05f : 0.3f * Mathf.Clamp01(1f - (k - 0.15f) / 0.6f);
        });
        shake = 0f;

        // 6. Kết thúc
        yield return End();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (ending || kb == null) return;
        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
        {
            StopAllCoroutines();
            StartCoroutine(End());
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 offset = Vector3.zero;
        if (shake > 0f)
        {
            float t = Time.time * 22f;
            offset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * 2f * shake;
        }
        cam.transform.SetPositionAndRotation(camPos + camRot * offset, camRot);
    }

    IEnumerator End()
    {
        ending = true;
        shake = 0f;
        yield return ScreenFader.Fade(fader, 1f, fadeOutTime);
        SceneManager.LoadScene(nextScene);
    }

    // ---------- Setup ----------

    void Setup()
    {
        if (cam == null) cam = Camera.main;
        camPos = establishingShot.position;
        camRot = establishingShot.rotation;
        LateUpdate();

        CreateLetterbox();
        fader = ScreenFader.CreateOverlay(1f);

        foreach (Transform piece in pieces)
        {
            piece.localScale = Vector3.one * pieceScale;
            piece.gameObject.SetActive(false);
        }

        // Cá sấu: giấu dưới đáy sông, đứng yên ở khung đầu của Scream
        crocHome = crocodile.position;
        crocodile.position = crocHome - Vector3.up * riseDepth;
        crocodileAnimator.Play(screamState, 0, 0f);
        crocodileAnimator.speed = 0f;
        crocodileAnimator.Update(0f);

        voice = crocodile.gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;

        var flashGo = new GameObject("Combine Flash");
        flash = flashGo.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = flashColor;
        flash.range = 8f;
        flash.intensity = 0f;

        splash = CreateSplash();
    }

    void CreateLetterbox()
    {
        if (letterbox <= 0f) return;
        var go = new GameObject("Letterbox", typeof(Canvas));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        Bar(go.transform, 0f, letterbox);
        Bar(go.transform, 1f - letterbox, 1f);
    }

    static void Bar(Transform parent, float yMin, float yMax)
    {
        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)bar.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, yMin);
        rect.anchorMax = new Vector2(1f, yMax);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        bar.GetComponent<Image>().color = Color.black;
    }

    ParticleSystem CreateSplash()
    {
        var go = new GameObject("Splash");
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // hình nón bắn lên trên
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
        main.startColor = new Color(0.85f, 0.93f, 1f, 0.9f);
        main.gravityModifier = 1.6f;
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 1.6f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = splashMaterial != null ? splashMaterial : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        return ps;
    }

    // ---------- Helpers ----------

    void PlacePieces(float k)
    {
        Vector3 center = Chest();
        float approach = 1f - (1f - k) * (1f - k);          // xa -> gần, chậm dần
        float radius = Mathf.Lerp(pieceStartDistance, orbitRadius, approach);
        float height = Mathf.Lerp(pieceStartHeight, 0f, approach);
        float angle = orbitTurns * 360f * k * k;             // quay nhanh dần

        for (int i = 0; i < pieces.Length; i++)
        {
            float a = (angle + i * 360f / pieces.Length) * Mathf.Deg2Rad;
            float bob = Mathf.Sin(a * 2f) * 0.2f;
            pieces[i].position = center + new Vector3(Mathf.Cos(a) * radius, height + bob, Mathf.Sin(a) * radius);
            pieces[i].Rotate(new Vector3(90f, 140f, 60f) * Time.deltaTime, Space.Self);
        }
    }

    Vector3 Chest()
    {
        Transform chest = character.isHuman ? character.GetBoneTransform(HumanBodyBones.Chest) : null;
        return chest != null ? chest.position : character.transform.position + Vector3.up * 1.3f;
    }

    void Splash(int count)
    {
        splash.transform.position = new Vector3(crocHome.x, waterLevel, crocHome.z);
        splash.Emit(count);
    }

    static IEnumerator Over(float duration, Action<float> step)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            step(t / duration);
            yield return null;
        }
        step(1f);
    }

    static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
