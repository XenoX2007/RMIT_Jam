using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    [SerializeField] private InputActionReference rightClick;

    [Header("Smoothing")]
    [SerializeField] private float accelTime = 0.08f;
    [SerializeField] private float decelTime = 0.18f;

    private CinemachineInputAxisController controller;
    private float[] baseGains;

    private void Awake()
    {
        controller = GetComponent<CinemachineInputAxisController>();
    }

    private void Start()
    {
        // Controllers are created by Cinemachine, so read them here
        var controllers = controller.Controllers;
        baseGains = new float[controllers.Count];

        for (int i = 0; i < controllers.Count; i++)
        {
            baseGains[i] = controllers[i].Input.Gain;
            controllers[i].Driver.AccelTime = accelTime;
            controllers[i].Driver.DecelTime = decelTime;
        }
    }

    private void Update()
    {
        bool canRotate = rightClick.action.IsPressed();
        var controllers = controller.Controllers;

        // Keep the controllers on and zero the gain instead,
        // so the camera eases to a stop when RMB is released
        for (int i = 0; i < controllers.Count && i < baseGains.Length; i++)
        {
            controllers[i].Enabled = true;
            controllers[i].Input.Gain = canRotate ? baseGains[i] : 0f;
        }
    }
}
