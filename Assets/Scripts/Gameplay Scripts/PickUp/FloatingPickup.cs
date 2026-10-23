using UnityEngine;

// Collect items hover in the air: bob up and down and slowly spin.
public class FloatingPickup : MonoBehaviour
{
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float spinSpeed = 45f;   // degrees per second

    private Vector3 basePosition;
    private float seed;

    private void Start()
    {
        basePosition = transform.position;
        seed = Random.Range(0f, 10f);
    }

    private void Update()
    {
        transform.position = basePosition + Vector3.up * (Mathf.Sin((Time.time + seed) * bobSpeed) * bobHeight);
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }
}
