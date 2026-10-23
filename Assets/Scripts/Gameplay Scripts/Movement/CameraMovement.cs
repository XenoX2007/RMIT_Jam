using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// Roblox-style camera: hold RMB to orbit (cursor hidden, 1:1 with the mouse), scroll to zoom
[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class CameraMovement : MonoBehaviour
{
    [SerializeField] private InputActionReference rightClick;

    [Header("Orbit (hold RMB)")]
    [SerializeField] private float sensitivity = 0.3f;   // degrees per pixel of mouse movement
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Zoom (mouse wheel)")]
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 15f;
    [SerializeField] private float zoomStep = 0.15f;     // fraction of the distance per wheel notch
    [SerializeField] private float zoomSmoothTime = 0.08f;

    [Header("Hiding (jar / barrel)")]
    [Tooltip("Camera distance while hiding, so the player can watch around the jar.")]
    [SerializeField] private float hiddenDistance = 9f;
    [SerializeField] private float hideZoomSmoothTime = 0.35f;

    private CinemachineOrbitalFollow orbit;
    private float targetDistance;
    private float zoomVelocity;
    private bool looking;
    private bool skipDelta;
    private Vector2 cursorRestore;

    private void Awake()
    {
        orbit = GetComponent<CinemachineOrbitalFollow>();

        // This script drives the orbit itself, so the default (smoothed) input is turned off
        var controller = GetComponent<CinemachineInputAxisController>();
        if (controller != null) controller.enabled = false;

        orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbit.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
        orbit.TrackerSettings.PositionDamping = Vector3.zero;
        orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
        orbit.HorizontalAxis.Wrap = true;
        orbit.VerticalAxis.Range = new Vector2(minPitch, maxPitch);
        orbit.VerticalAxis.Value = Mathf.Clamp(orbit.VerticalAxis.Value, minPitch, maxPitch);

        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        orbit.Radius = targetDistance;
    }

    private void OnEnable()
    {
        rightClick.action.Enable();
    }

    private void OnDisable()
    {
        if (looking) StopLooking();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        bool held = rightClick.action.IsPressed();
        if (held && !looking) StartLooking(mouse);
        else if (!held && looking) StopLooking();

        if (looking)
        {
            Vector2 delta = mouse.delta.ReadValue();
            // The frame the cursor locks it jumps to the centre, ignore that jump
            if (skipDelta) { delta = Vector2.zero; skipDelta = false; }

            float yaw = orbit.HorizontalAxis.Value + delta.x * sensitivity;
            orbit.HorizontalAxis.Value = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            orbit.VerticalAxis.Value = Mathf.Clamp(orbit.VerticalAxis.Value - delta.y * sensitivity, minPitch, maxPitch);
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
            targetDistance = Mathf.Clamp(targetDistance * (1f - Mathf.Sign(scroll) * zoomStep), minDistance, maxDistance);
        // Hiding in a jar: pull back for a wider view of the surroundings, then return to the player's zoom
        float desired = InteractionController.CurrentBarrel != null ? Mathf.Max(targetDistance, hiddenDistance) : targetDistance;
        float smooth = Mathf.Abs(desired - orbit.Radius) > 1f ? hideZoomSmoothTime : zoomSmoothTime;
        orbit.Radius = Mathf.SmoothDamp(orbit.Radius, desired, ref zoomVelocity, smooth);
    }

    private void StartLooking(Mouse mouse)
    {
        cursorRestore = mouse.position.ReadValue();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        looking = true;
        skipDelta = true;
    }

    private void StopLooking()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Put the cursor back where it was, like Roblox does
        if (Mouse.current != null) Mouse.current.WarpCursorPosition(cursorRestore);
        looking = false;
    }
}
