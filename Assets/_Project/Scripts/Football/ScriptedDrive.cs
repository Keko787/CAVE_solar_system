using System.Collections.Generic;

namespace Stadium3D.Football
{
    /// <summary>
    /// The shipped drive: Bayside trailing 10–14 midway through the second quarter, taking over on their own
    /// 35 and going sixty-five yards for the touchdown.
    /// </summary>
    /// <remarks>
    /// Six snaps, five official plays. The sequence is chosen so the walls always have something true to
    /// show: a modest run to establish the down-and-distance readout, a conversion that moves the chains, a
    /// sack that puts the offense behind schedule, a flag that replays a down, an incompletion that stops the
    /// clock, and a deep touchdown to finish.
    ///
    /// <para>The last play is the <b>hero play</b>. Its ball hangs for 2.4 seconds, which is the only event in
    /// American football long enough for a person standing in a CAVE to physically use more than one surface —
    /// everything else is over in four seconds with the viewer's head locked forward. The beat schedule
    /// stretches that flight to roughly 0.3× so the side walls can show the receiver and the defender
    /// life-size while the ball is still up.</para>
    ///
    /// <para>Play content lives in <see cref="ScriptedDrivePlays"/>, one method per snap.</para>
    /// </remarks>
    public static class ScriptedDrive
    {
        /// <summary>Seconds of huddle between snaps, during which bodies walk to their next alignment.</summary>
        public const float HuddleSeconds = 3f;

        /// <summary>Seconds the result holds on the walls after the whistle.</summary>
        public const float PostPlaySeconds = 3.5f;

        /// <summary>
        /// Bake rate. 60 Hz is visually indistinguishable from anything higher once
        /// <see cref="MotionSample.Lerp"/> interpolates between samples, and the whole drive costs about
        /// 1.5 MB at this rate — irrelevant. Below 30 a tight break starts to visibly flatten, because the
        /// interpolation is a straight chord.
        /// </summary>
        public const float SampleHz = 60f;

        /// <summary>
        /// The situation at the first snap: 1st and 10 on their own 35, 12:04 left in the second quarter,
        /// Bayside down four.
        /// </summary>
        public static GameSituation StartSituation => new GameSituation
        {
            Down = 1,
            DistanceYards = 10f,
            LineOfScrimmageYard = 35f,
            Possession = TeamSide.Home,
            HomeScore = 10,
            AwayScore = 14,
            Quarter = 2,
            GameClockSeconds = 12f * 60f + 4f,
            PlayClockSeconds = 40f,
            HomeTimeouts = 3,
            AwayTimeouts = 2,
            GoalToGo = false,
            ClockRunning = false,
        };

        /// <summary>The whole drive, ready to hand to a <see cref="FootballSim"/>.</summary>
        public static DriveScript Build() => new DriveScript(
            DriveRoster.Build(),
            ScriptedDrivePlays.All(),
            StartSituation,
            HuddleSeconds,
            PostPlaySeconds,
            loop: true,
            sampleHz: SampleHz);

        /// <summary>
        /// The box score this drive is expected to produce, as a plain expectation an EditMode test asserts
        /// against. Authoring the answer separately from the content is the point: it catches a play whose
        /// outcome was edited without its stat line being reconsidered.
        /// </summary>
        public static class Expected
        {
            public const int Plays = 6;

            /// <summary>
            /// Field position gained: the 35 to the goal line. This is <b>not</b> the same number as
            /// <see cref="TeamTotalYards"/>, and the gap is real football rather than an inconsistency — a
            /// penalty moves the ball without being charged as offensive yardage.
            /// </summary>
            public const float FieldPositionYards = 65f;

            /// <summary>Box-score total yards: +6 run, +14 pass, −7 sack, +62 touchdown.</summary>
            public const float TeamTotalYards = 75f;

            /// <summary>Sack yardage is charged against team passing, per the NFL convention.</summary>
            public const float TeamPassingYards = 69f;   // 14 − 7 + 62
            public const float TeamRushingYards = 6f;

            public const int HomeScoreAfter = 16;   // 10 + 6, before the extra point
            public const int AwayScoreAfter = 14;
            public const int HomeFirstDowns = 2;    // the dagger conversion and the touchdown
            public const int HomePenalties = 1;
            public const float HomePenaltyYards = 10f;
            public const int ThirdDownAttempts = 1; // only the touchdown came on third down
            public const int ThirdDownConversions = 1;

            /// <summary>#9 Vance: 3 attempts, 2 completions, 76 yards, 1 touchdown.</summary>
            public const int PasserNumber = 9;
            public const int PassAttempts = 3;
            public const int PassCompletions = 2;
            public const float PassingYards = 76f;   // 14 + 62; the sack is not an attempt
            public const int PassingTouchdowns = 1;

            /// <summary>The down-and-distance the drive must walk through, snap by snap.</summary>
            public static readonly (int Down, float Distance, float Yard)[] Sequence =
            {
                (1, 10f, 35f),   // 1st & 10 own 35
                (2,  4f, 41f),   // after the +6 run
                (1, 10f, 55f),   // the dagger moves the chains
                (2, 17f, 48f),   // after the sack
                (2, 27f, 38f),   // holding, down replayed
                (3, 27f, 38f),   // after the incompletion
            };
        }
    }
}
