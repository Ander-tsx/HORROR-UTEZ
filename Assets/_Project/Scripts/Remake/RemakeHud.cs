using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HorrorUtez.Remake
{
    // Programmatic uGUI in a low-poly horror style: VT323 (terminal) for information, Creepster for titles
    // (both OFL, in Resources/Fonts). R.E.P.O.-like layout: health/energy top-left, haul top-right, a
    // crosshair that reacts to grabbable gear, value prompts, damage/low-health overlays, a spectator
    // banner when fallen and an upgrade shop between days.
    public sealed class RemakeHud : MonoBehaviour
    {
        private RemakeGame game;
        private Font font, title;
        private RectTransform root;
        private GameObject menu, hud, pause, result, touch, deathBanner;
        private Text cargo, status, clock, vitals, prompt, roster, summary, connection, direction, mic, spectate, energyText, healthText;
        private Image cargoBar, staminaBar, healthBar, crosshair, damage, vignette;
        private InputField nameField, addressField;
        private Button next;
        private readonly Button[] shop = new Button[6];
        private readonly Text[] shopText = new Text[6];
        private bool grab, interact, jump;
        private Vector2 look;
        private int lastHealth = -1;
        private float flash;
        public Vector2 MoveInput { get; private set; }
        public bool RunHeld { get; private set; }
        public bool GrabHeld { get; internal set; }
        public bool CrouchHeld { get; private set; }
        public bool MenuOpen => menu != null && (menu.activeSelf || pause.activeSelf || result.activeSelf);
        public bool TouchEnabled { get; private set; }
        private static readonly Color Bone = new Color(.86f, .84f, .74f), Toxic = new Color(.45f, .95f, .6f), Blood = new Color(.66f, .07f, .05f),
            Dim = new Color(.5f, .55f, .52f), Ink = new Color(.015f, .018f, .02f, .86f), Amber = new Color(1f, .66f, .2f);

        public void Setup(RemakeGame owner)
        {
            game = owner;
            font = Resources.Load<Font>("Fonts/VT323-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title = Resources.Load<Font>("Fonts/Creepster-Regular") ?? font;
            if (FindAnyObjectByType<EventSystem>() == null)
            { var es = new GameObject("Remake EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>(); }
            var canvasGo = new GameObject("Remake UI"); canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scale = canvasGo.AddComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1600, 900); scale.matchWidthOrHeight = .5f;
            canvasGo.AddComponent<GraphicRaycaster>(); root = canvasGo.GetComponent<RectTransform>();

            // ---------------------------------------------------------------- main menu
            menu = Panel("Menu principal", root, new Vector4(0, 0, 1, 1), new Color(0, 0, 0, .35f));
            Overlay(menu.transform, Vignette(), new Color(0, 0, 0, .9f));
            Label(menu.transform, "HORROR", new Vector4(.05f, .74f, .6f, .95f), 120, Blood, TextAnchor.LowerLeft, title);
            Label(menu.transform, "UTEZ", new Vector4(.06f, .6f, .6f, .79f), 104, Bone, TextAnchor.UpperLeft, title);
            Label(menu.transform, "TURNO NOCTURNO  //  RECUPERACIÓN DE EQUIPO", new Vector4(.065f, .55f, .7f, .61f), 30, Toxic);
            RectTransform pane = Panel("Hoja de turno", menu.transform, new Vector4(.06f, .07f, .44f, .53f), Ink).GetComponent<RectTransform>();
            Border(pane, Blood);
            Label(pane, "> NOMBRE DEL ESTUDIANTE", new Vector4(.06f, .87f, .94f, .96f), 22, Dim);
            nameField = Field(pane, "Estudiante", new Vector4(.06f, .76f, .94f, .87f));
            Button(pane, "EXPLORAR SOLO", new Vector4(.06f, .6f, .49f, .72f), () => game.Begin(false, nameField.text));
            Button(pane, "HOSPEDAR · 5", new Vector4(.51f, .6f, .94f, .72f), () => game.Begin(true, nameField.text));
            Label(pane, "> IP DEL ANFITRIÓN · TCP 27777", new Vector4(.06f, .48f, .94f, .56f), 22, Dim);
            addressField = Field(pane, "127.0.0.1", new Vector4(.06f, .37f, .66f, .48f));
            Button(pane, "UNIRME", new Vector4(.68f, .37f, .94f, .48f), () => game.Join(addressField.text, nameField.text));
            Button(pane, "MICRÓFONO", new Vector4(.06f, .21f, .49f, .32f), () => ToggleMic());
            Button(pane, "SALIR", new Vector4(.51f, .21f, .94f, .32f), () => Application.Quit());
            connection = Label(pane, "VERSIÓN EXPERIMENTAL / REMAKE", new Vector4(.06f, .03f, .94f, .18f), 22, Toxic);
            Label(menu.transform, "EL CAMIÓN DE MUDANZA ES\nLA ÚNICA SALIDA.\n\nNO ROMPAS EL EQUIPO.\nNO TE QUEDES ATRÁS.\nNO MIRES AL RECTOR.",
                new Vector4(.6f, .44f, .95f, .9f), 34, Bone, TextAnchor.UpperRight);
            Label(menu.transform, "1-5 ESTUDIANTES · BRAZOS ELÁSTICOS · CAMPUS UTEZ", new Vector4(.55f, .05f, .95f, .1f), 22, Dim, TextAnchor.LowerRight);

            // ---------------------------------------------------------------- gameplay HUD
            hud = Panel("HUD", root, new Vector4(0, 0, 1, 1), Color.clear, false);
            damage = Overlay(hud.transform, null, new Color(.6f, 0, 0, 0));
            vignette = Overlay(hud.transform, Vignette(), new Color(.5f, 0, 0, 0));
            var vit = Panel("Vitales", hud.transform, new Vector4(.02f, .86f, .27f, .975f), Ink); Border(vit.GetComponent<RectTransform>(), Blood);
            healthText = Label(vit.transform, "", new Vector4(.05f, .55f, .95f, .98f), 30, Bone);
            healthBar = Panel("Salud", vit.transform, new Vector4(.05f, .43f, .95f, .53f), Blood, false).GetComponent<Image>();
            energyText = Label(vit.transform, "", new Vector4(.05f, .14f, .95f, .42f), 22, Toxic);
            staminaBar = Panel("Energía", vit.transform, new Vector4(.05f, .06f, .95f, .13f), Toxic, false).GetComponent<Image>();
            var top = Panel("Cuota", hud.transform, new Vector4(.7f, .88f, .98f, .975f), Ink); Border(top.GetComponent<RectTransform>(), Toxic);
            cargo = Label(top.transform, "", new Vector4(.05f, .35f, .95f, .98f), 34, Toxic, TextAnchor.MiddleRight);
            cargoBar = Panel("Progreso", top.transform, new Vector4(.05f, .12f, .95f, .24f), Toxic, false).GetComponent<Image>();
            clock = Label(hud.transform, "", new Vector4(.7f, .82f, .98f, .87f), 26, Bone, TextAnchor.UpperRight);
            roster = Label(hud.transform, "", new Vector4(.76f, .55f, .98f, .8f), 22, Dim, TextAnchor.UpperRight);
            direction = Label(hud.transform, "", new Vector4(.38f, .93f, .62f, .98f), 24, Dim, TextAnchor.MiddleCenter);
            crosshair = Panel("Mira", hud.transform, new Vector4(.4985f, .4975f, .5015f, .5025f), Bone, false).GetComponent<Image>();
            status = Label(hud.transform, "", new Vector4(.2f, .06f, .8f, .11f), 24, Bone, TextAnchor.MiddleCenter);
            prompt = Label(hud.transform, "", new Vector4(.2f, .14f, .8f, .26f), 28, Toxic, TextAnchor.MiddleCenter);
            vitals = Label(hud.transform, "", new Vector4(.02f, .8f, .4f, .855f), 22, Dim);
            mic = Label(hud.transform, "V: HABLAR", new Vector4(.78f, .02f, .98f, .07f), 22, Dim, TextAnchor.LowerRight);
            Label(hud.transform, "WASD mover · Shift correr · Espacio saltar · Ctrl agacharse/barrida · Q rodar · clic/G agarrar · rueda distancia · R girar · E usar · F luz",
                new Vector4(.2f, .005f, .8f, .045f), 17, new Color(.45f, .5f, .47f), TextAnchor.MiddleCenter);
            deathBanner = Panel("Caído", hud.transform, new Vector4(0, .38f, 1, .62f), new Color(0, 0, 0, .55f), false);
            Label(deathBanner.transform, "CAÍSTE", new Vector4(0, .38f, 1, 1), 96, Blood, TextAnchor.MiddleCenter, title);
            spectate = Label(deathBanner.transform, "", new Vector4(0, 0, 1, .38f), 28, Bone, TextAnchor.MiddleCenter);
            deathBanner.SetActive(false);
            BuildTouch();

            // ---------------------------------------------------------------- pause
            pause = Panel("Pausa", root, new Vector4(.3f, .2f, .7f, .8f), Ink); Border(pause.GetComponent<RectTransform>(), Blood);
            Label(pause.transform, "PAUSA", new Vector4(.08f, .8f, .92f, .96f), 64, Blood, TextAnchor.MiddleCenter, title);
            Label(pause.transform, "El turno sigue corriendo para los demás.", new Vector4(.08f, .68f, .92f, .78f), 24, Dim, TextAnchor.MiddleCenter);
            Button(pause.transform, "VOLVER", new Vector4(.08f, .53f, .92f, .64f), () => TogglePause());
            Button(pause.transform, "MICRÓFONO / PERMISO", new Vector4(.08f, .39f, .92f, .5f), () => ToggleMic());
            Button(pause.transform, "CONTROLES TÁCTILES", new Vector4(.08f, .25f, .92f, .36f), () => { TouchEnabled = !TouchEnabled; touch.SetActive(TouchEnabled); });
            Button(pause.transform, "MENÚ PRINCIPAL", new Vector4(.08f, .09f, .92f, .2f), () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));

            // ---------------------------------------------------------------- results + upgrade shop
            result = Panel("Resultado", root, new Vector4(.14f, .07f, .86f, .93f), Ink); Border(result.GetComponent<RectTransform>(), Toxic);
            Label(result.transform, "REPORTE DEL TURNO", new Vector4(.04f, .86f, .96f, .98f), 56, Blood, TextAnchor.MiddleLeft, title);
            summary = Label(result.transform, "", new Vector4(.04f, .5f, .5f, .86f), 25, Bone, TextAnchor.UpperLeft);
            Label(result.transform, "TIENDA DE LA COOPERATIVA", new Vector4(.53f, .8f, .96f, .86f), 26, Toxic);
            for (int i = 0; i < shop.Length; i++)
            {
                int kind = i;
                float y = .71f - i * .095f;
                shop[i] = Button(result.transform, "", new Vector4(.53f, y, .96f, y + .085f), () => game.Request("upgrade", kind));
                shopText[i] = shop[i].GetComponentInChildren<Text>(); shopText[i].alignment = TextAnchor.MiddleLeft; shopText[i].fontSize = 22;
            }
            next = Button(result.transform, "SIGUIENTE DÍA", new Vector4(.04f, .14f, .5f, .25f), () => game.Request("next"));
            Button(result.transform, "MENÚ PRINCIPAL", new Vector4(.04f, .03f, .5f, .12f), () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            hud.SetActive(false); pause.SetActive(false); result.SetActive(false);
        }

        private void ToggleMic()
        {
            game.Voice.SetEnabled(!game.Voice.Enabled);
            SetConnectionStatus(game.Voice.Enabled ? "Micrófono activo. Mantén V para hablar; en móvil, botón VOZ." : "Micrófono desactivado.");
        }
        public void SetConnectionStatus(string text) { if (connection != null) connection.text = text; }
        public void ShowGame() { menu.SetActive(false); hud.SetActive(true); pause.SetActive(false); result.SetActive(false); SetCursor(false); }
        public void TogglePause()
        {
            if (menu.activeSelf || result.activeSelf) return;
            pause.SetActive(!pause.activeSelf); SetCursor(pause.activeSelf); MoveInput = Vector2.zero; look = Vector2.zero; RunHeld = false;
        }
        private void SetCursor(bool menuOn) { Cursor.lockState = menuOn || TouchEnabled ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = menuOn || TouchEnabled; }
        public void Disconnected(string reason)
        {
            menu.SetActive(false); hud.SetActive(false); result.SetActive(true); summary.text = reason;
            foreach (Button b in shop) b.interactable = false; next.interactable = false; SetCursor(true);
        }

        private void Update()
        {
            if (game == null || !game.Started || game.Local == null) return;
            StudentState student = game.Local;
            if (game.Phase == 3 || game.Phase == 4)
            {
                result.SetActive(true); pause.SetActive(false); SetCursor(true);
                string list = "";
                foreach (StudentState s in game.Students.Values) list += s.name + "  ·  " + (s.escaped ? "EN EL CAMIÓN" : "SE QUEDÓ · PIERDE MEJORAS") + "\n";
                summary.text = game.Notice + "\n\n" + list + "\nTU SALDO: $" + student.credits;
                for (int i = 0; i < shop.Length; i++)
                {
                    int level = RemakeGame.UpgradeLevel(student, i), cost = RemakeGame.UpgradeCost(student, i);
                    string pips = "[" + new string('#', level) + new string('.', 3 - level) + "]";
                    shopText[i].text = "  " + RemakeGame.UpgradeNames[i] + "  " + pips + "   " + (level >= 3 ? "MÁXIMO" : "$" + cost) + "\n  " + RemakeGame.UpgradeInfo[i];
                    shop[i].interactable = game.Phase == 3 && student.escaped && level < 3 && student.credits >= cost;
                }
                next.interactable = game.Authority;
                next.GetComponentInChildren<Text>().text = game.Authority ? (game.Phase == 4 ? "REINTENTAR" : "SIGUIENTE DÍA") : "ESPERANDO AL HOST";
                return;
            }
            if (result.activeSelf) { result.SetActive(false); SetCursor(false); }
            float hp = student.health / (float)Mathf.Max(1, student.MaxHealth);
            healthText.text = "SALUD  " + student.health + " / " + student.MaxHealth;
            healthBar.rectTransform.anchorMax = new Vector2(.05f + .9f * hp, .53f);
            float energy = game.Player != null ? game.Player.Stamina / 100 : 1;
            energyText.text = "ENERGÍA  " + Mathf.RoundToInt(game.Player != null ? game.Player.Energy : 0) + (game.Player != null && game.Player.Running ? "  ›››" : "");
            staminaBar.rectTransform.anchorMax = new Vector2(.05f + .9f * energy, .13f);
            staminaBar.color = energy < .2f ? Blood : Toxic;
            cargo.text = "$" + game.Cargo + " / $" + game.Quota;
            cargo.color = game.Cargo >= game.Quota ? Amber : Toxic;
            cargoBar.rectTransform.anchorMax = new Vector2(.05f + .9f * Mathf.Clamp01((float)game.Cargo / game.Quota), .24f);
            clock.text = "DÍA " + game.Day + "  ·  " + (game.Seconds / 60).ToString("00") + ":" + (game.Seconds % 60).ToString("00");
            clock.color = game.Seconds < 60 ? Blood : Bone;
            roster.text = ""; foreach (StudentState s in game.Students.Values) roster.text += s.name + "  " + (s.alive ? s.health + " HP" : "CAÍDO") + "\n";
            vitals.text = "FUERZA " + student.strength + " · ALCANCE " + student.range + " · VEL " + student.speed;
            status.text = game.Notice;

            // Damage flash on every hit, a pulsing red vignette when badly hurt, a banner when fallen.
            if (lastHealth >= 0 && student.health < lastHealth) flash = 1;
            lastHealth = student.health;
            flash = Mathf.MoveTowards(flash, 0, Time.deltaTime * 2.2f);
            damage.color = new Color(.6f, 0, 0, flash * .45f);
            float low = student.alive && hp < .35f ? (.35f - hp) / .35f : 0;
            vignette.color = new Color(.55f, 0, 0, low * (.55f + .35f * Mathf.Sin(Time.time * 6)));
            deathBanner.SetActive(!student.alive);
            if (!student.alive)
                spectate.text = game.Player != null && game.Player.Spectating != null
                    ? "OBSERVANDO A " + game.Player.Spectating.ToUpper() + "  ·  CLIC PARA CAMBIAR\nQue lleven tu credencial al camión para revivirte."
                    : "Que alguien lleve tu credencial al camión para revivirte.";

            if (game.Player != null)
            {
                Vector3 delta = game.TruckPosition - game.Player.transform.position;
                float angle = Vector3.SignedAngle(game.Player.transform.forward, new Vector3(delta.x, 0, delta.z), Vector3.up);
                direction.text = (Mathf.Abs(angle) < 30 ? "^" : angle > 0 ? ">" : "<") + " CAMIÓN " + Mathf.RoundToInt(delta.magnitude) + " m";
                RemakeLoot item = game.Player.AimedLoot;
                bool holding = student.held >= 0 && student.held < game.Loot.Count;
                float size = holding ? .006f : item != null ? .0045f : .0018f;
                crosshair.rectTransform.anchorMin = new Vector2(.5f - size * .5625f, .5f - size);
                crosshair.rectTransform.anchorMax = new Vector2(.5f + size * .5625f, .5f + size);
                crosshair.color = holding ? Toxic : item != null ? Amber : new Color(Bone.r, Bone.g, Bone.b, .7f);
                if (holding)
                {
                    item = game.Loot[student.held];
                    prompt.text = item.Label + "  ·  " + item.Body.mass + " kg" + (item.Value > 0 ? "  ·  $" + item.Value : "") + "\nRUEDA distancia · R girar · suelta para dejar";
                }
                else if (game.InTruck(game.Player.transform.position))
                    prompt.text = game.Cargo >= game.Quota ? "[E] ARRANCAR EL CAMIÓN" : "Acomoda y suelta la carga. Faltan $" + (game.Quota - game.Cargo);
                else if (item != null)
                    prompt.text = item.Label + (item.Value > 0 ? "  ·  $" + item.Value : "") + "\n" + (item.Kind == RemakeLoot.CredentialKind ? "LLÉVALA AL CAMIÓN PARA REVIVIR"
                        : item.Kind == RemakeLoot.CartKind ? "AGARRA EL ASA PARA EMPUJAR" : item.Body.mass > 24 ? "PESADO · PIDE AYUDA" : "[CLIC] AGARRAR");
                else if (game.Player.AimedDoor != null) prompt.text = "[E] PUERTA";
                else prompt.text = "";
            }
            mic.text = game.Voice.Status;
        }

        private void BuildTouch()
        {
            touch = Panel("Controles móviles", hud.transform, new Vector4(0, 0, 1, 1), Color.clear, false);
            var joystick = Panel("Mover", touch.transform, new Vector4(.015f, .02f, .235f, .36f), new Color(.08f, .12f, .1f, .28f));
            Label(joystick.transform, "MOVER", new Vector4(0, 0, 1, 1), 26, Toxic, TextAnchor.MiddleCenter);
            joystick.AddComponent<RemakeTouchPad>().Setup(p => MoveInput = Vector2.ClampMagnitude(p / (Screen.height * .12f), 1), () => MoveInput = Vector2.zero, false);
            var aim = Panel("Mirar", touch.transform, new Vector4(.35f, .28f, .99f, .87f), new Color(0, 0, 0, .001f));
            aim.AddComponent<RemakeTouchPad>().Setup(p => look += p * .09f, () => { }, true);
            HoldButton(touch.transform, "AGARRAR", new Vector4(.79f, .14f, .98f, .25f), held => GrabHeld = held);
            Button(touch.transform, "USAR", new Vector4(.79f, .015f, .98f, .125f), () => interact = true);
            Button(touch.transform, "SALTAR", new Vector4(.6f, .015f, .77f, .125f), () => jump = true);
            Button(touch.transform, "LUZ", new Vector4(.6f, .14f, .77f, .25f), () => game.Player?.ToggleTorch());
            Button(touch.transform, "MENÚ", new Vector4(.4f, .015f, .58f, .125f), () => TogglePause());
            HoldButton(touch.transform, "CORRER", new Vector4(.25f, .14f, .42f, .25f), held => RunHeld = held);
            HoldButton(touch.transform, "VOZ", new Vector4(.25f, .015f, .39f, .125f), held => game.Voice.TouchTalking = held);
            TouchEnabled = Application.isMobilePlatform || Array.IndexOf(Environment.GetCommandLineArgs(), "-remakeTouch") >= 0;
            touch.SetActive(TouchEnabled);
        }
        public bool ConsumeGrab() { bool value = grab; grab = false; return value; }
        public bool ConsumeInteract() { bool value = interact; interact = false; return value; }
        public bool ConsumeJump() { bool value = jump; jump = false; return value; }
        public Vector2 ConsumeLook() { Vector2 value = look; look = Vector2.zero; return value; }

        // ---------------------------------------------------------------- widgets
        private static Texture2D vignetteTexture;
        private static Texture2D Vignette()
        {
            if (vignetteTexture != null) return vignetteTexture;
            const int s = 128;
            vignetteTexture = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dx = (x + .5f) / s * 2 - 1, dy = (y + .5f) / s * 2 - 1;
                float a = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - .55f) / .6f);
                vignetteTexture.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            vignetteTexture.Apply();
            return vignetteTexture;
        }
        private Image Overlay(Transform parent, Texture2D texture, Color color)
        {
            var go = new GameObject("Capa", typeof(RectTransform)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), new Vector4(0, 0, 1, 1));
            var image = go.AddComponent<Image>(); image.raycastTarget = false; image.color = color;
            if (texture != null) image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            return image;
        }
        private void Border(RectTransform rect, Color color)
        {
            Color c = new Color(color.r, color.g, color.b, .8f);
            Panel("Borde", rect, new Vector4(0, 0, 1, .012f), c, false); Panel("Borde", rect, new Vector4(0, .988f, 1, 1), c, false);
            Panel("Borde", rect, new Vector4(0, 0, .006f, 1), c, false); Panel("Borde", rect, new Vector4(.994f, 0, 1, 1), c, false);
        }
        private GameObject Panel(string name, Transform parent, Vector4 area, Color color, bool raycast = true)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), area);
            var image = go.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return go;
        }
        private Text Label(Transform parent, string text, Vector4 area, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, Font face = null)
        {
            var go = new GameObject("Texto", typeof(RectTransform)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), area);
            var label = go.AddComponent<Text>(); label.font = face ?? font; label.fontSize = size; label.text = text; label.color = color; label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow; label.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .85f); shadow.effectDistance = new Vector2(2, -2);
            return label;
        }
        private Button Button(Transform parent, string label, Vector4 area, Action action)
        {
            GameObject go = Panel(label, parent, area, new Color(.16f, .02f, .02f, .92f)); var button = go.AddComponent<Button>();
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.6f, 1.2f, 1.1f);
            colors.pressedColor = new Color(.6f, 1.4f, .8f); colors.disabledColor = new Color(.35f, .35f, .35f, .7f); button.colors = colors;
            Border(go.GetComponent<RectTransform>(), Blood);
            Label(go.transform, label, new Vector4(.03f, .05f, .97f, .95f), 26, Bone, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => { game?.Audio?.Play2D("ui_click", .6f); action(); });
            return button;
        }
        private void HoldButton(Transform parent, string label, Vector4 area, Action<bool> action)
        {
            Button button = Button(parent, label, area, () => { }); button.gameObject.AddComponent<RemakeHoldButton>().Changed = action;
        }
        private InputField Field(Transform parent, string value, Vector4 area)
        {
            GameObject go = Panel("Entrada", parent, area, new Color(.02f, .06f, .04f, .95f));
            Border(go.GetComponent<RectTransform>(), Toxic);
            Text text = Label(go.transform, value, new Vector4(.04f, 0, .96f, 1), 28, Toxic);
            var field = go.AddComponent<InputField>(); field.textComponent = text; field.text = value; field.characterLimit = 64; return field;
        }
        private static void Place(RectTransform rect, Vector4 area)
        { rect.anchorMin = new Vector2(area.x, area.y); rect.anchorMax = new Vector2(area.z, area.w); rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
    public sealed class RemakeTouchPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private Action<Vector2> moved; private Action released; private bool delta; private Vector2 origin; private int pointer = int.MinValue;
        public void Setup(Action<Vector2> move, Action release, bool look) { moved = move; released = release; delta = look; }
        public void OnPointerDown(PointerEventData e) { if (pointer != int.MinValue) return; pointer = e.pointerId; origin = e.position; }
        public void OnDrag(PointerEventData e) { if (e.pointerId == pointer) moved(delta ? e.delta : e.position - origin); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId != pointer) return; pointer = int.MinValue; released(); }
        private void OnDisable() { pointer = int.MinValue; released?.Invoke(); }
    }
    public sealed class RemakeHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action<bool> Changed;
        public void OnPointerDown(PointerEventData e) => Changed?.Invoke(true);
        public void OnPointerUp(PointerEventData e) => Changed?.Invoke(false);
        private void OnDisable() => Changed?.Invoke(false);
    }
}
