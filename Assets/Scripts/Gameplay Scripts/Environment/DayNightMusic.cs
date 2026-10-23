using System.Collections;
using UnityEngine;

// Nhạc nền theo ngày/đêm: ban ngày phát CrocodileMorning, ban đêm phát CrocodileNight.
// Khi trời chuyển ngày <-> đêm thì nhạc mờ dần sang bài kia (crossfade).
// Âm lượng nhân với Music Volume trong menu (GameAudio.Music).
public class DayNightMusic : MonoBehaviour
{
    [SerializeField] DayNightCycle dayNightCycle;
    [SerializeField] AudioClip morningMusic;
    [SerializeField] AudioClip nightMusic;
    [SerializeField, Range(0f, 1f)] float volume = 0.5f;
    [Tooltip("Seconds to fade from one song to the other.")]
    [SerializeField] float fadeTime = 4f;

    AudioSource morningSource;
    AudioSource nightSource;
    float morningWeight;   // 0-1: phần của mỗi bài trong lúc crossfade
    float nightWeight;
    bool night;
    Coroutine fade;

    void Start()
    {
        if (dayNightCycle == null) dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        morningSource = CreateSource(morningMusic);
        nightSource = CreateSource(nightMusic);

        // Bắt đầu ngay với bài đúng thời điểm, không cần fade
        night = dayNightCycle != null && dayNightCycle.IsNight;
        if (night) nightWeight = 1f; else morningWeight = 1f;
        (night ? nightSource : morningSource).Play();
        ApplyVolume();
    }

    void Update()
    {
        if (dayNightCycle != null && dayNightCycle.IsNight != night)
        {
            night = dayNightCycle.IsNight;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(Crossfade(night));
        }
        ApplyVolume();   // theo kịp thanh Music Volume khi đang chỉnh trong menu
    }

    void ApplyVolume()
    {
        float music = volume * GameAudio.Music;
        morningSource.volume = morningWeight * music;
        nightSource.volume = nightWeight * music;
    }

    IEnumerator Crossfade(bool toNight)
    {
        AudioSource to = toNight ? nightSource : morningSource;
        AudioSource from = toNight ? morningSource : nightSource;
        if (!to.isPlaying) to.Play();   // bài mới bắt đầu từ đầu
        float fromStart = toNight ? morningWeight : nightWeight;
        float toStart = toNight ? nightWeight : morningWeight;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeTime;
            k = k * k * (3f - 2f * k);
            SetWeights(toNight, Mathf.Lerp(fromStart, 0f, k), Mathf.Lerp(toStart, 1f, k));
            yield return null;
        }
        SetWeights(toNight, 0f, 1f);
        from.Stop();
        fade = null;
    }

    void SetWeights(bool toNight, float fromWeight, float toWeight)
    {
        if (toNight) { morningWeight = fromWeight; nightWeight = toWeight; }
        else { nightWeight = fromWeight; morningWeight = toWeight; }
    }

    AudioSource CreateSource(AudioClip clip)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // nhạc nền 2D, nghe như nhau ở mọi nơi
        source.volume = 0f;
        return source;
    }
}
