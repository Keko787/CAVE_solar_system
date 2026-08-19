using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// The guard that stops authored content from silently breaking the demo. Every check here is for a
    /// mistake that is invisible at the desk and obvious in the CAVE — a throw with nothing to catch it, a
    /// body aligned three yards out of bounds, an event scheduled after the whistle.
    /// </summary>
    /// <remarks>
    /// Returns messages rather than throwing, so an EditMode test can assert the shipped drive is clean and a
    /// build step can print everything wrong at once instead of one thing per run.
    /// </remarks>
    public static class PlayValidator
    {
        /// <summary>Validate one play. An empty list means it is sound.</summary>
        public static List<string> Validate(PlayScript play, Roster roster, BakedPlay baked = null)
        {
            var errors = new List<string>();
            string where = play == null ? "<null play>" : play.Name;

            if (play == null)
            {
                errors.Add("Play is null.");
                return errors;
            }

            // ── roster coverage ───────────────────────────────────────────────────────────────────
            var seen = new HashSet<int>();
            for (int i = 0; i < play.Players.Count; i++)
            {
                var ps = play.Players[i];
                if (ps == null)
                {
                    errors.Add($"{where}: player assignment {i} is null.");
                    continue;
                }

                if (roster != null && !roster.Contains(ps.PlayerId))
                    errors.Add($"{where}: #{ps.Number} ({ps.Side}) has an assignment but is not on the roster.");

                if (!seen.Add(ps.PlayerId))
                    errors.Add($"{where}: #{ps.Number} ({ps.Side}) has more than one assignment.");

                if (ps.Waypoints == null || ps.Waypoints.Count == 0)
                    errors.Add($"{where}: #{ps.Number} has no waypoints — not even an alignment.");
            }

            if (roster != null)
            {
                for (int i = 0; i < roster.All.Count; i++)
                {
                    var p = roster.All[i];
                    if (!seen.Contains(p.Id))
                        errors.Add($"{where}: #{p.Number} ({p.Side}) is on the roster but has no assignment.");
                }
            }

            // ── alignment is on the field and on the right side of the ball ───────────────────────
            for (int i = 0; i < play.Players.Count; i++)
            {
                var ps = play.Players[i];
                if (ps?.Waypoints == null || ps.Waypoints.Count == 0) continue;

                var first = ps.Waypoints[0];
                if (first == null) continue;

                Vector3 spot = first.ToPitchLocal(play.LineOfScrimmageYard, play.BallLateralYards, play.Offense);
                if (!FieldGeometry.InBounds(spot))
                    errors.Add($"{where}: #{ps.Number} aligns out of bounds at " +
                               $"{FieldGeometry.YardAt(spot):0.0} yd / {FieldGeometry.LateralAt(spot):0.0} lateral.");
            }

            // ── ball events ───────────────────────────────────────────────────────────────────────
            var events = play.BallEvents;
            if (events == null || events.Count == 0)
            {
                errors.Add($"{where}: no ball events — every play needs at least a snap.");
                return errors;
            }

            if (events[0] == null || events[0].Kind != BallEventKind.Snap)
                errors.Add($"{where}: the first ball event must be a Snap.");
            else if (!Mathf.Approximately(events[0].Time, 0f))
                errors.Add($"{where}: the snap must be at t = 0, not {events[0].Time:0.00}.");

            float last = float.NegativeInfinity;
            float duration = baked?.Duration ?? float.MaxValue;

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e == null) { errors.Add($"{where}: ball event {i} is null."); continue; }

                if (e.Time < last)
                    errors.Add($"{where}: ball event {i} ({e.Kind}) at {e.Time:0.00}s is out of time order.");
                last = e.Time;

                if (e.Time < 0f)
                    errors.Add($"{where}: ball event {i} ({e.Kind}) is before the snap.");

                if (baked != null && e.Time > duration + 1e-3f)
                    errors.Add($"{where}: ball event {i} ({e.Kind}) at {e.Time:0.00}s is after the play ends " +
                               $"at {duration:0.00}s.");

                if (roster != null)
                {
                    if (NeedsFrom(e.Kind) && !roster.Contains(e.FromPlayerId))
                        errors.Add($"{where}: ball event {i} ({e.Kind}) comes from #{e.FromNumber}, who is not on the roster.");
                    if (NeedsTo(e.Kind) && !roster.Contains(e.ToPlayerId))
                        errors.Add($"{where}: ball event {i} ({e.Kind}) goes to #{e.ToNumber}, who is not on the roster.");
                }
            }

            // ── every throw is closed exactly once ────────────────────────────────────────────────
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] == null || events[i].Kind != BallEventKind.Throw) continue;

                bool closed = false;
                for (int j = i + 1; j < events.Count; j++)
                {
                    var k = events[j]?.Kind;
                    if (k == BallEventKind.Catch || k == BallEventKind.Incomplete) { closed = true; break; }
                    if (k == BallEventKind.Throw) break;
                }

                if (!closed)
                    errors.Add($"{where}: the throw at {events[i].Time:0.00}s is never caught or ruled incomplete.");
            }

            // ── the outcome agrees with the ball events ───────────────────────────────────────────
            if (play.Outcome == null)
            {
                errors.Add($"{where}: no outcome — the drive cannot fold without one.");
            }
            else
            {
                bool hasIncomplete = Contains(events, BallEventKind.Incomplete);
                if (play.Outcome.Kind == PlayResultKind.PassIncomplete && !hasIncomplete)
                    errors.Add($"{where}: outcome says the pass fell incomplete but no Incomplete event exists.");
                if (hasIncomplete && play.Outcome.Kind == PlayResultKind.PassComplete)
                    errors.Add($"{where}: outcome says the pass was complete but the ball events say otherwise.");
                if (play.Outcome.HasPenalty && string.IsNullOrEmpty(play.Outcome.PenaltyName))
                    errors.Add($"{where}: a penalty is flagged but unnamed — the wall would show a blank.");
            }

            return errors;
        }

        /// <summary>Validate a whole drive, plus the properties that only make sense across plays.</summary>
        public static List<string> ValidateDrive(DriveScript drive, IReadOnlyList<BakedPlay> baked = null)
        {
            var errors = new List<string>();

            if (drive == null) { errors.Add("Drive is null."); return errors; }
            if (drive.Plays == null || drive.Plays.Count == 0) { errors.Add("Drive has no plays."); return errors; }
            if (drive.Roster == null || drive.Roster.All.Count == 0) { errors.Add("Drive has no roster."); return errors; }

            for (int i = 0; i < drive.Plays.Count; i++)
                errors.AddRange(Validate(drive.Plays[i], drive.Roster, baked != null && i < baked.Count ? baked[i] : null));

            // The spot each play starts on must be where the previous one's outcome left the ball, or the
            // scoreboard and the field disagree — which reads as "the sim is broken" rather than "the content
            // has a typo".
            var situation = drive.Start;
            for (int i = 0; i < drive.Plays.Count; i++)
            {
                var play = drive.Plays[i];
                if (!Mathf.Approximately(Mathf.Round(situation.LineOfScrimmageYard * 100f) / 100f,
                                         Mathf.Round(play.LineOfScrimmageYard * 100f) / 100f))
                {
                    errors.Add($"Play {i} ({play.Name}) is authored at the {play.LineOfScrimmageYard:0.#} " +
                               $"but the drive fold puts the ball on the {situation.LineOfScrimmageYard:0.#}.");
                }

                if (play.Offense != situation.Possession)
                    errors.Add($"Play {i} ({play.Name}) is authored for {play.Offense} but {situation.Possession} has the ball.");

                situation = GameStateProjector.Next(situation, play.Outcome);
            }

            return errors;
        }

        static bool NeedsFrom(BallEventKind k) =>
            k == BallEventKind.Handoff || k == BallEventKind.Throw ||
            k == BallEventKind.Down || k == BallEventKind.CrossGoalLine;

        static bool NeedsTo(BallEventKind k) =>
            k == BallEventKind.Snap || k == BallEventKind.Handoff || k == BallEventKind.Catch;

        static bool Contains(IReadOnlyList<BallEvent> events, BallEventKind kind)
        {
            for (int i = 0; i < events.Count; i++)
                if (events[i] != null && events[i].Kind == kind)
                    return true;
            return false;
        }
    }
}
