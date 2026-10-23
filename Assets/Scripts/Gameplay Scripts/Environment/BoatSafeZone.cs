using UnityEngine;

// Đặt lên thuyền: khi người chơi đứng trên thuyền thì cá sấu không phát hiện, đuổi hay cắn được.
public class BoatSafeZone : MonoBehaviour
{
    static int checkedFrame = -1;
    static bool aboard;

    // Is the player standing on any boat right now (checked once per frame)
    public static bool IsPlayerAboard(Transform player)
    {
        if (player == null) return false;
        if (checkedFrame == Time.frameCount) return aboard;
        checkedFrame = Time.frameCount;
        aboard = false;

        // Short cast down from the body centre to just under the feet
        foreach (RaycastHit hit in Physics.SphereCastAll(player.position, 0.25f, Vector3.down, 1.3f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(player)) continue;
            if (hit.collider.GetComponentInParent<BoatSafeZone>() != null) { aboard = true; break; }
        }
        return aboard;
    }
}
