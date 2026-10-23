using UnityEngine;
using UnityEngine.UI;

// Nối 3 thanh trượt Master / Music / SFX với GameAudio. Dùng chung cho Main Menu và menu tạm dừng.
// Đặt trên một object luôn bật (không đặt trong panel Settings đang ẩn) và kéo 3 slider vào.
public class SoundSettingsUI : MonoBehaviour
{
    [SerializeField] Slider masterSlider;
    [SerializeField] Slider musicSlider;
    [SerializeField] Slider sfxSlider;

    // Start chạy sau Awake của các menu, nên giá trị mặc định (cho nút Reset) vẫn là giá trị trong scene
    void Start()
    {
        Bind(masterSlider, GameAudio.Master, v => GameAudio.Master = v);
        Bind(musicSlider, GameAudio.Music, v => GameAudio.Music = v);
        Bind(sfxSlider, GameAudio.Sfx, v => GameAudio.Sfx = v);
    }

    void OnDisable()
    {
        GameAudio.Save();
    }

    static void Bind(Slider slider, float value, UnityEngine.Events.UnityAction<float> apply)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(apply);
    }
}
