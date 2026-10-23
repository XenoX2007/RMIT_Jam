using UnityEngine;
using UnityEngine.InputSystem;

// Player sound effects:
// - Footstep loop while walking / running on anything solid (faster and louder when running)
// - Swimming loop while swimming (not while just floating)
// - Damaged sound each time HP goes down (crocodile bite)
// All follow the SFX Volume slider in the menus (GameAudio.Sfx).
public class PlayerSounds : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip footstepLoop;
    [SerializeField] private AudioClip swimLoop;
    [SerializeField] private AudioClip damaged;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float runVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float swimVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float damagedVolume = 1f;

    [Header("Footsteps")]
    [SerializeField] private InputActionReference sprintAction;
    [Tooltip("Playback speed of the footstep loop when walking / running.")]
    [SerializeField] private float walkPitch = 1f;
    [SerializeField] private float runPitch = 1.35f;
    [Tooltip("Slower than this (m/s) the footsteps stop.")]
    [SerializeField] private float minMoveSpeed = 0.5f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [Tooltip("Seconds to fade a loop in / out, so it never cuts off hard.")]
    [SerializeField] private float fadeTime = 0.15f;

    private Rigidbody rb;
    private Swimming swimming;
    private PlayerHealth health;
    private AudioSource footstepSource;
    private AudioSource swimSource;
    private AudioSource oneShotSource;
    private int lastHealth;
    private readonly Collider[] groundHits = new Collider[8];

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        swimming = GetComponent<Swimming>();
        health = GetComponent<PlayerHealth>();

        footstepSource = CreateSource(footstepLoop, true);
        swimSource = CreateSource(swimLoop, true);
        oneShotSource = CreateSource(null, false);
    }

    private void OnEnable()
    {
        if (health != null)
        {
            lastHealth = health.Health;
            health.HealthChanged += OnHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int current, int max)
    {
        if (current < lastHealth && damaged != null) oneShotSource.PlayOneShot(damaged, damagedVolume * GameAudio.Sfx);
        lastHealth = current;
    }

    private void Update()
    {
        Vector3 velocity = rb == null || rb.isKinematic ? Vector3.zero : rb.linearVelocity;
        velocity.y = 0f;
        bool moving = velocity.magnitude > minMoveSpeed;
        bool hidden = InteractionController.CurrentBarrel != null;
        bool inWater = swimming != null && swimming.InWater;
        bool running = sprintAction != null && sprintAction.action.IsPressed();

        bool walking = moving && !inWater && !hidden && OnGround();
        footstepSource.pitch = running ? runPitch : walkPitch;
        Fade(footstepSource, walking ? (running ? runVolume : footstepVolume) * GameAudio.Sfx : 0f);

        bool swimmingNow = !hidden && swimming != null && swimming.State == Swimming.SwimState.Swimming;
        Fade(swimSource, swimmingNow ? swimVolume * GameAudio.Sfx : 0f);
    }

    // Any solid thing under the feet (terrain, house floor, boat...), not the player itself
    private bool OnGround()
    {
        if (groundCheck == null) return true;
        int count = Physics.OverlapSphereNonAlloc(groundCheck.position, groundCheckRadius, groundHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!groundHits[i].transform.IsChildOf(transform)) return true;
        return false;
    }

    // Loops keep running silently in the background, so they resume where they were instead of restarting
    private void Fade(AudioSource source, float target)
    {
        if (source.clip == null) return;
        float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
        source.volume = Mathf.MoveTowards(source.volume, target, step);
        if (source.volume > 0f && !source.isPlaying)
        {
            if (source.time > 0f) source.UnPause();
            else source.Play();
        }
        else if (source.volume <= 0f && source.isPlaying) source.Pause();
    }

    private AudioSource CreateSource(AudioClip clip, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // player sounds are heard the same wherever the camera is
        source.volume = loop ? 0f : 1f;
        return source;
    }
}
