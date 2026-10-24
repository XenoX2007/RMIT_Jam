using TMPro;
using UnityEngine;

public class ResolutionsSettings : MonoBehaviour
{
    [SerializeField] TMP_Dropdown resolutionDropdown;

    void Start()
    {
        if (resolutionDropdown == null)
        {
            Debug.LogError("Chưa gán Resolution Dropdown.");
            return;
        }

        resolutionDropdown.onValueChanged.AddListener(ApplyResolution);

        // Mặc định là Full HD
        resolutionDropdown.SetValueWithoutNotify(0);
        ApplyResolution(0);
    }

    void ApplyResolution(int index)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Trên trình duyệt, trang web quyết định kích thước canvas: đặt độ phân giải sẽ làm hình bị lệch / nhỏ ở góc
        return;
#endif
        switch (index)
        {
            case 0:
                Screen.SetResolution(1920, 1080, Screen.fullScreenMode);
                break;

            case 1:
                Screen.SetResolution(1600, 900, Screen.fullScreenMode);
                break;

            case 2:
                Screen.SetResolution(1680, 1050, Screen.fullScreenMode);
                break;
        }
    }

    public void ResetResolution()
    {
        if (resolutionDropdown == null) return;

        resolutionDropdown.SetValueWithoutNotify(0);
        ApplyResolution(0);
    }
}