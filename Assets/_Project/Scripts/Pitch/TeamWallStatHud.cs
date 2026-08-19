using TMPro;
using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// A team wall's card for the player that wall is following: who he is, and what he is doing right now.
    /// </summary>
    /// <remarks>
    /// <b>The subject comes from the camera, not from a second decision.</b> <see cref="TeamIsoPitchCamera"/>
    /// already chooses who this wall is framing, and the focus ring and the route trail already follow that
    /// choice. A card that picked its own player would look right in every still and be wrong the moment the
    /// wall cut to somebody else — the failure this project keeps meeting, where two systems agree in the
    /// common case and diverge silently.
    ///
    /// <para><b>Live and season are different beats.</b> §8 wants speed, top speed, distance and separation
    /// while the play runs, and the player's stat line at the result. Both are drawn; the live block simply
    /// has nothing to say before the snap, and says so with a dash rather than a row of zeroes.</para>
    /// </remarks>
    public class TeamWallStatHud : StatHud
    {
        [SerializeField, Tooltip("Which wall this is. Decides whose focus the card follows.")]
        TeamSide m_Side = TeamSide.Home;

        [Header("Card")]
        [SerializeField] TMP_Text m_Name;
        [SerializeField] TMP_Text m_Vitals;
        [SerializeField] TMP_Text m_Stats;

        [Header("Live")]
        [SerializeField] TMP_Text m_Live;
        [SerializeField] TMP_Text m_Route;

        /// <summary>Which side's focused player this card follows.</summary>
        public TeamSide Side => m_Side;

        /// <summary>Wire the labels from a builder.</summary>
        public void Configure(TeamSide side, TMP_Text name, TMP_Text vitals, TMP_Text stats,
                              TMP_Text live, TMP_Text route)
        {
            m_Side = side;
            m_Name = name;
            m_Vitals = vitals;
            m_Stats = stats;
            m_Live = live;
            m_Route = route;
        }

        protected override void OnApply(FootballSim sim)
        {
            var driver = FootballSimDriver.Instance;
            Apply(sim, driver != null ? driver.FocusOf(m_Side) : 0);
        }

        /// <summary>
        /// Fill the card for one player. Public because the editor capture tooling has no run loop, and
        /// because the wall's subject is decided by a camera that has to settle first.
        /// </summary>
        public void Apply(FootballSim sim, int playerId)
        {
            if (sim == null || !Visible) return;
            EnsureLaidOut();

            var identity = playerId == 0 ? null : sim.Roster.Get(playerId);
            if (identity == null)
            {
                Set(m_Name, string.Empty);
                Set(m_Vitals, string.Empty);
                Set(m_Stats, string.Empty);
                Set(m_Live, string.Empty);
                Set(m_Route, string.Empty);
                return;
            }

            Set(m_Name, StatLines.Card(identity));
            Set(m_Vitals, StatLines.Vitals(identity));
            Set(m_Stats, StatLines.ForPosition(identity, sim.Score.For(playerId)));

            var telemetry = sim.TelemetryFor(playerId);
            Set(m_Live, StatLines.Live(telemetry));
            Set(m_Route, string.IsNullOrEmpty(telemetry.RouteName) ? string.Empty : telemetry.RouteName.ToUpperInvariant());
        }
    }
}
