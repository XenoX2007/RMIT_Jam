using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Điều khiển trận boss nhịp điệu: sinh nốt theo BPM, đọc phím mũi tên,
// chấm điểm theo thời gian, và đẩy thanh tiến độ lên/xuống.
// Thanh đầy = boss thua, thanh cạn = player thua.
public class RhythmGameManager : MonoBehaviour
{
    [Header("Song")]
    [Tooltip("Optional. Leave the clip empty to play without music.")]
    [SerializeField] AudioSource music;
    [SerializeField] float bpm = 110f;
    [Tooltip("Seconds into the clip where the first beat lands.")]
    [SerializeField] float firstBeatOffset = 0f;
    [SerializeField] float startDelay = 3f;
    [Tooltip("Seconds a note takes to slide from the right edge to the hit zone.")]
    [SerializeField] float noteTravelTime = 2f;
    [SerializeField, Range(0f, 1f)] float noteChance = 0.8f;
    [SerializeField, Range(0f, 1f)] float halfBeatChance = 0.15f;

    [Header("Timing windows (seconds)")]
    [SerializeField] float perfectWindow = 0.07f;
    [SerializeField] float goodWindow = 0.14f;
    [SerializeField] float missWindow = 0.22f;

    [Header("Progress bar")]
    [SerializeField, Range(0f, 0.5f)] float startProgress = 0f;
    [SerializeField] float perfectGain = 0.015f;
    [SerializeField] float goodGain = 0.008f;
    [SerializeField] float missPenalty = 0.04f;
    [SerializeField] float wrongPressPenalty = 0.02f;
    [SerializeField] RectTransform barFill;
    [SerializeField] float barSmoothSpeed = 8f;

    [Header("Notes")]
    [SerializeField] RhythmNote notePrefab;
    [SerializeField] RectTransform noteTrack;
    [SerializeField] RectTransform hitZone;
    [SerializeField] Image hitZoneImage;

    [Header("Characters")]
    [SerializeField] BattleCharacter player;
    [SerializeField] BattleCharacter boss;

    [Header("Text")]
    [SerializeField] TMP_Text judgementText;
    [SerializeField] TMP_Text comboText;
    [SerializeField] TMP_Text centerText;

    [Header("Result")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] TMP_Text resultText;
    [SerializeField] float resultDelay = 1.5f;
    [SerializeField] string menuScene = "MainMenu";

    readonly List<RhythmNote> activeNotes = new List<RhythmNote>();
    double songTime;
    int nextHalfBeat;
    bool musicStarted;
    bool gameOver;
    float progress;
    float shownProgress;
    int combo;
    int lastCountdown = -1;
    Color hitZoneBaseColor;
    Coroutine judgementRoutine;
    Coroutine hitZoneRoutine;

    static readonly ArrowDirection[] Directions =
        { ArrowDirection.Left, ArrowDirection.Down, ArrowDirection.Up, ArrowDirection.Right };

    void Start()
    {
        progress = shownProgress = startProgress;
        songTime = -startDelay;

        if (hitZoneImage != null)
            hitZoneBaseColor = hitZoneImage.color;
        if (resultPanel != null)
            resultPanel.SetActive(false);

        judgementText.text = "";
        comboText.text = "";
        UpdateBar(true);
    }

    void Update()
    {
        UpdateBar(false);
        if (gameOver) return;

        songTime += Time.deltaTime;

        if (!musicStarted && songTime >= 0)
        {
            musicStarted = true;
            if (music != null && music.clip != null)
                music.Play();
        }

        UpdateCountdown();
        SpawnNotes();
        MoveNotes();
        ReadInput();
        CheckMissedNotes();
    }

    void UpdateCountdown()
    {
        if (centerText == null) return;

        int count = Mathf.CeilToInt((float)-songTime);
        if (count == lastCountdown) return;
        lastCountdown = count;

        if (count > 0 && count <= 3)
            centerText.text = count.ToString();
        else if (count == 0)
            centerText.text = "GO!";
        else if (count < 0)
            centerText.text = "";
    }

    void SpawnNotes()
    {
        double halfBeat = 30.0 / bpm;

        // Sinh trước các nốt sẽ chạm vạch trong khoảng noteTravelTime tới
        while (firstBeatOffset + nextHalfBeat * halfBeat - songTime <= noteTravelTime)
        {
            double hitTime = firstBeatOffset + nextHalfBeat * halfBeat;
            bool onBeat = nextHalfBeat % 2 == 0;
            nextHalfBeat++;

            if (Random.value > (onBeat ? noteChance : halfBeatChance)) continue;

            RhythmNote note = Instantiate(notePrefab, noteTrack);
            note.Setup(Directions[Random.Range(0, Directions.Length)], hitTime);
            activeNotes.Add(note);
            PositionNote(note);
        }
    }

    void MoveNotes()
    {
        foreach (RhythmNote note in activeNotes)
            PositionNote(note);
    }

    void PositionNote(RhythmNote note)
    {
        Vector3 hitPos = hitZone.localPosition;
        float travelDistance = noteTrack.rect.xMax - hitPos.x + note.Rect.rect.width;
        float t = (float)((note.HitTime - songTime) / noteTravelTime);
        note.Rect.localPosition = new Vector3(hitPos.x + t * travelDistance, hitPos.y, 0f);
    }

    void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;

        if ((kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) ||
            (pad != null && pad.dpad.left.wasPressedThisFrame))
            HandlePress(ArrowDirection.Left);

        if ((kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) ||
            (pad != null && pad.dpad.down.wasPressedThisFrame))
            HandlePress(ArrowDirection.Down);

        if ((kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) ||
            (pad != null && pad.dpad.up.wasPressedThisFrame))
            HandlePress(ArrowDirection.Up);

        if ((kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) ||
            (pad != null && pad.dpad.right.wasPressedThisFrame))
            HandlePress(ArrowDirection.Right);
    }

    void HandlePress(ArrowDirection direction)
    {
        // Tìm nốt cùng hướng gần vạch nhất
        RhythmNote closest = null;
        double closestError = double.MaxValue;

        foreach (RhythmNote note in activeNotes)
        {
            if (note.Direction != direction) continue;

            double error = System.Math.Abs(note.HitTime - songTime);
            if (error < closestError)
            {
                closest = note;
                closestError = error;
            }
        }

        if (closest != null && closestError <= missWindow)
        {
            if (closestError <= perfectWindow)
                RegisterHit(closest, "PERFECT!", perfectGain);
            else if (closestError <= goodWindow)
                RegisterHit(closest, "GOOD", goodGain);
            else
                RegisterMiss(closest, songTime < closest.HitTime ? "TOO EARLY" : "TOO LATE");
        }
        else if (songTime >= 0)
        {
            // Bấm sai phím hoặc bấm khi không có nốt nào ở vạch
            combo = 0;
            comboText.text = "";
            ShowJudgement("WRONG!", new Color(1f, 0.4f, 0.4f));
            FlashHitZone(new Color(1f, 0.3f, 0.3f));
            player.Hurt();
            ChangeProgress(-wrongPressPenalty);
        }
    }

    void CheckMissedNotes()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (songTime - activeNotes[i].HitTime > missWindow)
                RegisterMiss(activeNotes[i], "MISS");
        }
    }

    void RegisterHit(RhythmNote note, string label, float gain)
    {
        activeNotes.Remove(note);
        note.Hit();

        combo++;
        comboText.text = combo >= 2 ? combo + " COMBO" : "";
        ShowJudgement(label, note.NoteColor);
        FlashHitZone(note.NoteColor);

        player.Attack();
        boss.Hurt();
        ChangeProgress(gain);
    }

    void RegisterMiss(RhythmNote note, string label)
    {
        activeNotes.Remove(note);
        note.Miss();

        combo = 0;
        comboText.text = "";
        ShowJudgement(label, new Color(1f, 0.4f, 0.4f));

        boss.Attack();
        player.Hurt();
        ChangeProgress(-missPenalty);
    }

    void ChangeProgress(float delta)
    {
        if (gameOver) return;

        progress = Mathf.Clamp01(progress + delta);

        if (progress >= 1f)
            EndGame(true);
        else if (progress <= 0f)
            EndGame(false);
    }

    void UpdateBar(bool instant)
    {
        if (barFill == null) return;

        shownProgress = instant ? progress : Mathf.Lerp(shownProgress, progress, 1f - Mathf.Exp(-barSmoothSpeed * Time.deltaTime));
        barFill.anchorMax = new Vector2(shownProgress, 1f);
    }

    void EndGame(bool playerWon)
    {
        gameOver = true;
        comboText.text = "";

        foreach (RhythmNote note in activeNotes)
            note.Miss();
        activeNotes.Clear();

        if (playerWon)
            boss.Defeat();
        else
            player.Defeat();

        if (music != null)
            music.Stop();

        StartCoroutine(ShowResult(playerWon));
    }

    IEnumerator ShowResult(bool playerWon)
    {
        yield return new WaitForSeconds(resultDelay);

        if (centerText != null)
            centerText.text = "";
        if (resultText != null)
            resultText.text = playerWon ? "BOSS DEFEATED!" : "YOU WERE DEFEATED";
        if (resultPanel != null)
            resultPanel.SetActive(true);
    }

    void ShowJudgement(string label, Color color)
    {
        if (judgementRoutine != null)
            StopCoroutine(judgementRoutine);
        judgementRoutine = StartCoroutine(JudgementPop(label, color));
    }

    IEnumerator JudgementPop(string label, Color color)
    {
        judgementText.text = label;
        float elapsed = 0f;
        const float duration = 0.6f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Phóng to rồi thu về, mờ dần ở nửa sau
            float pop = Mathf.Clamp01(t / 0.15f);
            judgementText.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, pop * pop * (3f - 2f * pop));
            float fade = Mathf.Clamp01((t - 0.5f) / 0.5f);
            judgementText.color = new Color(color.r, color.g, color.b, 1f - fade);
            yield return null;
        }

        judgementText.text = "";
    }

    void FlashHitZone(Color color)
    {
        if (hitZoneImage == null) return;

        if (hitZoneRoutine != null)
            StopCoroutine(hitZoneRoutine);
        hitZoneRoutine = StartCoroutine(HitZonePulse(color));
    }

    IEnumerator HitZonePulse(Color color)
    {
        float elapsed = 0f;
        const float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            hitZone.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, eased);
            hitZoneImage.color = Color.Lerp(color, hitZoneBaseColor, eased);
            yield return null;
        }

        hitZone.localScale = Vector3.one;
        hitZoneImage.color = hitZoneBaseColor;
    }

    public void Retry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene(menuScene);
    }
}
