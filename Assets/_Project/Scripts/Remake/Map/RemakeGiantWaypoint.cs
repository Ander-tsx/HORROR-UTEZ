using UnityEngine;

namespace HorrorUtez.Remake
{
    // One stop of El Rector's loop around the campus. The loop follows the hierarchy order of the waypoints under
    // their parent: reorder them there. Unreachable waypoints are dropped at runtime and reported in the log.
    public sealed class RemakeGiantWaypoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, .25f);
            Transform parent = transform.parent;
            if (parent == null) return;
            int next = (transform.GetSiblingIndex() + 1) % parent.childCount;
            Gizmos.DrawLine(transform.position, parent.GetChild(next).position);
        }
    }
}
