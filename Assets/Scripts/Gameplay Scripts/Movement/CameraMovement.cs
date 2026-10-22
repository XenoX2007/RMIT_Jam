using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    [SerializeField] private InputActionReference rightClick;

    private CinemachineInputAxisController controller;

    private void Awake()
    {
        controller = GetComponent<CinemachineInputAxisController>();
    }

    private void Update()
    {
        bool canRotate = rightClick.action.IsPressed();

        foreach (var c in controller.Controllers)
        {
            c.Enabled = canRotate;
        }
    }
}