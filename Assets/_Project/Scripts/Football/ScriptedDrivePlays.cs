using System.Collections.Generic;

namespace Stadium3D.Football
{
    /// <summary>
    /// The six snaps of the shipped drive, one method each. Pure data — every route is authored waypoints in
    /// yards relative to the ball, so a play reads like the diagram it came from and can be run from any spot
    /// on the field.
    /// </summary>
    /// <remarks>
    /// Depth is downfield from the line of scrimmage and lateral is positive to the offense's right, which is
    /// how a play is drawn and talked about. <see cref="RouteBaker"/> is what turns these into motion, and it
    /// is the thing that decides how fast a body can actually come through an authored break — so a corner
    /// that looks too slow on the floor view is a physics answer, not a bug. <see cref="BakeReport"/> surfaces
    /// those at authoring time.
    ///
    /// <para>Only skill players carry real routes. Most of the offensive line gets two or three waypoints,
    /// because on the wall they are a wash of bodies at the line and nobody is watching a guard's footwork —
    /// spending waypoints there would cost bake memory and buy nothing.</para>
    /// </remarks>
    public static class ScriptedDrivePlays
    {
        /// <summary>The drive in order, first snap to the touchdown.</summary>
        public static IReadOnlyList<PlayScript> All() => new List<PlayScript>
        {
            BuildPlay1(),
            BuildPlay2(),
            BuildPlay3(),
            BuildPlay4(),
            BuildPlay5(),
            BuildPlay6(),
        };

        static PlayScript BuildPlay1()
        {
            // Local shorthand so the route data reads like a play diagram instead of a wall of initializers.
            static RouteWaypoint At(float depth, float lateral) => new RouteWaypoint(depth, lateral);
            static RouteWaypoint Cut(float depth, float lateral, float speed) =>
                new RouteWaypoint(depth, lateral, WaypointKind.Cut) { SpeedFraction = speed };
            static RouteWaypoint End(float depth, float lateral) =>
                new RouteWaypoint(depth, lateral, WaypointKind.Stop);

            static PlayerScript Home(int number, string route, params RouteWaypoint[] path) => new PlayerScript
            {
                Number = number,
                Side = TeamSide.Home,
                RouteName = route,
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>(path),
            };

            static PlayerScript Away(int number, string route, params RouteWaypoint[] path) => new PlayerScript
            {
                Number = number,
                Side = TeamSide.Away,
                RouteName = route,
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>(path),
            };

            var play = new PlayScript
            {
                Name = "Gun Split Right — Inside Zone Right",
                Formation = "Shotgun 2x2 Split Right, 11 personnel (TE attached right, RB offset right)",
                DefensiveCall = "Nickel 4-2-5 Over — Cover 3 Sky, SS rolled down",
                Offense = TeamSide.Home,
                LineOfScrimmageYard = 35f,                          // Bayside's own 35.
                BallLateralYards = -FieldGeometry.HashLateralYards, // Spotted on the left hash after the punt return.
                MinDurationSeconds = 4.3f,
            };

            // ── Backfield ─────────────────────────────────────────────────────────────────────────────
            // Vance rides the mesh and then carries out the boot away from the run. The fake is not decoration:
            // it is the only thing holding the backside end and the WILL from running the play down from behind.
            play.Players.Add(Home(9, "boot fake",
                At(-5.0f, 0.0f),          // gun depth, secures the snap here at 0.45 s
                At(-5.2f, 1.2f),          // mesh point at 1.05 s
                Cut(-5.6f, -1.0f, 0.5f),  // pulls the ball and turns back to the backside
                End(-6.4f, -4.5f)));

            // Pruitt aims at the right A gap, presses the double so the linebackers have to declare, then makes
            // the one cut into the B gap the combo just opened. Everything after the tackle point is churn.
            play.Players.Add(Home(26, "inside zone",
                At(-4.8f, 2.0f),
                At(-5.2f, 1.2f),          // mesh
                Cut(-2.0f, 0.9f, 0.45f),  // presses right A at 1.55 s — the press is what sets up the cut
                At(0.0f, 2.3f),           // crosses the LOS in the right B gap at 2.05 s
                At(3.5f, 4.1f),           // first contact, MIKE fills at 2.75 s
                At(5.2f, 5.4f),           // runs through the arm tackle, pile moving at 3.55 s
                End(6.0f, 6.2f)));        // down by contact at 4.1 s

            // ── Playside interior: the combo that decides the play ────────────────────────────────────
            // Lindqvist and Pellegrini zone-step right and double the 3-tech. The centre stays on it only long
            // enough to get vertical push, then climbs to the MIKE; the guard takes #98 over solo.
            play.Players.Add(Home(55, "inside zone",
                At(-0.5f, 0.0f),
                At(-0.3f, 0.8f),          // zone step, hip to hip with the guard
                End(1.6f, 2.0f)));        // climbs off the double to the second level

            play.Players.Add(Home(68, "inside zone",
                At(-0.5f, 1.1f),
                At(-0.2f, 1.9f),          // takes over #98
                End(0.6f, 2.4f)));        // washes him down and away from the B gap

            // ── Right edge ────────────────────────────────────────────────────────────────────────────
            // Achebe and Rasmussen combo the 5-tech so the edge cannot spill the ball back outside, then the
            // tackle works up to the linebacker level.
            play.Players.Add(Home(77, "inside zone",
                At(-0.5f, 2.4f),
                At(-0.2f, 3.2f),
                End(2.2f, 2.2f)));

            play.Players.Add(Home(85, "inside zone",
                At(-0.5f, 3.7f),
                At(0.0f, 4.4f),           // bases #90 on the edge
                End(2.2f, 4.8f)));

            // ── Backside ──────────────────────────────────────────────────────────────────────────────
            // The cut-off and the hinge. Neither man wins his block outright; they only have to stop the 1-tech
            // crossing a face and keep Grant from chasing flat down the line.
            play.Players.Add(Home(66, "inside zone",
                At(-0.5f, -1.1f),
                At(0.0f, -0.5f),
                End(0.8f, -0.2f)));

            play.Players.Add(Home(74, "inside zone",
                At(-0.5f, -2.4f),
                At(-1.0f, -2.9f),         // hinges back to wall the backside
                End(-1.2f, -3.4f)));

            // ── Perimeter ─────────────────────────────────────────────────────────────────────────────
            // Two stalks and a cut-off release. Against single-high the corners are the last men on the edge, so
            // the receivers' job is leverage, not separation — and the X takes the top off any throwback look.
            play.Players.Add(Home(83, "stalk block",
                At(-1.0f, 12.0f),
                At(3.0f, 11.8f),
                Cut(5.5f, 11.6f, 0.4f),   // breaks down under control rather than running past the corner
                End(6.4f, 11.4f)));

            play.Players.Add(Home(17, "stalk block",
                At(-1.0f, 6.5f),
                At(2.5f, 6.7f),
                Cut(4.4f, 6.8f, 0.4f),    // inside-out on the nickel so the cutback stays walled
                End(5.0f, 7.0f)));

            play.Players.Add(Home(11, "cut-off release",
                At(0.0f, -9.5f),
                At(5.0f, -9.8f),
                End(10.0f, -10.2f)));

            // ── Defensive front ───────────────────────────────────────────────────────────────────────
            // Grant squeezes and chases flat; Marchetti two-gaps the weak A. Okafor is the man the play is run
            // at — he fights the combo and loses ground both back and down the line, which is the B gap opening.
            play.Players.Add(Away(91, "squeeze and chase",
                At(1.0f, -3.2f),
                Cut(0.3f, -2.4f, 0.5f),
                End(0.8f, 1.5f)));

            play.Players.Add(Away(94, "two-gap weak A",
                At(1.0f, -0.6f),
                At(0.0f, 0.2f),
                End(-0.3f, 0.8f)));

            play.Players.Add(Away(98, "fight the combo",
                At(1.0f, 1.6f),
                At(1.6f, 1.0f),           // driven back and washed toward the centre
                End(2.2f, 0.4f)));

            play.Players.Add(Away(90, "set the edge",
                At(1.0f, 4.6f),
                At(0.6f, 5.2f),
                End(1.0f, 5.6f)));

            // ── Linebackers ───────────────────────────────────────────────────────────────────────────
            // Renfroe scrapes over the top of the combo and is the first man to the ball at 2.75 s. Bailey fits
            // backside and only arrives on the pursuit — the boot fake costs him the half-count.
            play.Players.Add(Away(54, "scrape and fill",
                At(4.5f, 0.5f),
                Cut(4.0f, 2.2f, 0.5f),    // plants and comes downhill once the ball declares
                At(3.5f, 4.1f),           // fills the playside B gap, first contact
                End(5.4f, 5.0f)));        // gives ground as the pile moves

            play.Players.Add(Away(58, "backside fill",
                At(4.5f, -3.0f),
                Cut(3.6f, -0.8f, 0.5f),
                End(4.4f, 2.4f)));

            // ── Secondary: Cover 3 sky ────────────────────────────────────────────────────────────────
            // With the strong safety rolled down the run fits are sound, so this is a plus-6, not an explosive.
            // Whitlock is the force player and the man who finishes the tackle; the corners keep their thirds.
            play.Players.Add(Away(33, "cover 3 force",
                At(7.5f, 7.0f),
                Cut(6.6f, 6.8f, 0.5f),    // triggers downhill on run action
                End(6.1f, 6.4f)));        // arrives to finish at the spot

            play.Players.Add(Away(29, "cover 3 curl/flat",
                At(5.5f, 6.5f),
                At(6.4f, 6.9f),           // stacked up by the slot's stalk
                End(6.0f, 7.6f)));

            play.Players.Add(Away(21, "cover 3 deep third",
                At(7.0f, 11.5f),
                At(9.0f, 11.7f),          // bails first, as the coverage demands
                End(7.0f, 11.2f)));       // sheds the stalk late and comes up

            play.Players.Add(Away(24, "cover 3 deep third",
                At(7.0f, -10.0f),
                At(10.5f, -10.4f),
                End(9.0f, -8.5f)));       // squeezes back for backside contain

            play.Players.Add(Away(31, "cover 3 middle third",
                At(13.0f, 0.0f),
                At(11.0f, 2.5f),
                End(8.0f, 5.0f)));        // alley fill from depth, arrives after the whistle spot

            // ── The ball ──────────────────────────────────────────────────────────────────────────────
            // Shotgun snap, mesh at 1.05 s, then Pruitt carries it to the whistle. Nothing leaves the ground.
            play.BallEvents.Add(new BallEvent
            {
                Time = 0f, Kind = BallEventKind.Snap, FromNumber = 55, ToNumber = 9, Side = TeamSide.Home,
            });
            play.BallEvents.Add(new BallEvent
            {
                Time = 1.05f, Kind = BallEventKind.Handoff, FromNumber = 9, ToNumber = 26, Side = TeamSide.Home,
            });
            play.BallEvents.Add(new BallEvent
            {
                Time = 4.1f, Kind = BallEventKind.Down, FromNumber = 26, ToNumber = 26, Side = TeamSide.Home,
            });

            // A good first-down run and nothing more: the ball dies at the 41, outside the right hash, and the
            // clock keeps moving. ElapsedSeconds is snap to next snap (12:04 → 11:22), which is what the drive
            // fold subtracts from the game clock — not the 4.3 s of movement.
            play.Outcome = new PlayOutcome
            {
                Kind = PlayResultKind.Rush,
                YardsGained = 6f,
                FirstDown = false,
                ElapsedSeconds = 42f,
                ClockRunsAfter = true,
                BallCarrierNumber = 26,
                PasserNumber = 0,
                HasPenalty = false,
            };

            return play;
        }

        static PlayScript BuildPlay2()
        {
            var play = new PlayScript
            {
                Name = "Gun Trey Right — PA Dagger",
                Formation = "Gun Trey Right (11 personnel)",
                DefensiveCall = "Nickel Over — Cover 3 Sky",
                Offense = TeamSide.Home,
                LineOfScrimmageYard = 41f,
                BallLateralYards = FieldGeometry.HashLateralYards,   // right hash, abs +3.08
                MinDurationSeconds = 4.6f,
            };

            // Quarterback. The whole concept is sold from the mesh: he rides the zone fake to #26, then works to
            // the top of a three-step gun drop and holds there with his eyes on the seam, because the free safety
            // only stays in the middle third as long as he believes the ball is going over the top. The throw is
            // an anticipation ball — it leaves at 2.5 s, before #17 has finished crossing.
            play.Players.Add(new PlayerScript
            {
                Number = 9,
                Side = TeamSide.Home,
                RouteName = "PA drop — dagger",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-5.5f, 0f),
                    new RouteWaypoint(-5.4f, -1.2f),                                        // ride the fake to the back
                    new RouteWaypoint(-7.5f, 0.3f, WaypointKind.Dwell) { DwellSeconds = 0.5f, SpeedFraction = 0.5f },
                    new RouteWaypoint(-7.5f, 0.8f, WaypointKind.Stop),                      // release point, x +0.8
                },
            });

            // Running back. His job is the two steps the linebackers take, not the football. He sells inside zone
            // to the right, then checks the rush and leaks to the strong flat as the outlet the play never needs.
            play.Players.Add(new PlayerScript
            {
                Number = 26,
                Side = TeamSide.Home,
                RouteName = "zone fake / check-release",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-5.2f, -2f),
                    new RouteWaypoint(-5.4f, -0.4f),                                        // the mesh
                    new RouteWaypoint(-4.4f, 2.4f),                                         // carry out the zone track
                    new RouteWaypoint(-1f, 5.6f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(3f, 9f, WaypointKind.Stop),
                },
            });

            // #2 receiver — the dig, and the reason the play exists. He pushes twelve vertical while the hook
            // defenders are still recovering from the fake, plants on the outside foot, and runs into grass that
            // #54 and #58 vacated. The ball is already out when he breaks.
            play.Players.Add(new PlayerScript
            {
                Number = 17,
                Side = TeamSide.Home,
                RouteName = "dig",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-1f, 6f),
                    new RouteWaypoint(2.5f, 6.5f),
                    new RouteWaypoint(8f, 7.2f),
                    new RouteWaypoint(12f, 7.4f, WaypointKind.Cut) { SpeedFraction = 0.45f },   // the break, t = 2.4
                    new RouteWaypoint(12f, -1.5f),                                             // the catch, t = 3.3
                    new RouteWaypoint(14f, -5f, WaypointKind.Stop),                            // down at NOR 45
                },
            });

            // #1 receiver — the clear-out. He never gets the ball and he is the most important body on the field:
            // the seam carries CB #21 out of the flat and gives the free safety a reason to stay on his hash, so
            // nobody is left to cap the dig underneath.
            play.Players.Add(new PlayerScript
            {
                Number = 83,
                Side = TeamSide.Home,
                RouteName = "seam/go",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-1f, 11f),
                    new RouteWaypoint(5f, 9.8f),                                            // stem inside to threaten the seam
                    new RouteWaypoint(15f, 9.4f),
                    new RouteWaypoint(27f, 9f, WaypointKind.Stop),
                },
            });

            // Tight end. Blocks first so the run action reads, then releases late into the strong flat — that is
            // what keeps SS #33 squeezed to the sideline instead of falling back under the dig window.
            play.Players.Add(new PlayerScript
            {
                Number = 85,
                Side = TeamSide.Home,
                RouteName = "block-release flat",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 3.7f),
                    new RouteWaypoint(0.6f, 4.5f, WaypointKind.Cut) { SpeedFraction = 0.45f },  // sell the down block
                    new RouteWaypoint(2.4f, 7.6f),
                    new RouteWaypoint(4f, 11f, WaypointKind.Stop),
                },
            });

            // Backside X. Isolated on an island with a bailing corner, running the comeback as the late answer if
            // the dig gets capped. On this snap it stays a decoy.
            play.Players.Add(new PlayerScript
            {
                Number = 11,
                Side = TeamSide.Home,
                RouteName = "comeback",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(0f, -13f),
                    new RouteWaypoint(8f, -13.6f),
                    new RouteWaypoint(16f, -13.8f, WaypointKind.Cut) { SpeedFraction = 0.4f },  // sit it down
                    new RouteWaypoint(14f, -15.2f, WaypointKind.Stop),                          // snap back to the sideline
                },
            });

            // Offensive line. Play-action pass protection: everyone takes the zone step that sells the run, then
            // recovers into a set. The zone step is the tell the linebackers are reading, so it has to be real —
            // which is exactly why the line gives ground for a beat before it holds the pocket.
            play.Players.Add(new PlayerScript
            {
                Number = 55,
                Side = TeamSide.Home,
                RouteName = "PA set — A-gap looper",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 0f),
                    new RouteWaypoint(-0.4f, 0.6f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(-1.4f, -0.5f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 66,
                Side = TeamSide.Home,
                RouteName = "PA set — 1-tech",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, -1.1f),
                    new RouteWaypoint(-0.3f, -0.5f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(-1.3f, -0.9f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 74,
                Side = TeamSide.Home,
                RouteName = "kick-slide vs DE",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, -2.4f),
                    new RouteWaypoint(-1.5f, -3f),
                    new RouteWaypoint(-2.8f, -3.5f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 68,
                Side = TeamSide.Home,
                RouteName = "PA set — 3-tech",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 1.1f),
                    new RouteWaypoint(-0.4f, 1.7f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(-1.4f, 1.9f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 77,
                Side = TeamSide.Home,
                RouteName = "set vs DE",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 2.4f),
                    new RouteWaypoint(-1.2f, 3f),
                    new RouteWaypoint(-2.4f, 3.8f, WaypointKind.Stop),
                },
            });

            // Four-man rush. Both ends rush contain rather than upfield, which is what lets the quarterback stand
            // at the top of his drop for three-quarters of a second — the pocket holds, so the coverage has to
            // hold too, and this coverage cannot.
            play.Players.Add(new PlayerScript
            {
                Number = 91,
                Side = TeamSide.Away,
                RouteName = "edge rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1f, -3.2f),
                    new RouteWaypoint(-1.5f, -4.3f),
                    new RouteWaypoint(-4.8f, -3.8f, WaypointKind.Cut) { SpeedFraction = 0.5f },  // turn the corner
                    new RouteWaypoint(-6.8f, -2.4f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 94,
                Side = TeamSide.Away,
                RouteName = "A-gap bull",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1f, -0.6f),
                    new RouteWaypoint(-0.6f, -0.9f),
                    new RouteWaypoint(-2.4f, -1f, WaypointKind.Stop),                       // hands up in the lane
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 98,
                Side = TeamSide.Away,
                RouteName = "B-gap power",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1f, 1.6f),
                    new RouteWaypoint(-0.4f, 2f),
                    new RouteWaypoint(-2.2f, 2.4f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 90,
                Side = TeamSide.Away,
                RouteName = "contain rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1f, 4.6f),
                    new RouteWaypoint(-1.6f, 5.6f),
                    new RouteWaypoint(-5f, 5.2f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(-7.2f, 3.6f, WaypointKind.Stop),
                },
            });

            // Hook defenders. Both take the two downhill steps the fake asks for, then backpedal to their drops —
            // and both arrive at the eleven-to-twelve-yard window a half-count after the ball does. A Cover 3 hook
            // player can be right on the run fake or right in coverage; this is what it costs to be right on the
            // fake. Their facings are pinned so they retreat looking at the quarterback, not at the end zone.
            play.Players.Add(new PlayerScript
            {
                Number = 54,
                Side = TeamSide.Away,
                RouteName = "cover 3 hook-curl",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(4.5f, 0.5f),
                    new RouteWaypoint(2.6f, 1.5f, WaypointKind.Cut) { SpeedFraction = 0.4f },   // bites the fake
                    new RouteWaypoint(8f, 3.2f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(11f, 4.2f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 270f },
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 58,
                Side = TeamSide.Away,
                RouteName = "cover 3 weak hook",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(4.5f, -3f),
                    new RouteWaypoint(2.6f, -2.4f, WaypointKind.Cut) { SpeedFraction = 0.4f },
                    new RouteWaypoint(8.5f, -2.8f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(11.6f, -3.4f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 270f },
                },
            });

            // Strong safety in the flat. He carries the tight end to the sideline, which is his call and also the
            // reason there is no second body over the dig.
            play.Players.Add(new PlayerScript
            {
                Number = 33,
                Side = TeamSide.Away,
                RouteName = "cover 3 strong flat",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(7.5f, 7f),
                    new RouteWaypoint(5.5f, 8.6f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(4f, 11f, WaypointKind.Stop),
                },
            });

            // Nickel, curl-to-flat. He is the one defender who reads it and triggers, driving outside-in from his
            // drop — too late to break the pass up, in time to make the tackle two yards after the catch.
            play.Players.Add(new PlayerScript
            {
                Number = 29,
                Side = TeamSide.Away,
                RouteName = "cover 3 curl/flat",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(5.5f, 6.5f),
                    new RouteWaypoint(9.5f, 7.4f) { PinFacing = true, FacingDegrees = 270f },  // drop with the eyes on the QB
                    new RouteWaypoint(12.4f, 2.2f, WaypointKind.Cut) { SpeedFraction = 0.5f },  // trigger on the dig
                    new RouteWaypoint(14f, -4.6f, WaypointKind.Stop),                           // the tackle
                },
            });

            // Corners in the outside thirds. #21 bails and gets run off by the seam exactly as designed; #24 is
            // alone on the backside comeback and irrelevant to the ball.
            play.Players.Add(new PlayerScript
            {
                Number = 21,
                Side = TeamSide.Away,
                RouteName = "cover 3 deep third",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(7f, 11.5f),
                    new RouteWaypoint(12f, 11f) { PinFacing = true, FacingDegrees = 270f },   // the bail
                    new RouteWaypoint(22f, 9.8f),                                             // open and run with #83
                    new RouteWaypoint(27f, 9.4f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 24,
                Side = TeamSide.Away,
                RouteName = "cover 3 deep third",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(7f, -10.5f),
                    new RouteWaypoint(13f, -11.6f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(17.5f, -13f, WaypointKind.Cut) { SpeedFraction = 0.5f },  // drive on the comeback
                    new RouteWaypoint(15f, -14.6f, WaypointKind.Stop),
                },
            });

            // Free safety, middle third. Held by the quarterback's eyes and by #83 running through his zone, so he
            // is still on his hash when the dig comes open underneath. He breaks down late and gets the assist.
            play.Players.Add(new PlayerScript
            {
                Number = 31,
                Side = TeamSide.Away,
                RouteName = "cover 3 middle third",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(13f, 0f),
                    new RouteWaypoint(18f, 2f),                                                 // carried toward the seam
                    new RouteWaypoint(15.5f, -1.6f, WaypointKind.Cut) { SpeedFraction = 0.45f }, // break on the ball
                    new RouteWaypoint(14.2f, -4.4f, WaypointKind.Stop),
                },
            });

            // The ball. No handoff event on the mesh — the fake is a body beat, the ball never leaves #9's hands.
            // The throw is out at 2.5 s, a tenth after the break, and travels 18.6 yards of air in 0.80 s, so the
            // arc is flat: an intermediate dig, not a lofted ball over the top.
            play.BallEvents.Add(new BallEvent
            {
                Time = 0f, Kind = BallEventKind.Snap, FromNumber = 55, ToNumber = 9, Side = TeamSide.Home,
            });
            play.BallEvents.Add(new BallEvent
            {
                Time = 2.5f, Kind = BallEventKind.Throw, FromNumber = 9, ToNumber = 17, Side = TeamSide.Home,
                ApexMeters = 3.2f,
            });
            play.BallEvents.Add(new BallEvent
            {
                Time = 3.3f, Kind = BallEventKind.Catch, FromNumber = 9, ToNumber = 17, Side = TeamSide.Home,
            });
            play.BallEvents.Add(new BallEvent
            {
                Time = 4.35f, Kind = BallEventKind.Down, FromNumber = 17, Side = TeamSide.Home,
            });

            // Twelve yards of air and two after the catch: BAY 41 to NOR 45, and the chains move. The clock keeps
            // running because he was tackled in bounds, which is why the fold spends 43 seconds to the next snap.
            play.Outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PassComplete,
                YardsGained = 14f,
                FirstDown = true,
                ElapsedSeconds = 43f,
                ClockRunsAfter = true,
                BallCarrierNumber = 17,
                PasserNumber = 9,
                HasPenalty = false,
            };

            return play;
        }

        static PlayScript BuildPlay3()
        {
            var play = new PlayScript
            {
                Name = "Gun Trips Right - 5-Man Slide (Sack)",
                Formation = "Gun Trips Right 3x1, 11 personnel",
                DefensiveCall = "Nickel 4-2-5 Cross-Dog, Cover 1 Man-Free",
                Offense = TeamSide.Home,
                LineOfScrimmageYard = 55f,   // the opponent's 45
                BallLateralYards = -2.0f,    // the dig died between the hashes, so the ball is spotted where it stopped
                MinDurationSeconds = 3.2f,
            };

            // QB #9 Vance. Five-step gun drop, hitch, and nothing to throw: Cover 1 has every route locked, so he
            // holds it. The Cut at 2.35 s is the hitch — he drifts back and away from the edge rather than climbing
            // into the pocket, which is exactly why the sack is seven deep instead of two.
            play.Players.Add(new PlayerScript
            {
                Number = 9,
                Side = TeamSide.Home,
                RouteName = "5-step gun drop",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-6.0f, 0.0f),
                    new RouteWaypoint(-7.0f, -1.2f),
                    new RouteWaypoint(-7.4f, -3.0f, WaypointKind.Cut) { SpeedFraction = 0.4f },
                    new RouteWaypoint(-7.0f, -6.2f, WaypointKind.Stop),
                },
            });

            // RB #26 Pruitt. The five-man slide releases the back, so there is no chip and no help — this route is
            // the reason the left tackle is alone. He runs weak into the flat and is never looked at.
            play.Players.Add(new PlayerScript
            {
                Number = 26,
                Side = TeamSide.Home,
                RouteName = "weak flat",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-5.8f, -2.2f),
                    new RouteWaypoint(-4.5f, -6.5f),
                    new RouteWaypoint(-1.5f, -10.0f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(1.5f, -12.0f, WaypointKind.Stop),
                },
            });

            // Trips right. #83 runs the 9 with the corner in trail, #17 the ten-yard out with the nickel in phase,
            // #85 the six-yard option matched by the strong safety. Three routes, three man defenders, no window —
            // which is the whole point of pairing a five-man pressure with Cover 1.
            play.Players.Add(new PlayerScript
            {
                Number = 83,
                Side = TeamSide.Home,
                RouteName = "9-route",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-1.0f, 12.0f),
                    new RouteWaypoint(4.0f, 12.6f),
                    new RouteWaypoint(12.0f, 13.0f),
                    new RouteWaypoint(20.0f, 13.2f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 17,
                Side = TeamSide.Home,
                RouteName = "out",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-1.0f, 8.0f),
                    new RouteWaypoint(5.0f, 8.4f),
                    new RouteWaypoint(10.0f, 8.6f, WaypointKind.Cut) { SpeedFraction = 0.4f },
                    new RouteWaypoint(10.5f, 15.0f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 85,
                Side = TeamSide.Home,
                RouteName = "option",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-1.0f, 4.5f),
                    new RouteWaypoint(4.0f, 4.8f),
                    new RouteWaypoint(6.0f, 4.6f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(6.5f, 1.0f, WaypointKind.Stop),
                },
            });

            // X #11 Colston, isolated backside. The dig at fifteen is the answer to this coverage, but it does not
            // uncover until 3.4 s and the quarterback is on the ground at 3.0 — the route is right and the clock
            // is wrong.
            play.Players.Add(new PlayerScript
            {
                Number = 11,
                Side = TeamSide.Home,
                RouteName = "deep dig",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(0.0f, -11.0f),
                    new RouteWaypoint(7.0f, -11.4f),
                    new RouteWaypoint(15.0f, -11.0f, WaypointKind.Cut) { SpeedFraction = 0.4f },
                    new RouteWaypoint(15.2f, -8.0f, WaypointKind.Stop),
                },
            });

            // Left tackle #74 Redmond. The protection slid away from him, so he is one-on-one with a wide-9 speed
            // rusher and no one to pass him off to. He oversets, opens the gate, and loses the outside arc at 2.0 s.
            // Facing is pinned so he stays square through the kick-slide instead of turning to run with his feet.
            play.Players.Add(new PlayerScript
            {
                Number = 74,
                Side = TeamSide.Home,
                RouteName = "iso pass set",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, -2.4f),
                    new RouteWaypoint(-1.6f, -3.4f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-2.6f, -4.2f, WaypointKind.Cut) { SpeedFraction = 0.5f, PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-3.2f, -4.6f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            // The rest of the slide. Five linemen, four rushers plus the WILL, and the whole unit steps right to the
            // trips: the guard jumps the A-gap blitzer, the center passes the looper off, and the right tackle ends
            // up blocking air when #90 long-sticks inside. Short paths — nobody here travels more than two yards.
            play.Players.Add(new PlayerScript
            {
                Number = 66,
                Side = TeamSide.Home,
                RouteName = "slide right",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, -1.1f),
                    new RouteWaypoint(-0.8f, -0.5f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-1.3f, -0.7f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 55,
                Side = TeamSide.Home,
                RouteName = "slide right",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 0.0f),
                    new RouteWaypoint(-0.9f, 0.6f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-1.3f, 0.4f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 68,
                Side = TeamSide.Home,
                RouteName = "slide right",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 1.1f),
                    new RouteWaypoint(-1.0f, 1.7f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-1.5f, 1.9f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 77,
                Side = TeamSide.Home,
                RouteName = "slide right",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 2.4f),
                    new RouteWaypoint(-1.0f, 3.2f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-1.4f, 3.6f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            // DE #91 Grant, wide-9. The arc is the play: he goes up and around the tackle's outside hand, clears the
            // shoulder at 2.0 s two yards deeper than the launch point, then flattens to the quarterback. The Cut is
            // where the speed rush converts to a flat path instead of running past the pocket.
            play.Players.Add(new PlayerScript
            {
                Number = 91,
                Side = TeamSide.Away,
                RouteName = "speed rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, -3.4f),
                    new RouteWaypoint(-1.5f, -6.0f),
                    new RouteWaypoint(-4.0f, -5.6f, WaypointKind.Cut) { SpeedFraction = 0.55f },
                    new RouteWaypoint(-7.0f, -6.2f, WaypointKind.Stop),
                },
            });

            // Interior rush. #94 pushes the strong A to hold the center on his slide, #98 bull-rushes the guard, and
            // #90 long-sticks from the 5-tech all the way into the strong A gap — the loop half of the cross-dog,
            // and the reason the right tackle has nobody.
            play.Players.Add(new PlayerScript
            {
                Number = 94,
                Side = TeamSide.Away,
                RouteName = "A-gap push",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, -0.6f),
                    new RouteWaypoint(-0.2f, 0.0f),
                    new RouteWaypoint(-1.2f, 0.4f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 98,
                Side = TeamSide.Away,
                RouteName = "bull rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, 1.6f),
                    new RouteWaypoint(-0.5f, 1.4f),
                    new RouteWaypoint(-1.8f, 1.2f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 90,
                Side = TeamSide.Away,
                RouteName = "long-stick loop",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, 3.2f),
                    new RouteWaypoint(0.2f, 1.8f),
                    new RouteWaypoint(-0.8f, 0.8f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(-2.2f, 0.6f, WaypointKind.Stop),
                },
            });

            // Linebackers. The WILL inserts weak A at 1.35 s and is picked up — his job is to occupy the center and
            // guard so nobody can help outside. The MIKE spies instead of rushing, which takes away the scramble
            // lane the quarterback would otherwise have had once the edge came free.
            play.Players.Add(new PlayerScript
            {
                Number = 58,
                Side = TeamSide.Away,
                RouteName = "weak A blitz",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(4.0f, -2.2f),
                    new RouteWaypoint(2.0f, -1.2f),
                    new RouteWaypoint(0.5f, -0.8f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(-1.0f, -0.6f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 54,
                Side = TeamSide.Away,
                RouteName = "spy",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(4.0f, 0.5f),
                    new RouteWaypoint(3.0f, 0.0f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(1.5f, -1.5f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(0.0f, -2.5f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 270f },
                },
            });

            // Man-free secondary. Five in trail underneath with the free safety alone on top. The corners open and
            // run with the verticals, the nickel trails the out from inside leverage, and the strong safety carries
            // the flexed tight end. Nothing comes open inside three seconds, which is the coverage doing its job.
            play.Players.Add(new PlayerScript
            {
                Number = 21,
                Side = TeamSide.Away,
                RouteName = "man on #1",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(6.0f, 12.0f),
                    new RouteWaypoint(10.0f, 12.6f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(17.0f, 13.2f),
                    new RouteWaypoint(23.0f, 13.4f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 29,
                Side = TeamSide.Away,
                RouteName = "man on #2",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(5.5f, 8.0f),
                    new RouteWaypoint(7.5f, 7.6f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(10.5f, 8.0f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(11.5f, 13.5f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 33,
                Side = TeamSide.Away,
                RouteName = "man on the TE",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(5.0f, 4.5f),
                    new RouteWaypoint(6.5f, 4.6f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(7.0f, 3.0f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(7.2f, 1.2f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 24,
                Side = TeamSide.Away,
                RouteName = "man on X",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(6.0f, -11.5f),
                    new RouteWaypoint(9.0f, -11.2f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(15.5f, -10.6f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(16.0f, -8.5f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 31,
                Side = TeamSide.Away,
                RouteName = "free safety middle",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(13.5f, -1.0f),
                    new RouteWaypoint(16.0f, -1.5f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(18.0f, -2.0f, WaypointKind.Stop),
                },
            });

            // Two custody events and nothing between them. The ball never leaves the quarterback: shotgun snap at
            // zero, and Grant puts him on the ground at 3.0 s, seven yards behind the line.
            play.BallEvents.Add(new BallEvent
            {
                Time = 0f,
                Kind = BallEventKind.Snap,
                FromNumber = 55,
                ToNumber = 9,
                Side = TeamSide.Home,
            });

            play.BallEvents.Add(new BallEvent
            {
                Time = 3.0f,
                Kind = BallEventKind.Down,
                FromNumber = 9,
                ToNumber = 9,
                Side = TeamSide.Home,
            });

            // A sack is charged to the passer, so the carrier and the passer are the same number. Minus seven turns
            // 1st & 10 in scoring range into 2nd & 17, and the clock keeps running.
            play.Outcome = new PlayOutcome
            {
                Kind = PlayResultKind.Sack,
                YardsGained = -7f,
                FirstDown = false,
                ElapsedSeconds = 3.2f,
                ClockRunsAfter = true,
                BallCarrierNumber = 9,
                PasserNumber = 9,
                HasPenalty = false,
                PenaltyName = "",
                PenaltyYards = 0f,
                PenalizedSide = TeamSide.Home,
                AutomaticFirstDown = false,
                ReplayDown = false,
                SituationOverride = false,
            };

            return play;
        }

        static PlayScript BuildPlay4()
        {
            var play = new PlayScript
            {
                Name = "Gun Right — RB Slip Screen Right",
                Formation = "Shotgun 2x2 'Gun Right', 11 personnel",
                DefensiveCall = "Nickel 4-2-5, 4-Man Rush, Cover 2",
                Offense = TeamSide.Home,
                // 2nd & 17 from the Ironhawks' own 48. The sack on the previous snap died at absolute −8.2,
                // outside the left hash, so the ball is brought back in to the left hash.
                LineOfScrimmageYard = 48f,
                BallLateralYards = -FieldGeometry.HashLateralYards,
                MinDurationSeconds = 5.4f,
            };

            // QB #9 Vance. The screen only works if the rush believes the drop. He sells it, retreats two extra
            // steps to pull the four-man rush past the launch point, and flips it over them at 1.6.
            play.Players.Add(new PlayerScript
            {
                Number = 9,
                Side = TeamSide.Home,
                RouteName = "screen flip",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-5.5f, 0.0f),
                    new RouteWaypoint(-7.0f, -0.3f),
                    new RouteWaypoint(-7.8f, -0.6f, WaypointKind.Cut) { SpeedFraction = 0.40f },
                    new RouteWaypoint(-7.6f, 0.3f, WaypointKind.Stop),
                },
            });

            // RB #26 Pruitt. Two-count protection fake so the linebackers stay in their hooks, then he slips
            // behind the right tackle into the vacated grass. Catch at y −2, cross the LOS at 2.7 behind the
            // #55/#68 convoy, cut inside the kick-out at 4.0, down at the Sentinels' 43.
            play.Players.Add(new PlayerScript
            {
                Number = 26,
                Side = TeamSide.Home,
                RouteName = "slip screen",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-5.2f, 2.0f),
                    new RouteWaypoint(-4.7f, 2.7f, WaypointKind.Dwell) { DwellSeconds = 0.35f, PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-2.0f, 4.0f),
                    new RouteWaypoint(-2.0f, 5.5f, WaypointKind.Cut) { SpeedFraction = 0.50f },
                    new RouteWaypoint(0.0f, 6.6f),
                    new RouteWaypoint(4.5f, 8.8f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(9.0f, 9.0f, WaypointKind.Stop),
                },
            });

            // C #55 Lindqvist. Invite, then release right and go find the force player. He kicks CB #21 Tiller
            // out at 4.0, which is the block that turns a 3-yard screen into 9.
            play.Players.Add(new PlayerScript
            {
                Number = 55,
                Side = TeamSide.Home,
                RouteName = "screen release / kick",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 0.0f),
                    new RouteWaypoint(-1.2f, 0.2f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(0.2f, 3.0f),
                    new RouteWaypoint(2.0f, 7.0f),
                    new RouteWaypoint(2.5f, 9.4f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(2.3f, 10.4f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            // RG #68 Pellegrini. Same invite-and-release as the centre — except he loses the 1-tech inside on the
            // give-ground step, grabs the outside of #94's jersey at 1.5 and restricts him at the LOS. The back
            // judge flags it at 1.6, before the ball is even caught. He then lets go and climbs into the convoy,
            // which is the cruel part: the block he makes downfield never counts.
            play.Players.Add(new PlayerScript
            {
                Number = 68,
                Side = TeamSide.Home,
                RouteName = "invite / release right",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 1.1f),
                    new RouteWaypoint(-0.9f, 0.9f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(0.5f, 0.4f, WaypointKind.Dwell)
                    {
                        DwellSeconds = 0.5f, SpeedFraction = 0.35f, PinFacing = true, FacingDegrees = 90f,
                    },
                    new RouteWaypoint(2.5f, 4.5f),
                    new RouteWaypoint(5.0f, 7.0f, WaypointKind.Cut) { SpeedFraction = 0.50f },
                    new RouteWaypoint(7.8f, 7.6f, WaypointKind.Stop),
                },
            });

            // RT #77 Achebe. The one lineman who does not invite anybody: he sets hard on the contain rusher and
            // rides him upfield, past the QB and past the lane the back is about to slip into.
            play.Players.Add(new PlayerScript
            {
                Number = 77,
                Side = TeamSide.Home,
                RouteName = "hard set / ride",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 2.4f),
                    new RouteWaypoint(-1.5f, 3.2f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-3.0f, 4.2f, WaypointKind.Cut) { SpeedFraction = 0.45f, PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-4.2f, 4.6f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            // LG #66 Vitale and LT #74 Redmond. Full slide left. Redmond takes #91 Grant — the man who sacked
            // Vance a snap ago — and carries him past the launch point rather than trying to win the rep.
            play.Players.Add(new PlayerScript
            {
                Number = 66,
                Side = TeamSide.Home,
                RouteName = "full slide left",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, -1.1f),
                    new RouteWaypoint(-1.0f, -1.9f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-1.6f, -2.4f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 74,
                Side = TeamSide.Home,
                RouteName = "hard set / carry",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, -2.4f),
                    new RouteWaypoint(-2.0f, -3.4f) { PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-4.5f, -4.2f, WaypointKind.Cut) { SpeedFraction = 0.45f, PinFacing = true, FacingDegrees = 90f },
                    new RouteWaypoint(-6.2f, -4.0f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            // TE #85 Rasmussen. Releases outside like a route and then turns back into the nickel, walling #29
            // Pace away from the screen lane. The flat defender is the only man with a straight line to the catch.
            play.Players.Add(new PlayerScript
            {
                Number = 85,
                Side = TeamSide.Home,
                RouteName = "arc / block NCB",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 3.7f),
                    new RouteWaypoint(1.0f, 4.6f),
                    new RouteWaypoint(3.0f, 5.8f),
                    new RouteWaypoint(4.2f, 6.6f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(4.6f, 7.4f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                },
            });

            // Z #83 Sandoval. Pure clear-out. He does not want the ball; he wants CB #21 to turn and run so the
            // corner is 12 yards deep when the screen comes out behind him.
            play.Players.Add(new PlayerScript
            {
                Number = 83,
                Side = TeamSide.Home,
                RouteName = "clear-out go",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-1.0f, 11.0f),
                    new RouteWaypoint(6.0f, 11.4f),
                    new RouteWaypoint(16.0f, 11.8f),
                    new RouteWaypoint(24.0f, 12.0f, WaypointKind.Stop),
                },
            });

            // Slot #17 Whitfield. Vertical push to 12 to lift the nickel out of the flat, then crack back down
            // onto the second wave once the ball is out.
            play.Players.Add(new PlayerScript
            {
                Number = 17,
                Side = TeamSide.Home,
                RouteName = "vertical push / crack",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-1.0f, 6.5f),
                    new RouteWaypoint(6.0f, 6.8f),
                    new RouteWaypoint(12.0f, 7.0f, WaypointKind.Cut) { SpeedFraction = 0.40f },
                    new RouteWaypoint(7.0f, 8.4f),
                    new RouteWaypoint(4.0f, 9.0f, WaypointKind.Stop),
                },
            });

            // X #11 Colston. Backside decoy into the boundary — an 18-yard comeback that occupies the squat corner
            // and keeps the weak safety honest while everything happens on the other hash.
            play.Players.Add(new PlayerScript
            {
                Number = 11,
                Side = TeamSide.Home,
                RouteName = "comeback",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(0.0f, -10.0f),
                    new RouteWaypoint(9.0f, -10.4f),
                    new RouteWaypoint(18.0f, -10.8f, WaypointKind.Cut) { SpeedFraction = 0.35f },
                    new RouteWaypoint(14.0f, -12.0f, WaypointKind.Stop),
                },
            });

            // DE #91 Grant. Wins the edge again and is let win it — the tackle carries him past the launch point,
            // which is exactly what a screen wants from its best rusher.
            play.Players.Add(new PlayerScript
            {
                Number = 91,
                Side = TeamSide.Away,
                RouteName = "edge rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, -3.2f),
                    new RouteWaypoint(-1.2f, -4.0f),
                    new RouteWaypoint(-4.5f, -4.6f),
                    new RouteWaypoint(-6.6f, -4.2f, WaypointKind.Stop),
                },
            });

            // DT #94 Marchetti. Slants inside, beats the right guard clean — and never gets anywhere, because the
            // guard's hand is in his jersey. He is stood up on the LOS and turned; that stall IS the foul.
            play.Players.Add(new PlayerScript
            {
                Number = 94,
                Side = TeamSide.Away,
                RouteName = "1-tech rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, -0.6f),
                    new RouteWaypoint(0.7f, 0.1f),
                    new RouteWaypoint(0.5f, 0.4f, WaypointKind.Dwell)
                    {
                        DwellSeconds = 0.8f, SpeedFraction = 0.35f, PinFacing = true, FacingDegrees = 200f,
                    },
                    new RouteWaypoint(-0.8f, 0.2f),
                    new RouteWaypoint(-1.8f, 1.2f, WaypointKind.Stop),
                },
            });

            // DT #98 Okafor and DE #90 Voss. Both rush a spot the ball has already left: the 3-tech penetrates
            // himself out of the play, the contain end is ridden upfield past the screen lane by the right tackle.
            play.Players.Add(new PlayerScript
            {
                Number = 98,
                Side = TeamSide.Away,
                RouteName = "3-tech rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, 1.6f),
                    new RouteWaypoint(-1.5f, 1.2f),
                    new RouteWaypoint(-4.0f, 0.6f, WaypointKind.Cut) { SpeedFraction = 0.50f },
                    new RouteWaypoint(-5.5f, 0.8f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 90,
                Side = TeamSide.Away,
                RouteName = "contain rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, 4.0f),
                    new RouteWaypoint(-0.8f, 4.6f),
                    new RouteWaypoint(-3.0f, 5.2f),
                    new RouteWaypoint(-4.6f, 5.0f, WaypointKind.Stop),
                },
            });

            // MIKE #54 Renfroe and WILL #58 Bailey. Cover 2 hooks with their eyes on the quarterback, which is the
            // exact discipline a screen punishes: by the time they diagnose it the blocking surface is past them.
            play.Players.Add(new PlayerScript
            {
                Number = 54,
                Side = TeamSide.Away,
                RouteName = "cover 2 strong hook",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(4.5f, 0.5f),
                    new RouteWaypoint(6.0f, 1.6f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(5.2f, 4.0f, WaypointKind.Cut) { SpeedFraction = 0.50f },
                    new RouteWaypoint(5.6f, 7.0f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 58,
                Side = TeamSide.Away,
                RouteName = "cover 2 weak hook",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(4.5f, -3.0f),
                    new RouteWaypoint(5.5f, -2.0f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(5.0f, 2.0f),
                    new RouteWaypoint(7.6f, 6.6f, WaypointKind.Stop),
                },
            });

            // NCB #29 Pace. Cover 2 flat, and the first defender with a real angle on the catch — until the tight
            // end gets across his face and turns a tackle for two into a nine-yard gain.
            play.Players.Add(new PlayerScript
            {
                Number = 29,
                Side = TeamSide.Away,
                RouteName = "cover 2 flat",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(5.5f, 6.5f),
                    new RouteWaypoint(5.0f, 7.6f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(4.4f, 7.0f, WaypointKind.Cut) { SpeedFraction = 0.50f },
                    new RouteWaypoint(4.4f, 8.0f, WaypointKind.Stop),
                },
            });

            // CB #21 Tiller. Cover 2 corner: jam the Z, let him go, squat the flat. He triggers correctly on the
            // screen and gets kicked out by the centre at 4.0 — the block that opens the sideline.
            play.Players.Add(new PlayerScript
            {
                Number = 21,
                Side = TeamSide.Away,
                RouteName = "cover 2 corner",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(6.0f, 11.5f),
                    new RouteWaypoint(4.0f, 10.8f, WaypointKind.Cut) { SpeedFraction = 0.50f, PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(2.6f, 10.0f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(2.2f, 11.2f, WaypointKind.Stop),
                },
            });

            // CB #24 Moreau. Backside Cover 2 corner. Jams the X, squats, and watches the whole play happen on the
            // far hash with nothing to do about it.
            play.Players.Add(new PlayerScript
            {
                Number = 24,
                Side = TeamSide.Away,
                RouteName = "cover 2 corner",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(6.0f, -10.5f),
                    new RouteWaypoint(4.2f, -10.2f, WaypointKind.Cut) { SpeedFraction = 0.50f, PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(5.5f, -11.0f) { PinFacing = true, FacingDegrees = 270f },
                    new RouteWaypoint(6.5f, -9.5f, WaypointKind.Stop),
                },
            });

            // SS #33 Whitlock and FS #31 Delgado. Two-high halves, and the reason a screen only gets nine: the
            // deep safeties are the unblocked men. Delgado rallies all the way from the opposite hash and makes
            // the tackle at the Sentinels' 43 — a tackle that, thanks to the flag, goes in nobody's ledger.
            play.Players.Add(new PlayerScript
            {
                Number = 33,
                Side = TeamSide.Away,
                RouteName = "cover 2 right half",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(13.0f, 7.0f),
                    new RouteWaypoint(16.0f, 9.0f),
                    new RouteWaypoint(12.0f, 9.5f, WaypointKind.Cut) { SpeedFraction = 0.50f },
                    new RouteWaypoint(9.6f, 9.8f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 31,
                Side = TeamSide.Away,
                RouteName = "cover 2 left half",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(13.0f, -7.0f),
                    new RouteWaypoint(15.0f, -5.0f),
                    new RouteWaypoint(12.0f, 1.0f),
                    new RouteWaypoint(9.5f, 6.5f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(9.0f, 9.0f, WaypointKind.Stop),
                },
            });

            // The ball. A screen is a soft lob over the rush, not a throw — low apex, 0.3s of flight, caught two
            // yards behind the line. The flag is already on the ground by the time it arrives.
            play.BallEvents.Add(new BallEvent { Time = 0.0f, Kind = BallEventKind.Snap, FromNumber = 55, ToNumber = 9, Side = TeamSide.Home });
            play.BallEvents.Add(new BallEvent { Time = 1.6f, Kind = BallEventKind.Throw, FromNumber = 9, ToNumber = 26, Side = TeamSide.Home, ApexMeters = 2.0f });
            play.BallEvents.Add(new BallEvent { Time = 1.9f, Kind = BallEventKind.Catch, FromNumber = 9, ToNumber = 26, Side = TeamSide.Home });
            play.BallEvents.Add(new BallEvent { Time = 5.1f, Kind = BallEventKind.Down, FromNumber = 26, Side = TeamSide.Home });

            // Nominally a nine-yard completion to the Sentinels' 43. It is not a nine-yard completion. The foul is
            // behind the line on a pass play, so it is marked off ten from the PREVIOUS spot, the down is replayed,
            // and no player is credited with anything — the reception does not exist. 2nd & 27 at the Ironhawks' 38.
            play.Outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PenaltyNullified,
                YardsGained = 0f,
                FirstDown = false,
                ElapsedSeconds = 5.4f,
                ClockRunsAfter = true,
                BallCarrierNumber = 26,
                PasserNumber = 9,
                HasPenalty = true,
                PenaltyName = "Offensive Holding",
                PenaltyYards = 10f,
                PenalizedSide = TeamSide.Home,
                AutomaticFirstDown = false,
                ReplayDown = true,
            };

            return play;
        }

        static PlayScript BuildPlay5()
        {
            var play = new PlayScript
            {
                Name = "Gun 2x2 - Deep Over",
                Formation = "Gun 2x2, 11 Personnel (TE Attached Left)",
                DefensiveCall = "Nickel 4-2-5 - Four-Man Rush, Cover 2",
                Offense = TeamSide.Home,
                LineOfScrimmageYard = 38f,                        // BAY's own 38, second-and-27 after the holding call
                BallLateralYards = -FieldGeometry.HashLateralYards, // left hash, abs x -3.08
                MinDurationSeconds = 3.7f,
            };

            // QB #9 Vance. Six-man protection buys the full 2.4 s, so this is a real five-step gun drop with one
            // hitch rather than a hurried launch: he tops the drop at 7.5, resets, and slides a yard left to throw
            // the over route on the upfield shoulder.
            play.Players.Add(new PlayerScript
            {
                Number = 9,
                Side = TeamSide.Home,
                RouteName = "5-step drop",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-6.0f, 0.0f),
                    new RouteWaypoint(-7.5f, 0.0f, WaypointKind.Dwell) { DwellSeconds = 0.35f },
                    new RouteWaypoint(-7.5f, -1.0f, WaypointKind.Stop),
                },
            });

            // WR #17 Whitfield - the target. Against a two-deep shell the only soft spot on a long down is the seam
            // between the safeties, so he takes the nickel's reroute and wins inside, stems the push to 8, then
            // climbs back out across the middle and gets there at 20. The ball is on time; Delgado is just better.
            play.Players.Add(new PlayerScript
            {
                Number = 17,
                Side = TeamSide.Home,
                RouteName = "over",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-1.0f, 6.0f),
                    new RouteWaypoint(4.5f, 4.0f),
                    new RouteWaypoint(8.0f, 3.5f),
                    new RouteWaypoint(13.0f, 4.0f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(20.0f, 6.0f),                       // catch point, NOR 42 - where the ball arrives
                    new RouteWaypoint(23.0f, 7.5f, WaypointKind.Stop),
                },
            });

            // WR #11 Colston runs the deep post from the field side. His whole job is to occupy the free safety's
            // half so the over comes open underneath the top - the safety passes him off to his running mate and
            // drives the crosser anyway, which is the play.
            play.Players.Add(new PlayerScript
            {
                Number = 11,
                Side = TeamSide.Home,
                RouteName = "deep post",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-1.0f, 11.5f),
                    new RouteWaypoint(7.0f, 11.0f),
                    new RouteWaypoint(13.0f, 10.5f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(20.0f, 6.0f),
                    new RouteWaypoint(25.0f, 2.5f, WaypointKind.Stop),
                },
            });

            // WR #83 Sandoval into the boundary on a 16-yard comeback. On second-and-27 he is the checkdown-beater:
            // if the over is capped the ball comes back out here for something, not nothing.
            play.Players.Add(new PlayerScript
            {
                Number = 83,
                Side = TeamSide.Home,
                RouteName = "comeback",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(0.0f, -10.0f),
                    new RouteWaypoint(8.0f, -10.5f),
                    new RouteWaypoint(16.0f, -11.0f, WaypointKind.Cut) { SpeedFraction = 0.4f },
                    new RouteWaypoint(12.5f, -12.5f, WaypointKind.Stop),
                },
            });

            // TE #85 Rasmussen stays attached left as the sixth protector. He and the left tackle wall #91 Grant,
            // the rusher who got home for the sack two plays ago - this time the edge never opens.
            play.Players.Add(new PlayerScript
            {
                Number = 85,
                Side = TeamSide.Home,
                RouteName = "pass pro",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, -3.7f),
                    new RouteWaypoint(-1.0f, -4.2f),
                    new RouteWaypoint(-1.6f, -4.8f, WaypointKind.Stop),
                },
            });

            // RB #26 Pruitt scans the right side of the protection. Nobody comes free, so he leaks late to the flat
            // as the last outlet rather than standing in an empty pocket.
            play.Players.Add(new PlayerScript
            {
                Number = 26,
                Side = TeamSide.Home,
                RouteName = "scan / flat",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-5.5f, 2.0f),
                    new RouteWaypoint(-5.0f, 3.5f),
                    new RouteWaypoint(-3.0f, 6.5f),
                    new RouteWaypoint(-1.0f, 9.5f, WaypointKind.Stop),
                },
            });

            // Offensive line, standard splits, six-man protection. The tackles kick-slide to widen the edge rushers
            // past the launch point; the interior three vertical-set and give the quarterback a lane to step up into.
            // Two beats each is the whole job - they hold ground, they do not run routes.
            play.Players.Add(new PlayerScript
            {
                Number = 74,
                Side = TeamSide.Home,
                RouteName = "pass pro",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, -2.4f),
                    new RouteWaypoint(-1.3f, -3.0f),
                    new RouteWaypoint(-2.1f, -3.6f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 66,
                Side = TeamSide.Home,
                RouteName = "pass pro",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, -1.1f),
                    new RouteWaypoint(-1.2f, -1.3f),
                    new RouteWaypoint(-1.8f, -1.4f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 55,
                Side = TeamSide.Home,
                RouteName = "pass pro",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 0.0f),
                    new RouteWaypoint(-1.1f, -0.3f),
                    new RouteWaypoint(-1.6f, -0.5f, WaypointKind.Stop),
                },
            });

            // RG #68 Pellegrini - the man who drew the flag on the previous snap. Clean vertical set on the 3-tech
            // this time, no hands outside the frame.
            play.Players.Add(new PlayerScript
            {
                Number = 68,
                Side = TeamSide.Home,
                RouteName = "pass pro",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 1.1f),
                    new RouteWaypoint(-1.2f, 1.4f),
                    new RouteWaypoint(-1.8f, 1.6f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 77,
                Side = TeamSide.Home,
                RouteName = "pass pro",
                InitialFacingDegrees = 90f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(-0.5f, 2.4f),
                    new RouteWaypoint(-1.3f, 3.1f),
                    new RouteWaypoint(-2.1f, 3.7f, WaypointKind.Stop),
                },
            });

            // Four-man rush. With six blockers on four rushers there is nothing to win: the ends get run past the
            // top of the arc and the tackles get stalemated, so no rusher is inside two yards of the launch point
            // when the ball comes out at 2.4 s. Coverage beats this play, not pressure.
            play.Players.Add(new PlayerScript
            {
                Number = 91,
                Side = TeamSide.Away,
                RouteName = "speed rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, -3.4f),
                    new RouteWaypoint(-1.0f, -4.6f),
                    new RouteWaypoint(-3.0f, -4.4f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 94,
                Side = TeamSide.Away,
                RouteName = "bull rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, -0.6f),
                    new RouteWaypoint(-0.8f, -0.9f),
                    new RouteWaypoint(-2.2f, -1.1f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 98,
                Side = TeamSide.Away,
                RouteName = "long arm",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, 1.6f),
                    new RouteWaypoint(-0.7f, 1.2f),
                    new RouteWaypoint(-2.0f, 0.9f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 90,
                Side = TeamSide.Away,
                RouteName = "contain rush",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(1.0f, 4.0f),
                    new RouteWaypoint(-1.6f, 5.2f),
                    new RouteWaypoint(-3.4f, 4.6f, WaypointKind.Stop),
                },
            });

            // Linebackers start at 8 yards - the second-and-very-long shell. MIKE #54 opens and carries the crosser
            // up the seam as far as his hook zone allows before he has to pass it to the safety; WILL #58 has nothing
            // in his half and just walls off the middle behind the comeback.
            play.Players.Add(new PlayerScript
            {
                Number = 54,
                Side = TeamSide.Away,
                RouteName = "mike hook",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(8.0f, 0.5f),
                    new RouteWaypoint(9.5f, 2.5f),
                    new RouteWaypoint(12.0f, 4.5f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                    new RouteWaypoint(14.0f, 5.5f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 58,
                Side = TeamSide.Away,
                RouteName = "will hook",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(8.0f, -4.0f),
                    new RouteWaypoint(9.5f, -6.0f),
                    new RouteWaypoint(10.5f, -7.5f, WaypointKind.Stop),
                },
            });

            // Nickel #29 Pace gets a hand on #17 at the release and loses him inside, then drops to the curl-flat.
            // That reroute is the only contact the over route sees before the ball arrives.
            play.Players.Add(new PlayerScript
            {
                Number = 29,
                Side = TeamSide.Away,
                RouteName = "cover 2 flat",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(7.0f, 6.5f),
                    new RouteWaypoint(8.0f, 5.0f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(8.5f, 9.0f),
                    new RouteWaypoint(8.0f, 12.0f, WaypointKind.Stop),
                },
            });

            // Corners bailed pre-snap and squat in the Cover 2 flats - #21 sits under the post to the field, #24
            // sinks with the boundary stem and drives back down on the comeback. Nothing gets over either of them.
            play.Players.Add(new PlayerScript
            {
                Number = 21,
                Side = TeamSide.Away,
                RouteName = "cover 2 corner",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(8.0f, 12.0f),
                    new RouteWaypoint(9.5f, 12.5f),
                    new RouteWaypoint(11.5f, 11.0f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 24,
                Side = TeamSide.Away,
                RouteName = "cover 2 corner",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(8.0f, -11.0f),
                    new RouteWaypoint(12.0f, -11.5f),
                    new RouteWaypoint(15.5f, -11.5f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                    new RouteWaypoint(13.0f, -12.0f, WaypointKind.Stop),
                },
            });

            // Two-high at 14. SS #33 takes the post over the top, which frees FS #31 Delgado to trust his running
            // mate, read the quarterback's eyes off the drop, and drive roughly fifteen yards downhill. He arrives at
            // x +6 / 20 yards at the same instant the ball does and knocks it out - correct call, correct read,
            // better defensive player.
            play.Players.Add(new PlayerScript
            {
                Number = 33,
                Side = TeamSide.Away,
                RouteName = "deep half",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(14.0f, 8.0f),
                    new RouteWaypoint(18.0f, 6.5f),
                    new RouteWaypoint(23.0f, 4.0f, WaypointKind.Stop),
                },
            });

            play.Players.Add(new PlayerScript
            {
                Number = 31,
                Side = TeamSide.Away,
                RouteName = "deep half",
                InitialFacingDegrees = 270f,
                Waypoints = new List<RouteWaypoint>
                {
                    new RouteWaypoint(14.0f, -6.0f),
                    new RouteWaypoint(16.0f, -3.0f),
                    new RouteWaypoint(17.5f, 0.5f, WaypointKind.Cut) { SpeedFraction = 0.55f },
                    new RouteWaypoint(20.0f, 6.0f, WaypointKind.Stop),   // arrives with the ball - PBU
                },
            });

            // Ball: snap, a full 2.4 s in a clean pocket, then 28.4 yards of air in 0.95 s to the NOR 42. Delgado
            // gets there at the same beat, so the throw is closed by an Incomplete at the catch point rather than a
            // Catch - the ball settles for a second before the whistle.
            play.BallEvents.Add(new BallEvent
            {
                Time = 0f,
                Kind = BallEventKind.Snap,
                FromNumber = 55,
                ToNumber = 9,
                Side = TeamSide.Home,
            });

            play.BallEvents.Add(new BallEvent
            {
                Time = 2.4f,
                Kind = BallEventKind.Throw,
                FromNumber = 9,
                ToNumber = 17,
                Side = TeamSide.Home,
                ApexMeters = 5f,
            });

            play.BallEvents.Add(new BallEvent
            {
                Time = 3.4f,
                Kind = BallEventKind.Incomplete,
                FromNumber = 9,
                ToNumber = 17,
                Side = TeamSide.Home,
                TargetYardLine = 58f,                              // 20 downfield from the 38 - the NOR 42
                TargetLateralYards = 6f - FieldGeometry.HashLateralYards,
                DeadAfterSeconds = 1f,
            });

            // No gain, no flag. The clock stops at 9:12 and restarts on the snap, which is the only reason the next
            // play is a 3rd & 27 from the same spot with four seconds off the game clock instead of forty.
            play.Outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PassIncomplete,
                YardsGained = 0f,
                FirstDown = false,
                ElapsedSeconds = 4f,
                ClockRunsAfter = false,
                BallCarrierNumber = 17,
                PasserNumber = 9,
                HasPenalty = false,
            };

            return play;
        }

        static PlayScript BuildPlay6()
            {
                var play = new PlayScript
                {
                    Name = "Gun Trips Right — Four Verticals",
                    Formation = "Shotgun Trips Right (11 personnel), X isolated into the boundary",
                    DefensiveCall = "Nickel 4-2-5 — 4-Man Rush, 2-Man Under",
                    Offense = TeamSide.Home,

                    // 3rd & 27 from the Ironhawks' own 38, ball on the LEFT hash. The boundary is short and the
                    // field is long, which is the whole point: the trips are in the field, the X is alone in the
                    // boundary, and every safety's eye discipline is being taxed on the wide side.
                    LineOfScrimmageYard = 38f,
                    BallLateralYards = -FieldGeometry.HashLateralYards,

                    MinDurationSeconds = 6.8f,
                };

                // ── QUARTERBACK ────────────────────────────────────────────────────────────────────────────
                // Vance takes the gun snap at 6.5, drops to 7.6, and climbs back up to 7.0 on one hitch. The hitch
                // is not footwork for its own sake — it is the beat where his eyes hold #33 on the trips seams. He
                // does not look backside until the ball is already coming out at 2.6 s.
                play.Players.Add(new PlayerScript
                {
                    Number = 9, Side = TeamSide.Home, RouteName = "gun drop / hitch", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-6.5f, 0.0f),
                        new RouteWaypoint(-7.6f, -0.4f),
                        new RouteWaypoint(-7.0f, -1.0f, WaypointKind.Cut) { SpeedFraction = 0.35f },
                        new RouteWaypoint(-7.4f, -1.6f, WaypointKind.Stop),
                    },
                });

                // ── THE SCORER: X ISOLATED IN THE BOUNDARY ────────────────────────────────────────────────
                // Colston beats the press with an outside release at 5 yards, which is the only moment of the play
                // Moreau can still win. From there it is a straight line: stacked at 18, under the ball at 31,
                // caught in stride at 46 (the NOR 16), and across at 62. The stem is authored dead straight after
                // the release so the ball and the body arrive at the same point without either being nudged.
                play.Players.Add(new PlayerScript
                {
                    Number = 11, Side = TeamSide.Home, RouteName = "go", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(0.0f, -11.0f),
                        new RouteWaypoint(5.0f, -12.3f, WaypointKind.Cut) { SpeedFraction = 0.55f },
                        new RouteWaypoint(18.0f, -12.0f),
                        new RouteWaypoint(31.0f, -12.0f),
                        new RouteWaypoint(46.0f, -12.0f),
                        new RouteWaypoint(62.0f, -12.0f),
                        new RouteWaypoint(70.0f, -12.2f, WaypointKind.Stop),
                    },
                });

                // ── THE TRIPS: THREE STEMS THAT MOVE THE DEEP HALF ────────────────────────────────────────
                // #17 is the #2 and the lever on the whole play. His seam bends away from the hash rather than up
                // it, so Whitlock has to chase width to keep leverage and drags his half off the middle of the
                // field. Nothing he does matters to the ball; everything he does matters to where the safety is.
                play.Players.Add(new PlayerScript
                {
                    Number = 17, Side = TeamSide.Home, RouteName = "seam", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-1.0f, 8.0f),
                        new RouteWaypoint(6.0f, 8.4f),
                        new RouteWaypoint(16.0f, 9.2f),
                        new RouteWaypoint(28.0f, 10.0f),
                        new RouteWaypoint(40.0f, 10.6f, WaypointKind.Stop),
                    },
                });

                // #83 is the #1 to the field. He releases outside and runs at the numbers with no intention of
                // being thrown to — his job is to keep Tiller pinned so there is nobody left to rotate over.
                play.Players.Add(new PlayerScript
                {
                    Number = 83, Side = TeamSide.Home, RouteName = "go", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-1.0f, 12.0f),
                        new RouteWaypoint(6.0f, 13.5f, WaypointKind.Cut) { SpeedFraction = 0.55f },
                        new RouteWaypoint(20.0f, 14.5f),
                        new RouteWaypoint(34.0f, 15.0f),
                        new RouteWaypoint(44.0f, 15.2f, WaypointKind.Stop),
                    },
                });

                // #85 is the #3, and the bender is what empties the middle. He climbs off the flex and leans back
                // toward the hash, which forces Renfroe to run 25 yards up the pipe in trail. A MIKE carrying a
                // vertical is a MIKE who cannot rob anything.
                play.Players.Add(new PlayerScript
                {
                    Number = 85, Side = TeamSide.Home, RouteName = "bender", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-1.0f, 4.5f),
                        new RouteWaypoint(7.0f, 4.0f),
                        new RouteWaypoint(16.0f, 2.2f, WaypointKind.Cut) { SpeedFraction = 0.55f },
                        new RouteWaypoint(25.0f, 1.2f),
                        new RouteWaypoint(32.0f, 0.8f, WaypointKind.Stop),
                    },
                });

                // ── RUNNING BACK: THE SIXTH PROTECTOR ─────────────────────────────────────────────────────
                // Pruitt scans right, chips Voss to make the right tackle's rep a formality, and only then
                // check-releases to the flat. He is the reason the pocket lasts 2.6 s, and he is never a read.
                play.Players.Add(new PlayerScript
                {
                    Number = 26, Side = TeamSide.Home, RouteName = "chip / flat", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-6.0f, 2.0f),
                        new RouteWaypoint(-5.2f, 3.6f),
                        new RouteWaypoint(-3.0f, 5.2f, WaypointKind.Cut) { SpeedFraction = 0.4f },
                        new RouteWaypoint(-1.5f, 8.5f),
                        new RouteWaypoint(1.0f, 12.0f, WaypointKind.Stop),
                    },
                });

                // ── OFFENSIVE LINE: SIX-MAN PROTECTION, NOBODY PULLS ──────────────────────────────────────
                // Five vertical sets and a facing pinned square. Redmond kick-slides on Grant and wins the rep he
                // lost on the sack two plays ago; the interior simply anchors and gives Vance a launch point at
                // 7 yards that never moves.
                play.Players.Add(new PlayerScript
                {
                    Number = 74, Side = TeamSide.Home, RouteName = "kick-slide", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-0.5f, -2.4f),
                        new RouteWaypoint(-2.0f, -3.1f) { PinFacing = true, FacingDegrees = 90f },
                        new RouteWaypoint(-3.4f, -3.6f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 66, Side = TeamSide.Home, RouteName = "vertical set", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-0.5f, -1.1f),
                        new RouteWaypoint(-1.6f, -1.3f) { PinFacing = true, FacingDegrees = 90f },
                        new RouteWaypoint(-2.3f, -1.5f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 55, Side = TeamSide.Home, RouteName = "set / help left", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-0.5f, 0.0f),
                        new RouteWaypoint(-1.5f, -0.5f) { PinFacing = true, FacingDegrees = 90f },
                        new RouteWaypoint(-2.2f, -0.8f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 68, Side = TeamSide.Home, RouteName = "vertical set", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-0.5f, 1.1f),
                        new RouteWaypoint(-1.6f, 1.4f) { PinFacing = true, FacingDegrees = 90f },
                        new RouteWaypoint(-2.3f, 1.6f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 77, Side = TeamSide.Home, RouteName = "kick-slide", InitialFacingDegrees = 90f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(-0.5f, 2.4f),
                        new RouteWaypoint(-2.0f, 3.2f) { PinFacing = true, FacingDegrees = 90f },
                        new RouteWaypoint(-3.4f, 3.8f, WaypointKind.Stop) { PinFacing = true, FacingDegrees = 90f },
                    },
                });

                // ── DEFENSIVE LINE: A FOUR-MAN RUSH THAT NEVER ARRIVES ────────────────────────────────────
                // Grant's speed rush arcs him past the launch point instead of into it, Voss gets chipped and
                // walled outside, and both interior rushers are stalled at the line. Four rushers against six
                // blockers is exactly the arithmetic 2-Man is willing to accept — and on 3rd & 27 it costs them.
                play.Players.Add(new PlayerScript
                {
                    Number = 91, Side = TeamSide.Away, RouteName = "speed rush", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(1.0f, -3.4f),
                        new RouteWaypoint(-1.0f, -4.6f),
                        new RouteWaypoint(-4.5f, -5.4f),
                        new RouteWaypoint(-7.0f, -4.2f, WaypointKind.Stop),
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 94, Side = TeamSide.Away, RouteName = "bull rush", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(1.0f, -0.6f),
                        new RouteWaypoint(0.0f, -1.0f),
                        new RouteWaypoint(-1.2f, -1.4f, WaypointKind.Stop),
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 98, Side = TeamSide.Away, RouteName = "3-tech rush", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(1.0f, 1.6f),
                        new RouteWaypoint(0.2f, 1.4f),
                        new RouteWaypoint(-1.0f, 1.0f, WaypointKind.Stop),
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 90, Side = TeamSide.Away, RouteName = "contain rush", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(1.0f, 4.0f),
                        new RouteWaypoint(-0.5f, 5.0f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                        new RouteWaypoint(-3.5f, 5.6f),
                        new RouteWaypoint(-6.0f, 4.6f, WaypointKind.Stop),
                    },
                });

                // ── LINEBACKERS: MAN COVERAGE ON PEOPLE WHO ARE NOT THE PROBLEM ───────────────────────────
                // Renfroe is legally obliged to run with the #3 bender and does, 30 yards up the middle. Bailey
                // has the back, who releases late into an empty flat. Neither is available to help the boundary,
                // which is the design of 2-Man and the reason the design fails here.
                play.Players.Add(new PlayerScript
                {
                    Number = 54, Side = TeamSide.Away, RouteName = "2-man trail", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(6.5f, 1.0f),
                        new RouteWaypoint(9.0f, 2.4f) { PinFacing = true, FacingDegrees = 270f },
                        new RouteWaypoint(17.0f, 2.8f),
                        new RouteWaypoint(26.0f, 1.8f),
                        new RouteWaypoint(30.0f, 1.4f, WaypointKind.Stop),
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 58, Side = TeamSide.Away, RouteName = "2-man trail", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(6.5f, -3.5f),
                        new RouteWaypoint(5.5f, -1.0f) { PinFacing = true, FacingDegrees = 270f },
                        new RouteWaypoint(4.0f, 4.0f),
                        new RouteWaypoint(2.5f, 9.5f, WaypointKind.Stop),
                    },
                });

                // ── CORNERS AND NICKEL: FIVE IN TRAIL, NOBODY WITH EYES ON THE BALL ───────────────────────
                // Pace stays hands-on through 15 on the #2 seam and Tiller runs the #1 to the sideline. Moreau is
                // the rep that decides the game: he presses, misses, has to flip his hips from a losing position,
                // and pays for it with a hard break at the release — the speed he loses there is the 2.4 yards he
                // is behind at the catch point and never gets back.
                play.Players.Add(new PlayerScript
                {
                    Number = 29, Side = TeamSide.Away, RouteName = "2-man trail", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(6.0f, 8.0f),
                        new RouteWaypoint(5.0f, 8.2f) { PinFacing = true, FacingDegrees = 270f },
                        new RouteWaypoint(10.0f, 8.6f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                        new RouteWaypoint(22.0f, 9.2f),
                        new RouteWaypoint(34.0f, 9.8f, WaypointKind.Stop),
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 21, Side = TeamSide.Away, RouteName = "2-man trail", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(7.0f, 12.5f),
                        new RouteWaypoint(6.0f, 13.0f) { PinFacing = true, FacingDegrees = 270f },
                        new RouteWaypoint(14.0f, 14.2f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                        new RouteWaypoint(28.0f, 15.0f),
                        new RouteWaypoint(40.0f, 15.4f, WaypointKind.Stop),
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 24, Side = TeamSide.Away, RouteName = "press-trail", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(2.0f, -11.5f),
                        new RouteWaypoint(0.5f, -11.9f, WaypointKind.Cut) { SpeedFraction = 0.35f },
                        new RouteWaypoint(5.5f, -12.9f, WaypointKind.Cut) { SpeedFraction = 0.45f },
                        new RouteWaypoint(17.0f, -11.6f),
                        new RouteWaypoint(33.0f, -11.5f),
                        new RouteWaypoint(46.0f, -11.4f),
                        new RouteWaypoint(61.0f, -11.0f, WaypointKind.Stop),
                    },
                });

                // ── SAFETIES: THE TWO DEEP HALVES, AND THE ONE THAT GOT MOVED ─────────────────────────────
                // Whitlock widens six yards off the hash chasing the #2 seam, which is honest coverage and also
                // the exact mistake — his half is now a sideline and the middle of the field is empty. Delgado is
                // left to overlap a boundary go from the far hash, breaks late, and takes a pursuit angle that was
                // already losing when he chose it. This is the replay a knowledgeable viewer will freeze.
                play.Players.Add(new PlayerScript
                {
                    Number = 33, Side = TeamSide.Away, RouteName = "deep half", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(15.0f, 6.0f),
                        new RouteWaypoint(19.0f, 11.5f) { PinFacing = true, FacingDegrees = 270f },
                        new RouteWaypoint(28.0f, 13.5f),
                        new RouteWaypoint(38.0f, 13.0f, WaypointKind.Stop),
                    },
                });
                play.Players.Add(new PlayerScript
                {
                    Number = 31, Side = TeamSide.Away, RouteName = "deep half", InitialFacingDegrees = 270f,
                    Waypoints = new List<RouteWaypoint>
                    {
                        new RouteWaypoint(15.0f, -6.0f),
                        new RouteWaypoint(19.0f, -6.5f) { PinFacing = true, FacingDegrees = 270f },
                        new RouteWaypoint(27.0f, -8.5f, WaypointKind.Cut) { SpeedFraction = 0.5f },
                        new RouteWaypoint(40.0f, -10.0f),
                        new RouteWaypoint(58.0f, -10.8f, WaypointKind.Stop),
                    },
                });

                // ── THE BALL ──────────────────────────────────────────────────────────────────────────────
                // Snap, one hitch, and the ball leaves at 2.6 s from 7 yards deep and a yard left of centre. It
                // travels 54.1 yards of air to x -12 / y +46 with 2.4 s of hang — a genuinely high arc, which is
                // why the apex is authored at 7 m rather than a dig's 2.5. Caught in stride at the NOR 16, across
                // the goal line at 6.6.
                play.BallEvents.Add(new BallEvent
                {
                    Time = 0f, Kind = BallEventKind.Snap, Side = TeamSide.Home, FromNumber = 55, ToNumber = 9,
                });
                play.BallEvents.Add(new BallEvent
                {
                    Time = 2.6f, Kind = BallEventKind.Throw, Side = TeamSide.Home, FromNumber = 9, ToNumber = 11,
                    ApexMeters = 7f,
                });
                play.BallEvents.Add(new BallEvent
                {
                    Time = 5.0f, Kind = BallEventKind.Catch, Side = TeamSide.Home, FromNumber = 9, ToNumber = 11,
                });
                play.BallEvents.Add(new BallEvent
                {
                    Time = 6.6f, Kind = BallEventKind.CrossGoalLine, Side = TeamSide.Home, FromNumber = 11, ToNumber = 11,
                });

                // 62 yards, 46 in the air and 16 after the catch, on 3rd & 27. The clock stops at 9:06 for the
                // score, and the drive ends here: BAY 17 - NOR 14 once the PAT is good.
                play.Outcome = new PlayOutcome
                {
                    Kind = PlayResultKind.Touchdown,
                    YardsGained = 62f,
                    FirstDown = true,
                    ElapsedSeconds = 6f,
                    ClockRunsAfter = false,
                    BallCarrierNumber = 11,
                    PasserNumber = 9,
                    HasPenalty = false,
                };

                return play;
            }

    }
}
