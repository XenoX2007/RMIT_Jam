using System;
using UnityEngine;

// Âm lượng chung của game: Master, Music, SFX (0-1).
// Lưu bằng PlayerPrefs nên chỉnh ở Main Menu hay ở menu tạm dừng đều giữ nguyên khi đổi scene / mở lại game.
// Master = AudioListener.volume. Nhạc nhân với Music, hiệu ứng âm thanh nhân với SFX.
public static class GameAudio
{
    const string MasterKey = "Volume_Master";
    const string MusicKey = "Volume_Music";
    const string SfxKey = "Volume_SFX";

    public static event Action Changed;

    public static float Master
    {
        get => PlayerPrefs.GetFloat(MasterKey, 1f);
        set { PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value)); AudioListener.volume = Master; Changed?.Invoke(); }
    }

    public static float Music
    {
        get => PlayerPrefs.GetFloat(MusicKey, 1f);
        set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
    }

    public static float Sfx
    {
        get => PlayerPrefs.GetFloat(SfxKey, 1f);
        set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
    }

    public static void Save() => PlayerPrefs.Save();

    // Áp dụng Master ngay khi game chạy, kể cả khi chưa mở menu nào
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        AudioListener.volume = Master;
    }
}
