using System;
using System.Collections.Generic;
using System.Linq;
using HorrorUtez.Rendering;
using HorrorUtez.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorUtez.Remake
{
    public sealed class RemakeGame : MonoBehaviour
    {
        public GameObject StudentModel, TruckModel, EnemyModel, GiantModel;
        public GameObject[] LootModels;
        public Material[] ScreenMaterials;
        public RemakeAudio Audio { get; private set; }
        public RemakeDressing Dressing { get; private set; }
        public Material PropMaterial, SkinMaterial, SleeveMaterial, SignalMaterial;
        public readonly Dictionary<int, StudentState> Students = new Dictionary<int, StudentState>();
        public readonly List<RemakeLoot> Loot = new List<RemakeLoot>();
        public readonly List<RemakeEnemy> Enemies = new List<RemakeEnemy>();
        public bool Authority { get; private set; } = true;
        public bool Started { get; private set; }
        public bool Online { get; private set; }
        public int LocalId { get; private set; }
        public int Phase { get; private set; } // 0 menu, 1 salvage, 2 departure, 3 next day, 4 failed
        public int Day { get; private set; } = 1;
        public int Quota { get; private set; } = 1600;
        public int Cargo { get; private set; }
        public int Seconds { get; private set; } = 480;
        public string Notice { get; private set; } = "Recupera equipo. Cárgalo en el camión. Vuelve con vida.";
        public string ConnectionStatus { get; private set; } = "";
        public readonly Vector3 TruckPosition = new Vector3(12, 0.06f, -2);
        public int WorldMask => ~((1 << 9) | (1 << 10));
        public StudentState Local => Students.TryGetValue(LocalId, out StudentState state) ? state : null;
        public RemakeStudent Player { get; private set; }
        public RemakeHud Hud { get; private set; }
        public RemakeVoice Voice { get; private set; }
        public RemakeWire Wire { get; private set; }
        private readonly Dictionary<int, RemakeStudent> avatars = new Dictionary<int, RemakeStudent>();
        private readonly Dictionary<int, float> lastPose = new Dictionary<int, float>();
        private HingedDoor[] doors;
        private float finishAt, deadline, nextSnapshot, nextCargoCheck, nextPose, nextStepNoise;
        private int pendingDay;
        private Camera menuCamera;
        private AudioClip impact, success, alarm;
        private NavigationGrid navigation;
        private TextMesh truckDisplay;
        private bool lastJoinConnected;
        public bool MenuOpen => Hud != null && Hud.MenuOpen;
        public Camera CaptureView => Started && Player != null ? Player.View : menuCamera;

        private void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = Application.isMobilePlatform ? 30 : 60;
            Time.timeScale = 1;
            PsxLook.Enabled = true;
            PsxLook.Style = PsxVisualStyle.RetroPixel;
            impact = RemakeSound.Tone("impact", 145, 0.22f, 0.5f);
            success = RemakeSound.Tone("departure", 390, 0.7f, 0.5f);
            alarm = RemakeSound.Tone("warning", 95, 0.6f, 0.5f);
            doors = FindObjectsByType<HingedDoor>(FindObjectsSortMode.None).OrderBy(d => d.transform.position.x)
                .ThenBy(d => d.transform.position.z).ThenBy(d => d.name).ToArray();
            // Open corridor leaves before baking the ground-floor navigation grid.
            foreach (HingedDoor door in doors) door.Open();
            Audio = gameObject.AddComponent<RemakeAudio>(); Audio.Setup(this);
            Dressing = new RemakeDressing(this); Dressing.Build();
            BuildTruck(); BuildLoot(); BuildStaging();
            var view = new GameObject("Remake menu camera");
            menuCamera = view.AddComponent<Camera>();
            view.AddComponent<AudioListener>();
            view.transform.SetPositionAndRotation(TruckPosition + new Vector3(-6, 2.5f, -9), Quaternion.Euler(8, 36, 0));
            menuCamera.fieldOfView = 62; menuCamera.nearClipPlane = 0.05f;
            menuCamera.backgroundColor = new Color(0.02f, 0.04f, 0.04f);
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            UnityEngine.Rendering.Universal.UniversalAdditionalCameraData cameraData = menuCamera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            Voice = gameObject.AddComponent<RemakeVoice>(); Voice.Setup(this);
            Hud = gameObject.AddComponent<RemakeHud>(); Hud.Setup(this);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            string[] args = Environment.GetCommandLineArgs();
            if (args.Contains("-remakeHost")) Begin(true, "Prueba host");
            int joinAt = Array.IndexOf(args, "-remakeJoin");
            if (joinAt >= 0 && joinAt + 1 < args.Length) Join(args[joinAt + 1], "Prueba cliente");
            if (args.Contains("-remakeSolo")) Begin(false, "Estudiante");
            if (args.Contains("-remakeSmoke")) gameObject.AddComponent<RemakeSmoke>().Setup(this);
            if (args.Contains("-remakeCapture") || args.Contains("-remakeTour")) gameObject.AddComponent<RemakeCapture>().Setup(this, args.Contains("-remakeTour"));
        }
        public void Begin(bool host, string name)
        {
            if (Started) return;
            try
            {
                if (host) { Wire = new RemakeWire(); Wire.Host(); }
                Online = host; Authority = true; LocalId = 0; Started = true; Phase = 1;
                Students.Clear(); AddStudent(0, CleanName(name));
                deadline = Time.time + 480;
                foreach (RemakeLoot item in Loot) item.SetAuthority(true);
                SpawnEnemies(); StartLocal();
                ConnectionStatus = host ? "ANFITRIÓN · " + RemakeWire.LocalAddresses() + ":27777" : "PARTIDA LOCAL";
                Hud.ShowGame();
                Debug.Log("[Remake] Started " + ConnectionStatus);
            }
            catch (Exception e) { Wire?.Dispose(); Wire = null; Tell("No se pudo hospedar: " + e.Message); }
        }
        public async void Join(string address, string name)
        {
            if (Started || Wire != null) return;
            Authority = false; Online = true; pendingDay = 0;
            Wire = new RemakeWire(); ConnectionStatus = "Conectando…";
            Hud.SetConnectionStatus(ConnectionStatus);
            await Wire.Join(address);
            if (this == null) return;
            if (Wire == null) return;
            if (Wire.Error != null)
            {
                Tell("No se pudo conectar: " + Wire.Error); Hud.SetConnectionStatus(Notice);
                Wire.Dispose(); Wire = null; Authority = true; Online = false; return;
            }
            Wire.Send(0, JsonUtility.ToJson(new WireMessage { type = "hello", name = CleanName(name), day = 1 }));
            ConnectionStatus = "CLIENTE · " + address + ":27777";
        }
        private static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Estudiante";
            name = name.Replace("<", "").Replace(">", "").Replace("\n", " ").Trim();
            return name.Substring(0, Mathf.Min(name.Length, 20));
        }
        private void AddStudent(int id, string name)
        {
            Students[id] = new StudentState { id = id, name = name, position = SpawnPoint(id), aim = Vector3.forward };
            lastPose[id] = Time.time;
        }
        public Vector3 SpawnPoint(int id) => TruckPosition + new Vector3(-2.6f - (id % 3)*0.8f, 0.1f, -5.2f + (id % 2)*0.8f);
        private void StartLocal()
        {
            menuCamera.gameObject.SetActive(false);
            if (Player == null)
            {
                var go = new GameObject("Local student"); Player = go.AddComponent<RemakeStudent>();
                Player.Setup(this, LocalId, true); avatars[LocalId] = Player;
            }
            Player.Teleport(Local != null ? Local.position : SpawnPoint(LocalId));
            foreach (WeatherSystem weather in FindObjectsByType<WeatherSystem>(FindObjectsSortMode.None)) weather.Follow(Player.transform);
        }
        private void SpawnEnemies()
        {
            if (Enemies.Count > 0) return;
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("El velador " + i) { layer = 9 };
                var enemy = go.AddComponent<RemakeEnemy>();
                enemy.Setup(this, EnemyKind.Caretaker, new Vector3(i == 0 ? -12 : 22, 0.1f, i == 0 ? 22 : -35), EnemyModel);
                Enemies.Add(enemy);
            }
            // El Rector: a giant that wakes after a while at the far end of the campus.
            var giant = new GameObject("El Rector") { layer = 9 };
            var rector = giant.AddComponent<RemakeEnemy>();
            rector.Setup(this, EnemyKind.Giant, new Vector3(26, 0.1f, -58), GiantModel);
            Enemies.Add(rector);
            foreach (Transform t in giant.GetComponentsInChildren<Transform>()) t.gameObject.layer = 9;
            foreach (RemakeEnemy e in Enemies) foreach (Transform t in e.GetComponentsInChildren<Transform>()) t.gameObject.layer = 9;
        }
        private void Update()
        {
            PumpNetwork();
            if (!Started) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Hud.TogglePause();
            if (Authority && Phase == 1)
            {
                Seconds = Mathf.Max(0, Mathf.CeilToInt(deadline - Time.time));
                if (Seconds == 0) Fail("Amaneció. El equipo de seguridad cerró el campus.");
                if (!Students.Values.Any(s => s.alive)) Fail("Nadie volvió al camión.");
            }
            if (Authority && Time.time > nextCargoCheck)
            {
                nextCargoCheck = Time.time + 0.2f; Cargo = ComputeCargo(); ReviveFromCards();
                if (Phase == 2)
                {
                    if (Cargo < Quota || !Students.Values.Any(s => s.alive && InTruck(s.position)))
                    { Phase = 1; Tell("Salida cancelada. Falta carga o un estudiante dentro del camión."); }
                    else if (Time.time >= finishAt) FinishDay();
                }
            }
            if (Player != null && Local != null && Local.alive && (Phase == 1 || Phase == 2))
            {
                Local.position = Player.transform.position; Local.yaw = Player.Yaw; Local.pitch = Player.Pitch;
                Local.aim = Player.View.transform.forward; Local.reach = Player.Reach; Local.torch = Player.TorchOn;
                Local.hold = Player.Hold; Local.eye = Player.EyeHeight; Local.tumble = Player.Tumbling;
                if (Authority && Time.time > nextStepNoise && Player.Speed > 0.7f)
                { Noise(Local.position, Player.Running ? 20 : Player.Crouching ? 3 : 8); nextStepNoise = Time.time + 0.5f; }
                if (!Authority && Time.unscaledTime > nextPose)
                {
                    nextPose = Time.unscaledTime + 0.05f;
                    Wire?.Send(0, JsonUtility.ToJson(new WireMessage { type = "pose", position = Local.position,
                        aim = Local.aim, yaw = Local.yaw, pitch = Local.pitch, reach = Local.reach, flag = Local.torch, rotation = Local.hold, eye = Local.eye, tumble = Local.tumble }));
                }
                if (Local.position.y < -6) Hurt(LocalId, 100);
            }
            if (Authority && Online && Time.unscaledTime > nextSnapshot)
            { nextSnapshot = Time.unscaledTime + 0.075f; Wire?.Broadcast(JsonUtility.ToJson(Snapshot())); }
            SyncAvatars(); UpdatePopups(); Dressing?.Tick();
            if (truckDisplay != null) truckDisplay.text = "$" + Cargo + " / $" + Quota + "\n" + (Cargo >= Quota ? "CARGA LISTA" : "RECUPERAR EQUIPO");
        }
        private void PumpNetwork()
        {
            if (Wire == null) return;
            int budget = 100;
            while (budget-- > 0 && Wire.Poll(out RemakeWire.Incoming incoming))
            {
                if (incoming.connected) { if (!Authority) lastJoinConnected = true; continue; }
                if (incoming.disconnected)
                {
                    if (Authority)
                    {
                        if (Students.TryGetValue(incoming.peer, out StudentState departed)) Tell(departed.name + " salió de la partida.");
                        Students.Remove(incoming.peer); lastPose.Remove(incoming.peer);
                    }
                    else if (lastJoinConnected)
                    { Started = false; Voice.StopMicrophone(); Hud.Disconnected("Se perdió la conexión con el anfitrión."); }
                    continue;
                }
                try
                {
                    WireMessage message = JsonUtility.FromJson<WireMessage>(incoming.json);
                    if (message == null) continue;
                    if (Authority) HandleClient(incoming.peer, message); else HandleServer(message);
                }
                catch (Exception e) { Debug.LogWarning("[Remake] Ignored malformed message: " + e.Message); }
            }
        }
        private void HandleClient(int peer, WireMessage message)
        {
            if (message.type == "hello")
            {
                if (message.day != 1 || Phase != 1 || Students.ContainsKey(peer) || Students.Count >= 5)
                { Wire.Disconnect(peer); return; }
                AddStudent(peer, CleanName(message.name));
                Wire.Send(peer, JsonUtility.ToJson(new WireMessage { type = "welcome", id = peer }));
                Wire.Send(peer, JsonUtility.ToJson(Snapshot())); Tell(Students[peer].name + " llegó al campus."); return;
            }
            if (!Students.TryGetValue(peer, out StudentState state)) return;
            if (message.type == "pose" && state.alive && (Phase == 1 || Phase == 2))
            {
                if (!Finite(message.position) || !Finite(message.aim) || !float.IsFinite(message.yaw) || !float.IsFinite(message.pitch)) return;
                float elapsed = Mathf.Clamp(Time.time - lastPose[peer], 0.04f, 0.5f);
                if (Vector3.Distance(state.position, message.position) > 9 * elapsed + 1.5f || Mathf.Abs(message.position.x) > 90 || Mathf.Abs(message.position.z) > 130) return;
                float moved = Vector3.Distance(state.position, message.position);
                state.position = message.position; state.aim = message.aim.normalized; state.yaw = message.yaw;
                state.pitch = Mathf.Clamp(message.pitch, -80, 80); state.reach = Mathf.Clamp(message.reach, RemakeStudent.MinReach, RemakeStudent.ReachCap(state)); state.torch = message.flag;
                state.eye = Mathf.Clamp(message.eye, 0.35f, 1.6f); state.tumble = message.tumble; if (ValidRotation(message.rotation)) state.hold = Normalize(message.rotation);
                lastPose[peer] = Time.time;
                if (moved > 0.03f) Noise(state.position, moved / elapsed > 3.5f ? 18 : 6);
                if (state.position.y < -6) Hurt(peer, 100);
            }
            else if (message.type == "voice")
            {
                if (message.audio == null || message.audio.Length > 11000 || !state.alive) return;
                message.id = peer; Voice.Receive(peer, message.audio);
                Wire.Broadcast(JsonUtility.ToJson(message), peer); Noise(state.position, 18);
            }
            else Command(peer, message);
        }
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        private static bool ValidRotation(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z)
            && float.IsFinite(q.w) && q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w > 0.5f;
        private static Quaternion Normalize(Quaternion q)
        { float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w); return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m); }
        private void HandleServer(WireMessage message)
        {
            if (message.type == "welcome") { LocalId = message.id; return; }
            if (message.type == "voice") { Voice.Receive(message.id, message.audio); return; }
            if (message.type == "sound") { Audio?.Play(string.IsNullOrEmpty(message.text) ? "impact_plastic" : message.text, message.position, message.reach > 0 ? message.reach : .5f); return; }
            if (message.type != "state" || message.students == null || message.loot == null) return;
            bool newDay = pendingDay != message.day;
            pendingDay = Day = message.day; Quota = message.quota; Cargo = message.cargo;
            Phase = message.phase; Seconds = message.seconds; Notice = message.text;
            bool wasDown = Local != null && !Local.alive;
            Students.Clear(); foreach (StudentState state in message.students) Students[state.id] = state;
            if (!Students.ContainsKey(LocalId)) return;
            if (wasDown && Local.alive && Player != null && !newDay) Player.Teleport(Local.position);
            foreach (LootState state in message.loot)
                if (state.id >= 0 && state.id < Loot.Count) Loot[state.id].Apply(state);
            SpawnEnemies();
            if (message.enemies != null)
                for (int i = 0; i < Mathf.Min(message.enemies.Length, Enemies.Count); i++) Enemies[i].Apply(message.enemies[i], message.enemyFlags != null && i < message.enemyFlags.Length ? message.enemyFlags[i] : 0);
            if (message.doors != null)
                for (int i = 0; i < Mathf.Min(message.doors.Length, doors.Length); i++)
                    if (message.doors[i]) doors[i].Open(); else doors[i].Close();
            if (!Started)
            {
                Started = true; foreach (RemakeLoot item in Loot) item.SetAuthority(false);
                StartLocal(); Hud.ShowGame();
            }
            if (newDay) Player.Teleport(Local.position);
        }
        public WireMessage Snapshot() => new WireMessage
        {
            type = "state", day = Day, quota = Quota, cargo = Cargo, phase = Phase, seconds = Seconds, text = Notice,
            students = Students.Values.ToArray(),
            loot = Loot.Select(l => new LootState { id = l.Id, value = l.Value, owner = l.Owner, position = l.transform.position, rotation = l.transform.rotation }).ToArray(),
            enemies = Enemies.Select(e => e.transform.position).ToArray(), enemyFlags = Enemies.Select(e => e.Flags).ToArray(), doors = doors.Select(d => d.IsOpen).ToArray()
        };
        public void Request(string action, int item = -1) => Request(action, item, Vector3.zero, default);
        public void Request(string action, int item, Vector3 point, Quaternion hold)
        {
            var message = new WireMessage { type = action, item = item, position = point, rotation = hold };
            if (Authority) Command(LocalId, message); else Wire?.Send(0, JsonUtility.ToJson(message));
        }
        private void Command(int id, WireMessage message)
        {
            if (!Students.TryGetValue(id, out StudentState state)) return;
            if (message.type == "next" && id == 0 && (Phase == 3 || Phase == 4)) { NextDay(Phase == 4); return; }
            if (message.type == "upgrade" && Phase == 3 && state.escaped)
            {
                int kind = Mathf.Clamp(message.item, 0, UpgradeNames.Length - 1), cost = UpgradeCost(state, kind);
                if (UpgradeLevel(state, kind) < 3 && state.credits >= cost)
                { state.credits -= cost; SetUpgrade(state, kind, UpgradeLevel(state, kind) + 1); Tell(state.name + " compró " + UpgradeNames[kind] + "."); }
                return;
            }
            if (!state.alive || Phase != 1) return;
            if (message.type == "drop") { state.held = -1; return; }
            if (message.type == "grab" && message.item >= 0 && message.item < Loot.Count)
            {
                RemakeLoot item = Loot[message.item]; Vector3 eyes = state.position + Vector3.up * state.eye;
                // The grab point is clamped to the object's box; the default (smoke, centre grabs) is the centre.
                Vector3 extents = item.Size * 0.5f;
                Vector3 grabLocal = Finite(message.position) ? Vector3.Max(-extents, Vector3.Min(extents, message.position)) : Vector3.zero;
                Vector3 direction = item.transform.TransformPoint(grabLocal) - eyes;
                if (direction.magnitude <= 2.8f && !Physics.Raycast(eyes, direction.normalized, Mathf.Max(0, direction.magnitude - 0.15f), WorldMask))
                {
                    state.held = item.Id; state.grabLocal = grabLocal;
                    state.hold = ValidRotation(message.rotation) ? Normalize(message.rotation)
                        : Quaternion.Inverse(Quaternion.Euler(state.pitch, state.yaw, 0)) * item.transform.rotation;
                }
            }
            if (message.type == "door" && message.item >= 0 && message.item < doors.Length &&
                Vector3.Distance(state.position, doors[message.item].transform.position) < 4) doors[message.item].Toggle();
            if (message.type == "extract")
            {
                Cargo = ComputeCargo();
                if (!InTruck(state.position)) { Tell("Entra en la caja del camión para iniciar la salida."); return; }
                if (Cargo < Quota) { Tell("Faltan $" + (Quota - Cargo) + ". Suelta y acomoda la carga dentro del camión."); return; }
                Phase = 2; finishAt = Time.time + 5;
                Tell("Salida en 5 segundos. ¡Todos al camión! Los que se queden perderán sus mejoras.");
                SoundAt(success, TruckPosition, 0.8f);
            }
        }
        // R.E.P.O.-style upgrade shop between days. Each line goes to level 3; prices rise per level.
        public static readonly string[] UpgradeNames = { "FUERZA", "ENERGÍA", "ALCANCE", "VELOCIDAD", "SALUD", "SALTO EXTRA" };
        public static readonly string[] UpgradeInfo = { "Levantas más peso", "+10 de energía", "Brazos más largos", "Corres más rápido", "+20 de salud máxima", "Un salto en el aire" };
        public static int UpgradeLevel(StudentState s, int kind) => kind switch { 0 => s.strength, 1 => s.stamina, 2 => s.range, 3 => s.speed, 4 => s.vitality, _ => s.jumps };
        public static int UpgradeCost(StudentState s, int kind) => 60 + UpgradeLevel(s, kind) * 45 + (kind == 0 ? 20 : 0);
        private static void SetUpgrade(StudentState s, int kind, int level)
        {
            switch (kind) { case 0: s.strength = level; break; case 1: s.stamina = level; break; case 2: s.range = level; break;
                case 3: s.speed = level; break; case 4: s.vitality = level; s.health = s.MaxHealth; break; default: s.jumps = level; break; }
        }
        private static void ClearUpgrades(StudentState s) { s.strength = s.stamina = s.range = s.speed = s.vitality = s.jumps = 0; }
        private void ReviveFromCards()
        {
            foreach (RemakeLoot card in Loot)
            {
                if (card.Kind != RemakeLoot.CredentialKind || card.Owner < 0 || Students.Values.Any(s => s.held == card.Id)) continue;
                if (card.Body.linearVelocity.sqrMagnitude > 2.25f || !CargoContains(card.Bounds)) continue;
                if (Students.TryGetValue(card.Owner, out StudentState state) && !state.alive && (Phase == 1 || Phase == 2))
                {
                    state.alive = true; state.health = Mathf.Max(30, state.MaxHealth / 3); state.position = TruckPosition + new Vector3(0, .9f, -1.6f);
                    lastPose[state.id] = Time.time;
                    if (state.id == LocalId && Player != null) Player.Teleport(state.position);
                    Tell(state.name + " volvió en sí dentro del camión.");
                    Audio?.Play("music_extract", TruckPosition, .6f);
                }
                card.Hide();
            }
        }
        public int DoorId(HingedDoor door) => Array.IndexOf(doors, door);
        public bool InTruck(Vector3 position)
        {
            Vector3 p = position - TruckPosition;
            return Mathf.Abs(p.x) < 1.3f && p.z > -3.1f && p.z < 1.4f && p.y > 0.55f && p.y < 3.5f;
        }
        public bool CargoContains(Bounds bounds)
        {
            Vector3 min = bounds.min - TruckPosition, max = bounds.max - TruckPosition;
            return min.x > -1.34f && max.x < 1.34f && min.z > -3.12f && max.z < 1.42f && min.y > 0.73f && max.y < 3.12f;
        }
        public int ComputeCargo() => Loot.Where(l => l.Value > 0 && !Students.Values.Any(s => s.held == l.Id)
            && l.Body.linearVelocity.sqrMagnitude < 2.25f && CargoContains(l.Bounds)).Sum(l => l.Value);
        public void Hurt(int id, int damage) => Hurt(id, damage, Vector3.zero);
        // knock: launch velocity the victim's own client applies as a tumble (enemy blows, giant throws).
        public void Hurt(int id, int damage, Vector3 knock)
        {
            if (!Authority || Phase != 1 || !Students.TryGetValue(id, out StudentState state) || !state.alive) return;
            state.health = Mathf.Max(0, state.health - damage); state.knock = knock; state.hurtCount++;
            Audio?.Play("hit_player", state.position, .9f);
            if (state.health == 0)
            {
                state.alive = false; state.held = -1;
                // Like R.E.P.O.'s heads: the fallen student's ID card drops where they fell; carrying it into the truck revives them.
                RemakeLoot card = Loot.FirstOrDefault(l => l.Kind == RemakeLoot.CredentialKind && l.Owner < 0);
                if (card != null) card.Drop(id, state.name, state.position + Vector3.up * .6f);
                Tell(state.name + " cayó. Lleva su credencial al camión para revivirlo.");
                Audio?.Play("death_sting", state.position, 1, 40);
            }
        }
        private void FinishDay()
        {
            Phase = 3; int survivors = 0;
            foreach (StudentState state in Students.Values)
            {
                state.escaped = state.alive && InTruck(state.position); state.held = -1;
                if (state.escaped) { survivors++; state.credits += 100 + Mathf.Max(0, Cargo - Quota) / 8; }
                else { ClearUpgrades(state); state.credits = 0; }
            }
            Tell("Cuota saldada. " + survivors + " estudiante(s) escaparon. El resto revive mañana sin mejoras.");
            SoundAt(success, TruckPosition, 0.9f);
        }
        private void Fail(string reason)
        {
            Phase = 4; foreach (StudentState state in Students.Values) state.held = -1;
            Tell(reason + " La expedición terminó."); SoundAt(alarm, TruckPosition, 0.8f);
        }
        private void NextDay(bool restart)
        {
            Day = restart ? 1 : Day + 1; Quota = 1600 + Mathf.Min(Day - 1, 5) * 220; Phase = 1;
            deadline = Time.time + 480; Seconds = 480; Cargo = 0;
            foreach (StudentState state in Students.Values)
            {
                if (restart) { state.credits = 0; ClearUpgrades(state); }
                state.health = state.MaxHealth; state.alive = true; state.escaped = false; state.held = -1;
                state.position = SpawnPoint(state.id); lastPose[state.id] = Time.time;
            }
            foreach (RemakeLoot item in Loot) item.ResetLoot();
            foreach (RemakeLoot card in Loot) if (card.Kind == RemakeLoot.CredentialKind) card.Hide();
            foreach (RemakeEnemy enemy in Enemies) enemy.ResetEnemy();
            Player.Teleport(Local.position); Hud.ShowGame();
            Tell("Día " + Day + ". Recupera equipo por $" + Quota + ".");
        }
        private void SyncAvatars()
        {
            foreach (StudentState state in Students.Values)
            {
                if (state.id == LocalId) continue;
                if (!avatars.TryGetValue(state.id, out RemakeStudent avatar))
                {
                    var go = new GameObject(state.name); avatar = go.AddComponent<RemakeStudent>();
                    avatar.Setup(this, state.id, false); avatars[state.id] = avatar;
                }
                avatar.Apply(state);
            }
            foreach (int id in avatars.Keys.ToArray())
                if (id != LocalId && !Students.ContainsKey(id)) { Destroy(avatars[id].gameObject); avatars.Remove(id); }
        }
        public Transform Avatar(int id) => avatars.TryGetValue(id, out RemakeStudent avatar) ? avatar.transform : null;
        public void Tell(string message) { Notice = message; Debug.Log("[Remake] " + message); Hud?.SetConnectionStatus(message); }
        // Floating "-$" markers for damaged gear, like R.E.P.O.'s value-lost UI.
        private readonly List<(TextMesh label, float born)> popups = new List<(TextMesh, float)>();
        public void ValuePopup(Vector3 point, int amount, bool destroyed = false)
        {
            var go = new GameObject("Valor perdido"); go.transform.position = point + Vector3.up * 0.3f;
            var label = go.AddComponent<TextMesh>(); label.text = (destroyed ? "¡ROTO! -$" : "-$") + amount;
            label.fontSize = 60; label.characterSize = destroyed ? 0.02f : 0.013f; label.anchor = TextAnchor.MiddleCenter;
            label.color = destroyed ? new Color(1, 0.25f, 0.2f) : new Color(1, 0.55f, 0.35f);
            popups.Add((label, Time.time));
            if (destroyed && Player != null && Vector3.Distance(Player.transform.position, point) < 10) Player.Shake(2.5f, 0.5f);
        }
        private void UpdatePopups()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                (TextMesh label, float born) = popups[i];
                float age = Time.time - born;
                if (label == null || age > 1.4f) { if (label != null) Destroy(label.gameObject); popups.RemoveAt(i); continue; }
                label.transform.position += Vector3.up * (0.6f * Time.deltaTime);
                if (Player != null && Player.View != null)
                    label.transform.rotation = Quaternion.LookRotation(label.transform.position - Player.View.transform.position);
                Color c = label.color; c.a = Mathf.Clamp01((1.4f - age) / 0.5f); label.color = c;
            }
        }
        public void Noise(Vector3 point, float radius) { foreach (RemakeEnemy enemy in Enemies) enemy.Hear(point, radius); }
        public void PlayImpact(Vector3 point, float volume, string clip = "impact_plastic")
        {
            Audio?.Play(clip, point, volume);
            if (Online) Wire?.Broadcast(JsonUtility.ToJson(new WireMessage { type = "sound", position = point, text = clip, reach = volume }));
        }
        // Patrols: caretakers walk the plaza; the giant also prowls CECADEC's ground floor.
        private static readonly Vector3[] OutdoorPatrol = { new Vector3(-8, 0, 1), new Vector3(6, 0, -8), new Vector3(-12, 0, 18), new Vector3(18, 0, 8),
            new Vector3(22, 0, -30), new Vector3(-20, 0, -40), new Vector3(10, 0, -55) };
        public Vector3 PatrolPoint(bool indoors)
        {
            if (indoors && Dressing != null && Dressing.Patrol.Count > 0 && UnityEngine.Random.value < .6f)
                return Dressing.Patrol[UnityEngine.Random.Range(0, Dressing.Patrol.Count)];
            return OutdoorPatrol[UnityEngine.Random.Range(0, indoors ? OutdoorPatrol.Length : 4)];
        }
        private static void SoundAt(AudioClip clip, Vector3 point, float volume) => AudioSource.PlayClipAtPoint(clip, point, volume);
        public void SendVoice(string audio)
        {
            if (!Started || Local == null || !Local.alive) return;
            var message = new WireMessage { type = "voice", id = LocalId, audio = audio };
            if (Authority) { Wire?.Broadcast(JsonUtility.ToJson(message)); Noise(Local.position, 18); }
            else Wire?.Send(0, JsonUtility.ToJson(message));
        }
        public void FindPath(Vector3 start, Vector3 goal, List<Vector3> route)
        {
            if (navigation == null) navigation = new NavigationGrid(WorldMask);
            navigation.Find(start, goal, route);
        }
        private void OnDestroy() { Wire?.Dispose(); Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        private GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Destroy(go.GetComponent<Collider>());
            return go;
        }
        private void BuildTruck()
        {
            var truck = new GameObject("Camión · única extracción"); truck.transform.position = TruckPosition;
            if (TruckModel != null)
            {
                Instantiate(TruckModel, truck.transform);
            }
            // Separate, invisible primitive collision shell: hollow and mobile friendly.
            void CollisionBox(string name, Vector3 p, Vector3 size)
            {
                var go = new GameObject(name); go.transform.SetParent(truck.transform, false); go.transform.localPosition = p;
                var box = go.AddComponent<BoxCollider>(); box.size = size;
            }
            CollisionBox("Cargo floor", new Vector3(0, 0.75f, -0.8f), new Vector3(2.7f, 0.18f, 4.7f));
            CollisionBox("Left cargo wall", new Vector3(-1.42f, 1.91f, -0.8f), new Vector3(0.16f, 2.42f, 4.7f));
            CollisionBox("Right cargo wall", new Vector3(1.42f, 1.91f, -0.8f), new Vector3(0.16f, 2.42f, 4.7f));
            CollisionBox("Roof", new Vector3(0, 3.14f, -0.8f), new Vector3(3, 0.15f, 4.7f));
            CollisionBox("Front", new Vector3(0, 1.9f, 1.53f), new Vector3(2.7f, 2.4f, 0.15f));
            CollisionBox("Cab", new Vector3(0, 1.2f, 2.72f), new Vector3(2.64f, 2.4f, 2.05f));
            GameObject ramp = Box("Rampa de carga", truck.transform, new Vector3(0, 0.39f, -4.62f), new Vector3(2.6f, 0.09f, 3.3f), PropMaterial);
            ramp.transform.localRotation = Quaternion.Euler(-14, 0, 0);
            Box("Terminal de salida", truck.transform, new Vector3(1.12f, 1.65f, -2.85f), new Vector3(0.26f, 0.3f, 0.12f), SignalMaterial, false);
            truckDisplay = WorldText("CARGA", truck.transform, new Vector3(0, 2.92f, -3.24f), 0.026f);
            WorldText("UTEZ / RECUPERACIÓN\n01  TURNO NOCTURNO", truck.transform, new Vector3(-1.52f, 2.1f, -0.8f), 0.045f).transform.localRotation = Quaternion.Euler(0, 90, 0);
            var lamp = new GameObject("Luz caja"); lamp.transform.SetParent(truck.transform, false); lamp.transform.localPosition = new Vector3(0, 2.8f, -1.3f);
            Light light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1, 0.78f, 0.47f); light.intensity = 8; light.range = 9;
        }
        private TextMesh WorldText(string text, Transform parent, Vector3 point, float scale)
        {
            var go = new GameObject(text); go.transform.SetParent(parent, false); go.transform.localPosition = point;
            go.transform.localRotation = Quaternion.identity;
            var label = go.AddComponent<TextMesh>(); label.text = text; label.fontSize = 60; label.characterSize = scale;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = new Color(0.68f, 1, 0.79f);
            return label;
        }
        private void BuildLoot()
        {
            string[] names = { "Laptop del laboratorio", "Proyector de aula", "Microscopio", "Estación de trabajo", "UPS de servidores",
                "Impresora de servicios", "Carrito de mantenimiento", "Osciloscopio", "Switch de red" };
            int[] values = { 420, 560, 780, 840, 1100, 620, 0, 690, 380 };
            float[] mass = { 4, 8, 12, 24, 36, 18, 16, 7, 3 };
            Vector3[] sizes = { new Vector3(.66f,.43f,.46f), new Vector3(.61f,.27f,.5f), new Vector3(.45f,.85f,.39f),
                new Vector3(.49f,.91f,.59f), new Vector3(.53f,.66f,.61f), new Vector3(.77f,.67f,.78f), new Vector3(1.4f,.53f,1.7f),
                new Vector3(.44f,.26f,.4f), new Vector3(.46f,.08f,.34f) };
            // The first haul sits in the plaza; the rest is hidden in CECADEC's wrecked compuaulas.
            Vector3 Spot(string key, Vector3 fallback) => Dressing != null && Dressing.Spots.TryGetValue(key, out Vector3 p) ? p : fallback;
            Vector3[] points = { new Vector3(4,.5f,-6), new Vector3(1,.5f,-5), Spot("process_bench", new Vector3(-4,.6f,-3)),
                Spot("back_room", new Vector3(-7,.7f,-1)), Spot("electrical", new Vector3(-11,.55f,5)), Spot("nw1_floor", new Vector3(6,.6f,8)),
                Spot("cc9_teacher", new Vector3(-3,.6f,-15)), Spot("se_lab", new Vector3(2,.6f,-22)), Spot("aula2_teacher", new Vector3(-9,.7f,16)),
                Spot("aula1_floor", new Vector3(6,.6f,19)), new Vector3(8,.6f,-7), Spot("process_bench2", new Vector3(-6,.6f,-9)),
                Spot("storage_shelf", new Vector3(3,.6f,12)) };
            int[] kinds = { 0,1,2,3,4,5,0,2,1,3,6,7,8 };
            for (int i = 0; i < points.Length; i++)
            {
                int kind = kinds[i]; var go = new GameObject(names[kind]); go.layer = 10;
                Vector3 point = points[i];
                // Indoor spots probe from just above so the ray starts under the ceiling.
                if (Physics.Raycast(point + Vector3.up * .9f, Vector3.down, out RaycastHit ground, 3, WorldMask, QueryTriggerInteraction.Ignore))
                    point.y = ground.point.y + sizes[kind].y * 0.5f + 0.07f;
                go.transform.position = point;
                if (LootModels != null && kind < LootModels.Length && LootModels[kind] != null)
                {
                    Instantiate(LootModels[kind], go.transform);
                }
                else Box("Placeholder", go.transform, Vector3.zero, sizes[kind], PropMaterial, false);
                var box = go.AddComponent<BoxCollider>(); box.size = sizes[kind];
                if (kind == 0) box.center = new Vector3(0, 0.015f, 0.04f);
                if (kind == 6)
                {
                    box.size = new Vector3(1.3f, 0.12f, 1.65f); box.center = Vector3.zero;
                    go.transform.position = new Vector3(8, 0.38f, -7);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var sideGo = new GameObject("Cart side"); sideGo.transform.SetParent(go.transform, false);
                        sideGo.transform.localPosition = new Vector3(side * .63f, .2f, 0);
                        var wall = sideGo.AddComponent<BoxCollider>(); wall.size = new Vector3(.08f,.4f,1.65f);
                    }
                    var handle = new GameObject("Cart handle"); handle.transform.SetParent(go.transform, false);
                    handle.transform.localPosition = new Vector3(0,.6f,.77f);
                    var handleBox = handle.AddComponent<BoxCollider>(); handleBox.size = new Vector3(1.3f,1,.08f);
                    foreach(float x in new[]{-.63f,.63f}) foreach(float z in new[]{-.59f,.59f})
                    {
                        var wheel = new GameObject("Cart wheel collision"); wheel.transform.SetParent(go.transform,false);
                        wheel.transform.localPosition = new Vector3(x,-.2f,z); wheel.AddComponent<SphereCollider>().radius=.16f;
                    }
                }
                foreach (Transform child in go.GetComponentsInChildren<Transform>()) child.gameObject.layer = 10;
                var item = go.AddComponent<RemakeLoot>(); item.Setup(this, i, kind, names[kind], values[kind], mass[kind], sizes[kind]);
                item.SetAuthority(false); Loot.Add(item);
                if(kind==6)item.Body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            }
            // One hidden ID card per possible student, created identically on every peer so loot ids stay in sync.
            for (int c = 0; c < 5; c++)
            {
                var go = new GameObject("Credencial") { layer = 10 };
                go.transform.position = new Vector3(0, -200, 0);
                Box("Gafete", go.transform, Vector3.zero, new Vector3(.2f, .03f, .28f), SignalMaterial, false);
                Box("Cordón", go.transform, new Vector3(0, .02f, .2f), new Vector3(.03f, .015f, .16f), PropMaterial, false);
                go.AddComponent<BoxCollider>().size = new Vector3(.24f, .07f, .34f);
                var glow = new GameObject("Brillo"); glow.transform.SetParent(go.transform, false); glow.transform.localPosition = Vector3.up * .25f;
                Light light = glow.AddComponent<Light>(); light.type = LightType.Point; light.range = 2.5f; light.intensity = 2; light.color = new Color(1, .65f, .25f);
                foreach (Transform child in go.GetComponentsInChildren<Transform>()) child.gameObject.layer = 10;
                var card = go.AddComponent<RemakeLoot>(); card.Setup(this, Loot.Count, RemakeLoot.CredentialKind, "Credencial", 0, .6f, new Vector3(.24f, .07f, .34f));
                card.SetAuthority(false); Loot.Add(card); card.Hide();
            }
        }
        private void BuildStaging()
        {
            var staging = new GameObject("Remake · inventario nocturno");
            WorldText("EQUIPO EN RESGUARDO\nENTREGA PENDIENTE", staging.transform, new Vector3(0, 1.7f, -7), 0.04f);
            Box("Inventario", staging.transform, new Vector3(0, 1.7f, -6.95f), new Vector3(2.5f, 1, .09f), PropMaterial);
            for (int i = 0; i < 5; i++)
                Box("Barrera de obra", staging.transform, new Vector3(-16+i*1.3f,.15f,-8), new Vector3(.7f,.3f,.7f), SignalMaterial);
        }
    }

    // Ground-floor navigation from the real colliders (walls, furniture). 0.7 m cells, 8-way moves
    // without corner cutting, then line-of-sight smoothing so paths hug doorways instead of zig-zagging.
    internal sealed class NavigationGrid
    {
        private const int Width = 110, Height = 156;
        private const float Step = 0.7f;
        private static readonly Vector3 Origin = new Vector3(-38, 0, -66);
        private readonly bool[] open = new bool[Width * Height];
        private readonly int[] parent = new int[Width * Height];
        private readonly Queue<int> queue = new Queue<int>();
        private readonly List<Vector3> raw = new List<Vector3>();
        private readonly int mask;
        public NavigationGrid(int mask)
        {
            this.mask = mask;
            for (int z = 0; z < Height; z++) for (int x = 0; x < Width; x++)
            {
                Vector3 p = Point(x + z * Width);
                open[x+z*Width] = Physics.Raycast(p+Vector3.up*1.7f,Vector3.down,2,mask,QueryTriggerInteraction.Ignore) &&
                    !Physics.CheckCapsule(p+Vector3.up*.45f,p+Vector3.up*1.55f,.28f,mask,QueryTriggerInteraction.Ignore);
            }
        }
        private static Vector3 Point(int i) => Origin + new Vector3(i % Width * Step, 0.08f, i / Width * Step);
        private int Closest(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((p.x-Origin.x)/Step),0,Width-1);
            int z = Mathf.Clamp(Mathf.RoundToInt((p.z-Origin.z)/Step),0,Height-1);
            int best = -1; float distance = float.MaxValue;
            for(int dz=-4;dz<=4;dz++) for(int dx=-4;dx<=4;dx++)
            {
                int xx=x+dx, zz=z+dz;
                if(xx<0||xx>=Width||zz<0||zz>=Height) continue;
                int i=xx+zz*Width; if(!open[i])continue;
                float d=(Point(i)-p).sqrMagnitude; if(d<distance){distance=d;best=i;}
            }
            return best;
        }
        public void Find(Vector3 start, Vector3 goal, List<Vector3> route)
        {
            route.Clear(); int from=Closest(start), to=Closest(goal); if(from<0||to<0)return;
            for(int i=0;i<parent.Length;i++)parent[i]=-1;
            queue.Clear(); queue.Enqueue(from); parent[from]=from;
            while(queue.Count>0)
            {
                int current=queue.Dequeue(); if(current==to)break;
                int cx=current%Width, cz=current/Width;
                for(int dz=-1;dz<=1;dz++) for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dz==0)continue;
                    int nx=cx+dx, nz=cz+dz;
                    if(nx<0||nx>=Width||nz<0||nz>=Height)continue;
                    int n=nx+nz*Width; if(!open[n]||parent[n]>=0)continue;
                    if(dx!=0&&dz!=0&&(!open[cx+dx+cz*Width]||!open[cx+(cz+dz)*Width]))continue;
                    parent[n]=current;queue.Enqueue(n);
                }
            }
            if(parent[to]<0)return;
            raw.Clear();
            for(int node=to;node!=from;node=parent[node])raw.Add(Point(node));
            raw.Reverse();
            // String pulling: skip waypoints while the body can sweep straight to a later one.
            Vector3 at=start;
            int k=0;
            while(k<raw.Count)
            {
                int far=k;
                for(int j=Mathf.Min(raw.Count-1,k+14);j>k;j--)
                    if(Clear(at,raw[j])){far=j;break;}
                route.Add(raw[far]); at=raw[far]; k=far+1;
            }
        }
        private bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 from=new Vector3(a.x,a.y+.7f,a.z), to=new Vector3(b.x,b.y+.7f,b.z), d=to-from;
            return d.sqrMagnitude<.01f || !Physics.SphereCast(from,.3f,d.normalized,out _,d.magnitude,mask,QueryTriggerInteraction.Ignore);
        }
    }
}
