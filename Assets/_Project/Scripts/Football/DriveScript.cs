using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>A whole authored drive: who is playing, what they run, and how the transport paces it.</summary>
    public sealed class DriveScript
    {
        public Roster Roster { get; }
        public IReadOnlyList<PlayScript> Plays { get; }

        /// <summary>The situation at the snap of the first play.</summary>
        public GameSituation Start { get; }

        /// <summary>Seconds of huddle between plays, during which bodies walk to their next alignment.</summary>
        public float HuddleSeconds { get; }

        /// <summary>Seconds the result holds on the walls after the whistle, before the next huddle.</summary>
        public float PostPlaySeconds { get; }

        /// <summary>Return to play 0 after the last play instead of stopping.</summary>
        public bool Loop { get; }

        /// <summary>
        /// Bake rate. 60 is visually indistinguishable from higher; 30 halves the memory and is still fine
        /// after interpolation. Below 30 a tight cut starts to visibly flatten, because
        /// <see cref="MotionSample.Lerp"/> is a straight chord between samples.
        /// </summary>
        public float SampleHz { get; }

        public DriveScript(Roster roster, IReadOnlyList<PlayScript> plays, GameSituation start,
                           float huddleSeconds = 3f, float postPlaySeconds = 2.5f,
                           bool loop = true, float sampleHz = 60f)
        {
            Roster = roster;
            Plays = plays;
            Start = start;
            HuddleSeconds = Mathf.Max(0.1f, huddleSeconds);
            PostPlaySeconds = Mathf.Max(0.1f, postPlaySeconds);
            Loop = loop;
            SampleHz = Mathf.Clamp(sampleHz, 15f, 120f);
        }
    }

    /// <summary>One team's aggregate line — the front wall's team-comparison block.</summary>
    public struct TeamStats
    {
        public float TotalYards;
        public float PassingYards;
        public float RushingYards;
        public int FirstDowns;
        public int ThirdDownAttempts;
        public int ThirdDownConversions;
        public float TimeOfPossessionSeconds;
        public int Turnovers;
        public int Penalties;
        public float PenaltyYards;
        public int Points;

        /// <summary>"3 / 5" for the third-down line.</summary>
        public string ThirdDownDisplay() => $"{ThirdDownConversions} / {ThirdDownAttempts}";

        /// <summary>Time of possession as "2:58".</summary>
        public string TimeOfPossessionDisplay()
        {
            int m = Mathf.FloorToInt(TimeOfPossessionSeconds / 60f);
            int s = Mathf.FloorToInt(TimeOfPossessionSeconds - m * 60f);
            return $"{m}:{s:00}";
        }
    }

    /// <summary>One player's line, for the focused-player card.</summary>
    public struct PlayerStats
    {
        public int PassAttempts;
        public int PassCompletions;
        public float PassingYards;
        public int PassingTouchdowns;
        public int Interceptions;

        public int Carries;
        public float RushingYards;
        public int RushingTouchdowns;

        public int Targets;
        public int Receptions;
        public float ReceivingYards;
        public int ReceivingTouchdowns;
        public float LongestReception;

        public int Sacks;

        /// <summary>Passer rating, NFL formula, clamped to the legal 0–158.3 range.</summary>
        public float PasserRating()
        {
            if (PassAttempts <= 0) return 0f;

            float a = Mathf.Clamp((PassCompletions / (float)PassAttempts - 0.3f) * 5f, 0f, 2.375f);
            float b = Mathf.Clamp((PassingYards / PassAttempts - 3f) * 0.25f, 0f, 2.375f);
            float c = Mathf.Clamp((PassingTouchdowns / (float)PassAttempts) * 20f, 0f, 2.375f);
            float d = Mathf.Clamp(2.375f - (Interceptions / (float)PassAttempts * 25f), 0f, 2.375f);
            return (a + b + c + d) / 6f * 100f;
        }
    }

    /// <summary>Both teams' and every player's stats after some number of plays.</summary>
    public sealed class BoxScore
    {
        readonly Dictionary<int, PlayerStats> m_Players = new Dictionary<int, PlayerStats>();

        public TeamStats Home;
        public TeamStats Away;

        /// <summary>This player's line. Returns an empty line for anyone who has not touched the ball.</summary>
        public PlayerStats For(int playerId) => m_Players.TryGetValue(playerId, out var s) ? s : default;

        /// <summary>Team aggregate for a side.</summary>
        public TeamStats Team(TeamSide side) => side == TeamSide.Home ? Home : Away;

        internal void Mutate(int playerId, System.Func<PlayerStats, PlayerStats> edit)
        {
            m_Players.TryGetValue(playerId, out var s);
            m_Players[playerId] = edit(s);
        }

        internal ref TeamStats TeamRef(TeamSide side) =>
            ref (side == TeamSide.Home ? ref Home : ref Away);

        internal BoxScore Clone()
        {
            var copy = new BoxScore { Home = Home, Away = Away };
            foreach (var kv in m_Players) copy.m_Players[kv.Key] = kv.Value;
            return copy;
        }
    }

    /// <summary>
    /// Precomputes, once at bind time, the situation at the snap of <em>every</em> play and the box score
    /// after each — by folding the authored outcomes forward a single time.
    /// </summary>
    /// <remarks>
    /// Folding once rather than accumulating as plays go by is what makes the ledger <b>path-independent</b>:
    /// jumping straight to play 4, or looping the drive twice, produces exactly the state that playing
    /// through would have. An accumulating scoreboard would double-count on the second lap.
    /// </remarks>
    public sealed class DriveLedger
    {
        readonly GameSituation[] m_AtSnap;
        readonly BoxScore[] m_After;

        public int PlayCount => m_AtSnap.Length;

        public DriveLedger(DriveScript drive)
        {
            var plays = drive.Plays;
            m_AtSnap = new GameSituation[plays.Count];
            m_After = new BoxScore[plays.Count + 1];

            var situation = drive.Start;
            var box = new BoxScore();
            m_After[0] = box.Clone();

            for (int i = 0; i < plays.Count; i++)
            {
                m_AtSnap[i] = situation;

                var play = plays[i];
                Accumulate(box, situation, play);

                situation = GameStateProjector.Next(situation, play.Outcome);
                box.Home.Points = situation.HomeScore;
                box.Away.Points = situation.AwayScore;

                m_After[i + 1] = box.Clone();
            }
        }

        /// <summary>The situation at the snap of play <paramref name="index"/>.</summary>
        public GameSituation StateAtSnap(int index) =>
            m_AtSnap[Mathf.Clamp(index, 0, m_AtSnap.Length - 1)];

        /// <summary>The box score after <paramref name="playsCompleted"/> plays.</summary>
        public BoxScore ScoreAfter(int playsCompleted) =>
            m_After[Mathf.Clamp(playsCompleted, 0, m_After.Length - 1)];

        static void Accumulate(BoxScore box, in GameSituation atSnap, PlayScript play)
        {
            var o = play.Outcome;
            if (o == null) return;

            TeamSide offense = play.Offense;
            ref TeamStats team = ref box.TeamRef(offense);

            team.TimeOfPossessionSeconds += Mathf.Max(0f, o.ElapsedSeconds);

            if (atSnap.Down == 3)
            {
                team.ThirdDownAttempts++;
                if (o.FirstDown || o.Kind == PlayResultKind.Touchdown)
                    team.ThirdDownConversions++;
            }

            if (o.HasPenalty)
            {
                ref TeamStats flagged = ref box.TeamRef(o.PenalizedSide);
                flagged.Penalties++;
                flagged.PenaltyYards += o.PenaltyYards;
            }

            // A nullified snap counts for nothing but the flag and the clock.
            if (o.Kind == PlayResultKind.PenaltyNullified)
                return;

            if (o.FirstDown || o.Kind == PlayResultKind.Touchdown)
                team.FirstDowns++;

            int passerId = PlayerIdentity.PlayerKey(offense, o.PasserNumber);
            int carrierId = PlayerIdentity.PlayerKey(offense, o.BallCarrierNumber);
            bool touchdown = o.Kind == PlayResultKind.Touchdown;
            bool throughTheAir = o.Kind == PlayResultKind.PassComplete
                                 || o.Kind == PlayResultKind.PassIncomplete
                                 || (touchdown && o.PasserNumber != 0);

            switch (o.Kind)
            {
                case PlayResultKind.Rush:
                    team.TotalYards += o.YardsGained;
                    team.RushingYards += o.YardsGained;
                    box.Mutate(carrierId, s => { s.Carries++; s.RushingYards += o.YardsGained; return s; });
                    break;

                case PlayResultKind.PassComplete:
                    team.TotalYards += o.YardsGained;
                    team.PassingYards += o.YardsGained;
                    box.Mutate(passerId, s => { s.PassAttempts++; s.PassCompletions++; s.PassingYards += o.YardsGained; return s; });
                    box.Mutate(carrierId, s =>
                    {
                        s.Targets++; s.Receptions++; s.ReceivingYards += o.YardsGained;
                        s.LongestReception = Mathf.Max(s.LongestReception, o.YardsGained);
                        return s;
                    });
                    break;

                case PlayResultKind.PassIncomplete:
                    box.Mutate(passerId, s => { s.PassAttempts++; return s; });
                    if (o.BallCarrierNumber != 0)
                        box.Mutate(carrierId, s => { s.Targets++; return s; });
                    break;

                case PlayResultKind.Sack:
                    // NFL convention: sack yardage is charged against team passing, not the passer's rushing.
                    team.TotalYards += o.YardsGained;
                    team.PassingYards += o.YardsGained;
                    if (o.BallCarrierNumber != 0)
                        box.Mutate(carrierId, s => { s.Sacks++; return s; });
                    break;

                case PlayResultKind.Touchdown:
                    team.TotalYards += o.YardsGained;
                    if (throughTheAir)
                    {
                        team.PassingYards += o.YardsGained;
                        box.Mutate(passerId, s =>
                        {
                            s.PassAttempts++; s.PassCompletions++;
                            s.PassingYards += o.YardsGained; s.PassingTouchdowns++;
                            return s;
                        });
                        box.Mutate(carrierId, s =>
                        {
                            s.Targets++; s.Receptions++; s.ReceivingYards += o.YardsGained;
                            s.ReceivingTouchdowns++;
                            s.LongestReception = Mathf.Max(s.LongestReception, o.YardsGained);
                            return s;
                        });
                    }
                    else
                    {
                        team.RushingYards += o.YardsGained;
                        box.Mutate(carrierId, s => { s.Carries++; s.RushingYards += o.YardsGained; s.RushingTouchdowns++; return s; });
                    }
                    break;

                case PlayResultKind.Turnover:
                    team.Turnovers++;
                    break;
            }
        }
    }
}
