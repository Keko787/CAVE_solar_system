using System.Collections.Generic;

namespace Stadium3D.Football
{
    /// <summary>
    /// The two rosters the shipped drive is played by: the <b>Bayside Ironhawks</b> on offense and the
    /// <b>Northgate Sentinels</b> on defense.
    /// </summary>
    /// <remarks>
    /// Both teams and every player are fictional, deliberately. A demo that shows real franchises and real
    /// athletes invites a licensing conversation nobody wants to have about a research prototype, and
    /// invented names cost nothing here.
    ///
    /// <para>Jersey numbers follow the real positional ranges — quarterbacks in the 1–19 band, backs in the
    /// 20s, linebackers in the 50s, linemen in the 60s and 70s — because a viewer who knows football reads
    /// those instantly, and a lineman wearing 4 would be the first thing they noticed.</para>
    /// </remarks>
    public static class DriveRoster
    {
        public const string HomeName = "Bayside Ironhawks";
        public const string AwayName = "Northgate Sentinels";
        public const string HomeAbbreviation = "BAY";
        public const string AwayAbbreviation = "NOR";

        /// <summary>Both eleven-man units, offense first.</summary>
        public static Roster Build() => new Roster(Players(), HomeName, AwayName, HomeAbbreviation, AwayAbbreviation);

        static IEnumerable<PlayerIdentity> Players()
        {
            // ── Bayside Ironhawks — offense ────────────────────────────────────────────────────────
            yield return P(9,  "Marcus Vance",      PlayerPosition.Quarterback,   TeamSide.Home, 75, 218);
            yield return P(26, "Deion Pruitt",      PlayerPosition.RunningBack,   TeamSide.Home, 71, 214);
            yield return P(11, "Amari Colston",     PlayerPosition.WideReceiver,  TeamSide.Home, 73, 198);
            yield return P(83, "Trey Sandoval",     PlayerPosition.WideReceiver,  TeamSide.Home, 70, 186);
            yield return P(17, "Kai Whitfield",     PlayerPosition.WideReceiver,  TeamSide.Home, 72, 192);
            yield return P(85, "Boone Rasmussen",   PlayerPosition.TightEnd,      TeamSide.Home, 77, 252);
            yield return P(74, "Silas Redmond",     PlayerPosition.OffensiveLine, TeamSide.Home, 78, 315);
            yield return P(66, "Nolan Vitale",      PlayerPosition.OffensiveLine, TeamSide.Home, 76, 310);
            yield return P(55, "Ezra Lindqvist",    PlayerPosition.OffensiveLine, TeamSide.Home, 75, 302);
            yield return P(68, "Dario Pellegrini",  PlayerPosition.OffensiveLine, TeamSide.Home, 76, 318);
            yield return P(77, "Jonah Achebe",      PlayerPosition.OffensiveLine, TeamSide.Home, 79, 328);

            // ── Northgate Sentinels — defense ──────────────────────────────────────────────────────
            yield return P(91, "Tobias Grant",      PlayerPosition.DefensiveLine, TeamSide.Away, 76, 268);
            yield return P(94, "Vince Marchetti",   PlayerPosition.DefensiveLine, TeamSide.Away, 75, 305);
            yield return P(98, "Ruben Okafor",      PlayerPosition.DefensiveLine, TeamSide.Away, 74, 312);
            yield return P(90, "Kellen Voss",       PlayerPosition.DefensiveLine, TeamSide.Away, 75, 262);
            yield return P(54, "Isaiah Renfroe",    PlayerPosition.Linebacker,    TeamSide.Away, 73, 238);
            yield return P(58, "Emeka Bailey",      PlayerPosition.Linebacker,    TeamSide.Away, 74, 232);
            yield return P(24, "Jalen Moreau",      PlayerPosition.Cornerback,    TeamSide.Away, 71, 192);
            yield return P(21, "Rashad Tiller",     PlayerPosition.Cornerback,    TeamSide.Away, 72, 197);
            yield return P(29, "Dominic Pace",      PlayerPosition.Cornerback,    TeamSide.Away, 70, 186);
            yield return P(31, "Cyrus Delgado",     PlayerPosition.Safety,        TeamSide.Away, 73, 205);
            yield return P(33, "Grady Whitlock",    PlayerPosition.Safety,        TeamSide.Away, 74, 214);
        }

        static PlayerIdentity P(int number, string name, PlayerPosition position, TeamSide side,
                                int heightInches, int weightPounds) => new PlayerIdentity
        {
            Number = number,
            Name = name,
            Position = position,
            Side = side,
            HeightInches = heightInches,
            WeightPounds = weightPounds,
            UseDefaultLimits = true,
        };
    }
}
