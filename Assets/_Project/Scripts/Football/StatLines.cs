using System.Globalization;

namespace Stadium3D.Football
{
    /// <summary>
    /// Turns a stat line into the one sentence a viewer would actually want for that position.
    /// </summary>
    /// <remarks>
    /// <b>Formatting is here, not in a view.</b> §8 asks the team walls for "position-appropriate stats",
    /// which is a decision — a quarterback's line is completions and rating, a receiver's is catches and a
    /// long — and decisions belong somewhere a test can reach without a camera. The same argument
    /// <see cref="LiveTelemetry"/> already makes about units: the view receives finished strings and does no
    /// arithmetic, so a stray conversion cannot creep into a label.
    ///
    /// <para><b>An untouched player reads as a dash.</b> A receiver who has not been thrown at is not
    /// "0 rec, 0 yds" — that is a bad game, and this is a player who has not been involved. The drive puts
    /// twenty-two men on the field and most of them never touch the ball, so this is the common case, not
    /// the edge one.</para>
    /// </remarks>
    public static class StatLines
    {
        /// <summary>Shown where a player has no stats worth a line yet.</summary>
        public const string Empty = "—";

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>Name, number and position, as a team wall's card header.</summary>
        public static string Card(PlayerIdentity identity) =>
            identity == null
                ? string.Empty
                : $"#{identity.Number}  {identity.Name.ToUpperInvariant()}";

        /// <summary>Position, height and weight — the second line of the card.</summary>
        public static string Vitals(PlayerIdentity identity) =>
            identity == null
                ? string.Empty
                : $"{identity.PositionAbbreviation}  ·  {identity.HeightDisplay}  ·  {identity.WeightPounds} lb";

        /// <summary>
        /// The stat line for this player, chosen by position and falling back to whatever they have done.
        /// </summary>
        /// <remarks>
        /// Position leads rather than stat volume, because a quarterback who has also run twice is still
        /// read as a quarterback — but it only leads. A running back who has caught four passes and carried
        /// nothing gets the receiving line, since the alternative is showing him "0 car, 0 yds" while his
        /// four catches sit in the same struct.
        /// </remarks>
        public static string ForPosition(PlayerIdentity identity, PlayerStats stats)
        {
            var position = identity != null ? identity.Position : PlayerPosition.WideReceiver;

            switch (position)
            {
                case PlayerPosition.Quarterback:
                    if (stats.PassAttempts > 0) return Passing(stats);
                    break;

                case PlayerPosition.RunningBack:
                    if (stats.Carries > 0) return Rushing(stats);
                    break;

                case PlayerPosition.WideReceiver:
                case PlayerPosition.TightEnd:
                    if (stats.Targets > 0 || stats.Receptions > 0) return Receiving(stats);
                    break;
            }

            if (stats.PassAttempts > 0) return Passing(stats);
            if (stats.Receptions > 0 || stats.Targets > 0) return Receiving(stats);
            if (stats.Carries > 0) return Rushing(stats);
            if (stats.Sacks > 0) return stats.Sacks == 1 ? "1 sack" : $"{stats.Sacks} sacks";

            return Empty;
        }

        static string Passing(PlayerStats s)
        {
            string line = $"{s.PassCompletions}/{s.PassAttempts}  {Yards(s.PassingYards)} yds";
            if (s.PassingTouchdowns > 0) line += $"  {s.PassingTouchdowns} TD";
            if (s.Interceptions > 0) line += $"  {s.Interceptions} INT";
            return $"{line}  ·  {s.PasserRating().ToString("0.0", Invariant)} rtg";
        }

        static string Rushing(PlayerStats s)
        {
            string line = $"{s.Carries} car  {Yards(s.RushingYards)} yds";
            if (s.RushingTouchdowns > 0) line += $"  {s.RushingTouchdowns} TD";
            return line;
        }

        static string Receiving(PlayerStats s)
        {
            string line = $"{s.Receptions}/{s.Targets} rec  {Yards(s.ReceivingYards)} yds";
            if (s.ReceivingTouchdowns > 0) line += $"  {s.ReceivingTouchdowns} TD";
            if (s.LongestReception > 0f) line += $"  ·  long {Yards(s.LongestReception)}";
            return line;
        }

        /// <summary>
        /// The live readout: speed now, best this play, ground covered, and who is nearest.
        /// </summary>
        /// <remarks>
        /// Separation is dropped rather than shown as a negative when there is nobody to measure against,
        /// which is what <see cref="LiveTelemetry.SeparationYards"/> reports for a lone player. "-1.0 yd" on
        /// a wall is worse than a missing field.
        /// </remarks>
        public static string Live(in LiveTelemetry telemetry)
        {
            string line = $"{telemetry.SpeedMph.ToString("0.0", Invariant)} mph" +
                          $"  ·  top {telemetry.TopSpeedMph.ToString("0.0", Invariant)}" +
                          $"  ·  {Yards(telemetry.DistanceYards)} yds";

            if (telemetry.SeparationYards >= 0f)
                line += $"  ·  sep {Yards(telemetry.SeparationYards)}";

            return line;
        }

        /// <summary>Whole yards. Nothing on a wall is read to a decimal place at four metres.</summary>
        static string Yards(float yards) => yards.ToString("0", Invariant);
    }
}
