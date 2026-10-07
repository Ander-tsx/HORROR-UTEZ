using UnityEngine;

namespace HorrorUtez.Remake
{
    // Centre of a room: candidate spot for later-day loot, day lights and enemy day spawns.
    // Keep it on walkable floor in the middle of the room.
    public sealed class RemakeRoom : MonoBehaviour
    {
        public string RoomName;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(.2f, 1f, .4f);
            Gizmos.DrawWireSphere(transform.position, .6f);
            RemakeMarkerGizmos.Label(transform.position + Vector3.up * .8f, "Cuarto · " + RoomName);
        }
    }
}
