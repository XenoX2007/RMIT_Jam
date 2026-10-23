using UnityEngine;

// Cho một AudioSource bình thường (ví dụ tiếng mưa) đi theo âm lượng Music hoặc SFX trong menu.
// Âm lượng gốc = volume của AudioSource lúc bắt đầu.
[RequireComponent(typeof(AudioSource))]
public class AudioVolumeChannel : MonoBehaviour
{
    public enum Channel { Music, Sfx }

    [SerializeField] Channel channel = Channel.Sfx;

    AudioSource source;
    float baseVolume;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
        Apply();
    }

    void OnEnable() => GameAudio.Changed += Apply;
    void OnDisable() => GameAudio.Changed -= Apply;

    void Apply()
    {
        if (source != null) source.volume = baseVolume * (channel == Channel.Music ? GameAudio.Music : GameAudio.Sfx);
    }
}
