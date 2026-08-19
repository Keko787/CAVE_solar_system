using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// Where the two broadcast lines belong for a given situation, in absolute yards: the spot the ball is
    /// on, and the spot the offence has to reach.
    /// </summary>
    /// <remarks>
    /// <b>Pure, and in the sim assembly rather than beside the renderer that draws it.</b> The line to gain
    /// is the one number on the floor a viewer reads continuously, and the sign behind it — home drives up
    /// the yard axis, away drives down it — is exactly the kind of thing that yields a completely plausible
    /// picture with the line on the wrong side of the ball. At 1:22 nobody can catch that by eye; on the
    /// broadcast wall a viewer who follows football would catch it instantly and stop trusting the rest.
    /// Answering it here means a test pins it instead.
    ///
    /// <para>This deliberately says nothing about colour, thickness or height. Those are properties of the
    /// surface the lines are drawn on, and the floor renders at a scale where a painted 4-inch stripe is
    /// four millimetres — so the overlay has to be drawn wider than life, which is a rendering decision and
    /// not a football one.</para>
    /// </remarks>
    public readonly struct FieldStateLines
    {
        /// <summary>Absolute yard of the line of scrimmage, clamped into the field of play.</summary>
        public readonly float ScrimmageYard;

        /// <summary>Absolute yard of the line to gain, never beyond the goal line being attacked.</summary>
        public readonly float LineToGainYard;

        /// <summary>Whether the line to gain is worth drawing at all.</summary>
        public readonly bool ShowLineToGain;

        FieldStateLines(float scrimmageYard, float lineToGainYard, bool showLineToGain)
        {
            ScrimmageYard = scrimmageYard;
            LineToGainYard = lineToGainYard;
            ShowLineToGain = showLineToGain;
        }

        /// <summary>Solve both lines from a situation.</summary>
        public static FieldStateLines For(in GameSituation situation)
        {
            float scrimmage = Mathf.Clamp(
                situation.LineOfScrimmageYard,
                FieldGeometry.HomeGoalLineYard,
                FieldGeometry.AwayGoalLineYard);

            float goal = FieldGeometry.GoalLineFor(situation.Possession);
            float direction = FieldGeometry.DirectionFor(situation.Possession);
            float raw = scrimmage + situation.DistanceYards * direction;

            // A penalty can leave more distance than there is field: first and 15 from the opponent's 12
            // still gains at the goal line, and a line drawn in the seats is worse than no line.
            float lineToGain = direction > 0f ? Mathf.Min(raw, goal) : Mathf.Max(raw, goal);

            // Goal to go draws nothing. The line to gain is then the goal line, which is already painted on
            // the turf — a second stripe on top of it reads as two lines rather than as agreement, and the
            // viewer spends a moment working out which one matters.
            bool show = !situation.GoalToGo && !Mathf.Approximately(lineToGain, goal);

            return new FieldStateLines(scrimmage, lineToGain, show);
        }
    }
}
