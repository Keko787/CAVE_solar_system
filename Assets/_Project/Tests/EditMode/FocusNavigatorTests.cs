using System.Collections.Generic;
using Stadium3D.CaveBox;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.CaveBox.Tests
{
    /// <summary>
    /// Where a focus step lands. This is the whole feel of gamepad navigation — pressing Right must reach
    /// the next chip in the row, not a nearer one in the row below — and it is the one part of the feature
    /// that cannot be judged from a log in a CAVE session, so the layout maths is pinned here.
    /// </summary>
    public class FocusNavigatorTests
    {
        // A 3x3 grid, indices left-to-right then bottom-to-top:
        //   6 7 8   y=2
        //   3 4 5   y=1
        //   0 1 2   y=0
        static List<Vector2> Grid()
        {
            var points = new List<Vector2>();
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    points.Add(new Vector2(x, y));
            return points;
        }

        [Test]
        public void StepsToTheImmediateNeighbourInEachDirection()
        {
            var grid = Grid();
            var centre = grid[4];   // (1,1)

            Assert.AreEqual(7, FocusNavigator.Pick(centre, grid, FocusDirection.Up));
            Assert.AreEqual(1, FocusNavigator.Pick(centre, grid, FocusDirection.Down));
            Assert.AreEqual(3, FocusNavigator.Pick(centre, grid, FocusDirection.Left));
            Assert.AreEqual(5, FocusNavigator.Pick(centre, grid, FocusDirection.Right));
        }

        [Test]
        public void PrefersStayingInTheRowOverAStrictlyNearerChipOffIt()
        {
            // From (0,1) pressing Right: (1,1) is in the row, (1,2) and (1,0) are the same distance along
            // but one row off. A row of filter chips has to walk like a row.
            var grid = Grid();

            Assert.AreEqual(4, FocusNavigator.Pick(grid[3], grid, FocusDirection.Right));
        }

        [Test]
        public void DoesNotStepOntoChipsLevelWithTheOrigin()
        {
            var row = new List<Vector2> { new(0f, 0f), new(1f, 0f), new(2f, 0f) };

            Assert.AreEqual(-1, FocusNavigator.Pick(row[0], row, FocusDirection.Up),
                "nothing is above a single row — including the row itself");
            Assert.AreEqual(-1, FocusNavigator.Pick(row[2], row, FocusDirection.Right),
                "the last chip in a row has nowhere further right");
        }

        [Test]
        public void WithoutWrap_TheEdgeIsTheEnd()
        {
            var grid = Grid();

            Assert.AreEqual(-1, FocusNavigator.Pick(grid[1], grid, FocusDirection.Down, wrap: false));
        }

        [Test]
        public void WithWrap_ComesBackRoundToTheFarEdgeOfTheSameColumn()
        {
            var grid = Grid();

            // From (1,0) pressing Down lands on (1,2) — the top of the same column, not (1,1) which is
            // merely the nearest chip behind.
            Assert.AreEqual(7, FocusNavigator.Pick(grid[1], grid, FocusDirection.Down, wrap: true));

            // And the mirror: from (0,1) pressing Left lands on (2,1).
            Assert.AreEqual(5, FocusNavigator.Pick(grid[3], grid, FocusDirection.Left, wrap: true));
        }

        [Test]
        public void WrapPrefersColumnAlignmentOverRawDistance()
        {
            // (1,2) is directly above; (0,3) is further back but a column off. Wrapping must stay in the
            // column the operator was walking.
            var points = new List<Vector2> { new(1f, 0f), new(1f, 2f), new(0f, 3f) };

            Assert.AreEqual(1, FocusNavigator.Pick(points[0], points, FocusDirection.Down, wrap: true));
        }

        [Test]
        public void PickInitial_StartsWhereAReaderWould()
        {
            var grid = Grid();

            Assert.AreEqual(6, FocusNavigator.PickInitial(grid), "top row, left-most");
        }

        [Test]
        public void EmptyOrMissingSets_ReportNowhereToGo()
        {
            var empty = new List<Vector2>();

            Assert.AreEqual(-1, FocusNavigator.Pick(Vector2.zero, empty, FocusDirection.Up));
            Assert.AreEqual(-1, FocusNavigator.Pick(Vector2.zero, null, FocusDirection.Up));
            Assert.AreEqual(-1, FocusNavigator.PickInitial(empty));
            Assert.AreEqual(-1, FocusNavigator.PickInitial(null));
        }

        [Test]
        public void ASingleChipHasNowhereToStep()
        {
            var one = new List<Vector2> { Vector2.zero };

            Assert.AreEqual(-1, FocusNavigator.Pick(one[0], one, FocusDirection.Right, wrap: true),
                "wrapping onto yourself is not a move");
        }

        [Test]
        public void RaisingThePerpendicularPenalty_TightensTheStepToTheRow()
        {
            // (3,0) is far along the row; (1,1) is nearer but a row off. With no penalty the nearer chip
            // wins; with the default penalty the row does.
            var points = new List<Vector2> { new(0f, 0f), new(3f, 0f), new(1f, 1f) };

            Assert.AreEqual(2, FocusNavigator.Pick(points[0], points, FocusDirection.Right,
                wrap: false, perpendicularPenalty: 0f));
            Assert.AreEqual(1, FocusNavigator.Pick(points[0], points, FocusDirection.Right));
        }
    }
}
