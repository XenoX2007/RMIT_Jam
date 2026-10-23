using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchState : BaseState
{
    private float searchTimer;
    private float moveTimer;
    private float nextMoveTime;
    public override void Enter()
    {
        enemy.Agent.SetDestination(enemy.ClosestReachablePoint(enemy.LastKnowPos));
        enemy.SetAnimBool("IsLookAround", false);
        nextMoveTime = Random.Range(3f, 5f);
    }
    public override void Perform()
    {
        if(enemy.PlayerInDetectionZone())
        {
            stateMachine.ChangeState(enemy.GetAttackState());//
            return;
        }
        if(!enemy.ReachedDestination())
            return;

        // At the last known position: look around, drift a bit, then go back to the path
        enemy.SetAnimBool("IsLookAround", true);
        searchTimer += Time.deltaTime;
        moveTimer += Time.deltaTime;
        if(moveTimer > nextMoveTime)
        {
            Vector3 point = enemy.transform.position + Random.insideUnitSphere * 10;
            point.y = enemy.transform.position.y;

            enemy.Agent.SetDestination(enemy.ClosestReachablePoint(point));
            enemy.SetAnimBool("IsLookAround", false);
            moveTimer = 0f;
            nextMoveTime = Random.Range(3f, 5f);
        }
        if(searchTimer > enemy.searchTime)
        {
            stateMachine.ChangeState(new PatrolState());
        }
    }
    public override void Exit()
    {
         enemy.SetAnimBool("IsLookAround", false);
    }

}
