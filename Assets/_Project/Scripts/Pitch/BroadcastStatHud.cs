using TMPro;
using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// The front wall's readout: down and distance where it can be read, everything else out at the edges.
    /// </summary>
    /// <remarks>
    /// <b>§8's split is a geometric fact, not a style choice.</b> The front wall subtends 116° at 1.11 m and
    /// comfortable single-glance vision is about 60°, so only the central 1.28 m of 3.556 m is read without
    /// moving your eyes — <b>64% of that wall is peripheral</b>. Down and distance, the ball's spot and the
    /// clock go in the middle and go large; score, quarter and possession live in the corners where they can
    /// be found on purpose but never compete. <see cref="HudLayout.IsReadable(float,float)"/> is asserted in
    /// the tests, because on a 2560-pixel preview at a desk every part of that wall looks equally legible.
    /// </remarks>
    public class BroadcastStatHud : StatHud
    {
        [Header("Central — read at a glance")]
        [SerializeField] TMP_Text m_DownDistance;
        [SerializeField] TMP_Text m_BallOn;
        [SerializeField] TMP_Text m_Clock;

        [Header("Peripheral — present, not load-bearing")]
        [SerializeField] TMP_Text m_Score;
        [SerializeField] TMP_Text m_Quarter;

        /// <summary>Wire the labels from a builder.</summary>
        public void Configure(TMP_Text downDistance, TMP_Text ballOn, TMP_Text clock,
                              TMP_Text score, TMP_Text quarter)
        {
            m_DownDistance = downDistance;
            m_BallOn = ballOn;
            m_Clock = clock;
            m_Score = score;
            m_Quarter = quarter;
        }

        protected override void OnApply(FootballSim sim)
        {
            var situation = sim.Situation;

            Set(m_DownDistance, situation.DownAndDistance());
            Set(m_BallOn, situation.BallOn());
            Set(m_Clock, situation.GameClock());

            Set(m_Score, Score(sim));
            Set(m_Quarter, $"Q{situation.Quarter}");
        }

        /// <summary>
        /// Both scores on one line, with a marker against whoever has the ball.
        /// </summary>
        /// <remarks>
        /// Possession as a mark on the score rather than its own field: it is one bit, it is always about
        /// one of exactly two names already on screen, and a separate "BAY ball" label out in the periphery
        /// is a third thing to find in the 64% of the wall nobody looks at.
        ///
        /// <para><b>The points come from the box score, not from the situation.</b>
        /// <see cref="GameSituation"/> is the state <em>at the snap</em>, so a scoreboard driven from it
        /// lags a play forever: this drive ends on a 62-yard touchdown and the wall would still read 10–14
        /// while the receiver was in the end zone, and would never reach 16 at all. The box score updates
        /// once the play is over, which is when a real scoreboard changes. Mid-drive the two agree exactly,
        /// which is why this shipped and why an acceptance test that plays the drive out is what found
        /// it.</para>
        /// </remarks>
        static string Score(FootballSim sim)
        {
            var box = sim.Score;
            var roster = sim.Roster;

            string home = $"{roster.HomeAbbreviation} {box.Home.Points}";
            string away = $"{roster.AwayAbbreviation} {box.Away.Points}";

            return sim.Situation.Possession == TeamSide.Home
                ? $"• {home}   {away}"
                : $"{home}   • {away}";
        }
    }
}
