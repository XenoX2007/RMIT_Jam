using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsCameraTransition : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] Camera mainCamera;
    [SerializeField] Transform settingsView;
    [SerializeField] float rotateDuration = 2f;

    [Header("Panels")]
    [SerializeField] GameObject settingsPanel;
    [SerializeField] GameObject[] mainMenuObjects;

    [Header("Settings to reset")]
    [SerializeField] Slider[] sliders;
    [SerializeField] Toggle[] toggles;
    [SerializeField] TMP_Dropdown[] tmpDropdowns;
    [SerializeField] Dropdown[] dropdowns;

    Quaternion mainMenuRotation;
    float[] defaultSliderValues;
    bool[] defaultToggleValues;
    int[] defaultTMPDropdownValues;
    int[] defaultDropdownValues;
    bool isTransitioning;

    void Awake()
    {
        if (mainCamera != null)
            mainMenuRotation = mainCamera.transform.rotation;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        SaveDefaultSettings();
    }

    void SaveDefaultSettings()
    {
        defaultSliderValues = new float[sliders.Length];
        for (int i = 0; i < sliders.Length; i++)
            if (sliders[i] != null)
                defaultSliderValues[i] = sliders[i].value;

        defaultToggleValues = new bool[toggles.Length];
        for (int i = 0; i < toggles.Length; i++)
            if (toggles[i] != null)
                defaultToggleValues[i] = toggles[i].isOn;

        defaultTMPDropdownValues = new int[tmpDropdowns.Length];
        for (int i = 0; i < tmpDropdowns.Length; i++)
            if (tmpDropdowns[i] != null)
                defaultTMPDropdownValues[i] = tmpDropdowns[i].value;

        defaultDropdownValues = new int[dropdowns.Length];
        for (int i = 0; i < dropdowns.Length; i++)
            if (dropdowns[i] != null)
                defaultDropdownValues[i] = dropdowns[i].value;
    }

    public void OpenSettings()
    {
        if (!isTransitioning)
            StartCoroutine(OpenSettingsRoutine());
    }

    IEnumerator OpenSettingsRoutine()
    {
        if (mainCamera == null || settingsView == null || settingsPanel == null)
        {
            Debug.LogError("Hãy gán Camera, Settings View và Settings Panel.");
            yield break;
        }

        isTransitioning = true;
        SetObjectsActive(mainMenuObjects, false);

        yield return RotateCamera(settingsView.rotation);

        settingsPanel.SetActive(true);
        isTransitioning = false;
    }

    public void BackToMainMenu()
    {
        if (!isTransitioning)
            StartCoroutine(BackRoutine());
    }

    IEnumerator BackRoutine()
    {
        if (mainCamera == null || settingsPanel == null)
        {
            Debug.LogError("Hãy gán Camera và Settings Panel.");
            yield break;
        }

        isTransitioning = true;
        settingsPanel.SetActive(false);

        yield return RotateCamera(mainMenuRotation);

        SetObjectsActive(mainMenuObjects, true);
        isTransitioning = false;
    }

    public void ResetSettings()
    {
        for (int i = 0; i < sliders.Length; i++)
            if (sliders[i] != null)
                sliders[i].value = defaultSliderValues[i];

        for (int i = 0; i < toggles.Length; i++)
            if (toggles[i] != null)
                toggles[i].isOn = defaultToggleValues[i];

        for (int i = 0; i < tmpDropdowns.Length; i++)
            if (tmpDropdowns[i] != null)
                tmpDropdowns[i].value = defaultTMPDropdownValues[i];

        for (int i = 0; i < dropdowns.Length; i++)
            if (dropdowns[i] != null)
                dropdowns[i].value = defaultDropdownValues[i];
    }

    IEnumerator RotateCamera(Quaternion targetRotation)
    {
        Quaternion startRotation = mainCamera.transform.rotation;
        float elapsed = 0f;

        while (elapsed < rotateDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / rotateDuration);
            float smoothT = t * t * (3f - 2f * t);

            mainCamera.transform.rotation =
                Quaternion.Slerp(startRotation, targetRotation, smoothT);

            yield return null;
        }

        mainCamera.transform.rotation = targetRotation;
    }

    void SetObjectsActive(GameObject[] objects, bool active)
    {
        foreach (GameObject item in objects)
            if (item != null)
                item.SetActive(active);
    }
}