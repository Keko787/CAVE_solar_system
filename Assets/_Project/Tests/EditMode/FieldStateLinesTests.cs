using NUnit.Framework;
using Stadium3D.Football;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The line to gain, pinned. Every assertion here exists because the failure it guards produces a
    /// picture that looks entirely correct — a clean yellow line across a field, on the wrong side of the
    /// ball, or a yard short of where the chains actually are.
    /// </summary>
    public class FieldStateLinesTests
    {
        static GameSituation Situation(TeamSide possession, float scrimmageYard, float distance,
                                       bool goalToGo = false)
        {
            var s = GameSituation.Kickoff(possession, scrimmageYard);
            s.Down = 1;
            s.DistanceYards = distance;
            s.LineOfScrimmageYard = scrimmageYard;
            s.GoalToGo = goalToGo;
            return s;
        }

        [Test]
        public void Home_LineToGain_IsUpTheYardAxis()
        {
            var lines = FieldStateLines.For(Situation(TeamSide.Home, 30f, 10f));

            Assert.That(lines.ScrimmageYard, Is.EqualTo(30f).Within(1e-4f));
            Assert.That(lines.LineToGainYard, Is.EqualTo(40f).Within(1e-4f),
                "home attacks yard 100, so first and ten from the 30 is gained at the 40");
            Assert.That(lines.ShowLineToGain, Is.True);
        }

        [Test]
        public void Away_LineToGain_IsDownTheYardAxis()
        {
            var lines = FieldStateLines.For(Situation(TeamSide.Away, 70f, 10f));

            Assert.That(lines.LineToGainYard, Is.EqualTo(60f).Within(1e-4f),
                "away attacks yard 0, so the line to gain is below the ball, not above it");
        }

        /// <summary>
        /// The one that matters most. Both teams, same spot, same distance — the lines must land on
        /// opposite sides of the ball and the same distance from it. A sign error passes every other test
        /// in this file by simply moving both lines the same way.
        /// </summary>
        [Test]
        public void Possession_DecidesWhichSideOfTheBallTheLineFallsOn()
        {
            var home = FieldStateLines.For(Situation(TeamSide.Home, 50f, 8f));
            var away = FieldStateLines.For(Situation(TeamSide.Away, 50f, 8f));

            Assert.That(home.LineToGainYard, Is.GreaterThan(50f));
            Assert.That(away.LineToGainYard, Is.LessThan(50f));
            Assert.That(home.LineToGainYard - 50f, Is.EqualTo(50f - away.LineToGainYard).Within(1e-4f),
                "the two must be mirror images, or one direction is being scaled as well as flipped");
        }

        [Test]
        public void LineToGain_NeverGoesPastTheGoalLineBeingAttacked()
        {
            // First and 15 from the opponent's 12 after a penalty: the ball still only has to reach the end.
            var home = FieldStateLines.For(Situation(TeamSide.Home, 88f, 15f));
            Assert.That(home.LineToGainYard, Is.EqualTo(FieldGeometry.AwayGoalLineYard).Within(1e-4f));

            var away = FieldStateLines.For(Situation(TeamSide.Away, 12f, 15f));
            Assert.That(away.LineToGainYard, Is.EqualTo(FieldGeometry.HomeGoalLineYard).Within(1e-4f));
        }

        [Test]
        public void GoalToGo_DrawsNoLineToGain()
        {
            var lines = FieldStateLines.For(Situation(TeamSide.Home, 94f, 6f, goalToGo: true));

            Assert.That(lines.ShowLineToGain, Is.False,
                "the line to gain is the goal line, which is already painted — drawing it twice reads as two lines");
        }

        [Test]
        public void ALineThatLandsExactlyOnTheGoalLineIsNotDrawnEither()
        {
            // Not flagged goal-to-go, but the arithmetic still puts the line on painted paint.
            var lines = FieldStateLines.For(Situation(TeamSide.Home, 90f, 10f));

            Assert.That(lines.LineToGainYard, Is.EqualTo(100f).Within(1e-4f));
            Assert.That(lines.ShowLineToGain, Is.False);
        }

        [Test]
        public void Scrimmage_IsClampedIntoTheFieldOfPlay()
        {
            var behind = FieldStateLines.For(Situation(TeamSide.Home, -4f, 10f));
            Assert.That(behind.ScrimmageYard, Is.EqualTo(0f).Within(1e-4f));

            var beyond = FieldStateLines.For(Situation(TeamSide.Away, 106f, 10f));
            Assert.That(beyond.ScrimmageYard, Is.EqualTo(100f).Within(1e-4f));
        }
    }
}
