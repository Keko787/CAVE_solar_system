using System.Collections;
using System.Collections.Generic;
using Stadium3D.Telemetry;   // InteractionLog
using UnityEngine;
using Stadium3D.Vr;    // MiddleVRWand

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// Opens the front CAVE wall into the simulated airspace (Phase 4, M0/M3). MiddleVR already renders
    /// each wall as a head-tracked, parallax-correct window, so the "portal" is <b>geometric</b>: in
    /// <em>window</em> mode this ORs a <c>PitchMiniature</c> layer bit into every MiddleVR display camera's
    /// culling mask and hides the front-wall renderer, so the operator sees <em>through</em> the wall to the
    /// airspace geometry with true off-axis depth; <em>wall</em> mode reverses it. Pure Unity — no <c>.vrx</c>
    /// edit (design plan risk #6). MVR's <c>UpdateCameraProperties</c> only rewrites near/far/projection each
    /// frame, never the culling mask, so a one-shot flip persists (no per-frame re-apply). The RenderTexture
    /// monitor is the reserve fallback if the URP/MVR pipeline re-culls the layer out of band.
    ///
    /// Opening also sends the front wall's <see cref="RegionPanelGroup"/> to the floor — the map, Routes, Fleet
    /// and tables together. Flicking the stick up is a request to <em>look at the airspace</em>, and four panels
    /// left hanging in the window defeat it. Closing restores whichever step the group was on beforehand rather
    /// than forcing it back onto the wall, since the operator may have parked it on the floor themselves.
    /// </summary>
    public class FrontWallPortal : MonoBehaviour
    {
        [SerializeField, Tooltip("Front-wall renderers hidden when the window opens. Auto-resolved by name " +
            "(Front_Wall*) if left empty.")]
        Renderer[] m_WallRenderers;

        [SerializeField, Tooltip("Unity layers revealed through the wall when open — the airspace and the " +
            "sensor cones. Created by the portal-spike / sensing-visuals builders.")]
        string[] m_RevealLayerNames = { "PitchMiniature" };

        [SerializeField, Tooltip("The front-wall panel group, moved to the floor while the window is open so it " +
            "isn't standing in front of the airspace you opened the wall to see. Auto-found if empty.")]
        RegionPanelGroup m_PanelGroup;

        [SerializeField, Min(0), Tooltip("Cycle step the group takes while the window is open. Every front-wall " +
            "panel is built wall = entry 0, floor = entry 1, so 1 clears the wall.")]
        int m_OpenStep = 1;

        [SerializeField, Tooltip("Extra panels to evacuate that are NOT in the group. Legacy per-panel path: it " +
            "only understands the FrontWall ⇄ Floor pair. Group members are skipped here.")]
        RegionPanel[] m_PanelsToEvacuate;

        [SerializeField, Tooltip("Push the wand thumbstick past this to toggle: up = open, down = close. " +
            "0.6 sits above the .vrx 0.3 dead-zone on the Flystick's vertical axis.")]
        float m_StickThreshold = 0.6f;

        [SerializeField, Tooltip("Optional wand button that toggles the window. -1 = none — buttons 0-4 are " +
            "all taken (0=select, 1=panel, 2/3/4=goals), so the thumbstick is the default gesture.")]
        int m_ToggleButton = -1;

        [SerializeField, Tooltip("Optional interaction log — every open/close is a supervisory act.")]
        InteractionLog m_Log;

        [SerializeField, Tooltip("Start with the window open (default: closed, wall shown).")]
        bool m_StartOpen;

        int m_RevealMask;
        bool m_IsOpen;
        readonly List<RegionPanel> m_Evacuated = new();

        // The step the group was on before the window opened, so closing restores what the operator had rather
        // than forcing the panels back onto the wall — they may well have sent them to the floor themselves.
        int m_GroupStepBeforeOpen;
        bool m_GroupEvacuated;

        enum StickZone { Neutral, Up, Down }
        StickZone m_Stick;

        /// <summary>True while the front wall is a window into the airspace.</summary>
        public bool IsOpen => m_IsOpen;

        void Awake()
        {
            m_RevealMask = 0;
            if (m_RevealLayerNames != null)
                foreach (var name in m_RevealLayerNames)
                {
                    int layer = LayerMask.NameToLayer(name);
                    if (layer >= 0)
                        m_RevealMask |= 1 << layer;
                }
            if (m_RevealMask == 0)
            {
                Debug.LogError("[FrontWallPortal] None of the reveal layers exist. Run " +
                    "the PitchMiniature layer is defined in Project Settings ▸ Tags and Layers.", this);
                enabled = false;
                return;
            }

            if (m_WallRenderers == null || m_WallRenderers.Length == 0)
                m_WallRenderers = ResolveFrontWall();

            if (m_PanelGroup == null)
                m_PanelGroup = FindAnyObjectByType<RegionPanelGroup>(FindObjectsInactive.Include);
        }

        // Coroutine Start: MiddleVR builds its display cameras during MVRManager's Awake, but not
        // necessarily before ours — wait for them, then stamp the default (closed) state, which also strips
        // PitchMiniature from the display cameras in case they cloned a template mask that included it.
        IEnumerator Start()
        {
            float timeout = 10f;
            while (timeout > 0f && !HasDisplayCameras())
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Apply(m_StartOpen, log: false);
        }

        void Update()
        {
            // Thumbstick: flick up to open the window, down to close — once per push (re-arms near centre).
            float y = MiddleVRWand.VerticalAxis();
            if (y > m_StickThreshold)
            {
                if (m_Stick != StickZone.Up) { m_Stick = StickZone.Up; Open(); }
            }
            else if (y < -m_StickThreshold)
            {
                if (m_Stick != StickZone.Down) { m_Stick = StickZone.Down; Close(); }
            }
            else if (Mathf.Abs(y) < m_StickThreshold * 0.5f)
            {
                m_Stick = StickZone.Neutral;
            }

            // Optional button (disabled by default: no free wand button).
            if (m_ToggleButton >= 0 && MiddleVRWand.PressedThisFrame(m_ToggleButton))
                Toggle();
        }

        /// <summary>Open the window (reveal the airspace through the front wall). No-op if already open.</summary>
        public void Open() { if (!m_IsOpen) Apply(true); }

        /// <summary>Close the window (restore the solid front wall). No-op if already closed.</summary>
        public void Close() { if (m_IsOpen) Apply(false); }

        /// <summary>Flip between wall and window.</summary>
        [ContextMenu("Toggle Portal")]
        public void Toggle() => Apply(!m_IsOpen);

        void Apply(bool open, bool log = true)
        {
            foreach (var cam in DisplayCameras())
            {
                if (open) cam.cullingMask |= m_RevealMask;
                else cam.cullingMask &= ~m_RevealMask;
            }

            if (m_WallRenderers != null)
                foreach (var r in m_WallRenderers)
                    if (r != null)
                        r.enabled = !open;   // wall hidden while the window is open

            // Get the panels out of the way. Opening the wall is a request to look at the airspace, and the front
            // wall carries four panels — leaving them hanging in the window defeats the gesture. The whole group
            // travels, for the same reason it travels on the wand's move button: it is one display surface.
            if (open)
            {
                if (m_PanelGroup != null && !m_GroupEvacuated)
                {
                    m_GroupStepBeforeOpen = m_PanelGroup.Step;
                    m_PanelGroup.MoveToStep(m_OpenStep);
                    m_GroupEvacuated = true;
                }

                m_Evacuated.Clear();
                if (m_PanelsToEvacuate != null)
                    foreach (var p in m_PanelsToEvacuate)
                        if (p != null && !InGroup(p) && p.CurrentRegion == "FrontWall")
                        {
                            p.MoveTo("Floor");
                            m_Evacuated.Add(p);
                        }
            }
            else
            {
                if (m_PanelGroup != null && m_GroupEvacuated)
                {
                    m_PanelGroup.MoveToStep(m_GroupStepBeforeOpen);
                    m_GroupEvacuated = false;
                }

                foreach (var p in m_Evacuated)
                    if (p != null)
                        p.MoveTo("FrontWall");
                m_Evacuated.Clear();
            }

            m_IsOpen = open;
            if (log)
                m_Log?.Log("portal", open ? "open" : "close");
        }

        // A group member must not also be moved by the legacy per-panel path: that path only knows the
        // FrontWall ⇄ Floor pair, so it would drag a side panel onto the map's floor slot on top of the map.
        bool InGroup(RegionPanel panel) => m_PanelGroup != null && m_PanelGroup.Contains(panel);

        bool HasDisplayCameras()
        {
            foreach (var _ in DisplayCameras())
                return true;
            return false;
        }

        // The MiddleVR per-eye display cameras — shared with the x-ray wall-gate (see MvrDisplay.Cameras).
        IEnumerable<Camera> DisplayCameras() => MvrDisplay.Cameras();

        Renderer[] ResolveFrontWall()
        {
            var list = new List<Renderer>();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.name.StartsWith("Front_Wall"))
                    list.Add(r);
            return list.ToArray();
        }
    }
}
