using System;
using UnityEngine;

namespace HorrorUtez.Rendering
{
    /// <summary>
    /// Single on/off switch for the whole PS1 look, and the one place that remembers it.
    ///
    /// Off by default, on purpose. The filter is what the game ships with, but it is also
    /// what makes the editor hard to work in: the CRT warp, the 240p crunch and the vertex
    /// jitter all fight anyone trying to judge geometry or place a prop. Making the look
    /// opt-in costs nothing at runtime and gets it out of the way while the campus is still
    /// being built.
    ///
    /// The value lives in PlayerPrefs rather than in a scene or a setting asset because it
    /// is a per-machine preference, not project data — one person's editor being clean must
    /// not turn up in someone else's diff.
    /// </summary>
    public static class PsxLook
    {
        private const string PrefKey = "HorrorUtez.PsxLook.Enabled";

        private static bool _enabled;
        private static bool _loaded;

        /// <summary>
        /// Raised whenever the switch flips. Both the render features and the material
        /// override listen; UI listens too so a toggle drawn elsewhere stays in sync.
        /// </summary>
        public static event Action<bool> Changed;

        /// <summary>True when the PS1 filter should be applied. Defaults to false.</summary>
        public static bool Enabled
        {
            get
            {
                if (!_loaded)
                {
                    // PlayerPrefs is unavailable before the player loop starts in some
                    // contexts, so the read is deferred to first use rather than done in a
                    // static constructor.
                    _enabled = PlayerPrefs.GetInt(PrefKey, 0) != 0;
                    _loaded = true;
                }
                return _enabled;
            }
            set
            {
                if (_loaded && _enabled == value)
                    return;

                _enabled = value;
                _loaded = true;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke(value);
            }
        }

        public static void Toggle() => Enabled = !Enabled;

        private const string StyleKey = "HorrorUtez.PsxLook.Style";
        private static PsxVisualStyle _style;
        private static bool _styleLoaded;

        /// <summary>Raised when the visual style changes. PsxStyleApplier and the menu listen.</summary>
        public static event Action<PsxVisualStyle> StyleChanged;

        /// <summary>Which of the looks in <see cref="PsxStylePresets"/> is active. Defaults to RetroPixel.</summary>
        public static PsxVisualStyle Style
        {
            get
            {
                if (!_styleLoaded)
                {
                    _style = (PsxVisualStyle)Math.Clamp(PlayerPrefs.GetInt(StyleKey, 0), 0, PsxStylePresets.Count - 1);
                    _styleLoaded = true;
                }
                return _style;
            }
            set
            {
                if (_styleLoaded && _style == value)
                    return;
                _style = value;
                _styleLoaded = true;
                PlayerPrefs.SetInt(StyleKey, (int)value);
                PlayerPrefs.Save();
                StyleChanged?.Invoke(value);
            }
        }

        public static void NextStyle() => Style = (PsxVisualStyle)(((int)Style + 1) % PsxStylePresets.Count);
    }
}
