using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HorrorUtez.Remake
{
    // Reads the gameplay markers placed in the scene (Prefabs/Map/Gameplay_Marcadores and the zone prefabs).
    // The map itself is authored in the Unity editor; nothing here creates geometry.
    public sealed class RemakeMapMarkers
    {
        public readonly Dictionary<string, Vector3> Spots = new Dictionary<string, Vector3>();
        public readonly Dictionary<string, Vector3> RoomCentres = new Dictionary<string, Vector3>();
        public readonly List<Vector3> Patrol = new List<Vector3>();
        public readonly List<Vector3> OutdoorPatrol = new List<Vector3>();
        public readonly List<Vector3> GiantRoute = new List<Vector3>();
        public readonly List<Bounds> ExtraDoorways = new List<Bounds>();
        public Vector3 EastDoor { get; private set; }
        public Vector3 Corridor { get; private set; }
        public bool Ready => GiantRoute.Count > 0;

        public void Load()
        {
            foreach (RemakeLootSpot spot in Find<RemakeLootSpot>())
            {
                if (Spots.ContainsKey(spot.Key)) Debug.LogWarning("[Remake] Duplicate loot spot key " + spot.Key, spot);
                Spots[spot.Key] = spot.transform.position;
            }
            foreach (RemakeRoom room in Find<RemakeRoom>()) RoomCentres[room.RoomName] = room.transform.position;
            foreach (RemakePatrolPoint point in Find<RemakePatrolPoint>()) (point.Outdoor ? OutdoorPatrol : Patrol).Add(point.transform.position);
            foreach (RemakeGiantWaypoint waypoint in Find<RemakeGiantWaypoint>()) GiantRoute.Add(waypoint.transform.position);
            foreach (RemakeDoorway doorway in Find<RemakeDoorway>()) ExtraDoorways.Add(doorway.Bounds);
            RemakeSlidingDoor door = Find<RemakeSlidingDoor>().FirstOrDefault();
            if (door != null) EastDoor = door.transform.position;
            RemakeAnchor corridor = Find<RemakeAnchor>().FirstOrDefault(a => a.Key == "corridor");
            if (corridor != null) Corridor = corridor.transform.position;
            Debug.Log("[Remake] Map markers: " + Spots.Count + " loot spots, " + RoomCentres.Count + " rooms, " + Patrol.Count + "+" + OutdoorPatrol.Count
                + " patrol points, " + GiantRoute.Count + " giant waypoints, " + ExtraDoorways.Count + " doorways");
        }

        // Active markers in hierarchy order, so designers control sequences by reordering them in the Hierarchy.
        private static IEnumerable<T> Find<T>() where T : Component =>
            Object.FindObjectsByType<T>(FindObjectsSortMode.None).OrderBy(c => HierarchyKey(c.transform), System.StringComparer.Ordinal);

        private static string HierarchyKey(Transform t)
        {
            var parts = new List<string>();
            string scene = t.gameObject.scene.name;
            for (; t != null; t = t.parent) parts.Add(t.GetSiblingIndex().ToString("D5"));
            parts.Add(scene);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
