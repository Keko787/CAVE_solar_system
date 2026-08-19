using System;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// The complete game state the front wall reads. Treated as an immutable value throughout — every
    /// transition produces a new one rather than editing in place, which is what lets
    /// <see cref="DriveLedger"/> fold the whole drive once and then answer "what was the situation at the
    /// snap of play 4" without replaying anything.
    /// </summary>
    /// <remarks>
    /// The fields are writable only because Unity's serializer cannot round-trip a readonly struct, and
    /// <see cref="PlayOutcome.ForcedSituation"/> has to be authorable in the Inspector. Nothing in the sim
    /// mutates one after construction; use the <c>With…</c> helpers.
    /// </remarks>
    [Serializable]
    public struct GameSituation
    {
        [Tooltip("1 through 4.")]
        public int Down;

        [Tooltip("Yards needed for a first down. Ignored for display when GoalToGo is set.")]
        public float DistanceYards;

        [Tooltip("Absolute yard of the ball: 0 = home goal line, 50 = midfield, 100 = away goal line.")]
        public float LineOfScrimmageYard;

        [Tooltip("Which team has the ball.")]
        public TeamSide Possession;

        public int HomeScore;
        public int AwayScore;

        [Tooltip("1 through 4.")]
        public int Quarter;

        [Tooltip("Seconds left in the quarter.")]
        public float GameClockSeconds;

        [Tooltip("Seconds left on the play clock.")]
        public float PlayClockSeconds;

        public int HomeTimeouts;
        public int AwayTimeouts;

        [Tooltip("Set when the goal line is closer than the yards-to-gain, so the readout says \"& Goal\".")]
        public bool GoalToGo;

        [Tooltip("Is the game clock running right now?")]
        public bool ClockRunning;

        /// <summary>A sensible opening state: 1st and 10, top of the second quarter, all timeouts.</summary>
        public static GameSituation Kickoff(TeamSide possession, float lineOfScrimmageYard) => new GameSituation
        {
            Down = 1,
            DistanceYards = 10f,
            LineOfScrimmageYard = lineOfScrimmageYard,
            Possession = possession,
            HomeScore = 0,
            AwayScore = 0,
            Quarter = 1,
            GameClockSeconds = 15f * 60f,
            PlayClockSeconds = 40f,
            HomeTimeouts = 3,
            AwayTimeouts = 3,
            GoalToGo = false,
            ClockRunning = false,
        };

        /// <summary>Score for a side.</summary>
        public int ScoreOf(TeamSide side) => side == TeamSide.Home ? HomeScore : AwayScore;

        /// <summary>Timeouts left for a side.</summary>
        public int TimeoutsOf(TeamSide side) => side == TeamSide.Home ? HomeTimeouts : AwayTimeouts;

        /// <summary>Yards from the line of scrimmage to the possessing team's goal line.</summary>
        public float YardsToGoal => FieldGeometry.YardsToGoal(Possession, LineOfScrimmageYard);

        /// <summary>"1st &amp; 10", "3rd &amp; 27", "2nd &amp; Goal".</summary>
        public string DownAndDistance()
        {
            string down;
            switch (Down)
            {
                case 1:  down = "1st"; break;
                case 2:  down = "2nd"; break;
                case 3:  down = "3rd"; break;
                default: down = "4th"; break;
            }
            return GoalToGo ? $"{down} & Goal" : $"{down} & {Mathf.RoundToInt(DistanceYards)}";
        }

        /// <summary>
        /// "OWN 35" / "OPP 45" from the possessing team's point of view — how the ball spot is actually said
        /// out loud, and what the front wall shows.
        /// </summary>
        public string BallOn()
        {
            int marked = FieldGeometry.MarkedYardLine(LineOfScrimmageYard);
            if (Mathf.Approximately(LineOfScrimmageYard, FieldGeometry.MidfieldYard))
                return "50";

            bool ownHalf = Possession == TeamSide.Home
                ? LineOfScrimmageYard < FieldGeometry.MidfieldYard
                : LineOfScrimmageYard > FieldGeometry.MidfieldYard;

            return $"{(ownHalf ? "OWN" : "OPP")} {marked}";
        }

        /// <summary>Game clock as "12:04".</summary>
        public string GameClock()
        {
            float c = Mathf.Max(0f, GameClockSeconds);
            int minutes = Mathf.FloorToInt(c / 60f);
            int seconds = Mathf.FloorToInt(c - minutes * 60f);
            return $"{minutes}:{seconds:00}";
        }

        public GameSituation WithClock(float gameClockSeconds, float playClockSeconds, bool clockRunning)
        {
            var s = this;
            s.GameClockSeconds = Mathf.Max(0f, gameClockSeconds);
            s.PlayClockSeconds = Mathf.Max(0f, playClockSeconds);
            s.ClockRunning = clockRunning;
            return s;
        }

        public GameSituation WithBallOn(float lineOfScrimmageYard)
        {
            var s = this;
            s.LineOfScrimmageYard = lineOfScrimmageYard;
            return s;
        }
    }
}
