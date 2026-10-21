using System.Collections;
using UnityEngine;

public class ShowTextAtAngle : MonoBehaviour
{
    [SerializeField] GameObject textObject;
    [SerializeField] float delaySeconds = 2f;

    IEnumerator Start()
    {
        if (textObject == null)
        {
            Debug.LogError("Hãy gán Text Object trong Inspector.");
            yield break;
        }

        textObject.SetActive(false);
        yield return new WaitForSeconds(delaySeconds);
        textObject.SetActive(true);
    }
}