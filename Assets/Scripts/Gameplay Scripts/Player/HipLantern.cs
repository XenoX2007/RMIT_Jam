using UnityEngine;

// A lantern hanging at the player's hip: lights up around the player at night so they can still see.
// It follows the hip bone, keeps hanging upright and swings a little as the player moves.
// The light fades in when night comes and out in the day; it goes out while hiding in a jar.
public class HipLantern : MonoBehaviour
{
    [SerializeField] private Animator body;
    [SerializeField] private Transform lantern;
    [SerializeField] private Light lanternLight;
    [Tooltip("Where the lantern hangs, relative to the hips (x = side, y = up, z = forward).")]
    [SerializeField] private Vector3 hipOffset = new Vector3(-0.2f, -0.05f, 0.04f);

    [Header("Light")]
    [SerializeField] private float nightIntensity = 4f;
    [Tooltip("Light left on during the day (0 = off).")]
    [SerializeField] private float dayIntensity = 0f;
    [SerializeField] private float range = 14f;
    [SerializeField] private float fadeTime = 2f;
    [SerializeField] private float flicker = 0.08f;

    [Header("Swing")]
    [SerializeField] private float swingAmount = 12f;
    [SerializeField] private float swingSmoothing = 6f;

    private DayNightCycle dayNight;
    private Transform hips;
    private Vector3 lastPosition;
    private Vector3 swing;
    private Quaternion baseRotation = Quaternion.identity;
    private float level;

    private void Start()
    {
        dayNight = FindAnyObjectByType<DayNightCycle>();
        if (body == null) body = GetComponentInChildren<Animator>();
        if (body != null && body.isHuman) hips = body.GetBoneTransform(HumanBodyBones.Hips);
        if (lanternLight != null)
        {
            lanternLight.range = range;
            lanternLight.shadows = LightShadows.None;
        }
        if (lantern != null)
        {
            lastPosition = lantern.position;
            // the model's own upright pose (some models are rotated in their file)
            baseRotation = Quaternion.Inverse(Quaternion.Euler(0f, lantern.eulerAngles.y, 0f)) * lantern.rotation;
        }
        level = dayNight != null && dayNight.IsNight ? 1f : 0f;
    }

    private void LateUpdate()
    {
        if (lantern == null || hips == null) return;

        // Hang beside the hip, facing the way the player faces
        Quaternion facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 target = hips.position + facing * hipOffset;

        // Swing against the movement, like a lantern on a cord
        Vector3 velocity = (target - lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = target;
        Vector3 local = Quaternion.Inverse(facing) * velocity;
        Vector3 wanted = new Vector3(Mathf.Clamp(local.z, -4f, 4f), 0f, Mathf.Clamp(-local.x, -4f, 4f)) * (swingAmount / 4f);
        swing = Vector3.Lerp(swing, wanted, 1f - Mathf.Exp(-swingSmoothing * Time.deltaTime));
        lantern.SetPositionAndRotation(target, facing * Quaternion.Euler(swing) * baseRotation);

        // Night light
        bool night = dayNight != null && dayNight.IsNight;
        bool hidden = InteractionController.CurrentBarrel != null;
        float goal = hidden ? 0f : (night ? 1f : 0f);
        level = Mathf.MoveTowards(level, goal, Time.deltaTime / Mathf.Max(0.01f, fadeTime));
        if (lanternLight != null)
        {
            float glow = Mathf.Lerp(dayIntensity, nightIntensity, level);
            float wobble = 1f + flicker * (Mathf.PerlinNoise(Time.time * 3f, 0.3f) - 0.5f) * 2f;
            lanternLight.intensity = glow * wobble;
            lanternLight.enabled = lanternLight.intensity > 0.01f;
        }
    }
}
