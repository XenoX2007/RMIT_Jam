using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu tạm dừng trong game: ESC mở/đóng.
// Nút: Back To Game, Reset Character, Settings, Back To Main Menu.
// Khi mở: game dừng (timeScale = 0) và input của người chơi tạm tắt để click menu không ném dừa / xoay camera.
public class PauseMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject menuRoot;        // dim background + buttons
    [SerializeField] GameObject buttonList;
    [SerializeField] GameObject settingsPanel;

    [Header("Game")]
    [SerializeField] string mainMenuScene = "MainMenu";
    [SerializeField] PlayerHealth player;
    [Tooltip("Gameplay input asset. Its actions are paused while the menu is open.")]
    [SerializeField] InputActionAsset gameplayInput;

    [Header("Settings")]
    [SerializeField] Slider masterVolume;
    [SerializeField] Slider sensitivity;
    [SerializeField] CameraMovement cameraMovement;
    [SerializeField] TMP_Dropdown resolutionDropdown;
    [Tooltip("Sliders put back to their starting value by Reset.")]
    [SerializeField] Slider[] sliders;

    public static bool IsPaused { get; private set; }

    readonly List<InputAction> pausedActions = new List<InputAction>();
    float[] defaultSliderValues;
    float defaultSensitivity;
    float defaultSensitivitySlider;

    static readonly Vector2Int[] Resolutions = { new Vector2Int(1920, 1080), new Vector2Int(1600, 900), new Vector2Int(1680, 1050) };

    void Awake()
    {
        IsPaused = false;
        if (menuRoot != null) menuRoot.SetActive(false);

        defaultSliderValues = new float[sliders != null ? sliders.Length : 0];
        for (int i = 0; i < defaultSliderValues.Length; i++)
            if (sliders[i] != null) defaultSliderValues[i] = sliders[i].value;

        if (cameraMovement != null) defaultSensitivity = cameraMovement.Sensitivity;
        if (sensitivity != null) defaultSensitivitySlider = Mathf.Max(0.05f, sensitivity.normalizedValue);
        if (masterVolume != null) masterVolume.onValueChanged.AddListener(v => AudioListener.volume = v);
        if (sensitivity != null) sensitivity.onValueChanged.AddListener(ApplySensitivity);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(ApplyResolution);
    }

    void OnDestroy()
    {
        // Never leave the next scene frozen
        if (IsPaused) Time.timeScale = 1f;
        IsPaused = false;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        if (GameOverScreen.IsShowing) return;   // the game-over panel has its own buttons

        if (!IsPaused) Pause();
        else if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
        else Resume();
    }

    // ---------- Pause / resume ----------

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        // Pause every gameplay action that is on, remember them for Resume
        pausedActions.Clear();
        if (gameplayInput != null)
            foreach (InputAction action in gameplayInput)
                if (action.enabled)
                {
                    pausedActions.Add(action);
                    action.Disable();
                }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        menuRoot.SetActive(true);
        buttonList.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // "Back To Game"
    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        foreach (InputAction action in pausedActions) action.Enable();
        pausedActions.Clear();

        menuRoot.SetActive(false);
    }

    // "Reset Character": back to the start point with full health
    public void ResetCharacter()
    {
        if (InteractionController.CurrentBarrel != null)
            InteractionController.CurrentBarrel.ExitBarrel();
        if (ThrowableObject.HeldObject != null)
            ThrowableObject.HeldObject.Release(Vector3.zero);   // drop it where you stand

        if (player != null) player.Respawn();
        Resume();
    }

    // "Back To Main Menu"
    public void BackToMainMenu()
    {
        Resume();
        // Statics survive scene loads, so clear what the player was holding / hiding in
        InteractionController.CurrentBarrel = null;
        ThrowableObject.HeldObject = null;
        SceneManager.LoadScene(mainMenuScene);
    }

    // ---------- Settings ----------

    public void OpenSettings()
    {
        buttonList.SetActive(false);
        settingsPanel.SetActive(true);

        // Show the current values
        if (masterVolume != null) masterVolume.SetValueWithoutNotify(AudioListener.volume);
        if (resolutionDropdown != null)
        {
            int index = 0;
            for (int i = 0; i < Resolutions.Length; i++)
                if (Screen.width == Resolutions[i].x && Screen.height == Resolutions[i].y) index = i;
            resolutionDropdown.SetValueWithoutNotify(index);
        }
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        buttonList.SetActive(true);
    }

    public void ResetSettings()
    {
        for (int i = 0; i < defaultSliderValues.Length; i++)
            if (sliders[i] != null) sliders[i].value = defaultSliderValues[i];

        if (cameraMovement != null) cameraMovement.Sensitivity = defaultSensitivity;
        if (resolutionDropdown != null) resolutionDropdown.value = 0;
    }

    void ApplySensitivity(float value)
    {
        // The slider's starting position = normal speed; further right = faster (up to 3x), left = slower
        if (cameraMovement != null)
            cameraMovement.Sensitivity = defaultSensitivity * Mathf.Clamp(sensitivity.normalizedValue / defaultSensitivitySlider, 0.2f, 3f);
    }

    void ApplyResolution(int index)
    {
        if (index < 0 || index >= Resolutions.Length) return;
        Screen.SetResolution(Resolutions[index].x, Resolutions[index].y, Screen.fullScreenMode);
    }
}
