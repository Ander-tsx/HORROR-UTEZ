using UnityEngine;

namespace HorrorUtez.Remake
{
    // Shared drawing for the map markers so level designers see them in the Scene view (never in game).
    internal static class RemakeMarkerGizmos
    {
        public static void Label(Vector3 position, string text)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
#endif
        }
    }
}
