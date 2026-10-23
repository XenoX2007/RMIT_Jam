using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Alligator: swims along its path, hunts the player inside the detection zone and bites
public class Enemy : MonoBehaviour
{
    private StateMachine stateMachine;
    private NavMeshAgent agent;
    private GameObject player;
    private Vector3 lastKnowPos;
    private Coroutine hitStun;
    private PlayerHealth playerHealth;
    public NavMeshAgent Agent { get => agent;}
    public GameObject Player {get => player;}
    public Vector3 LastKnowPos { get => lastKnowPos; set => lastKnowPos = value; }
    [SerializeField] private string currentState;
    public EnemyPath path;
    public Animator Animator;

    [Header("Detection Zone")]
    public float detectionRadius = 15f; // player inside this -> start hunting
    public float loseRadius = 22f;      // player has to get this far away to escape
    public float losePlayerTime = 3f;   // seconds outside loseRadius before it gives up and searches

    [Header("Movement")]
    public float patrolSpeed = 2.5f;
    public float chaseSpeed = 6f;
    public float turnSpeed = 6f;
    public float patrolWaitTime = 3f;   // pause at each waypoint
    public float searchTime = 8f;       // how long it looks around before going back to the path

    [Header("Stats")]
    public int health;
    public float hitStunDuration = 0.5f; // match this to your hurt animation's length

    [Header("Hit By Thrown Item")]
    public float throwStunDuration = 4f;   // seconds frozen after being hit (knockback included)
    public float knockbackDistance = 3f;
    public float knockbackTime = 0.35f;

    [Header("Looks")]
    [Tooltip("Model renderers that switch to the angry material while chasing.")]
    public Renderer[] bodyRenderers;
    public Material calmMaterial;
    public Material angryMaterial;   // e.g. red eyes
    private bool angry;

    public bool IsStunned { get; private set; }
    public bool IsDead { get; private set; }

    [Header("Bite Attack")]
    public float attackRange = 3f;      // flat distance from the alligator's centre to the player
    public float attackCooldown = 1.5f;
    public float biteHitDelay = 0.5f;   // time from the start of the Attack animation to the jaws closing
    public int biteDamage = 1;
    public float knockbackForce = 6f;

    void Start()
    {
       // Animator.SetBool("Appair", true);
        stateMachine = GetComponent<StateMachine>();
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
        agent.speed = patrolSpeed;

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"{name}: not on a NavMesh. Bake a NavMesh over the water and put the alligator on it.", this);
            enabled = false;
            stateMachine.enabled = false;
            return;
        }
        stateMachine.Initialise();
    }

    void Update()
    {
        if (IsDead || stateMachine == null || stateMachine.activeState == null) return;

        currentState = stateMachine.activeState.ToString();

        // Red eyes while hunting the player
        bool hunting = stateMachine.activeState is AttackState;
        if (hunting != angry && angryMaterial != null && calmMaterial != null && bodyRenderers != null)
        {
            angry = hunting;
            foreach (Renderer r in bodyRenderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = angry ? angryMaterial : calmMaterial;
                r.sharedMaterials = mats;
            }
        }
        float speed = Agent.velocity.magnitude;
        SetAnimFloat("Speed", speed);
    }

    public virtual BaseState GetAttackState()
{
    return new AttackState();
}

    public void TakeDamage(int damage)
    {
        if (IsDead) return;

        Debug.Log(damage);
        health -= damage;

        if (health <= 0)
        {
            IsDead = true;
            SetAnimTrigger("IsDie");
            StartCoroutine(DeathAnimation());
        }
        else
        {
            if (hitStun != null) StopCoroutine(hitStun);
            hitStun = StartCoroutine(HitStun());
        }
    }

    private IEnumerator HitStun()
    {
        IsStunned = true;
        SetAnimTrigger("IsGettingDamage");
        Agent.isStopped = true;

        yield return new WaitForSeconds(hitStunDuration);

        Agent.isStopped = false;
        IsStunned = false;
        hitStun = null;
    }

    // A thrown item (coconut) hit it: pushed back, stunned, then straight back to chasing
    public void HitByThrow(Vector3 direction)
    {
        if (IsDead) return;

        if (hitStun != null) StopCoroutine(hitStun);
        hitStun = StartCoroutine(ThrowStun(direction));
    }

    private IEnumerator ThrowStun(Vector3 direction)
    {
        IsStunned = true;
        SetAnimTrigger("IsGettingDamage");
        Agent.isStopped = true;
        Agent.ResetPath();

        // Slide back along the NavMesh (stays in the water), fast at first then easing out
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = -transform.forward;
        direction.Normalize();
        float t = 0f;
        while (t < knockbackTime)
        {
            float speed = knockbackDistance / knockbackTime * 2f * (1f - t / knockbackTime);
            Agent.Move(direction * speed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, throwStunDuration - knockbackTime));

        Agent.isStopped = false;
        IsStunned = false;
        hitStun = null;
        if (player != null) LastKnowPos = player.transform.position;
        stateMachine.ChangeState(GetAttackState());
    }

    private IEnumerator DeathAnimation()
    {
        Agent.isStopped = true;
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }

    // Ignores height, so an alligator in the water can still reach a player on the bank
    public float DistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    // Hiding in a jar/barrel or standing on a boat makes the player unreachable
    public bool PlayerHidden => InteractionController.CurrentBarrel != null
        || (player != null && BoatSafeZone.IsPlayerAboard(player.transform))
        || (playerHealth != null && playerHealth.IsDead);

    public bool PlayerInDetectionZone()
    {
        return !PlayerHidden && DistanceToPlayer() < detectionRadius;
    }

    public void FaceTarget(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
    }

    // Closest spot the alligator can swim to (the player is usually on land, off the NavMesh)
    public Vector3 ClosestReachablePoint(Vector3 target)
    {
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, loseRadius, agent.areaMask))
            return hit.position;
        return transform.position;
    }

    public bool ReachedDestination()
    {
        if (agent.pathPending) return false;
        return !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.3f;
    }

    // The alligator still works before it has an Animator
    public void SetAnimFloat(string parameter, float value) { if (Animator != null) Animator.SetFloat(parameter, value); }
    public void SetAnimBool(string parameter, bool value) { if (Animator != null) Animator.SetBool(parameter, value); }
    public void SetAnimTrigger(string parameter) { if (Animator != null) Animator.SetTrigger(parameter); }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, loseRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
