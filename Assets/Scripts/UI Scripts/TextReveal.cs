using System.Collections;
using TMPro;
using UnityEngine;

public class TextReveal : MonoBehaviour
{
    [SerializeField] float duration = 1.5f;
    [SerializeField] float riseDistance = 4f;

    Vector3 targetPosition;

    void Start()
    {
        targetPosition = transform.position;
        transform.position = targetPosition + Vector3.down * riseDistance;
        StartCoroutine(Rise());
    }

    IEnumerator Rise()
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            t = t * t * (3f - 2f * t); // chuyển động mượt

            transform.position = Vector3.Lerp(
                targetPosition + Vector3.down * riseDistance,
                targetPosition,
                t
            );

            yield return null;
        }

        transform.position = targetPosition;
    }
}