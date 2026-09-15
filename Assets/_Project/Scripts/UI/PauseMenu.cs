using HorrorUtez.Core;
using HorrorUtez.Player;
using HorrorUtez.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HorrorUtez.UI
{
    /// <summary>
    /// The pause screen and, for now, the whole settings screen.
    ///
    /// It builds its own hierarchy in Awake rather than being authored as a prefab. That is
    /// a deliberate call for this phase: a script-built menu is one file to read and review,
    /// it produces no serialised hierarchy to bloat the scene diff, and it cannot drift out
    /// of sync with the settings it drives — adding a row means adding a line, not clicking
    /// through an inspector and remembering to wire a callback. When the menu grows art it
    /// should become a prefab; until then this is cheaper in every direction.
    ///
    /// It listens to <see cref="PauseController"/> rather than being driven by it, so
    /// pausing keeps working with no UI in the scene at all.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        // Oxblood and bone, taken off the CECADEC facade so the menu belongs to the game.
        private static readonly Color Ink = new(0.87f, 0.86f, 0.82f);
        private static readonly Color Dim = new(0.02f, 0.02f, 0.03f, 0.72f);
        private static readonly Color PanelFill = new(0.09f, 0.08f, 0.09f, 0.96f);
        private static readonly Color ControlFill = new(0.17f, 0.16f, 0.17f, 1f);
        private static readonly Color Accent = new(0.62f, 0.14f, 0.12f, 1f);

        private const int RowHeight = 52;
        private const int LabelSize = 22;

        private Canvas _canvas;
        private CanvasGroup _group;

        private Toggle _psxToggle;
        private Toggle _invertToggle;
        private Toggle _fullscreenToggle;
        private Slider _volumeSlider;
        private Text _volumeValue;
        private Slider _sensitivitySlider;
        private Text _sensitivityValue;
        private Text _frameCapValue;
        private Text _resolutionValue;

        /// <summary>Guards the callbacks against the refresh that their own writes trigger.</summary>
        private bool _refreshing;

        private void Awake()
        {
            EnsureEventSystem();
            Build();
            SetVisible(PauseController.IsPaused);
        }

        private void OnEnable()
        {
            PauseController.PauseChanged += SetVisible;
            GameSettings.Changed += Refresh;
            PsxLook.Changed += OnPsxChanged;
        }

        private void OnDisable()
        {
            PauseController.PauseChanged -= SetVisible;
            GameSettings.Changed -= Refresh;
            PsxLook.Changed -= OnPsxChanged;
        }

        private void OnPsxChanged(bool _) => Refresh();

        private void SetVisible(bool visible)
        {
            // Alpha alone leaves the canvas rendering an invisible menu every frame, and
            // toggling the GameObject throws away the built mesh. Disabling the Canvas keeps
            // the mesh and stops the work.
            _canvas.enabled = visible;
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;

            if (visible)
                Refresh();
        }

        // ---- Construction -------------------------------------------------------

        /// <summary>
        /// uGUI needs an EventSystem to route clicks, and with the new Input System it needs
        /// the InputSystemUIInputModule specifically — the legacy StandaloneInputModule
        /// throws outright. Creating it here keeps the menu self-contained.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above anything the game might add later, and outside the PSX blit, which runs
            // before post-processing — overlay UI is never touched by the CRT pass, so the
            // menu stays readable with the filter on.
            _canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // 0.5 so the menu shrinks with the narrower axis on a phone held either way.
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();

            // Full-screen scrim. It is a raycast target on purpose: it swallows clicks that
            // would otherwise reach the game behind the menu.
            var dim = Panel("Dim", transform, Dim);
            Stretch(dim);

            var panel = Panel("Panel", dim, PanelFill);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 0f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // The panel is built once and never changes size afterwards, so the fitter is a
            // one-off cost rather than the per-frame rebuild the uGUI guidance warns about.
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Title(panel, "PAUSA");
            Caption(panel, "Ajustes — fase de desarrollo");

            _psxToggle = ToggleRow(panel, "Filtro PSX", value => PsxLook.Enabled = value);
            Caption(panel, "Apagado por defecto: el filtro estorba al modelar y colocar props.");

            (_volumeSlider, _volumeValue) = SliderRow(panel, "Volumen", 0f, 1f,
                value => GameSettings.MasterVolume = value);

            (_sensitivitySlider, _sensitivityValue) = SliderRow(panel, "Sensibilidad", 0.25f, 3f,
                value => GameSettings.LookSensitivity = value);

            _invertToggle = ToggleRow(panel, "Invertir eje Y", value => GameSettings.InvertY = value);

            _frameCapValue = CycleRow(panel, "Límite de FPS", () =>
                GameSettings.FrameCapIndex = (GameSettings.FrameCapIndex + 1) % GameSettings.FrameCaps.Length);

            // A phone's window is the screen. Offering to resize it is offering a row that
            // does nothing, which is worse than not offering it.
            if (GameSettings.SupportsWindowing)
            {
                _resolutionValue = CycleRow(panel, "Resolución", () =>
                    GameSettings.ResolutionIndex =
                        (GameSettings.ResolutionIndex + 1) % GameSettings.Resolutions.Length);

                _fullscreenToggle = ToggleRow(panel, "Pantalla completa",
                    value => GameSettings.Fullscreen = value);
            }

            ButtonRow(panel, "Reanudar", PauseController.Resume);
        }

        // ---- Row builders -------------------------------------------------------

        private static RectTransform Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text Label(string name, Transform parent, string content, int size,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            // TextMeshPro needs its Essential Resources imported by hand before it can draw
            // anything, and nobody has. The legacy runtime font is always present.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Ink;
            text.alignment = anchor;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            // Nothing here is clickable, and every raycast target costs a hit test.
            text.raycastTarget = false;
            return text;
        }

        private static void Title(RectTransform parent, string content)
        {
            var text = Label("Title", parent, content, 38, TextAnchor.MiddleCenter);
            SetHeight(text.rectTransform, 60);
        }

        private static void Caption(RectTransform parent, string content)
        {
            var text = Label("Caption", parent, content, 15, TextAnchor.MiddleLeft);
            text.color = new Color(Ink.r, Ink.g, Ink.b, 0.45f);
            SetHeight(text.rectTransform, 22);
        }

        private static void SetHeight(RectTransform rect, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        /// <summary>A row with the name on the left and room for a control on the right.</summary>
        private static RectTransform Row(RectTransform parent, string label, float controlWidth)
        {
            var go = new GameObject($"Row_{label}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            SetHeight(rect, RowHeight);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var text = Label("Label", rect, label, LabelSize);
            var stretchLabel = text.gameObject.AddComponent<LayoutElement>();
            stretchLabel.flexibleWidth = 1f;

            var control = new GameObject("Control", typeof(RectTransform));
            control.transform.SetParent(rect, false);
            var controlElement = control.AddComponent<LayoutElement>();
            controlElement.preferredWidth = controlWidth;
            controlElement.minWidth = controlWidth;
            return (RectTransform)control.transform;
        }

        private static Toggle ToggleRow(RectTransform parent, string label,
            UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var control = Row(parent, label, 40f);

            var box = Panel("Box", control, ControlFill);
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.sizeDelta = new Vector2(34f, 34f);

            var check = Panel("Check", box, Accent);
            check.anchorMin = Vector2.zero;
            check.anchorMax = Vector2.one;
            check.offsetMin = new Vector2(6f, 6f);
            check.offsetMax = new Vector2(-6f, -6f);
            check.GetComponent<Image>().raycastTarget = false;

            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        private static (Slider, Text) SliderRow(RectTransform parent, string label,
            float min, float max, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var control = Row(parent, label, 300f);

            var track = Panel("Track", control, ControlFill);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(0f, -9f);
            track.offsetMax = new Vector2(-64f, 9f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(track, false);
            Stretch((RectTransform)fillArea.transform);

            var fill = Panel("Fill", (RectTransform)fillArea.transform, Accent);
            fill.GetComponent<Image>().raycastTarget = false;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            var slider = track.gameObject.AddComponent<Slider>();
            slider.targetGraphic = track.GetComponent<Image>();
            slider.fillRect = fill;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(onChanged);

            var value = Label("Value", control, "", LabelSize, TextAnchor.MiddleRight);
            value.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            value.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            value.rectTransform.pivot = new Vector2(1f, 0.5f);
            value.rectTransform.sizeDelta = new Vector2(58f, RowHeight);

            return (slider, value);
        }

        /// <summary>A button that steps through a fixed list and shows where it landed.</summary>
        private static Text CycleRow(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var control = Row(parent, label, 300f);

            var box = Panel("Box", control, ControlFill);
            Stretch(box);
            box.offsetMin = new Vector2(0f, 9f);
            box.offsetMax = new Vector2(0f, -9f);

            var text = Label("Value", box, "", LabelSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);

            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            return text;
        }

        private static void ButtonRow(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var box = Panel($"Button_{label}", parent, Accent);
            SetHeight(box, 58);

            var text = Label("Label", box, label, 24, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);

            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(onClick);
        }

        // ---- State --------------------------------------------------------------

        /// <summary>
        /// Pushes current values into the widgets. Guarded because writing a slider raises
        /// its own onValueChanged, which would write the setting straight back.
        /// </summary>
        private void Refresh()
        {
            if (_refreshing)
                return;

            _refreshing = true;
            try
            {
                _psxToggle.isOn = PsxLook.Enabled;
                _invertToggle.isOn = GameSettings.InvertY;

                _volumeSlider.value = GameSettings.MasterVolume;
                _volumeValue.text = $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%";

                _sensitivitySlider.value = GameSettings.LookSensitivity;
                _sensitivityValue.text = $"{GameSettings.LookSensitivity:0.00}x";

                _frameCapValue.text = GameSettings.FrameCapLabel;

                if (_resolutionValue != null)
                    _resolutionValue.text = GameSettings.ResolutionLabel;
                if (_fullscreenToggle != null)
                    _fullscreenToggle.isOn = GameSettings.Fullscreen;
            }
            finally
            {
                _refreshing = false;
            }
        }
    }
}
