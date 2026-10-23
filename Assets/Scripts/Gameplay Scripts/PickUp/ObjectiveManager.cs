using TMPro;
using UnityEngine;

public class ObjectiveManager : MonoBehaviour
{
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private AudioClip collectSound;
    [SerializeField, Range(0f, 1f)] private float collectVolume = 1f;

    [Header("Win")]
    [Tooltip("Scene loaded after the 3rd piece (the win cutscene).")]
    [SerializeField] private string winScene = "WinCutscene";
    [Tooltip("Seconds after the 3rd piece before the screen fades out.")]
    [SerializeField] private float winDelay = 1.5f;

    public static ObjectiveManager Instance;

    private int collectedPieces;

    private void Awake()
    {
        Instance = this;
    }

    public void CollectPiece()
    {
        collectedPieces++;
        PlayCollectSound();

        Debug.Log(
            $"Collected {collectedPieces}/3");

        if (collectedPieces >= 3)
        {
            WinGame();
        }
    }

    // 2D one-shot, so it sounds the same however far the camera is
    private void PlayCollectSound()
    {
        if (collectSound == null) return;
        if (!TryGetComponent(out AudioSource source))
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        source.PlayOneShot(collectSound, collectVolume * GameAudio.Sfx);
    }

    // All 3 pieces: fade out and play the win cutscene (which then goes to the boss fight)
    private void WinGame()
    {
        Debug.Log("YOU WIN");
        if (string.IsNullOrEmpty(winScene)) return;
        ScreenFader.FadeAndLoad(this, winScene, 1.2f, winDelay);
    }

    private void OnDestroy()
    {
        // Statics survive scene loads
        InteractionController.CurrentBarrel = null;
        ThrowableObject.HeldObject = null;
    }
    private void UpdateObjectiveText()
    {
        objectiveText.text = $"Collected {collectedPieces}/3";
    }
    private void Update()
    {
        UpdateObjectiveText();
    }
}