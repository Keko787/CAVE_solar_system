using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// Draws the line of scrimmage and the line to gain on the turf, from the live situation.
    /// </summary>
    /// <remarks>
    /// <b>Floor only.</b> Both strips live on <see cref="PitchLayers.Tactical"/>, which only the top-down
    /// camera carries. On the broadcast wall they would be wrong twice over: a real broadcast composites
    /// the yellow line under the players, and this cannot — the strips are flat geometry a few centimetres
    /// above the grass, so from a low angle they would slice through ankles.
    ///
    /// <para><b>Deliberately wider than a painted line.</b> The floor frames 48.9 m of field across a
    /// 2.2225 m surface, so a real 4-inch stripe arrives 4.6 mm wide — about six pixels, and the first
    /// thing lost to any projector softness. The overlay is drawn at three times that, which is also what
    /// television does and for the same reason.</para>
    ///
    /// <para>Nothing here is cached from build time. The strips are found through serialized references and
    /// everything else is read from the sim each frame, so a reloaded scene is in exactly the same state as
    /// a freshly built one.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class FieldStateOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("Strip marking where the ball is spotted. Built by the pitch rig builder.")]
        Transform m_Scrimmage;

        [SerializeField, Tooltip("Strip marking where the offence has to reach.")]
        Transform m_LineToGain;

        [SerializeField, Tooltip("Height above the turf, metres. Enough to clear the painted lines and their " +
            "z-fighting, small enough that the floor camera still reads it as paint rather than as a kerb.")]
        float m_Height = 0.02f;

        Renderer m_LineToGainRenderer;

        void Awake()
        {
            if (m_LineToGain != null)
                m_LineToGainRenderer = m_LineToGain.GetComponent<Renderer>();
        }

        void LateUpdate()
        {
            var driver = FootballSimDriver.Instance;
            var sim = driver != null ? driver.Sim : null;
            if (sim == null) return;

            Apply(sim.Situation);
        }

        /// <summary>
        /// Place both strips for a situation. Public because the editor capture tooling has no run loop:
        /// without this seam a captured floor shows both lines stacked at midfield, which looks like a
        /// broken overlay rather than an un-ticked one.
        /// </summary>
        public void Apply(in GameSituation situation)
        {
            var lines = FieldStateLines.For(situation);

            Place(m_Scrimmage, lines.ScrimmageYard);
            Place(m_LineToGain, lines.LineToGainYard);

            // Toggled through the renderer rather than the GameObject: SetActive churns the transform
            // hierarchy every time the chains move, and this changes on nearly every snap.
            if (m_LineToGainRenderer == null && m_LineToGain != null)
                m_LineToGainRenderer = m_LineToGain.GetComponent<Renderer>();

            if (m_LineToGainRenderer != null && m_LineToGainRenderer.enabled != lines.ShowLineToGain)
                m_LineToGainRenderer.enabled = lines.ShowLineToGain;
        }

        /// <summary>
        /// Put a strip on an absolute yard. <b>Local</b> space — the sim works in field coordinates and the
        /// pitch is parked 1500 m below the room, so a world position would leave both lines up in the CAVE.
        /// </summary>
        void Place(Transform strip, float yard)
        {
            if (strip == null) return;

            Vector3 spot = FieldGeometry.Spot(yard, 0f);
            strip.localPosition = new Vector3(spot.x, m_Height, spot.z);
        }

        /// <summary>Wire the strips from a builder.</summary>
        public void Configure(Transform scrimmage, Transform lineToGain)
        {
            m_Scrimmage = scrimmage;
            m_LineToGain = lineToGain;
        }
    }
}
