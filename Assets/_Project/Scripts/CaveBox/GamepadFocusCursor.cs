using System.Collections.Generic;
using Stadium3D.Operators;
using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// The secondary operator's way around the room: D-pad or left stick walks focus between chips, A clicks
    /// the focused one, B drops focus, and the bumpers move between panels. No aiming — which is the point.
    /// The primary operator keeps pointing the wand at whatever they like at the same time; the two never
    /// contend, because focus and hover are separate marks on <see cref="WandButton"/> and this cursor never
    /// touches hover.
    /// </summary>
    /// <remarks>
    /// Deliberately thin: the reachable set lives in <see cref="UIFocusGraph"/>, the choice of where a step
    /// lands in <see cref="FocusNavigator"/>, and the stick's feel in <see cref="StickDirectionLatch"/> —
    /// all three testable without a pad. What is left here is projecting world positions into the focused
    /// chip's panel plane, which is the one part that genuinely needs transforms.
    /// </remarks>
    [DefaultExecutionOrder(-500)]   // after the input sources sample (-1000), before ordinary consumers
    public class GamepadFocusCursor : MonoBehaviour
    {
        [SerializeField, Tooltip("Which operator drives focus navigation. The pad is the secondary operator.")]
        OperatorRole m_Role = OperatorRole.Secondary;

        [SerializeField, Tooltip("Button/axis translation for this pad. Leave empty for the Unity Input " +
            "System backend, which already reports controls in canonical order.")]
        GamepadMap m_Map;

        [SerializeField, Tooltip("Step focus with the left stick as well as the D-pad.")]
        bool m_UseLeftStick = true;

        [SerializeField, Tooltip("Come back round from the far side when there is nothing further in the " +
            "direction of travel.")]
        bool m_Wrap = true;

        [Header("Stick feel")]
        [SerializeField, Range(0.05f, 1f)] float m_StickThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] float m_StickRearm = 0.3f;
        [SerializeField, Min(0f)] float m_FirstRepeatDelay = 0.45f;
        [SerializeField, Min(0.01f)] float m_RepeatInterval = 0.14f;

        [Header("Diagnostics")]
        [SerializeField, Tooltip("Narrate what this cursor is doing. Without it, 'the cursor is not in the " +
            "scene', 'it has no pad', and 'it has no chips to focus' all look identical from the outside — " +
            "they are silence. Leave on until the rig is wired and behaving.")]
        bool m_LogActivity = true;

        [SerializeField, Tooltip("If this operator's own device is absent, read the wand instead. On a desk " +
            "with no ART hardware, MiddleVR's DirectInput driver claims the pad and presents it AS the wand " +
            "— the pad is then working perfectly, just arriving on the other path, and without this the " +
            "cursor sits inert next to a live device. Turn OFF for the CAVE, where the wand is a real " +
            "Flystick belonging to the primary operator and must not drive this cursor.")]
        bool m_FallBackToWand = true;

        readonly List<WandButton> m_Candidates = new();
        readonly List<Vector2> m_Points = new();
        readonly List<Transform> m_Groups = new();

        PadOperator m_Pad;
        IOperatorInput m_Source;
        StickDirectionLatch m_Latch;
        WandButton m_Focused;
        Transform m_Group;
        bool m_WarnedNoSource;
        bool m_UsingWandFallback;

        /// <summary>The chip focus is on, or null.</summary>
        public WandButton Focused => m_Focused;

        /// <summary>Which operator drives this cursor.</summary>
        public OperatorRole Role => m_Role;

        // Announces itself so its absence from the scene is visible as an absence, not as inaction.
        void OnEnable()
        {
            if (m_LogActivity)
                Debug.Log($"[FocusCursor] alive — operator {m_Role}, map " +
                          $"{(m_Map != null ? m_Map.name : "(identity)")}. Press a direction to bring focus in.",
                          this);
        }

        void OnDisable() => ClearFocus();

        void Update()
        {
            var source = ResolveSource();
            if (source == null || !source.IsConnected)
            {
                if (m_LogActivity && !m_WarnedNoSource)
                {
                    m_WarnedNoSource = true;
                    Debug.LogWarning($"[FocusCursor] no connected input source for {m_Role}. Add a pad " +
                                     "source component with that role — Stadium3D/Build/Gamepad Operator Rig " +
                                     "does it.", this);
                }
                ClearFocus();       // no pad: no ring parked on a chip nobody can move
                m_Source = null;
                return;
            }

            m_WarnedNoSource = false;

            if (!ReferenceEquals(source, m_Source))
            {
                m_Source = source;
                m_Pad ??= new PadOperator(source, m_Map != null ? m_Map.Mapping : null);
                m_Pad.SetDevice(source);
                m_Latch ??= new StickDirectionLatch(m_StickThreshold, m_StickRearm,
                    m_FirstRepeatDelay, m_RepeatInterval);
                m_Latch.Reset();

                if (m_LogActivity)
                    Debug.Log($"[FocusCursor] reading '{source.DeviceName}' for {m_Role}; " +
                              $"{UIFocusGraph.Count} chip(s) currently registered.", this);
            }

            // The focused chip can vanish under us — a panel switching modes disables its content.
            if (m_Focused != null && !m_Focused.isActiveAndEnabled)
                ClearFocus();

            if (TryReadDirection(out var direction))
                Step(direction);

            if (m_Pad.PressedThisFrame(PadControl.A))
            {
                if (m_Focused != null)
                {
                    if (m_LogActivity)
                        Debug.Log($"[FocusCursor] A → clicking '{m_Focused.name}'.", m_Focused);
                    m_Focused.Click();
                }
                else if (m_LogActivity)
                {
                    Debug.Log("[FocusCursor] A pressed with nothing focused — press a direction first.", this);
                }
            }

            if (m_Pad.PressedThisFrame(PadControl.B))
                ClearFocus();

            if (m_Pad.PressedThisFrame(PadControl.RightBumper))
                CycleGroup(1);
            else if (m_Pad.PressedThisFrame(PadControl.LeftBumper))
                CycleGroup(-1);
        }

        /// <summary>
        /// This operator's device, or the wand if this operator has none and <see cref="m_FallBackToWand"/>
        /// allows it.
        /// </summary>
        /// <remarks>
        /// The fallback exists because of a real and confusing environment behaviour: MiddleVR's
        /// <c>vrDriverDirectInput</c> takes exclusive ownership of the pad and presents it as <c>Wand0</c>.
        /// When it wins that race — which it does more often than not, and sometimes before Unity has
        /// enumerated the device at all — the Input System reports no gamepad whatsoever while the pad is
        /// merrily driving wand buttons. Without this, the cursor reports "no source" and does nothing,
        /// next to a device that is working fine on the other path.
        ///
        /// Logged whenever it engages, because a cursor quietly reading a different operator's device is
        /// exactly the kind of thing that should never be a surprise.
        /// </remarks>
        IOperatorInput ResolveSource()
        {
            var own = OperatorInputRegistry.Get(m_Role);
            var wand = m_FallBackToWand && m_Role != OperatorRole.Primary
                ? OperatorInputRegistry.Get(OperatorRole.Primary) ?? WandInput.Shared
                : null;

            // Whichever source is actually producing input wins. Presence is NOT liveness here: the pad
            // routinely reports added, connected and enabled while delivering absolutely nothing, because
            // something else owns the device — and the same physical pad may be arriving on the other path
            // at that very moment. Asking "is anything moving?" is the only question that separates them.
            if (IsProducingInput(own))
            {
                if (m_UsingWandFallback && m_LogActivity)
                    Debug.Log($"[FocusCursor] {m_Role}'s own device came alive; switching back to it.", this);
                m_UsingWandFallback = false;
                return own;
            }

            if (IsProducingInput(wand))
            {
                if (!m_UsingWandFallback)
                {
                    m_UsingWandFallback = true;
                    if (m_LogActivity)
                        Debug.Log($"[FocusCursor] {m_Role}'s device is silent but the wand is live — the pad " +
                                  "is arriving through MiddleVR. Following it. Left stick steps focus (the " +
                                  "wand exposes no D-pad); A/B/LB/RB work as usual. Turn off Fall Back To " +
                                  "Wand for the CAVE.", this);
                }
                return wand;
            }

            // Nothing moving on either path: hold whichever we last used, so focus does not thrash between
            // two idle sources while the operator's hands are off the controls.
            if (m_UsingWandFallback && wand != null && wand.IsConnected)
                return wand;
            return own != null && own.IsConnected ? own : wand;
        }

        // Any button down or any axis meaningfully off centre. Cheap enough to run on two sources per frame,
        // and it is the only signal that distinguishes a live device from a present one.
        static bool IsProducingInput(IOperatorInput source)
        {
            if (source == null || !source.IsConnected)
                return false;

            for (int i = 0; i < source.ButtonCount; i++)
                if (source.Held(i))
                    return true;
            for (int i = 0; i < source.AxisCount; i++)
                if (Mathf.Abs(source.Axis(i)) > 0.5f)
                    return true;
            return false;
        }

        bool TryReadDirection(out FocusDirection direction)
        {
            direction = FocusDirection.Up;

            // The D-pad is discrete already, so it steps per press with no latch in the way.
            if (m_Pad.PressedThisFrame(PadControl.DpadUp)) { direction = FocusDirection.Up; return true; }
            if (m_Pad.PressedThisFrame(PadControl.DpadDown)) { direction = FocusDirection.Down; return true; }
            if (m_Pad.PressedThisFrame(PadControl.DpadLeft)) { direction = FocusDirection.Left; return true; }
            if (m_Pad.PressedThisFrame(PadControl.DpadRight)) { direction = FocusDirection.Right; return true; }

            if (!m_UseLeftStick || m_Latch == null)
                return false;
            return m_Latch.Tick(m_Pad.LeftStick, Time.deltaTime, out direction);
        }

        void Step(FocusDirection direction)
        {
            if (m_LogActivity)
                Debug.Log($"[FocusCursor] step {direction} (focused: " +
                          $"{(m_Focused != null ? m_Focused.name : "none")}, graph: {UIFocusGraph.Count}).", this);

            // The first step just brings focus into the room rather than moving it.
            if (m_Focused == null)
            {
                FocusInitial(m_Group);
                return;
            }

            UIFocusGraph.Collect(m_Candidates, m_Focused.FocusGroup);
            if (m_Candidates.Count == 0)
                return;

            var basis = m_Focused.transform;
            Project(basis, m_Candidates, m_Points);

            int index = FocusNavigator.Pick(Vector2.zero, m_Points, direction, m_Wrap);
            if (index >= 0)
                SetFocus(m_Candidates[index]);
        }

        void FocusInitial(Transform group)
        {
            UIFocusGraph.Collect(m_Candidates, group);
            if (m_Candidates.Count == 0)
            {
                // No chips in the remembered panel (or none ever seen): fall back to whatever is reachable.
                UIFocusGraph.Collect(m_Candidates, null, anyGroup: true);
                if (m_Candidates.Count == 0)
                {
                    if (m_LogActivity)
                        Debug.LogWarning("[FocusCursor] nothing to focus — no enabled WandButton is " +
                                         $"registered ({UIFocusGraph.Count} in the graph). Chips register " +
                                         "themselves on enable, so an empty graph means the panels carrying " +
                                         "them are inactive or absent from this scene.", this);
                    return;
                }
            }

            Project(m_Candidates[0].transform, m_Candidates, m_Points);
            int index = FocusNavigator.PickInitial(m_Points);
            if (index >= 0)
                SetFocus(m_Candidates[index]);
        }

        void CycleGroup(int step)
        {
            UIFocusGraph.Groups(m_Groups);
            if (m_Groups.Count == 0)
                return;

            int current = m_Focused != null ? m_Groups.IndexOf(m_Focused.FocusGroup) : m_Groups.IndexOf(m_Group);
            if (current < 0)
                current = 0;

            int next = ((current + step) % m_Groups.Count + m_Groups.Count) % m_Groups.Count;
            m_Group = m_Groups[next];
            ClearFocus(keepGroup: true);
            FocusInitial(m_Group);
        }

        // Flatten world positions into the plane of the chip we are stepping from. Chips on one panel are
        // coplanar, so its right/up axes are the panel's own axes — which is why steps have to stay inside a
        // panel and the bumpers exist.
        static void Project(Transform basis, List<WandButton> buttons, List<Vector2> into)
        {
            into.Clear();
            Vector3 origin = basis.position;
            Vector3 right = basis.right;
            Vector3 up = basis.up;

            for (int i = 0; i < buttons.Count; i++)
            {
                Vector3 delta = buttons[i].transform.position - origin;
                into.Add(new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, up)));
            }
        }

        void SetFocus(WandButton next)
        {
            if (next == m_Focused)
                return;
            if (m_Focused != null)
                m_Focused.SetFocused(false);
            m_Focused = next;
            if (m_Focused != null)
            {
                m_Focused.SetFocused(true);
                m_Group = m_Focused.FocusGroup;
                if (m_LogActivity)
                    Debug.Log($"[FocusCursor] focus → '{m_Focused.name}' on panel " +
                              $"'{(m_Group != null ? m_Group.name : "(none)")}'.", m_Focused);
            }
        }

        void ClearFocus(bool keepGroup = false)
        {
            if (m_Focused != null)
                m_Focused.SetFocused(false);
            m_Focused = null;
            if (!keepGroup)
                m_Latch?.Reset();
        }
    }
}
