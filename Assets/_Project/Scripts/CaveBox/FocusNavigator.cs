using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>A step of focus movement, in the plane of the panel the focused chip sits on.</summary>
    public enum FocusDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>
    /// Picks which chip focus moves to. Aiming a stick-driven ray at a chip is miserable; walking a grid of
    /// chips with a D-pad is what a gamepad is good at, so the secondary operator navigates rather than aims.
    ///
    /// Pure 2D maths on positions already flattened into the focused chip's panel plane — no
    /// <c>MonoBehaviour</c>, no transforms, no scene. The projection into that plane is the caller's job
    /// (<see cref="GamepadFocusCursor"/>), which keeps the part that is easy to get wrong — and impossible to
    /// eyeball in a CAVE — testable at a desk.
    /// </summary>
    public static class FocusNavigator
    {
        /// <summary>
        /// How much harder to work off-axis counts than distance along the direction of travel. High enough
        /// that pressing Right prefers the next chip in the same row over a closer one in the row below,
        /// which is what makes a row of filter chips feel like a row.
        /// </summary>
        public const float DefaultPerpendicularPenalty = 3f;

        // Candidates level with the origin along the direction of travel are not "in" that direction.
        const float Epsilon = 1e-4f;

        /// <summary>
        /// Index of the chip focus should move to, or -1 if there is nowhere to go. Positions are in the
        /// focused chip's panel plane, with <paramref name="origin"/> the focused chip itself.
        /// </summary>
        /// <param name="wrap">
        /// When nothing lies in the direction of travel, come back round from the far side — pressing Down
        /// at the bottom of a column lands at its top. Preferring column alignment over distance, so the
        /// wrap stays in the column you were walking.
        /// </param>
        public static int Pick(Vector2 origin, IReadOnlyList<Vector2> candidates, FocusDirection direction,
            bool wrap = false, float perpendicularPenalty = DefaultPerpendicularPenalty)
        {
            if (candidates == null || candidates.Count == 0)
                return -1;

            Axes(direction, out Vector2 along, out Vector2 perp);
            float penalty = Mathf.Max(0f, perpendicularPenalty);

            int best = -1;
            float bestScore = float.MaxValue;
            float bestPerp = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2 delta = candidates[i] - origin;
                float a = Vector2.Dot(delta, along);
                if (a <= Epsilon)
                    continue;

                float p = Mathf.Abs(Vector2.Dot(delta, perp));
                float score = a + penalty * p;

                // Ties resolve toward the better-aligned chip, then toward the lower index, so the same
                // layout always walks the same way.
                if (score < bestScore || (Mathf.Approximately(score, bestScore) && p < bestPerp))
                {
                    best = i;
                    bestScore = score;
                    bestPerp = p;
                }
            }

            if (best >= 0 || !wrap)
                return best;

            // Nothing ahead: take the chip furthest behind, which is the far edge seen from here.
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2 delta = candidates[i] - origin;
                float a = Vector2.Dot(delta, along);
                if (a >= -Epsilon)
                    continue;

                // `a` is negative here, so adding it rewards distance: the further back a chip is, the
                // better it scores, which lands the wrap on the far edge rather than the near neighbour.
                float p = Mathf.Abs(Vector2.Dot(delta, perp));
                float score = penalty * p + a;
                if (score < bestScore || (Mathf.Approximately(score, bestScore) && p < bestPerp))
                {
                    best = i;
                    bestScore = score;
                    bestPerp = p;
                }
            }
            return best;
        }

        /// <summary>
        /// Where focus lands when it arrives with nothing focused — top-most, then left-most, i.e. where a
        /// reader starts. Returns -1 for an empty set.
        /// </summary>
        public static int PickInitial(IReadOnlyList<Vector2> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return -1;

            int best = 0;
            for (int i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].y > candidates[best].y + Epsilon ||
                    (Mathf.Abs(candidates[i].y - candidates[best].y) <= Epsilon && candidates[i].x < candidates[best].x))
                    best = i;
            }
            return best;
        }

        static void Axes(FocusDirection direction, out Vector2 along, out Vector2 perp)
        {
            switch (direction)
            {
                case FocusDirection.Up:
                    along = Vector2.up; perp = Vector2.right; return;
                case FocusDirection.Down:
                    along = Vector2.down; perp = Vector2.right; return;
                case FocusDirection.Left:
                    along = Vector2.left; perp = Vector2.up; return;
                default:
                    along = Vector2.right; perp = Vector2.up; return;
            }
        }
    }
}
