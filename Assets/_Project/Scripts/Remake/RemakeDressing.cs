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
        public readonly Dictionary<string, Vector3> RoomCentres = new Dictionary<string, Vector3>();
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
                Patrol.Add(World(room.Centre.x, room.Centre.y)); RoomCentres[room.Name] = World(room.Centre.x, room.Centre.y);
            }
            Electrical();
            CutEastEntrance();
            OpenExtraDoors();
            for (float z = -2; z > -37; z -= 7) Patrol.Add(World(0, z));
            // Valuables kept indoors. Names are looked up by RemakeGame.BuildLoot.
            Spots["cc9_teacher"] = World(5.6f, -21.1f, .82f);
            Spots["aula2_teacher"] = World(5.6f, -13.0f, .82f);
            Spots["aula1_floor"] = World(8.9f, -1.3f, .5f);
            Spots["process_bench"] = World(4.6f, -32.75f, 1.0f);
            Spots["process_bench2"] = World(6.8f, -32.75f, 1.0f);
            Spots["se_lab"] = World(4.6f, -37.1f, .85f);
            // Keep heavy equipment in open aisles, within reach of the walkable room centre.
            Spots["electrical"] = RoomCentres["Bodega SW"] + Vector3.up * .4f;
            Spots["nw1_floor"] = RoomCentres["Aula NW1"] + Vector3.up * .45f;
            Spots["storage_shelf"] = World(-8.9f, -29.4f, .55f);
            Spots["back_room"] = World(7.8f, -43.6f, .5f);
            if (Application.isMobilePlatform == false) StaticBatchingUtility.Combine(root.gameObject);
            Debug.Log("[Remake] Dressed " + Rooms.Length + " rooms, " + root.GetComponentsInChildren<Renderer>().Length + " renderers.");
        }

        public void Tick()
        {
            TickDoor();
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
            foreach (Transform child in go.GetComponentsInChildren<Transform>()) child.gameObject.layer = 11;
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
            // The lab doubles as the east entrance: benches along the south wall keep a clear lane between the
            // big glass door (outer wall) and the corridor door (north-west corner).
            Put("Lab_Whiteboard", room.Centre.x - .6f, room.Z1 - .03f, 0, 1.0f);
            float z = room.Z0 + .55f;
            Put("Lab_Bench", 4.6f, z, 180); Put("Lab_Bench", 6.8f, z, 180);
            for (int s = 0; s < 3; s++)
            {
                float x = 4.0f + s * 1.35f;
                if (Chance(.3f)) Put("Lab_Stool", x, z + .8f, R(0, 360), -1, new Vector3(R(-95, -80), 0, 0));
                else Put("Lab_Stool", x + R(-.2f, .2f), z + .75f, R(0, 360));
            }
            Put("Lab_Shelf", room.X0 + .3f, room.Z0 + .6f, -90);
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

        // ------------------------------------------------------------------ east glass entrance
        // The paving outside the east facade (an entrance landmark laid out for the corridor's pre-v3 position) used
        // to end at solid wall. A big automatic glass door now opens there into the Lab de Procesos, which leads to
        // the corridor. Facade walls are replaced by trimmed pieces and the plaster lining is rebuilt with the
        // opening, because the landmark meshes cannot be edited at runtime.
        private const float DoorZ0 = -29.1f, DoorZ1 = -33.2f, DoorTop = 2.65f, DoorX = 9.8f;
        private Transform leafA, leafB;
        private float doorOpen;
        private bool doorWasOpen;
        public Vector3 EastDoor => World(DoorX, (DoorZ0 + DoorZ1) / 2);
        public Vector3 Corridor => World(0, -25.4f);

        // Openings the authored building lacks for play: one sliding panel of each tinted NW front slid aside, and a
        // doorway through the corridor's dead-end wall into the back room. Their footprints are carved into the grid.
        public readonly List<Bounds> ExtraDoorways = new List<Bounds>();
        private void OpenExtraDoors()
        {
            Vector3 north = interior.TransformDirection(Vector3.forward);
            foreach (string slide in new[] { "RoomNW1_Slide2", "RoomNW2_Slide2" })
            {
                Transform panel = null;
                foreach (Transform t in interior.GetComponentsInChildren<Transform>(true)) if (t.name == slide) { panel = t; break; }
                if (panel == null || !panel.TryGetComponent(out Renderer r)) { Debug.LogWarning("[Remake] Missing sliding panel " + slide); continue; }
                ExtraDoorways.Add(r.bounds);
                panel.position += north * 1.16f;
            }
            Transform end = null, skirting = null;
            foreach (Transform t in interior.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Wall_CorridorEnd") end = t;
                if (t.name == "Wall_CorridorEnd_Skirting") skirting = t;
            }
            if (end != null && end.TryGetComponent(out Renderer wall))
            {
                Material mat = wall.sharedMaterial;
                end.gameObject.SetActive(false);
                if (skirting != null) skirting.gameObject.SetActive(false);
                Vector2 z = new Vector2(-38.16f, -38.04f);
                Slab("Muro fondo", new Vector2(-2.2f, -.65f), z, new Vector2(0, 2.7f), mat, .5f);
                Slab("Muro fondo", new Vector2(.65f, 2.2f), z, new Vector2(0, 2.7f), mat, .5f);
                Slab("Muro fondo", new Vector2(-.65f, .65f), z, new Vector2(2.2f, 2.7f), mat, .5f);
                Vector3 a = World(-.6f, -38.1f, 1), b = World(.6f, -38.1f, 1);
                var gap = new Bounds((a + b) / 2, Vector3.zero); gap.Encapsulate(a); gap.Encapsulate(b); gap.Expand(new Vector3(.05f, 2, .05f));
                ExtraDoorways.Add(gap);
            }
        }

        private void CutEastEntrance()
        {
            Transform structure = interior.parent;
            foreach (string wallName in new[] { "Wall_East_F0_3", "Wall_East_F0_2" })
            {
                Transform wall = structure != null ? structure.Find(wallName) : null;
                var box = wall != null ? wall.GetComponent<BoxCollider>() : null;
                var render = wall != null ? wall.GetComponent<Renderer>() : null;
                if (box == null || render == null) { Debug.LogWarning("[Remake] East facade wall missing: " + wallName); continue; }
                Bounds local = LocalBounds(box);
                Material mat = render.sharedMaterial;
                wall.gameObject.SetActive(false);
                float za = local.max.z, zb = local.min.z, y0 = local.min.y, y1 = local.max.y;
                Vector2 x = new Vector2(local.min.x, local.max.x);
                if (za > DoorZ0) Slab("Muro este", x, new Vector2(Mathf.Max(zb, DoorZ0), za), new Vector2(y0, y1), mat, 2);
                if (zb < DoorZ1) Slab("Muro este", x, new Vector2(zb, Mathf.Min(za, DoorZ1)), new Vector2(y0, y1), mat, 2);
                float ha = Mathf.Min(za, DoorZ0), hb = Mathf.Max(zb, DoorZ1);
                if (ha > hb) Slab("Dintel", x, new Vector2(hb, ha), new Vector2(DoorTop + .15f, y1), mat, 2);
            }
            Transform lining = null;
            foreach (Transform t in interior.GetComponentsInChildren<Transform>(true)) if (t.name == "Lining_Rooms") { lining = t; break; }
            if (lining != null)
            {
                Renderer liningRenderer = lining.GetComponent<Renderer>();
                Material plaster = liningRenderer != null ? liningRenderer.sharedMaterial : null;
                lining.gameObject.SetActive(false);
                // Same seven boxes as gen_cecadec_interior.u_rooms (WALL_X 2.32, IN_X 9.6, ceiling 2.7), local (x, z).
                Slab("Revoque", new Vector2(2.32f, 9.6f), new Vector2(-.42f, -.4f), new Vector2(0, 2.7f), plaster, .5f);
                Slab("Revoque", new Vector2(-9.6f, -2.32f), new Vector2(-.42f, -.4f), new Vector2(0, 2.7f), plaster, .5f);
                Slab("Revoque", new Vector2(9.58f, 9.6f), new Vector2(-22.24f, -.42f), new Vector2(0, 2.7f), plaster, .5f);
                Slab("Revoque", new Vector2(-9.6f, -9.58f), new Vector2(-22.24f, -.42f), new Vector2(0, 2.7f), plaster, .5f);
                Slab("Revoque", new Vector2(9.58f, 9.6f), new Vector2(DoorZ0, -28.48f), new Vector2(0, 2.7f), plaster, .5f);
                Slab("Revoque", new Vector2(9.58f, 9.6f), new Vector2(-44.58f, DoorZ1), new Vector2(0, 2.7f), plaster, .5f);
                Slab("Revoque", new Vector2(9.58f, 9.6f), new Vector2(DoorZ1, DoorZ0), new Vector2(DoorTop + .15f, 2.7f), plaster, .5f);
                Slab("Revoque", new Vector2(-9.6f, -9.58f), new Vector2(-44.58f, -28.48f), new Vector2(0, 2.7f), plaster, .5f);
                Slab("Revoque", new Vector2(-9.6f, 9.6f), new Vector2(-44.6f, -44.58f), new Vector2(0, 2.7f), plaster, .5f);
            }
            // Frame, two sliding glass leaves, a light and a sign.
            Material metal = game.DoorMetal, glass = game.DoorGlass;
            float zc = (DoorZ0 + DoorZ1) / 2, w = DoorZ0 - DoorZ1;
            Slab("Marco", new Vector2(9.62f, 10.0f), new Vector2(DoorZ0 - .08f, DoorZ0), new Vector2(0, DoorTop + .15f), metal, 1);
            Slab("Marco", new Vector2(9.62f, 10.0f), new Vector2(DoorZ1, DoorZ1 + .08f), new Vector2(0, DoorTop + .15f), metal, 1);
            Slab("Marco", new Vector2(9.62f, 10.0f), new Vector2(DoorZ1, DoorZ0), new Vector2(DoorTop, DoorTop + .15f), metal, 1);
            leafA = Leaf("Hoja norte", zc + w * .25f, w * .5f - .1f, metal, glass);
            leafB = Leaf("Hoja sur", zc - w * .25f, w * .5f - .1f, metal, glass);
            var lamp = new GameObject("Luz entrada este"); lamp.transform.SetParent(root, false);
            lamp.transform.position = World(10.9f, zc, 2.9f);
            Light light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.range = 9; light.intensity = 3; light.color = new Color(.8f, .9f, 1f);
            lights.Add((light, 3, 42));
            Put("Lab_ExitSign", 9.55f, zc, -90, 2.45f, default, false);
        }
        private Transform Leaf(string name, float zc, float width, Material metal, Material glass)
        {
            var leaf = new GameObject(name) { layer = 10 }.transform;
            leaf.SetParent(interior, false); leaf.localPosition = new Vector3(DoorX, DoorTop / 2, zc); leaf.localRotation = Quaternion.identity;
            Part(leaf, Vector3.zero, new Vector3(.03f, DoorTop - .06f, width - .06f), glass);
            Part(leaf, new Vector3(0, -DoorTop / 2 + .05f, 0), new Vector3(.06f, .1f, width), metal);
            Part(leaf, new Vector3(0, DoorTop / 2 - .04f, 0), new Vector3(.06f, .08f, width), metal);
            Part(leaf, new Vector3(0, 0, width / 2 - .025f), new Vector3(.06f, DoorTop, .05f), metal);
            Part(leaf, new Vector3(0, 0, -width / 2 + .025f), new Vector3(.06f, DoorTop, .05f), metal);
            Part(leaf, new Vector3(-.06f, 0, 0), new Vector3(.03f, .04f, width * .6f), metal);
            leaf.gameObject.AddComponent<BoxCollider>().size = new Vector3(.08f, DoorTop, width);
            return leaf;
        }
        private static void Part(Transform parent, Vector3 position, Vector3 size, Material mat)
        {
            var go = new GameObject("Pieza") { layer = 10 };
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, 1);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        // Automatic doors: open for any student or enemy close by, identically on every peer.
        private void TickDoor()
        {
            if (leafA == null) return;
            Vector3 centre = EastDoor;
            bool near = false;
            foreach (StudentState s in game.Students.Values) if (s.alive && Flat(s.position - centre) < 4.5f) near = true;
            foreach (RemakeEnemy e in game.Enemies) if (Flat(e.transform.position - centre) < (e.Kind == EnemyKind.Giant ? 6.5f : 4.5f)) near = true;
            doorOpen = Mathf.MoveTowards(doorOpen, near ? 1 : 0, Time.deltaTime * 1.4f);
            if (near != doorWasOpen) { doorWasOpen = near; game.Audio?.Play("door_slide", centre + Vector3.up * 2, .8f, 18); }
            float e2 = Mathf.SmoothStep(0, 1, doorOpen), w = DoorZ0 - DoorZ1, zc = (DoorZ0 + DoorZ1) / 2;
            leafA.localPosition = new Vector3(DoorX - .1f * e2, DoorTop / 2, zc + w * .25f + e2 * w * .45f);
            leafB.localPosition = new Vector3(DoorX - .1f * e2, DoorTop / 2, zc - w * .25f - e2 * w * .45f);
        }
        private static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
        // Collider bounds expressed in the interior's frame (the facade walls are axis aligned with it).
        private Bounds LocalBounds(BoxCollider box)
        {
            var b = new Bounds(interior.InverseTransformPoint(box.transform.TransformPoint(box.center)), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = box.center + Vector3.Scale(box.size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(interior.InverseTransformPoint(box.transform.TransformPoint(c)));
            }
            return b;
        }
        // Solid box over interior-local ranges (x, z, y) with world-scaled UVs so long walls keep their texel size.
        private void Slab(string name, Vector2 x, Vector2 z, Vector2 y, Material mat, float tile)
        {
            Vector3 size = new Vector3(Mathf.Abs(x.y - x.x), Mathf.Abs(y.y - y.x), Mathf.Abs(z.y - z.x));
            if (size.x < .005f || size.y < .005f || size.z < .005f) return;
            var go = new GameObject(name);
            go.transform.SetParent(interior, false);
            go.transform.localPosition = new Vector3((x.x + x.y) / 2, (y.x + y.y) / 2, (z.x + z.y) / 2);
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, tile);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<BoxCollider>().size = size;
        }
        private static Mesh BoxMesh(Vector3 size, float tile)
        {
            Vector3 h = size * .5f;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>(); var tris = new List<int>();
            void Face(Vector3 n, Vector3 u, Vector3 v, float su, float sv)
            {
                int i = verts.Count; Vector3 c = Vector3.Scale(n, h);
                verts.Add(c - u * su - v * sv); verts.Add(c + u * su - v * sv); verts.Add(c + u * su + v * sv); verts.Add(c - u * su + v * sv);
                uvs.Add(Vector2.zero); uvs.Add(new Vector2(su * 2 / tile, 0)); uvs.Add(new Vector2(su * 2 / tile, sv * 2 / tile)); uvs.Add(new Vector2(0, sv * 2 / tile));
                for (int k = 0; k < 4; k++) normals.Add(n);
                tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            Face(Vector3.right, Vector3.forward, Vector3.up, h.z, h.y); Face(Vector3.left, Vector3.back, Vector3.up, h.z, h.y);
            Face(Vector3.forward, Vector3.left, Vector3.up, h.x, h.y); Face(Vector3.back, Vector3.right, Vector3.up, h.x, h.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, h.x, h.z); Face(Vector3.down, Vector3.right, Vector3.back, h.x, h.z);
            var mesh = new Mesh(); mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ the giant's route
        // A loop through the whole university: across the plaza, into CECADEC by the north door, down the corridor,
        // through the Lab de Procesos and out of the east glass door, around the building, past the auditorium and
        // CDS and back to the plaza. World points snapped to the ground.
        public List<Vector3> GiantRoute()
        {
            var points = new List<Vector3>();
            void L(float x, float z) => points.Add(Ground(World(x, z, 0)));
            void G(float x, float z) => points.Add(Ground(new Vector3(x, 0, z)));
            G(0, 2); L(0, 4); L(0, -6); L(0, -16); L(6f, -18.5f); L(0, -20); L(0, -24); L(0, -33); L(-4f, -41.5f); L(0, -35);
            L(2.0f, -29.5f); L(6.5f, -30.6f); L(12.5f, -31.1f); L(16f, -31f);
            L(14f, -50f); L(0, -52f); L(-15f, -48f); L(-15f, -25f); L(-15f, -48f); L(0, -52f); L(14f, -50f); L(17f, -20f); L(15f, -2f);
            G(8, 3); G(24, -8); G(26, 18); G(-4, 7);
            return points;
        }
        private Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4, game.WorldMask, QueryTriggerInteraction.Ignore)) return hit.point;
            return p;
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
