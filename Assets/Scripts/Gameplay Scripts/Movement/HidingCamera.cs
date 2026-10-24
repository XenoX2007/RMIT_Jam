using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// Horror-style hiding: while the player is in a jar / barrel the view switches to first person,
// peeking out over the jar's rim. Move the mouse to look around (no RMB needed).
// A gentle breathing sway sells the "inside" feel.
// Put this next to CameraMovement on the third-person CinemachineCamera.
public class HidingCamera : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private float fieldOfView = 68f;
    [Tooltip("How far below the top of the jar the eyes are (metres). Negative = peeking just over the rim.")]
    [SerializeField] private float eyeBelowRim = -0.06f;
    [SerializeField] private float minPitch = -45f;
    [SerializeField] private float maxPitch = 60f;
    [Tooltip("Seconds to blend between the third-person and the first-person view.")]
    [SerializeField] private float blendTime = 0.6f;

    [Header("Breathing")]
    [SerializeField] private float breathHeight = 0.025f;
    [SerializeField] private float breathSpeed = 1.4f;
    [SerializeField] private float breathRoll = 0.6f;

    private CameraMovement cameraMovement;
    private CinemachineOrbitalFollow orbit;
    private CinemachineCamera hideCam;
    private BarrelInteractable barrel;
    private Transform eye;            // follows the jar, so the view bobs with the water
    private float yaw;
    private float pitch;
    private bool skipDelta;
    private bool wasPaused;

    private void Awake()
    {
        cameraMovement = GetComponent<CameraMovement>();
        orbit = GetComponent<CinemachineOrbitalFollow>();

        var go = new GameObject("Hiding Camera (First Person)");
        hideCam = go.AddComponent<CinemachineCamera>();
        hideCam.Priority = 100;
        hideCam.Lens.FieldOfView = fieldOfView;
        hideCam.Lens.NearClipPlane = 0.05f;
        hideCam.enabled = false;
    }

    private void Start()
    {
        // Short, smooth blend into and out of the jar
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendTime);
    }

    private void Update()
    {
        BarrelInteractable current = InteractionController.CurrentBarrel;
        if (current != barrel)
        {
            if (barrel != null) Exit();
            if (current != null) Enter(current);
        }
        if (barrel == null) return;

        // Pause menu: give the cursor back, take it again on resume
        if (PauseMenu.IsPaused)
        {
            wasPaused = true;
            return;
        }
        if (wasPaused)
        {
            wasPaused = false;
            LockCursor();
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (skipDelta) { delta = Vector2.zero; skipDelta = false; }
            float sensitivity = cameraMovement != null ? cameraMovement.Sensitivity : 0.3f;
            yaw = Mathf.Repeat(yaw + delta.x * sensitivity + 180f, 360f) - 180f;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, minPitch, maxPitch);
        }

        // Breathing: slow rise and fall with a little roll
        float t = Time.time * breathSpeed;
        Vector3 breath = Vector3.up * Mathf.Sin(t) * breathHeight;
        hideCam.transform.SetPositionAndRotation(
            eye.position + breath,
            Quaternion.Euler(pitch, yaw, Mathf.Sin(t * 0.5f) * breathRoll));
    }

    private void Enter(BarrelInteractable jar)
    {
        barrel = jar;

        // Eyes at the rim, in the middle of the opening
        Bounds bounds = new Bounds(jar.transform.position, Vector3.zero);
        foreach (Renderer r in jar.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(r.bounds);
        eye = new GameObject("Jar Eye").transform;
        eye.SetParent(jar.transform, false);
        eye.position = new Vector3(bounds.center.x, bounds.max.y - eyeBelowRim, bounds.center.z);

        // Keep looking the way the camera was facing
        Transform cam = Camera.main != null ? Camera.main.transform : transform;
        yaw = cam.eulerAngles.y;
        pitch = 0f;
        hideCam.transform.SetPositionAndRotation(eye.position, Quaternion.Euler(pitch, yaw, 0f));
        hideCam.enabled = true;

        LockCursor();
    }

    private void Exit()
    {
        // Third-person camera comes back behind the player, facing where we were looking
        if (orbit != null) orbit.HorizontalAxis.Value = yaw;

        hideCam.enabled = false;
        if (eye != null) Destroy(eye.gameObject);
        eye = null;
        barrel = null;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        skipDelta = true;
    }

    private void OnDisable()
    {
        if (barrel != null) Exit();
    }
}
