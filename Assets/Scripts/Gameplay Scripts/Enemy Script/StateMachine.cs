using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine : MonoBehaviour
{
    public BaseState activeState;
   

    public void Initialise()
    {
        
        ChangeState(new PatrolState());
    }

    // Start is called before the first frame update
    void Start()
    {
        //
    }

    // Update is called once per frame
    void Update()
    {
        if(activeState != null)
        {
            // frozen while stunned, so no state can restart the agent early
            if(activeState.enemy != null && activeState.enemy.IsStunned)
                return;

            activeState.Perform();
        }

    }
    public void ChangeState(BaseState newState)
    {
        //check active state != null
        if(activeState != null)
        {
            
            activeState.Exit();
        }
        activeState = newState;

        //fail-safe null check to make sure new state wasn't null
        if(activeState != null)
        {
            //Setup new state
            activeState.stateMachine = this;
            activeState.enemy = GetComponent<Enemy>();
            activeState.Enter();
        }
    }
}
