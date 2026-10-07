using UnityEngine;

namespace HorrorUtez.Remake
{
    // Where a valuable spawns on the first day. Move it in the Scene view; the item is dropped onto the floor below.
    // RemakeGame.BuildLoot looks spots up by key, so keep the key when moving a spot.
    public sealed class RemakeLootSpot : MonoBehaviour
    {
        public string Key;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, .8f, .1f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * .4f);
            RemakeMarkerGizmos.Label(transform.position + Vector3.up * .4f, "Botín · " + Key);
        }
    }
}
