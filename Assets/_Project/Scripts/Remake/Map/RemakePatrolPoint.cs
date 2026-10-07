using UnityEngine;

namespace HorrorUtez.Remake
{
    // Patrol target for the caretakers and El Rector. Indoor points must be on the ground floor.
    // Outdoor points: the first four in hierarchy order are the plaza, used by enemies that stay outside.
    public sealed class RemakePatrolPoint : MonoBehaviour
    {
        public bool Outdoor;

        private void OnDrawGizmos()
        {
            Gizmos.color = Outdoor ? new Color(.2f, .8f, 1f) : new Color(.6f, .4f, 1f);
            Gizmos.DrawWireSphere(transform.position, .35f);
        }
    }
}
