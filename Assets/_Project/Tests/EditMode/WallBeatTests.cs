using NUnit.Framework;
using Stadium3D.Football;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The crossfade between a team wall's two readouts.
    /// </summary>
    /// <remarks>
    /// Tested as arithmetic because that is what it is. The property worth protecting is not that the fade
    /// looks nice but that it is a <em>function</em> — the same transport state always gives the same
    /// answer — since that is what keeps seeking, looping and slow motion exact everywhere else in this
    /// project. A remembered fade would pass any visual check and then leave a half-faded card behind after
    /// a scrub.
    /// </remarks>
    public class WallBeatTests
    {
        const float Fade = WallBeat.FadeSeconds;

        [Test]
        public void TheHuddleShowsTheCardAndALivePlayShowsTheTelemetry()
        {
            Assert.That(WallBeat.LiveWeight(PlayPhase.Huddle, 0f, 2f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(WallBeat.LiveWeight(PlayPhase.Live, 2f, 0f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(WallBeat.LiveWeight(PlayPhase.PostPlay, 3f, 2f), Is.EqualTo(0f).Within(0.001f),
                "at the result the wall is back to the card");
        }

        /// <summary>
        /// Nothing jumps at a phase change: each boundary is approached and left at the same weight.
        /// </summary>
        [Test]
        public void TheWeightIsContinuousAcrossEveryPhaseBoundary()
        {
            // Huddle runs out at 0, and the snap opens at 0.
            Assert.That(WallBeat.LiveWeight(PlayPhase.Huddle, 0f, 5f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(WallBeat.LiveWeight(PlayPhase.Live, 0f, 0f), Is.EqualTo(0f).Within(0.001f));

            // A live play ends at 1, and PostPlay opens at 1.
            Assert.That(WallBeat.LiveWeight(PlayPhase.Live, 4f, 0f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(WallBeat.LiveWeight(PlayPhase.PostPlay, 4f, 0f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void TheFadeIsMonotonicInBothDirections()
        {
            float previous = -1f;
            for (int i = 0; i <= 10; i++)
            {
                float weight = WallBeat.LiveWeight(PlayPhase.Live, Fade * i / 10f, 0f);
                Assert.That(weight, Is.GreaterThanOrEqualTo(previous), "the snap fades the telemetry in");
                previous = weight;
            }

            previous = 2f;
            for (int i = 0; i <= 10; i++)
            {
                float weight = WallBeat.LiveWeight(PlayPhase.PostPlay, 4f, Fade * i / 10f);
                Assert.That(weight, Is.LessThanOrEqualTo(previous), "the whistle fades the card in");
                previous = weight;
            }
        }

        /// <summary>
        /// The one an accumulated fade would fail: arriving mid-play by a seek is not a transition.
        /// </summary>
        [Test]
        public void SeekingIntoTheMiddleOfAPlayArrivesFullyLive()
        {
            Assert.That(WallBeat.LiveWeight(PlayPhase.Live, 3.4f, 0f), Is.EqualTo(1f).Within(0.001f),
                "no fade left over from a transition that never happened");
        }

        [Test]
        public void HalfwayThroughTheFadeIsHalfway()
        {
            float weight = WallBeat.LiveWeight(PlayPhase.Live, Fade * 0.5f, 0f);

            Assert.That(weight, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(1f - weight, Is.EqualTo(0.5f).Within(0.01f),
                "the two readouts always sum to one, so the wall is never blank between them");
        }

        [Test]
        public void TheEndOfTheDriveShowsTheCard()
        {
            Assert.That(WallBeat.LiveWeight(PlayPhase.DriveComplete, 4f, 2f), Is.EqualTo(0f).Within(0.001f),
                "the last thing the wall says is who did it and what he did");
        }
    }
}
