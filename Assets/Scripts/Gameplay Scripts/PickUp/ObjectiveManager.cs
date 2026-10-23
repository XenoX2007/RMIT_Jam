using TMPro;
using UnityEngine;

public class ObjectiveManager : MonoBehaviour
{
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private AudioClip collectSound;
    [SerializeField, Range(0f, 1f)] private float collectVolume = 1f;

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

    private void WinGame()
    {
        Debug.Log("YOU WIN");
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