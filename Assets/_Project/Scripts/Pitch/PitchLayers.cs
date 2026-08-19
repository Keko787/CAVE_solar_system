using UnityEngine;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// The layer names the pitch is built on, and the masks each camera uses.
    /// </summary>
    /// <remarks>
    /// The layer set is decided here, in M2, rather than discovered in M3 — because the constraint that
    /// fixes it belongs to the cameras, not to the geometry. Four pitch cameras each have to exclude the
    /// CAVE room, the stat canvases, the wand reticle, and <b>all four wall panels including their own</b>.
    /// Miss that last one and every wall shows a picture-in-picture of its neighbours while its own panel
    /// becomes a feedback loop. Designing the layers after the geometry exists means re-layering everything.
    ///
    /// <para>Masks are built as <b>allow-lists</b>. The inherited <c>TopDownMapCamera</c> used
    /// <c>~LayerMask.GetMask(exclude)</c> — everything-except — which is exactly the wrong default here: a
    /// layer added later silently appears in every pitch camera rather than silently missing from one.</para>
    /// </remarks>
    public static class PitchLayers
    {
        /// <summary>Turf, paint, numbers, goalposts, pylons, apron. Every pitch camera sees it.</summary>
        public const string Shared = "PitchShared";

        /// <summary>Full-colour un-dimmed player markers. Broadcast and floor only.</summary>
        public const string Players = "PitchPlayers";

        /// <summary>The home wall's per-player dressing: own team lit, opponents ghosted.</summary>
        public const string RightWall = "PitchRightWall";

        /// <summary>The away wall's per-player dressing.</summary>
        public const string LeftWall = "PitchLeftWall";

        /// <summary>Routes, coverage shading, down markers. Floor only — broadcast must never show these.</summary>
        public const string Tactical = "PitchTactical";

        /// <summary>Bowl, crowd, floodlights. The three stadium-eye cameras only.</summary>
        public const string Stadium = "PitchStadium";

        /// <summary>The four RT wall panels. The MiddleVR display cameras only.</summary>
        public const string CaveScreen = "CaveScreen";

        /// <summary>
        /// Jersey-number billboards. <b>The team walls only.</b>
        /// </summary>
        /// <remarks>
        /// A layer of their own because identity has to appear on exactly one kind of surface. On the
        /// broadcast wall, eleven numbers inside eight yards of scrimmage collapse into an unreadable pile;
        /// on the floor at 1:22 a number is a vertical billboard seen from directly overhead, so it renders
        /// as a white slab that obscures the very body it is labelling. Both were visible the moment the
        /// walls were first rendered. The team walls are the one place a player is close to life-size and a
        /// number both fits and means something.
        /// </remarks>
        public const string Numbers = "PitchNumbers";

        /// <summary>
        /// Football rendered as <b>real geometry standing in the room</b> — the life-size figure in M3.5 and
        /// the miniature field in M8.
        /// </summary>
        /// <remarks>
        /// The one category of pitch content the MiddleVR display cameras are allowed to see, and the only
        /// thing in the whole demo that is genuinely stereoscopic. Everything on the four walls is a flat
        /// picture of somewhere else — correct, useful, and reproducible on four televisions. This layer is
        /// where the room stops being a video wall, so it is deliberately excluded from
        /// <see cref="PitchMask"/> and from every wall camera's allow-list: the display cameras must keep it,
        /// and the pitch cameras must never pick it up and render a person floating over the field.
        /// </remarks>
        public const string Hologram = "PitchHologram";

        /// <summary>
        /// Every pitch layer the MiddleVR display cameras must strip. <see cref="Hologram"/> is deliberately
        /// absent — it is the content that exists to be seen in the room.
        /// </summary>
        public static int PitchMask => Mask(Shared, Players, RightWall, LeftWall, Tactical, Stadium, Numbers);

        /// <summary>Just the room-scale stereo content, for anything that needs to isolate it.</summary>
        public static int HologramMask => Mask(Hologram);

        /// <summary>The broadcast camera on the front wall: the whole field, both teams, no tactical overlay.</summary>
        public static int BroadcastMask => Mask(Shared, Players, Stadium);

        /// <summary>The home-team isolation camera on the right wall.</summary>
        /// <remarks>
        /// <see cref="Players"/> is included <em>as well as</em> <see cref="RightWall"/> because the per-wall
        /// dressing — own team lit, opponents ghosted — is M4's work and nothing lives on those layers yet.
        /// Without it a side wall renders turf and no bodies, which is what it did the first time this was
        /// built. When M4 adds the per-player dressing renderers, drop <see cref="Players"/> from these two
        /// masks so each wall shows its own version of a player rather than the undressed one.
        /// </remarks>
        public static int RightWallMask => Mask(Shared, Players, RightWall, Stadium, Numbers);

        /// <summary>The away-team isolation camera on the left wall. See <see cref="RightWallMask"/>.</summary>
        public static int LeftWallMask => Mask(Shared, Players, LeftWall, Stadium, Numbers);

        /// <summary>The floor's top-down tactical camera. No stadium bowl — it would only occlude.</summary>
        public static int FloorMask => Mask(Shared, Players, Tactical);

        /// <summary>
        /// A mask from layer names, skipping any that are not defined. Skipping rather than throwing is
        /// deliberate: a missing layer should degrade to "that content is not visible" while the rest of the
        /// demo still runs, not take the whole room down on startup in front of an audience.
        /// </summary>
        public static int Mask(params string[] names)
        {
            int mask = 0;
            for (int i = 0; i < names.Length; i++)
            {
                int layer = LayerMask.NameToLayer(names[i]);
                if (layer >= 0)
                    mask |= 1 << layer;
                else
                    Debug.LogWarning($"[Stadium3D] Layer \"{names[i]}\" is not defined in Tags and Layers.");
            }
            return mask;
        }

        /// <summary>Set a layer on an object and everything under it.</summary>
        public static void ApplyRecursively(GameObject root, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (root == null || layer < 0) return;

            root.layer = layer;
            var children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
                children[i].gameObject.layer = layer;
        }
    }
}
