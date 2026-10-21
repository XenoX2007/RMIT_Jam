using UnityEngine;

public class ShowTextWhenLookingUp : MonoBehaviour
{
    [SerializeField] Camera playerCamera;
    [SerializeField] float showAtAngle = 19.44f;

    void Update()
    {
        if (playerCamera == null) return;

        // Góc nhìn lên dương, nhìn xuống âm
        float pitch = playerCamera.transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        gameObject.SetActive(pitch >= showAtAngle);
    }
}