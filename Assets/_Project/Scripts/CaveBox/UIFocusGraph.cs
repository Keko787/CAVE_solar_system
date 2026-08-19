using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// Which chips focus can currently reach. Chips add themselves on enable and drop out on disable, so the
    /// graph is always "what is on screen right now" with nothing to scan and nothing to invalidate.
    ///
    /// That falls out of an existing decision rather than needing a new one: <c>PanelModeSwitcher</c> already
    /// toggles a whole mode's content <see cref="GameObject"/> off — it has to, or the hidden mode's colliders
    /// would swallow wand clicks meant for the visible one — so a hidden mode's chips are disabled, and
    /// disabled chips are exactly the ones focus must not be able to walk onto.
    /// </summary>
    public static class UIFocusGraph
    {
        static readonly List<WandButton> s_Buttons = new();

        /// <summary>Every chip currently registered, in registration order.</summary>
        public static IReadOnlyList<WandButton> Buttons => s_Buttons;

        /// <summary>How many chips are registered.</summary>
        public static int Count => s_Buttons.Count;

        // Enter-play-mode-without-domain-reload would otherwise carry the previous session's chips over.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Buttons.Clear();

        /// <summary>Add a chip. Called from <see cref="WandButton"/>'s OnEnable; harmless to repeat.</summary>
        public static void Register(WandButton button)
        {
            if (button != null && !s_Buttons.Contains(button))
                s_Buttons.Add(button);
        }

        /// <summary>Remove a chip. Called from <see cref="WandButton"/>'s OnDisable.</summary>
        public static void Unregister(WandButton button) => s_Buttons.Remove(button);

        /// <summary>Drop every chip. For tests and teardown.</summary>
        public static void Clear() => s_Buttons.Clear();

        /// <summary>
        /// Collect the reachable chips, optionally only those in one panel group. Also drops any chip that
        /// was destroyed without its OnDisable running, so a torn-down panel cannot strand focus.
        /// </summary>
        /// <param name="group">Panel group to filter to, or null for "chips with no group".</param>
        /// <param name="anyGroup">Ignore <paramref name="group"/> and take every reachable chip.</param>
        public static void Collect(List<WandButton> into, Transform group, bool anyGroup = false)
        {
            if (into == null)
                return;
            into.Clear();

            for (int i = s_Buttons.Count - 1; i >= 0; i--)
            {
                var button = s_Buttons[i];
                if (button == null)
                {
                    s_Buttons.RemoveAt(i);   // destroyed out from under us
                    continue;
                }
                if (!button.isActiveAndEnabled)
                    continue;
                if (!anyGroup && button.FocusGroup != group)
                    continue;
                into.Add(button);
            }

            into.Reverse();   // RemoveAt forced a backwards walk; hand back registration order
        }

        /// <summary>
        /// The distinct panel groups that currently have reachable chips, in registration order. This is
        /// what the bumpers cycle through — directional steps stay inside one panel, because chips on the
        /// front wall and chips on the floor share no sensible up or left.
        /// </summary>
        public static void Groups(List<Transform> into)
        {
            if (into == null)
                return;
            into.Clear();

            for (int i = 0; i < s_Buttons.Count; i++)
            {
                var button = s_Buttons[i];
                if (button == null || !button.isActiveAndEnabled)
                    continue;
                var group = button.FocusGroup;
                if (!into.Contains(group))
                    into.Add(group);
            }
        }
    }
}
