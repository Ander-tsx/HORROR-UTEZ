using System;
using UnityEngine;

namespace HorrorUtez.Core
{
    /// <summary>
    /// Player-facing settings, persisted per machine and applied the moment they change.
    ///
    /// PlayerPrefs rather than a settings asset: these are one person's preferences, not
    /// project data, and they must never turn up in someone else's diff. Static rather
    /// than a MonoBehaviour singleton so a value can be read before any scene has loaded —
    /// the frame cap and the volume have to be right on the very first frame, not after a
    /// menu has been opened once.
    ///
    /// The PSX filter is deliberately NOT here; it lives in HorrorUtez.Rendering.PsxLook,
    /// which owns everything the render features read. This assembly must stay free of a
    /// URP dependency.
    /// </summary>
    public static class GameSettings
    {
        private const string VolumeKey = "HorrorUtez.Settings.MasterVolume";
        private const string SensitivityKey = "HorrorUtez.Settings.LookSensitivity";
        private const string InvertYKey = "HorrorUtez.Settings.InvertY";
        private const string FrameCapKey = "HorrorUtez.Settings.FrameCap";
        private const string FullscreenKey = "HorrorUtez.Settings.Fullscreen";
        private const string ResolutionKey = "HorrorUtez.Settings.ResolutionIndex";

        /// <summary>
        /// Selectable frame caps. 0 means "no cap" — hand it to Unity as -1.
        ///
        /// This matters more on a phone than on a desktop: an uncapped renderer will
        /// happily cook the battery drawing frames nobody asked for, and a horror game at
        /// a steady 30 reads better than one oscillating between 45 and 80.
        /// </summary>
        public static readonly int[] FrameCaps = { 30, 60, 120, 0 };

        private static bool _loaded;
        private static float _masterVolume;
        private static float _lookSensitivity;
        private static bool _invertY;
        private static int _frameCapIndex;

        /// <summary>Raised after any setting changes, so open UI can refresh itself.</summary>
        public static event Action Changed;

        /// <summary>0..1, straight onto the AudioListener.</summary>
        public static float MasterVolume
        {
            get { Load(); return _masterVolume; }
            set
            {
                Load();
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(_masterVolume, value)) return;
                _masterVolume = value;
                PlayerPrefs.SetFloat(VolumeKey, value);
                Apply();
            }
        }

        /// <summary>
        /// Multiplier over the per-device look speeds in PlayerInputReader. One global knob
        /// covers mouse, stick and touch, so nobody has to tune three numbers to fix one
        /// complaint.
        /// </summary>
        public static float LookSensitivity
        {
            get { Load(); return _lookSensitivity; }
            set
            {
                Load();
                value = Mathf.Clamp(value, 0.25f, 3f);
                if (Mathf.Approximately(_lookSensitivity, value)) return;
                _lookSensitivity = value;
                PlayerPrefs.SetFloat(SensitivityKey, value);
                Apply();
            }
        }

        public static bool InvertY
        {
            get { Load(); return _invertY; }
            set
            {
                Load();
                if (_invertY == value) return;
                _invertY = value;
                PlayerPrefs.SetInt(InvertYKey, value ? 1 : 0);
                Apply();
            }
        }

        /// <summary>Index into <see cref="FrameCaps"/>.</summary>
        public static int FrameCapIndex
        {
            get { Load(); return _frameCapIndex; }
            set
            {
                Load();
                value = Mathf.Clamp(value, 0, FrameCaps.Length - 1);
                if (_frameCapIndex == value) return;
                _frameCapIndex = value;
                PlayerPrefs.SetInt(FrameCapKey, value);
                Apply();
            }
        }

        public static int FrameCap => FrameCaps[FrameCapIndex];

        /// <summary>Label for the current cap, for the menu to show.</summary>
        public static string FrameCapLabel => FrameCap == 0 ? "Sin límite" : $"{FrameCap} FPS";

        /// <summary>
        /// Whether this platform has a windowing system worth exposing. On a phone the
        /// window is the screen, so a fullscreen toggle is a row that does nothing.
        /// </summary>
        public static bool SupportsWindowing =>
            Application.platform != RuntimePlatform.Android &&
            Application.platform != RuntimePlatform.IPhonePlayer;

        public static bool Fullscreen
        {
            get { Load(); return Screen.fullScreen; }
            set
            {
                Load();
                if (Screen.fullScreen == value) return;
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                Screen.fullScreenMode = value
                    ? FullScreenMode.FullScreenWindow
                    : FullScreenMode.Windowed;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Distinct width x height pairs the display reports, coarsest first.
        ///
        /// Screen.resolutions lists every refresh rate as its own entry, so a 144 Hz
        /// monitor returns the same handful of sizes a dozen times over. Refresh rate is
        /// not something this menu offers, so the duplicates are collapsed.
        /// </summary>
        public static Vector2Int[] Resolutions
        {
            get
            {
                if (_resolutions != null)
                    return _resolutions;

                var seen = new System.Collections.Generic.List<Vector2Int>();
                foreach (var res in Screen.resolutions)
                {
                    var size = new Vector2Int(res.width, res.height);
                    if (!seen.Contains(size))
                        seen.Add(size);
                }

                // A headless or unusual display can report nothing at all; fall back to the
                // window we already have rather than handing the menu an empty list.
                if (seen.Count == 0)
                    seen.Add(new Vector2Int(Screen.width, Screen.height));

                seen.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
                _resolutions = seen.ToArray();
                return _resolutions;
            }
        }

        private static Vector2Int[] _resolutions;

        public static int ResolutionIndex
        {
            get
            {
                Load();
                var list = Resolutions;
                int stored = PlayerPrefs.GetInt(ResolutionKey, -1);
                if (stored >= 0 && stored < list.Length)
                    return stored;

                // Nothing stored yet: report whichever entry matches the current window.
                for (int i = 0; i < list.Length; i++)
                    if (list[i].x == Screen.width && list[i].y == Screen.height)
                        return i;
                return list.Length - 1;
            }
            set
            {
                Load();
                var list = Resolutions;
                value = Mathf.Clamp(value, 0, list.Length - 1);
                PlayerPrefs.SetInt(ResolutionKey, value);
                Screen.SetResolution(list[value].x, list[value].y, Screen.fullScreenMode);
                Changed?.Invoke();
            }
        }

        public static string ResolutionLabel
        {
            get
            {
                var size = Resolutions[ResolutionIndex];
                return $"{size.x} x {size.y}";
            }
        }

        /// <summary>
        /// Pushes everything onto Unity before the first scene runs.
        ///
        /// Without this the settings only take effect once the pause menu has been opened,
        /// which means the first session on a new machine ignores every one of them.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnBoot()
        {
            Load();
            Apply();
        }

        private static void Load()
        {
            if (_loaded)
                return;

            _loaded = true;
            _masterVolume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            _lookSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            _invertY = PlayerPrefs.GetInt(InvertYKey, 0) != 0;
            // Default to 60: high enough to feel modern, low enough not to melt a phone.
            _frameCapIndex = Mathf.Clamp(PlayerPrefs.GetInt(FrameCapKey, 1), 0, FrameCaps.Length - 1);
        }

        private static void Apply()
        {
            AudioListener.volume = _masterVolume;

            // vSync overrides targetFrameRate entirely, so the cap is meaningless until it
            // is off. Turning it off is also what lets a phone hold a steady 30.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameCap == 0 ? -1 : FrameCap;

            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
