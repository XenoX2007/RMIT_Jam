using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Melee: swim up to the player and bite when close enough
public class AttackState : BaseState
{
    private float losePlayerTimer;
    private float attackTimer;
    private float repathTimer;
    private float defaultStoppingDistance;
    private bool biting;

    public override void Enter()
    {
        defaultStoppingDistance = enemy.Agent.stoppingDistance;
        enemy.Agent.speed = enemy.chaseSpeed;
        enemy.Agent.stoppingDistance = enemy.attackRange * 0.7f;
        enemy.Agent.isStopped = false;
        attackTimer = enemy.attackCooldown; // ready to bite as soon as it reaches the player
    }

    public override void Exit()
    {
        enemy.Agent.speed = enemy.patrolSpeed;
        enemy.Agent.stoppingDistance = defaultStoppingDistance;
        enemy.Agent.isStopped = false;
    }

    public override void Perform()
    {
        if (enemy.IsStunned || enemy.IsDead)
            return; // frozen while hurt/death animation plays

        // Player hid: lose them right away and search where they were last seen
        if (enemy.PlayerHidden)
        {
            stateMachine.ChangeState(new SearchState());
            return;
        }

        attackTimer += Time.deltaTime;
        float distance = enemy.DistanceToPlayer();

        // Keeps hunting until the player has been out of loseRadius for losePlayerTime seconds
        if (distance < enemy.loseRadius)
        {
            losePlayerTimer = 0f;
            enemy.LastKnowPos = enemy.Player.transform.position;
        }
        else
        {
            losePlayerTimer += Time.deltaTime;
            if (losePlayerTimer >= enemy.losePlayerTime)
            {
                stateMachine.ChangeState(new SearchState());
                return;
            }
        }

        if (biting)
        {
            enemy.FaceTarget(enemy.Player.transform.position);
            return;
        }

        if (distance <= enemy.attackRange)
        {
            enemy.Agent.isStopped = true;
            enemy.FaceTarget(enemy.Player.transform.position);
            if (attackTimer >= enemy.attackCooldown && FacingPlayer())
                enemy.StartCoroutine(Bite());
        }
        else
        {
            enemy.Agent.isStopped = false;
            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                enemy.Agent.SetDestination(enemy.ClosestReachablePoint(enemy.LastKnowPos));
                repathTimer = 0.2f;
            }
            // Can't get any closer (player is on land): wait at the edge and watch them
            if (enemy.ReachedDestination())
                enemy.FaceTarget(enemy.Player.transform.position);
        }
    }

    private bool FacingPlayer()
    {
        Vector3 direction = enemy.Player.transform.position - enemy.transform.position;
        direction.y = 0f;
        return Vector3.Angle(enemy.transform.forward, direction) < 30f;
    }

    private IEnumerator Bite()
    {
        biting = true;
        attackTimer = 0f;
        enemy.SetAnimTrigger("Attack");

        yield return new WaitForSeconds(enemy.biteHitDelay);

        // cancel if interrupted mid-bite, or the player already got out of reach
        if (!enemy.IsStunned && !enemy.IsDead && !enemy.PlayerHidden && enemy.DistanceToPlayer() <= enemy.attackRange * 1.2f)
            HitPlayer();

        yield return new WaitForSeconds(0.3f);
        biting = false;
    }

    private void HitPlayer()
    {
        GameObject player = enemy.Player;
        if (player.TryGetComponent(out PlayerHealth health))
        {
            // PlayerHealth knows who bit, so a killing bite can start the last-stand QTE with this crocodile
            health.TakeDamage(enemy.biteDamage, enemy);
            if (health.IsDead) return;   // no knockback push once the player is down
        }
        else
        {
            // Any other script with a public TakeDamage(int) method still receives the bite
            player.SendMessage("TakeDamage", enemy.biteDamage, SendMessageOptions.DontRequireReceiver);
        }

        if (player.TryGetComponent(out Rigidbody rb))
        {
            Vector3 push = player.transform.position - enemy.transform.position;
            push.y = 0f;
            rb.AddForce(push.normalized * enemy.knockbackForce + Vector3.up * enemy.knockbackForce * 0.4f, ForceMode.VelocityChange);
        }
    }
}
