using Stadium3D.Football;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// Pins the property the whole ball design exists for: the ball is a <b>pure function of time</b> read off
    /// the authored ball events, and is never integrated. A throw is placed on an arc whose ends are the
    /// passer's hand at the authored release and the receiver's hands at the authored catch, so it arrives on
    /// time and on the right body <em>by construction</em> — where a gravity-integrated ball only lands
    /// correctly once someone has tuned a launch velocity until it happens to, and stops landing correctly the
    /// moment the route underneath it moves.
    /// </summary>
    /// <remarks>
    /// Two consequences of that shape are worth a test each, because both are invisible until something looks
    /// wrong in the CAVE. Placing the release means sampling the passer at an instant <em>in the past</em>
    /// relative to the frame being drawn, which only a seekable <see cref="MotionTrack"/> can answer — the
    /// fixture's passer deliberately keeps moving through the flight so that "his pose at the release" and
    /// "his pose now" are different answers. And because nothing is carried between calls, sampling the play
    /// backwards has to return exactly what sampling it forwards did; that identity is what makes rewind,
    /// looping and slow motion fall out for free instead of needing three pieces of machinery.
    /// </remarks>
    public class BallSolverTests
    {
        // ── fixture ────────────────────────────────────────────────────────────────────────────────

        const int QbNumber = 12;
        const int WrNumber = 80;

        const float LineOfScrimmageYard = 35f;
        const float ThrowTime = 2f;
        const float ResolutionTime = 3.5f;      // the catch, or the incompletion, depending on the play
        const float ApexMeters = 4f;
        const float DeadAfterSeconds = 0.5f;
        const float PlayLengthSeconds = 6f;
        const float SampleHz = 60f;

        const float IncompleteTargetYard = LineOfScrimmageYard + 12f;
        const float IncompleteTargetLateral = -6f;

        /// <summary>Facing +X, which is downfield for the home offense in pitch-local space.</summary>
        const float DownfieldFacingDegrees = 90f;

        static int QbId => PlayerIdentity.PlayerKey(TeamSide.Home, QbNumber);
        static int WrId => PlayerIdentity.PlayerKey(TeamSide.Home, WrNumber);

        static Roster TwoManRoster() => new Roster(new[]
        {
            new PlayerIdentity { Number = QbNumber, Side = TeamSide.Home, Name = "Passer", Position = PlayerPosition.Quarterback },
            new PlayerIdentity { Number = WrNumber, Side = TeamSide.Home, Name = "Target", Position = PlayerPosition.WideReceiver },
        });

        static BakedPlay Bake(PlayScript play) => BakedPlay.Bake(play, TwoManRoster(), SampleHz);

        /// <summary>
        /// Two bodies and no ball events yet. The passer drops and then rolls so that he is still travelling
        /// through the whole flight — several tests only mean something if he is somewhere else at the catch
        /// than he was at the release.
        /// </summary>
        static PlayScript TwoManPlay()
        {
            var play = new PlayScript
            {
                Name = "Test — Half Roll Dig",
                Offense = TeamSide.Home,
                LineOfScrimmageYard = LineOfScrimmageYard,
                BallLateralYards = 0f,
                MinDurationSeconds = PlayLengthSeconds,
            };

            play.Players.Add(new PlayerScript
            {
                Number = QbNumber,
                Side = TeamSide.Home,
                RouteName = "half roll",
                InitialFacingDegrees = DownfieldFacingDegrees,
                Waypoints =
                {
                    new RouteWaypoint(-1f, 0f),
                    new RouteWaypoint(-7f, 0f),
                    new RouteWaypoint(-7f, 10f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = WrNumber,
                Side = TeamSide.Home,
                RouteName = "dig",
                InitialFacingDegrees = DownfieldFacingDegrees,
                Waypoints =
                {
                    new RouteWaypoint(0f, -12f),
                    new RouteWaypoint(12f, -12f, WaypointKind.Cut),
                    new RouteWaypoint(14f, -4f, WaypointKind.Stop),
                },
            });

            return play;
        }

        static BallEvent Snap() => new BallEvent
        {
            Time = 0f, Kind = BallEventKind.Snap, ToNumber = QbNumber, Side = TeamSide.Home,
        };

        static BallEvent Throw() => new BallEvent
        {
            Time = ThrowTime, Kind = BallEventKind.Throw, FromNumber = QbNumber, ToNumber = WrNumber,
            Side = TeamSide.Home, ApexMeters = ApexMeters,
        };

        /// <summary>Snap, throw, catch — the ball ends the play in the receiver's hands.</summary>
        static PlayScript Completion()
        {
            var play = TwoManPlay();

            play.BallEvents.Add(Snap());
            play.BallEvents.Add(Throw());
            play.BallEvents.Add(new BallEvent
            {
                Time = ResolutionTime, Kind = BallEventKind.Catch,
                FromNumber = QbNumber, ToNumber = WrNumber, Side = TeamSide.Home,
            });

            play.Outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PassComplete, YardsGained = 14f,
                PasserNumber = QbNumber, BallCarrierNumber = WrNumber,
            };

            return play;
        }

        /// <summary>Snap, throw, incompletion — the ball ends the play on the turf at an authored spot.</summary>
        static PlayScript Incompletion()
        {
            var play = TwoManPlay();

            play.BallEvents.Add(Snap());
            play.BallEvents.Add(Throw());
            play.BallEvents.Add(new BallEvent
            {
                Time = ResolutionTime, Kind = BallEventKind.Incomplete,
                FromNumber = QbNumber, ToNumber = WrNumber, Side = TeamSide.Home,
                TargetYardLine = IncompleteTargetYard,
                TargetLateralYards = IncompleteTargetLateral,
                DeadAfterSeconds = DeadAfterSeconds,
            });

            play.Outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PassIncomplete, YardsGained = 0f,
                PasserNumber = QbNumber, ClockRunsAfter = false,
            };

            return play;
        }

        /// <summary>
        /// Distance on the turf. The ball's height is a carry/release/catch offset the solver owns, so every
        /// "is the ball on this body" question is a plan-view question.
        /// </summary>
        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ── a held ball is the carrier's pose ──────────────────────────────────────────────────────

        [Test]
        public void BeforeTheSnap_TheBallIsAlreadyWithWhoeverTakesIt()
        {
            var play = Completion();
            var baked = Bake(play);

            var ball = BallSolver.Sample(play, baked, -0.5f);

            Assert.AreEqual(BallCarriage.Held, ball.Carriage);
            Assert.AreEqual(QbId, ball.CarrierId, "the huddle view must not show a ball with no owner");

            // Pre-snap the track holds the authored alignment, so the ball sits on it rather than on the
            // pitch origin — which would be the 50, and very obviously wrong on the floor view.
            Assert.That(HorizontalDistance(ball.Position, baked.PoseAtSnap(QbId).Position), Is.LessThan(0.6f));
            Assert.That(ball.YardLine, Is.EqualTo(LineOfScrimmageYard - 1f).Within(0.6f),
                "the ball starts in the hands of a passer aligned a yard behind the spot");
        }

        [Test]
        public void HeldBall_AcrossTheCarry_RidesTheCarriersPoseAtAFixedOffset()
        {
            var play = Completion();
            var baked = Bake(play);

            Vector3 firstOffset = Vector3.zero;

            for (int i = 0; i < 20; i++)
            {
                float t = i * 0.1f;                                   // 0 → 1.9 s, all of it before the throw
                var ball = BallSolver.Sample(play, baked, t);
                var carrier = baked.Sample(QbId, t);

                Assert.AreEqual(BallCarriage.Held, ball.Carriage, $"carriage at t = {t:0.00}");
                Assert.AreEqual(QbId, ball.CarrierId, $"carrier at t = {t:0.00}");

                // Resolving the gap back into the carrier's own frame is the real assertion: a constant local
                // offset means the ball follows his heading as well as his position, which is what "rides the
                // pose" has to mean for a body that is turning.
                Vector3 local = Quaternion.Inverse(carrier.Rotation) * (ball.Position - carrier.Position);
                if (i == 0) firstOffset = local;

                Assert.AreEqual(0f, Vector3.Distance(local, firstOffset), 1e-3f,
                    $"the carry offset drifted in the carrier's frame at t = {t:0.00}");
                Assert.That(local.y, Is.GreaterThan(0f), "a carried ball is off the turf");
                Assert.That(new Vector2(local.x, local.z).magnitude, Is.LessThan(0.6f),
                    "a carried ball is in his hands, not a stride away from him");
            }
        }

        [Test]
        public void HeldBall_MovesWithTheCarriersOwnSpeedAndFacing()
        {
            var play = Completion();
            var baked = Bake(play);

            const float t = 1f;                                       // mid-drop: the passer is under way
            var ball = BallSolver.Sample(play, baked, t);
            var carrier = baked.Sample(QbId, t);

            Assert.That(carrier.SpeedMps, Is.GreaterThan(1f),
                "the fixture's passer has to be moving here or this test proves nothing");

            Assert.AreEqual(carrier.SpeedMps, ball.Velocity.magnitude, 1e-2f,
                "a held ball travels at exactly the speed of the body holding it");
            Assert.That(Vector3.Angle(ball.Velocity, carrier.Rotation * Vector3.forward), Is.LessThan(0.5f),
                "and along the way he is facing — the trail and the spin are read off this");
        }

        // ── a throw is an arc between two authored, sampled-in-time endpoints ──────────────────────

        [Test]
        public void Throw_AtItsReleaseTime_LeavesThePassersHandAsHeWasAtThatInstant()
        {
            var play = Completion();
            var baked = Bake(play);

            var ball = BallSolver.Sample(play, baked, ThrowTime);
            var passerAtRelease = baked.Sample(QbId, ThrowTime);
            var passerAtCatch = baked.Sample(QbId, ResolutionTime);

            Assert.AreEqual(BallCarriage.InFlight, ball.Carriage);
            Assert.AreEqual(0, ball.CarrierId, "nobody owns the ball while it is in the air");

            Assert.That(HorizontalDistance(ball.Position, passerAtRelease.Position), Is.LessThan(0.6f),
                "the arc starts in the passer's hand; the residual is the authored release offset");
            Assert.That(ball.Position.y, Is.GreaterThan(0.5f), "released from a raised hand, not off the turf");

            // The point of the whole design: the release is anchored to a pose in the PAST relative to any
            // later frame that draws the flight. A passer who has since rolled away cannot drag the arc's
            // start with him — an integrator, which only ever knows where he is now, has no way to promise that.
            Assert.That(Vector3.Distance(passerAtRelease.Position, passerAtCatch.Position), Is.GreaterThan(1f),
                "the fixture's passer has to keep moving through the flight or this test proves nothing");
            Assert.That(HorizontalDistance(ball.Position, passerAtCatch.Position), Is.GreaterThan(1f));
        }

        [Test]
        public void Throw_AtItsResolutionTime_ArrivesOnTheReceiverPosedAtTheAuthoredCatchTime()
        {
            var play = Completion();
            var baked = Bake(play);

            // The receiver as he is AT THE AUTHORED CATCH — not where an in-flight ball happened to be
            // heading. This is the arrival an integrated ball cannot guarantee without a tuned launch
            // velocity, and cannot keep the moment the route underneath it is edited.
            var receiverAtCatch = baked.Sample(WrId, ResolutionTime);

            var lastAirborne = BallSolver.Sample(play, baked, ResolutionTime - 0.005f);
            Assert.AreEqual(BallCarriage.InFlight, lastAirborne.Carriage);
            Assert.That(HorizontalDistance(lastAirborne.Position, receiverAtCatch.Position), Is.LessThan(0.6f),
                "a frame before the catch the ball is already on the receiver");

            var atCatch = BallSolver.Sample(play, baked, ResolutionTime);
            Assert.AreEqual(BallCarriage.Held, atCatch.Carriage);
            Assert.AreEqual(WrId, atCatch.CarrierId);
            Assert.That(HorizontalDistance(atCatch.Position, receiverAtCatch.Position), Is.LessThan(0.6f),
                "and custody changes without the ball jumping");
        }

        [Test]
        public void Throw_AtTheFlightMidpoint_RidesAboveTheChordByTheAuthoredApex()
        {
            var play = Completion();
            var baked = Bake(play);

            float span = ResolutionTime - ThrowTime;
            var quarter = BallSolver.Sample(play, baked, ThrowTime + 0.25f * span);
            var middle = BallSolver.Sample(play, baked, ThrowTime + 0.5f * span);
            var threeQuarter = BallSolver.Sample(play, baked, ThrowTime + 0.75f * span);

            // In plan view the flight is the straight chord, so the midpoint of two symmetric samples IS the
            // chord's midpoint — which recovers the chord without needing either endpoint.
            Vector3 chordMid = 0.5f * (quarter.Position + threeQuarter.Position);
            Assert.AreEqual(chordMid.x, middle.Position.x, 1e-3f);
            Assert.AreEqual(chordMid.z, middle.Position.z, 1e-3f);

            // Height above the chord is 4·A·s·(1−s): A at s = 0.5, and 0.75·A at both s = 0.25 and s = 0.75.
            // Averaging the symmetric pair cancels the chord's own rise and leaves 0.75·A, so what is left is
            // a quarter of the authored apex — derived from the arc's shape rather than a hand-computed height.
            Assert.AreEqual(0.25f * ApexMeters, middle.Position.y - chordMid.y, 1e-3f);

            Assert.That(middle.Position.y, Is.GreaterThan(BallSolver.Sample(play, baked, ThrowTime).Position.y),
                "the ball is over the chord at midflight, not on it");
        }

        // ── custody over a whole play ──────────────────────────────────────────────────────────────

        [Test]
        public void Carriage_OnACompletion_GoesHeldThenInFlightThenHeld()
        {
            var play = Completion();
            var baked = Bake(play);

            var beforeTheThrow = BallSolver.Sample(play, baked, 0.5f);
            Assert.AreEqual(BallCarriage.Held, beforeTheThrow.Carriage);
            Assert.AreEqual(QbId, beforeTheThrow.CarrierId);

            var inTheAir = BallSolver.Sample(play, baked, 0.5f * (ThrowTime + ResolutionTime));
            Assert.AreEqual(BallCarriage.InFlight, inTheAir.Carriage);
            Assert.AreEqual(0, inTheAir.CarrierId);

            var afterTheCatch = BallSolver.Sample(play, baked, ResolutionTime + 0.5f);
            Assert.AreEqual(BallCarriage.Held, afterTheCatch.Carriage);
            Assert.AreEqual(WrId, afterTheCatch.CarrierId, "custody moved to the receiver, not back to the passer");

            // Nothing closes the play after the catch, so the last event stands to the end of the track.
            var atTheWhistle = BallSolver.Sample(play, baked, baked.Duration);
            Assert.AreEqual(BallCarriage.Held, atTheWhistle.Carriage);
            Assert.AreEqual(WrId, atTheWhistle.CarrierId);
        }

        [Test]
        public void Carriage_OnAnIncompletion_GoesHeldThenInFlightThenLooseThenDead()
        {
            var play = Incompletion();
            var baked = Bake(play);

            Assert.AreEqual(BallCarriage.Held, BallSolver.Sample(play, baked, 1f).Carriage);
            Assert.AreEqual(BallCarriage.InFlight, BallSolver.Sample(play, baked, ThrowTime + 1f).Carriage);

            // The ball is live on the turf for the authored settle time, then dead — the pause is what stops
            // an incompletion reading as the ball vanishing at the whistle.
            var settling = BallSolver.Sample(play, baked, ResolutionTime + 0.5f * DeadAfterSeconds);
            Assert.AreEqual(BallCarriage.Loose, settling.Carriage);
            Assert.AreEqual(0, settling.CarrierId, "an incompletion belongs to nobody");

            var dead = BallSolver.Sample(play, baked, ResolutionTime + DeadAfterSeconds + 0.1f);
            Assert.AreEqual(BallCarriage.Dead, dead.Carriage);

            // Both land at the authored spot; the only spread is the short settle slide the solver adds.
            Vector3 spot = FieldGeometry.Spot(IncompleteTargetYard, IncompleteTargetLateral);
            Assert.That(HorizontalDistance(settling.Position, spot), Is.LessThan(0.8f));
            Assert.That(HorizontalDistance(dead.Position, spot), Is.LessThan(0.8f));
            Assert.That(dead.Position.y, Is.LessThan(0.5f), "a dead ball is on the grass, not hanging over it");
            Assert.That(dead.YardLine, Is.EqualTo(IncompleteTargetYard).Within(1f),
                "the spot readout has to agree with where the ball is drawn");
        }

        // ── the ball is a pure function of time ────────────────────────────────────────────────────

        [Test]
        public void Sampling_BackwardsThroughThePlay_IsIdenticalToForwards()
        {
            AssertTimeReversible(Completion());
            AssertTimeReversible(Incompletion());
        }

        /// <summary>
        /// Sweep the play forwards, then walk the same instants back the other way and demand the same
        /// answers. Asserted with no tolerance on purpose: sampling carries nothing between calls, so a rewind
        /// is not "close enough to the same", it is the same arithmetic on the same inputs. That is the whole
        /// reason scrubbing and looping need no machinery of their own.
        /// </summary>
        static void AssertTimeReversible(PlayScript play)
        {
            var baked = Bake(play);

            const float dt = 0.05f;
            int steps = Mathf.CeilToInt(baked.Duration / dt) + 4;     // run a little past the whistle too

            var forward = new BallState[steps + 1];
            for (int i = 0; i <= steps; i++)
                forward[i] = BallSolver.Sample(play, baked, i * dt);

            for (int i = steps; i >= 0; i--)
            {
                var back = BallSolver.Sample(play, baked, i * dt);
                string where = $"{play.Name} at t = {i * dt:0.00}";

                Assert.AreEqual(forward[i].Carriage, back.Carriage, where);
                Assert.AreEqual(forward[i].CarrierId, back.CarrierId, where);

                Assert.That(back.Position.x, Is.EqualTo(forward[i].Position.x), where);
                Assert.That(back.Position.y, Is.EqualTo(forward[i].Position.y), where);
                Assert.That(back.Position.z, Is.EqualTo(forward[i].Position.z), where);

                Assert.That(back.Velocity.x, Is.EqualTo(forward[i].Velocity.x), where);
                Assert.That(back.Velocity.y, Is.EqualTo(forward[i].Velocity.y), where);
                Assert.That(back.Velocity.z, Is.EqualTo(forward[i].Velocity.z), where);

                Assert.That(back.YardLine, Is.EqualTo(forward[i].YardLine), where);
            }
        }

        [Test]
        public void YardAt_AgreesWithTheSampledBallsOwnYardLine()
        {
            var play = Completion();
            var baked = Bake(play);

            // The floor view's ball marker and the spot readout come from these two calls respectively, so
            // any gap between them would put a number on the wall that the field contradicts.
            foreach (float t in new[] { 0f, 1f, 0.5f * (ThrowTime + ResolutionTime), ResolutionTime, baked.Duration })
                Assert.That(BallSolver.YardAt(play, baked, t),
                            Is.EqualTo(BallSolver.Sample(play, baked, t).YardLine), $"t = {t:0.00}");
        }

        [Test]
        public void PlayWithNoBallEvents_LeavesTheBallDeadOnTheSpot()
        {
            var play = TwoManPlay();                                  // deliberately unauthored: no snap at all
            var baked = Bake(play);

            var ball = BallSolver.Sample(play, baked, 1f);

            Assert.AreEqual(BallCarriage.Dead, ball.Carriage);
            Assert.AreEqual(0, ball.CarrierId);
            Assert.That(ball.YardLine, Is.EqualTo(play.LineOfScrimmageYard).Within(1e-3f),
                "with nothing authored the ball rests on the spot, not at the pitch origin");
            Assert.That(ball.Position.y, Is.LessThan(0.5f), "and on the turf");
        }
    }
}
