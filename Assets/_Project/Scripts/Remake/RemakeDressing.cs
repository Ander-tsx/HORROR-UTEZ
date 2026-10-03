using System.Collections.Generic;
using UnityEngine;

namespace HorrorUtez.Remake
{
    // Dresses CECADEC's ground-floor rooms as abandoned computer labs (compuaulas) at runtime.
    // Positions are in the CECADEC_Interior's local space (x east, z north; measured from the
    // interior generator and the batch probe in RemakeDiagnostics). A fixed seed makes every
    // peer build the same rooms without network traffic. Only a few valuables live here;
    // most PCs are broken scenery.
    public sealed class RemakeDressing
    {
        private struct Room
        {
            public string Name; public float X0, X1, Z0, Z1; public int Corridor; public string Type;
            public Room(string name, float x0, float x1, float z0, float z1, int corridor, string type)
            { Name = name; X0 = x0; X1 = x1; Z0 = z0; Z1 = z1; Corridor = corridor; Type = type; }
            public Vector2 Centre => new Vector2((X0 + X1) / 2, (Z0 + Z1) / 2);
        }
        // corridor = side the door/corridor is on: +1 east (x1), -1 west (x0), +2 north (z1).
        private static readonly Room[] Rooms =
        {
            new Room("Aula 1", 2.45f, 9.45f, -8.45f, -0.55f, -1, "lab"),
            new Room("Aula 2", 2.45f, 9.45f, -13.9f, -8.75f, -1, "lab"),
            new Room("CC9", 2.45f, 9.45f, -22.2f, -14.2f, -1, "lab"),
            new Room("Aula NW1", -9.45f, -2.45f, -5.55f, -0.55f, 1, "lab"),
            new Room("Aula NW2", -9.45f, -2.45f, -10.8f, -5.9f, 1, "lab"),
            new Room("Lab de Procesos", 2.45f, 9.45f, -33.3f, -28.6f, -1, "process"),
            new Room("Laboratorio SE", 2.45f, 9.45f, -38.0f, -33.7f, -1, "lab"),
            new Room("Bodega SW", -9.45f, -2.45f, -38.0f, -28.6f, 1, "storage"),
            new Room("Sala del fondo", -9.45f, 9.45f, -44.4f, -38.35f, 2, "lab"),
        };
        public readonly Dictionary<string, Vector3> Spots = new Dictionary<string, Vector3>();
        public readonly List<Vector3> Patrol = new List<Vector3>();
        public bool Ready => interior != null;
        private readonly RemakeGame game;
        private readonly System.Random rnd = new System.Random(20261003);
        private readonly Dictionary<string, GameObject> pieces = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Material> screens = new Dictionary<string, Material>();
        private readonly List<(Light light, float baseIntensity, float seed)> lights = new List<(Light, float, float)>();
        private Transform interior, root;

        public RemakeDressing(RemakeGame owner) { game = owner; }

        public void Build()
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == "CECADEC_Interior" && t.gameObject.scene.IsValid()) { interior = t; break; }
            if (interior == null) { Debug.LogWarning("[Remake] CECADEC_Interior not found; rooms left empty."); return; }
            root = new GameObject("Compuaulas · ambientación").transform;
            // The authored ceilings are visual only. Invisible slabs at 2.7 m let the giant feel the ceiling (and fold
            // into a crawl) and stop jumps through it; the west half of the cross corridor stays open for the stairs.
            foreach ((Vector3 centre, Vector3 size) in new[] {
                (new Vector3(0, 2.85f, -11.38f), new Vector3(19.2f, .3f, 21.96f)),
                (new Vector3(3.7f, 2.85f, -25.36f), new Vector3(11.8f, .3f, 6f)),
                (new Vector3(0, 2.85f, -36.48f), new Vector3(19.2f, .3f, 16.24f)) })
            {
                var slab = new GameObject("Techo · colisión"); slab.transform.SetParent(interior, false);
                slab.transform.localPosition = centre; slab.AddComponent<BoxCollider>().size = size;
            }
            foreach (string s in new[] { "Screen_Dead", "Screen_Cracked", "Screen_BSOD", "Screen_Terminal", "Screen_Static" })
                screens[s] = game.ScreenMaterials != null ? System.Array.Find(game.ScreenMaterials, m => m != null && m.name == s) : null;
            foreach (Room room in Rooms)
            {
                if (room.Type == "lab") Lab(room);
                else if (room.Type == "process") Process(room);
                else Storage(room);
                Clutter(room);
                Lights(room);
                Patrol.Add(World(room.Centre.x, room.Centre.y));
            }
            Electrical();
            for (float z = -2; z > -37; z -= 7) Patrol.Add(World(0, z));
            // Valuables kept indoors. Names are looked up by RemakeGame.BuildLoot.
            Spots["cc9_teacher"] = World(5.6f, -21.1f, .82f);
            Spots["aula2_teacher"] = World(5.6f, -13.0f, .82f);
            Spots["aula1_floor"] = World(8.9f, -1.3f, .5f);
            Spots["process_bench"] = World(5.0f, -29.4f, 1.0f);
            Spots["process_bench2"] = World(7.2f, -29.4f, 1.0f);
            Spots["se_lab"] = World(4.6f, -37.1f, .85f);
            Spots["electrical"] = World(-3.4f, -12.2f, .4f);
            Spots["nw1_floor"] = World(-8.6f, -1.4f, .45f);
            Spots["storage_shelf"] = World(-8.9f, -29.4f, .55f);
            Spots["back_room"] = World(7.8f, -43.6f, .5f);
            if (Application.isMobilePlatform == false) StaticBatchingUtility.Combine(root.gameObject);
            Debug.Log("[Remake] Dressed " + Rooms.Length + " rooms, " + root.GetComponentsInChildren<Renderer>().Length + " renderers.");
        }

        public void Tick()
        {
            RemakeEnemy giant = null;
            foreach (RemakeEnemy e in game.Enemies) if (e.Kind == EnemyKind.Giant) giant = e;
            foreach ((Light light, float baseIntensity, float seed) in lights)
            {
                if (light == null) continue;
                float n = Mathf.PerlinNoise(Time.time * 7 + seed, seed);
                bool cut = Mathf.PerlinNoise(Time.time * .7f + seed * 3, 1) > .72f && n > .45f;
                // The Rector's presence browns out the fluorescent tubes around it.
                float near = giant != null ? Mathf.Clamp01(1 - Vector3.Distance(giant.transform.position, light.transform.position) / 12) : 0;
                if (near > 0 && Mathf.PerlinNoise(Time.time * 18 + seed, 7) < near) cut = true;
                if (near > .3f && Random.value < .004f) game.Audio?.Play("spark", light.transform.position, .6f, 14);
                light.intensity = cut ? baseIntensity * .03f : baseIntensity * (.75f + n * .35f) * (1 - near * .5f);
            }
        }

        // ------------------------------------------------------------------ helpers
        private float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        private bool Chance(float p) => rnd.NextDouble() < p;

        private Vector3 World(float x, float z, float y = 0) => interior.TransformPoint(new Vector3(x, y, z));

        private GameObject Piece(string name)
        {
            if (!pieces.TryGetValue(name, out GameObject prefab)) pieces[name] = prefab = Resources.Load<GameObject>("Lab/" + name);
            return prefab;
        }

        // Places a piece at interior-local (x, y, z); y < 0 means "on the floor below that point".
        private GameObject Put(string name, float x, float z, float yaw, float y = -1, Vector3 tilt = default, bool solid = true)
        {
            GameObject prefab = Piece(name);
            if (prefab == null) return null;
            Vector3 p = World(x, z, Mathf.Max(y, 0));
            if (y < 0 && Physics.Raycast(p + Vector3.up * .45f, Vector3.down, out RaycastHit hit, 2, game.WorldMask, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            GameObject go = Object.Instantiate(prefab, p, interior.rotation * Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(tilt), root);
            if (solid)
                foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    var box = filter.gameObject.AddComponent<BoxCollider>();
                    box.center = filter.sharedMesh.bounds.center; box.size = Vector3.Max(filter.sharedMesh.bounds.size, Vector3.one * .02f);
                }
            return go;
        }

        private void Screen(GameObject monitor)
        {
            if (monitor == null) return;
            double r = rnd.NextDouble();
            string pick = r < .42 ? "Screen_Dead" : r < .68 ? "Screen_Cracked" : r < .8 ? "Screen_Static" : r < .9 ? "Screen_BSOD" : "Screen_Terminal";
            if (!screens.TryGetValue(pick, out Material mat) || mat == null) return;
            foreach (Renderer renderer in monitor.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name.StartsWith("Screen_")) mats[i] = mat;
                renderer.sharedMaterials = mats;
            }
        }

        // A desk with two workstations, mostly wrecked. (x, z) = desk centre, yaw = facing of its users.
        private void Desk(float x, float z, float yaw)
        {
            float jitter = Chance(.15f) ? R(-14, 14) : R(-2, 2);
            float dyaw = yaw + jitter;
            Put("Lab_Desk", x, z, dyaw);
            Quaternion q = Quaternion.Euler(0, dyaw, 0);
            Vector3 L(float lx, float lz) { Vector3 v = q * new Vector3(lx, 0, lz); return new Vector3(x + v.x, 0, z + v.z); }
            foreach (float sx in new[] { -.42f, .42f })
            {
                Vector3 m = L(sx, .12f);
                double r = rnd.NextDouble();
                string monitor = Chance(.65f) ? "Lab_CRT" : "Lab_LCD";
                if (r < .1) { Vector3 f = L(sx + R(-.3f, .3f), R(-1.0f, -.6f)); Screen(Put(monitor, f.x, f.z, R(0, 360), -1, new Vector3(R(-95, -80), 0, 0))); }
                else if (r < .22) Screen(Put(monitor, m.x, m.z, dyaw + R(-30, 30), .75f, new Vector3(-82, 0, 0), false));
                else Screen(Put(monitor, m.x, m.z, dyaw + R(-12, 12), .75f, default, false));
                if (Chance(.8f)) { Vector3 k = L(sx + R(-.05f, .05f), -.17f); Put("Lab_Keyboard", k.x, k.z, dyaw + R(-10, 10), .75f, default, false); }
                if (Chance(.4f)) { Vector3 k = L(sx + .25f, -.15f); Put("Lab_Mouse", k.x, k.z, dyaw + R(-30, 30), .75f, default, false); }
                double tr = rnd.NextDouble();
                Vector3 t = L(sx * 1.55f, .1f);
                if (tr < .45) Put("Lab_Tower", t.x, t.z, dyaw + R(-8, 8));
                else if (tr < .72) Put("Lab_TowerOpen", t.x, t.z, dyaw + R(-15, 15));
                else { Vector3 f = L(sx * 1.2f, -.8f); Put(Chance(.5f) ? "Lab_Tower" : "Lab_TowerOpen", f.x, f.z, R(0, 360), -1, new Vector3(0, 0, 90)); }
                if (Chance(.78f))
                {
                    Vector3 c = L(sx + R(-.15f, .15f), R(-.75f, -.5f));
                    if (Chance(.25f)) Put("Lab_Chair", c.x, c.z, R(0, 360), -1, new Vector3(R(-95, -80), 0, 0));
                    else Put("Lab_Chair", c.x, c.z, dyaw + 180 + R(-45, 45));
                }
            }
        }

        private void Lab(Room room)
        {
            bool north = room.Corridor == 2;
            if (north)
            {
                // Back room: board on the south wall, users face south.
                Put("Lab_Whiteboard", 0, room.Z0 + .03f, 180, 1.0f);
                Put("Lab_TeacherDesk", -2.4f, room.Z0 + 1.1f, 0);
                for (float z = room.Z1 - 1.0f; z > room.Z0 + 2.3f; z -= 1.55f)
                    for (float x = room.X0 + 1.05f; x < room.X1 - .7f; x += 1.75f)
                        if (Mathf.Abs(x) > 1.6f) Desk(x, z, 180);
                Put("Lab_ProjectorMount", 0, room.Centre.y, 0, 2.69f, default, false);
                Put("Lab_Rack", room.X1 - .45f, room.Z0 + .5f, 90);
                return;
            }
            // Board on the room's north wall, users face north; keep a strip clear by the corridor door.
            bool east = room.Corridor == -1;
            float clearX = east ? room.X0 + 1.25f : room.X1 - 1.25f;
            Put("Lab_Whiteboard", room.Centre.x + (east ? .4f : -.4f), room.Z1 - .03f, 0, 1.0f);
            Put("Lab_TeacherDesk", room.Centre.x + (east ? 1.6f : -1.6f), room.Z1 - .95f, 180);
            Vector3 td = new Vector3(room.Centre.x + (east ? 1.6f : -1.6f), 0, room.Z1 - .95f);
            Screen(Put("Lab_CRT", td.x - .3f, td.z + .1f, 180 + R(-15, 15), .77f, default, false));
            Put("Lab_Chair", td.x, td.z + .7f, R(-40, 40));
            float[] cols = east ? new[] { 4.35f, 6.05f, 8.55f } : new[] { -8.55f, -6.05f, -4.35f };
            for (float z = room.Z0 + 1.2f; z < room.Z1 - 2.2f; z += 1.55f)
                foreach (float x in cols)
                    if (east ? x - .8f > clearX : x + .8f < clearX) Desk(x, z, 0);
            Put("Lab_ProjectorMount", room.Centre.x, room.Centre.y, 0, 2.69f, default, false);
            float outer = east ? room.X1 - .12f : room.X0 + .12f;
            Put("Lab_AC", outer, room.Z1 - 1.8f, east ? 90 : -90, 2.3f, default, false);
            Put("Lab_Poster", outer - (east ? .02f : -.02f), room.Centre.y, east ? 90 : -90, 1.55f, default, false);
            Put("Lab_Poster", room.Centre.x + R(-1.5f, 1.5f), room.Z0 + .03f, 180, 1.5f, default, false);
            if (Chance(.6f)) Put(Chance(.5f) ? "Lab_Shelf" : "Lab_Locker", outer - (east ? .3f : -.3f), room.Z0 + .6f, east ? 90 : -90);
        }

        private void Process(Room room)
        {
            Put("Lab_Whiteboard", room.Centre.x, room.Z1 - .03f, 0, 1.0f);
            for (int i = 0; i < 2; i++)
            {
                float z = room.Z1 - 1.15f - i * 1.9f;
                Put("Lab_Bench", 5.0f, z, 0); Put("Lab_Bench", 7.2f, z, 0);
                for (int s = 0; s < 3; s++)
                {
                    float x = 4.3f + s * 1.3f;
                    if (Chance(.3f)) Put("Lab_Stool", x, z - .7f, R(0, 360), -1, new Vector3(R(-95, -80), 0, 0));
                    else Put("Lab_Stool", x + R(-.2f, .2f), z - .75f, R(0, 360));
                }
            }
            Put("Lab_Shelf", room.X1 - .3f, room.Z0 + .6f, 90);
            Put("Lab_Locker", room.X1 - .3f, room.Z1 - 2.2f, 90);
            Put("Lab_ProjectorMount", room.Centre.x, room.Centre.y, 0, 2.69f, default, false);
        }

        private void Storage(Room room)
        {
            for (float z = room.Z0 + .5f; z < room.Z1 - .5f; z += 1.05f) Put("Lab_Shelf", room.X0 + .3f, z, -90);
            Put("Lab_Rack", room.X0 + 1.6f, room.Z1 - .6f, 0); Put("Lab_Rack", room.X0 + 2.3f, room.Z1 - .6f, 0);
            Put("Lab_Locker", room.Centre.x, room.Z0 + .35f, 180); Put("Lab_Locker", room.Centre.x + 1, room.Z0 + .35f, 180);
            for (int i = 0; i < 5; i++)
            {
                Vector3 f = new Vector3(R(room.X0 + 1.4f, room.X1 - 1.6f), 0, R(room.Z0 + 1.2f, room.Z1 - 1.5f));
                string p = Chance(.5f) ? "Lab_CRT" : "Lab_TowerOpen";
                Screen(Put(p, f.x, f.z, R(0, 360), -1, Chance(.5f) ? new Vector3(0, 0, 90) : default));
            }
        }

        private void Electrical()
        {
            Put("Lab_Rack", -3.6f, -14.6f, 90);
            Put("Lab_CableMess", -3.6f, -13.2f, R(0, 360), -1, default, false);
        }

        private void Clutter(Room room)
        {
            Vector2 c = room.Centre;
            float W(float a, float b) => R(a, b);
            for (int i = 0; i < 2 + rnd.Next(2); i++) Put("Lab_Papers", W(room.X0 + .8f, room.X1 - .8f), W(room.Z0 + .8f, room.Z1 - .8f), R(0, 360), -1, default, false);
            if (Chance(.55f)) Put("Lab_CeilingTile", W(room.X0 + 1, room.X1 - 1), W(room.Z0 + 1, room.Z1 - 1), R(0, 360), -1, default, false);
            if (Chance(.5f)) Put("Lab_CableMess", W(room.X0 + 1, room.X1 - 1), W(room.Z0 + 1, room.Z1 - 1), R(0, 360), -1, default, false);
            if (Chance(.35f)) Put("Lab_FluoroHanging", c.x + R(-1.5f, 1.5f), c.y + R(-1.5f, 1.5f), R(0, 360), 2.69f, default, false);
            if (Chance(.2f)) Put("Lab_Blood", W(room.X0 + 1, room.X1 - 1), W(room.Z0 + 1, room.Z1 - 1), R(0, 360), -1, default, false);
            Put("Lab_Trash", room.Corridor == -1 ? room.X0 + .5f : room.Corridor == 1 ? room.X1 - .5f : room.X0 + 1, room.Z0 + .45f, R(0, 360));
            if (Chance(.3f)) Put("Lab_Tape", room.Corridor == -1 ? room.X0 + .1f : room.X1 - .1f, c.y, 90, 0, default, false);
        }

        private void Lights(Room room)
        {
            if (Chance(.35f)) return;                    // some rooms stay pitch black
            var go = new GameObject("Fluorescente · " + room.Name);
            go.transform.SetParent(root, true);
            go.transform.position = World(room.Centre.x + R(-1, 1), room.Centre.y + R(-1, 1), 2.45f);
            Light light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = 7.5f;
            light.color = Chance(.2f) ? new Color(.75f, 1f, .85f) : new Color(.85f, .92f, 1f);
            float intensity = R(1.6f, 3.2f); light.intensity = intensity; light.shadows = LightShadows.None;
            lights.Add((light, intensity, R(0, 100)));
            game.Audio?.Loop("amb_fluoro", go.transform.position, .25f, 7);
        }
    }
}
