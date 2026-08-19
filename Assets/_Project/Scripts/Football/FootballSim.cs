using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// The façade, and the only thing the Unity layer talks to. Deliberately shaped like the source project's
    /// <c>SimulatedFeedSource</c> — a plain class with <c>Tick(double dt)</c> and pose accessors — so the
    /// MonoBehaviour driver stays a thin adapter and every invariant below is testable off the Unity run loop.
    /// </summary>
    /// <remarks>
    /// The whole design is one idea: <b>bake the dynamics once, evaluate purely thereafter</b>. Routes bake
    /// into fixed-rate <see cref="MotionTrack"/>s at construction; the ball is derived from its surrounding
    /// pair of <see cref="BallEvent"/>s; the situation is a fold <see cref="DriveLedger"/> performs once. Per
    /// frame there is no integration anywhere, so nothing accumulates, nothing drifts, and seeking backwards
    /// is free and exact — which is what makes replay, looping and slow motion fall out for nothing instead of
    /// each needing its own machinery.
    /// </remarks>
    public sealed class FootballSim
    {
        readonly DriveScript m_Drive;
        readonly BakedPlay[] m_Baked;
        readonly DriveLedger m_Ledger;
        readonly DriveDirector m_Director;

        readonly PlayerFrame[] m_Frames;
        readonly int[] m_RosterIds;

        BallState m_Ball;
        GameSituation m_Situation;

        /// <summary>The transport: pause, replay, seek, time scale.</summary>
        public DriveDirector Director => m_Director;

        /// <summary>Both teams' players.</summary>
        public Roster Roster => m_Drive.Roster;

        /// <summary>The drive this sim was bound to.</summary>
        public DriveScript Drive => m_Drive;

        /// <summary>Every player's frame this tick, in stable roster order.</summary>
        public IReadOnlyList<PlayerFrame> Frames => m_Frames;

        /// <summary>The ball this tick.</summary>
        public BallState Ball => m_Ball;

        /// <summary>The game state this tick.</summary>
        public GameSituation Situation => m_Situation;

        /// <summary>The play currently on the field.</summary>
        public PlayScript CurrentPlay => m_Baked[m_Director.PlayIndex].Script;

        /// <summary>The baked form of the current play, for anything that needs to sample off-frame.</summary>
        public BakedPlay CurrentBakedPlay => m_Baked[m_Director.PlayIndex];

        /// <summary>Box score as of the plays completed so far.</summary>
        public BoxScore Score => m_Ledger.ScoreAfter(
            m_Director.Phase == PlayPhase.PostPlay ? m_Director.PlayIndex + 1 : m_Director.PlayIndex);

        /// <summary>Warnings from every play's bake, for the authoring pass.</summary>
        public BakeReport Report { get; }

        public FootballSim(DriveScript drive)
        {
            m_Drive = drive;
            Report = new BakeReport();

            m_Baked = new BakedPlay[drive.Plays.Count];
            for (int i = 0; i < drive.Plays.Count; i++)
                m_Baked[i] = BakedPlay.Bake(drive.Plays[i], drive.Roster, drive.SampleHz, Report);

            m_Ledger = new DriveLedger(drive);
            m_Director = new DriveDirector(drive, PlayDuration);

            var ids = new List<int>();
            for (int i = 0; i < drive.Roster.All.Count; i++)
                ids.Add(drive.Roster.All[i].Id);
            m_RosterIds = ids.ToArray();
            m_Frames = new PlayerFrame[m_RosterIds.Length];

            Evaluate();
        }

        /// <summary>Length of a play in seconds, derived from its bake.</summary>
        public float PlayDuration(int index) =>
            m_Baked[Mathf.Clamp(index, 0, m_Baked.Length - 1)].Duration;

        /// <summary>
        /// Advance the sim by <paramref name="dt"/> seconds and re-derive everything. Call exactly once per
        /// frame, from exactly one place.
        /// </summary>
        /// <remarks>
        /// A second caller — a per-camera callback, a <c>LateUpdate</c> as well as an <c>Update</c>, a second
        /// driver in a merged scene — advances the drive twice per frame and every clock runs at double speed.
        /// The symptom is a slightly fast game clock, which reads like a content problem and is not.
        /// </remarks>
        public void Tick(double dt)
        {
            m_Director.Tick(dt);
            Evaluate();
        }

        /// <summary>The pose of a player this tick.</summary>
        public bool TryGetPose(int playerId, out Vector3 position, out Quaternion rotation)
        {
            for (int i = 0; i < m_Frames.Length; i++)
            {
                if (m_Frames[i].PlayerId != playerId) continue;
                position = m_Frames[i].Position;
                rotation = m_Frames[i].Rotation;
                return true;
            }
            position = default;
            rotation = default;
            return false;
        }

        /// <summary>Live telemetry for a focused player, for a team wall.</summary>
        public LiveTelemetry TelemetryFor(int playerId)
        {
            var script = CurrentPlay.ScriptFor(playerId);
            return TelemetryBuilder.Build(playerId, m_Frames, script != null ? script.RouteName : "");
        }

        // ── the pure derivation ────────────────────────────────────────────────────────────────────

        void Evaluate()
        {
            var play = m_Baked[m_Director.PlayIndex];
            var phase = m_Director.Phase;
            float tPlay = phase == PlayPhase.Huddle ? 0f : Mathf.Clamp(m_Director.PlayTime, 0f, play.Duration);

            bool huddling = phase == PlayPhase.Huddle;
            int prev = m_Director.PreviousPlayIndex;
            bool blending = huddling && prev >= 0 && prev < m_Baked.Length;
            float u = blending ? Mathf.Clamp01(m_Director.PhaseTime / m_Drive.HuddleSeconds) : 0f;

            m_Ball = huddling
                ? new BallState(BallCarriage.Dead, 0,
                    FieldGeometry.Spot(play.Script.LineOfScrimmageYard, play.Script.BallLateralYards) + Vector3.up * 0.12f,
                    Vector3.zero)
                : BallSolver.Sample(play.Script, play, tPlay);

            for (int i = 0; i < m_RosterIds.Length; i++)
            {
                int id = m_RosterIds[i];
                var identity = m_Drive.Roster.Get(id);
                MotionSample s;

                if (blending)
                {
                    var from = m_Baked[prev].Has(id) ? m_Baked[prev].TerminalPose(id) : play.PoseAtSnap(id);
                    var to = play.PoseAtSnap(id);
                    s = HuddleBlend.Sample(from, to, u);
                }
                else
                {
                    s = play.Sample(id, tPlay);
                }

                m_Frames[i] = new PlayerFrame(
                    id,
                    identity != null ? identity.Side : TeamSide.Home,
                    s.Position, s.Rotation,
                    s.SpeedMps, s.PeakSpeedMps, s.DistanceMeters,
                    m_Ball.Carriage == BallCarriage.Held && m_Ball.CarrierId == id);
            }

            var atSnap = m_Ledger.StateAtSnap(m_Director.PlayIndex);
            m_Situation = huddling
                ? GameStateProjector.Huddle(atSnap, m_Director.PhaseTime, m_Drive.HuddleSeconds)
                : GameStateProjector.InPlay(atSnap, play.Script, tPlay, m_Ball.YardLine);
        }
    }
}
