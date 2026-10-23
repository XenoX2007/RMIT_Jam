using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PatrolState : BaseState
{
    public int waypointIndex;
    public float waitTimer;
    public override void Enter()
    {
        if (!HasPath()) return;

        // carry on from the closest waypoint (it may be coming back from a chase)
        waypointIndex = enemy.path.ClosestWaypoint(enemy.transform.position);
        enemy.Agent.SetDestination(enemy.path.waypoints[waypointIndex].position);
    }
    public override void Perform()
    {
        if(enemy.PlayerInDetectionZone())
        {
           stateMachine.ChangeState(enemy.GetAttackState());
           return;
        }
        PatrolCycle();
    }
    public override void Exit()
    {
        //
    }

    public void PatrolCycle()//
    {
        if (!HasPath()) return;

        if(enemy.ReachedDestination())
        {
            waitTimer += Time.deltaTime;
            if(waitTimer > enemy.patrolWaitTime)
            {
                waitTimer = 0f;
                if(waypointIndex < enemy.path.waypoints.Count - 1)
            {
                waypointIndex++;
            }
            else
            {
                waypointIndex = 0;

            }
             enemy.Agent.SetDestination(enemy.path.waypoints[waypointIndex].position);
            }

        }
    }

    private bool HasPath()
    {
        return enemy.path != null && enemy.path.waypoints != null && enemy.path.waypoints.Count > 0;
    }
}
