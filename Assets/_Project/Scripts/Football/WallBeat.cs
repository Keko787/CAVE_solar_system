using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// Which of a team wall's two readouts is showing: the player's card, or what he is doing right now.
    /// </summary>
    /// <remarks>
    /// <b>§8 asks for two different things from the same surface.</b> While the play runs the wall wants
    /// speed, top speed, ground covered and separation; at the whistle it wants the card — number, name,
    /// vitals and the stat line, which the ledger has just updated with the result of the play you watched.
    /// Drawing both at once, as the first build did, means the busiest moment on the wall is also the most
    /// cluttered, and the quiet moment carries a row of live numbers that have all stopped changing.
    ///
    /// <para><b>A weight, not a boolean, and a pure function of transport state.</b> Everything else in this
    /// project is exactly reproducible under seek, loop and slow motion because nothing integrates per
    /// frame, and a crossfade driven by a timer that counts wall-clock seconds would be the first thing that
    /// is not. Given the phase and its clock, this returns the same weight every time — so scrubbing into
    /// the middle of a play shows the live readout immediately, with no fade left over from a transition
    /// that never happened, and slow motion stretches the fade exactly as it stretches everything else.</para>
    ///
    /// <para><b>The clock is not the same field in every phase.</b> <see cref="DriveDirector"/> advances
    /// <c>PlayTime</c> while the play is live and leaves <c>PhaseTime</c> at zero; between plays it is the
    /// other way round. Both are passed in and the right one is chosen here, once, rather than at each of
    /// the call sites.</para>
    /// </remarks>
    public static class WallBeat
    {
        /// <summary>
        /// Seconds of transport time the crossfade takes.
        /// </summary>
        /// <remarks>
        /// Short enough not to be a transition anybody watches, long enough that a whole lower third does
        /// not appear between two frames a metre from someone's face.
        /// </remarks>
        public const float FadeSeconds = 0.35f;

        /// <summary>
        /// How live the wall is: 0 shows the card, 1 shows the telemetry, between the two is a crossfade.
        /// </summary>
        public static float LiveWeight(PlayPhase phase, float playTime, float phaseTime)
        {
            bool live = phase == PlayPhase.Live;

            float sinceBeatBegan = live ? playTime : phaseTime;
            float t = FadeSeconds <= 0f ? 1f : Mathf.Clamp01(sinceBeatBegan / FadeSeconds);

            // Both sides agree at every boundary: a live play ends at 1 and PostPlay opens at 1, a huddle
            // ends at 0 and the snap opens at 0. Nothing jumps at a phase change, which is the property
            // that lets the fade be recomputed from scratch every frame instead of remembered.
            return live ? t : 1f - t;
        }

        /// <inheritdoc cref="LiveWeight(PlayPhase,float,float)"/>
        public static float LiveWeight(DriveDirector director) =>
            director == null ? 0f : LiveWeight(director.Phase, director.PlayTime, director.PhaseTime);

        /// <summary>How present the card is. The complement of <see cref="LiveWeight(DriveDirector)"/>.</summary>
        public static float CardWeight(DriveDirector director) => 1f - LiveWeight(director);
    }
}
