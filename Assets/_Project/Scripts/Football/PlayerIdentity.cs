using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// The roster record behind the focused-player card on a team wall. Identity only — where a player is
    /// and how fast they are moving lives in <see cref="MotionTrack"/>, so nothing here changes during a play.
    /// </summary>
    [Serializable]
    public class PlayerIdentity
    {
        [Tooltip("Jersey number. Unique within a team, and the id every play script refers to a player by.")]
        public int Number;

        [Tooltip("Display name for the player card.")]
        public string Name = "Player";

        [Tooltip("Selects which stat line the card shows.")]
        public PlayerPosition Position = PlayerPosition.WideReceiver;

        [Tooltip("Which team — and therefore which CAVE wall reads this player as \"ours\".")]
        public TeamSide Side = TeamSide.Home;

        [Min(0)] [Tooltip("Height in inches, for the card.")]
        public int HeightInches = 72;

        [Min(0)] [Tooltip("Weight in pounds, for the card.")]
        public int WeightPounds = 200;

        [Tooltip("Motion envelope. Leave UseDefaultLimits set to take the position's defaults.")]
        public bool UseDefaultLimits = true;

        [Tooltip("Explicit motion envelope, used only when UseDefaultLimits is clear.")]
        public MotionLimits Limits;

        /// <summary>
        /// The id a <see cref="PlayerScript"/> uses. Jersey numbers repeat across teams — a #24 corner and a
        /// #24 back can be on the field together — so the id folds the side in.
        /// </summary>
        public int Id => PlayerKey(Side, Number);

        /// <summary>Stable id for a (side, number) pair.</summary>
        public static int PlayerKey(TeamSide side, int number) => ((int)side * 1000) + number;

        /// <summary>The motion envelope this player actually bakes with.</summary>
        public MotionLimits EffectiveLimits => UseDefaultLimits ? MotionLimits.For(Position) : Limits;

        /// <summary>Height as feet and inches, e.g. "6-3".</summary>
        public string HeightDisplay => $"{HeightInches / 12}-{HeightInches % 12}";

        /// <summary>Short position label for the card, e.g. "QB".</summary>
        public string PositionAbbreviation
        {
            get
            {
                switch (Position)
                {
                    case PlayerPosition.Quarterback:   return "QB";
                    case PlayerPosition.RunningBack:   return "RB";
                    case PlayerPosition.WideReceiver:  return "WR";
                    case PlayerPosition.TightEnd:      return "TE";
                    case PlayerPosition.OffensiveLine: return "OL";
                    case PlayerPosition.DefensiveLine: return "DL";
                    case PlayerPosition.Linebacker:    return "LB";
                    case PlayerPosition.Cornerback:    return "CB";
                    case PlayerPosition.Safety:        return "S";
                    default:                           return "--";
                }
            }
        }
    }

    /// <summary>
    /// Both teams' players, indexed for O(1) lookup by id. Built once when a drive binds.
    /// </summary>
    public sealed class Roster
    {
        readonly Dictionary<int, PlayerIdentity> m_ById;
        readonly PlayerIdentity[] m_All;

        public IReadOnlyList<PlayerIdentity> All => m_All;

        /// <summary>Team name shown on the scoreboard, by side.</summary>
        public string HomeName { get; }
        public string AwayName { get; }

        /// <summary>Three-letter code for the scoreboard strip, e.g. "BAY".</summary>
        public string HomeAbbreviation { get; }
        public string AwayAbbreviation { get; }

        public Roster(IEnumerable<PlayerIdentity> players,
                      string homeName = "Home", string awayName = "Away",
                      string homeAbbreviation = "HOM", string awayAbbreviation = "AWY")
        {
            var list = new List<PlayerIdentity>();
            m_ById = new Dictionary<int, PlayerIdentity>();

            foreach (var p in players)
            {
                if (p == null || m_ById.ContainsKey(p.Id)) continue;
                m_ById.Add(p.Id, p);
                list.Add(p);
            }

            m_All = list.ToArray();
            HomeName = homeName;
            AwayName = awayName;
            HomeAbbreviation = homeAbbreviation;
            AwayAbbreviation = awayAbbreviation;
        }

        /// <summary>The player with this id, or null.</summary>
        public PlayerIdentity Get(int id) => m_ById.TryGetValue(id, out var p) ? p : null;

        /// <summary>True if the id is on the roster.</summary>
        public bool Contains(int id) => m_ById.ContainsKey(id);

        /// <summary>Team name for a side.</summary>
        public string NameOf(TeamSide side) => side == TeamSide.Home ? HomeName : AwayName;

        /// <summary>Team abbreviation for a side.</summary>
        public string AbbreviationOf(TeamSide side) => side == TeamSide.Home ? HomeAbbreviation : AwayAbbreviation;

        /// <summary>
        /// Every player on one side, in roster order. This is the cycle a team wall's next/previous-player
        /// buttons walk, so the order is stable rather than whatever a dictionary happened to enumerate.
        /// </summary>
        public IReadOnlyList<PlayerIdentity> Side(TeamSide side)
        {
            var list = new List<PlayerIdentity>();
            for (int i = 0; i < m_All.Length; i++)
                if (m_All[i].Side == side)
                    list.Add(m_All[i]);
            return list;
        }
    }
}
