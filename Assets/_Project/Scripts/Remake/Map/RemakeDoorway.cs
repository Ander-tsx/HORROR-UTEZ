using UnityEngine;

namespace HorrorUtez.Remake
{
    // An opening the enemy navigation grid must treat as walkable although no door leaf marks it
    // (a doorway cut through a wall, a slid panel). Box centred on the transform, in world axes.
    public sealed class RemakeDoorway : MonoBehaviour
    {
        public Vector3 Size = new Vector3(1.2f, 2.2f, .4f);
        public Bounds Bounds => new Bounds(transform.position, Size);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, .5f, 0f);
            Gizmos.DrawWireCube(transform.position, Size);
        }
    }
}
