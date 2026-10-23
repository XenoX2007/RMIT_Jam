using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Named EnemyPath (not Path) so it doesn't hide System.IO.Path in other scripts
public class EnemyPath : MonoBehaviour
{

    public List<Transform> waypoints;
    [SerializeField]
    private bool alwaysDrawPath;
    [SerializeField]
    private bool drawAsLoop;
    [SerializeField]
    private bool drawNumbers;
    public Color debugColour = Color.white;

    public int ClosestWaypoint(Vector3 position)
    {
        int closest = 0;
        float best = Mathf.Infinity;
        for (int i = 0; i < waypoints.Count; i++)
        {
            float d = (waypoints[i].position - position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                closest = i;
            }
        }
        return closest;
    }

#if UNITY_EDITOR
    public void OnDrawGizmos()
    {
        if (alwaysDrawPath)
        {
            DrawPath();
        }
    }
    public void DrawPath()
    {
        if (waypoints == null) return;

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = 30;
        labelStyle.normal.textColor = debugColour;
        Gizmos.color = debugColour;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            if (drawNumbers)
                Handles.Label(waypoints[i].position, i.ToString(), labelStyle);
            //Draw Lines Between Points.
            if (i >= 1 && waypoints[i - 1] != null)
                Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
        }
        if (drawAsLoop && waypoints.Count > 2 && waypoints[0] != null && waypoints[waypoints.Count - 1] != null)
            Gizmos.DrawLine(waypoints[waypoints.Count - 1].position, waypoints[0].position);
    }
    public void OnDrawGizmosSelected()
    {
        if (alwaysDrawPath)
            return;
        else
            DrawPath();
    }
#endif
}
