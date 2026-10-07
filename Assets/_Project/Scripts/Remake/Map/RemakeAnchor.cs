using UnityEngine;

namespace HorrorUtez.Remake
{
    // Named reference point used by code and the automated tests ("corridor": CECADEC's main corridor).
    public sealed class RemakeAnchor : MonoBehaviour
    {
        public string Key;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position, new Vector3(.3f, 1.5f, .3f));
            RemakeMarkerGizmos.Label(transform.position + Vector3.up * 1.6f, "Ancla · " + Key);
        }
    }
}
