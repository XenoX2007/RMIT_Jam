using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Player HP. Each crocodile bite calls TakeDamage(biteDamage) (AttackState sends it to the player).
public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 3;
    [Tooltip("Seconds of safety after a hit, so one bite can't count twice.")]
    [SerializeField] private float invulnerableTime = 1f;

    [Header("Death")]
    [Tooltip("What happens when HP reaches 0. Leave empty to reload the scene after Reload Delay.")]
    [SerializeField] private UnityEvent onDied;
    [SerializeField] private float reloadDelay = 2.5f;

    public int MaxHealth => maxHealth;
    public int Health { get; private set; }
    public bool IsDead => Health <= 0;

    // (current health, max health)
    public event Action<int, int> HealthChanged;

    private float lastHitTime = -99f;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private void Awake()
    {
        Health = maxHealth;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    // Back to where the player started, full health, able to move
    public void Respawn()
    {
        Vector3 from = transform.position;
        if (TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = spawnPosition;
            rb.rotation = spawnRotation;
        }
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(transform, spawnPosition - from);

        if (TryGetComponent(out Movement movement)) movement.CanMove = true;
        StopAllCoroutines();   // cancel a pending death reload
        lastHitTime = -99f;
        Health = maxHealth;
        HealthChanged?.Invoke(Health, maxHealth);
    }

    private void Start()
    {
        HealthChanged?.Invoke(Health, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0) return;
        if (Time.time - lastHitTime < invulnerableTime) return;

        lastHitTime = Time.time;
        Health = Mathf.Max(0, Health - damage);
        HealthChanged?.Invoke(Health, maxHealth);

        if (Health == 0) Die();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        Health = Mathf.Min(maxHealth, Health + amount);
        HealthChanged?.Invoke(Health, maxHealth);
    }

    // Change the number of hearts at runtime (e.g. an upgrade)
    public void SetMaxHealth(int value, bool refill = true)
    {
        maxHealth = Mathf.Max(1, value);
        Health = refill ? maxHealth : Mathf.Min(Health, maxHealth);
        HealthChanged?.Invoke(Health, maxHealth);
    }

    private void Die()
    {
        if (TryGetComponent(out Movement movement)) movement.CanMove = false;

        if (onDied != null && onDied.GetPersistentEventCount() > 0)
            onDied.Invoke();
        else
            StartCoroutine(ReloadScene());
    }

    private IEnumerator ReloadScene()
    {
        yield return new WaitForSeconds(reloadDelay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
