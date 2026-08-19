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
        /// <summary>
        /// Everything every pitch camera renders <b>identically</b>: turf, paint, numbers, goalposts,
        /// pylons, apron — and the ball, the facing chevrons and the blob shadows.
        /// </summary>
        /// <remarks>
        /// The three dynamic members look out of place beside the scenery until you ask the question this
        /// layer actually answers, which is not "is it static" but "does every wall show the same thing".
        /// A chevron and a shadow are neutral, and the ball is the one object every viewer is tracking, so
        /// none of them wants a per-wall variant. Putting them here is also what lets <see cref="Players"/>
        /// come out of the two side-wall masks without the team walls losing the ball.
        /// </remarks>
        public const string Shared = "PitchShared";

        /// <summary>Full-colour un-dimmed player markers. Broadcast and floor only.</summary>
        public const string Players = "PitchPlayers";

        /// <summary>
        /// The home wall's per-player dressing: own team lit, opponents ghosted — <b>and the home team's
        /// jersey numbers</b>.
        /// </summary>
        public const string RightWall = "PitchRightWall";

        /// <summary>The away wall's per-player dressing, and the away team's jersey numbers.</summary>
        public const string LeftWall = "PitchLeftWall";

        /// <summary>Routes, coverage shading, down markers. Floor only — broadcast must never show these.</summary>
        public const string Tactical = "PitchTactical";

        /// <summary>Bowl, crowd, floodlights. The three stadium-eye cameras only.</summary>
        public const string Stadium = "PitchStadium";

        /// <summary>The four RT wall panels. The MiddleVR display cameras only.</summary>
        public const string CaveScreen = "CaveScreen";

        /// <summary>
        /// <b>Reserved, and carrying nothing today.</b> Jersey numbers ride <see cref="RightWall"/> and
        /// <see cref="LeftWall"/> instead.
        /// </summary>
        /// <remarks>
        /// The original reasoning was right as far as it went: identity belongs on exactly one kind of
        /// surface. On the broadcast wall eleven numbers inside eight yards of scrimmage collapse into an
        /// unreadable pile, and on the floor at 1:22 a number is a vertical billboard seen from directly
        /// overhead, so it renders as a white slab across the body it labels.
        ///
        /// <para>What one shared layer cannot express is <em>whose</em> number. It is visible to both team
        /// walls or neither, so the away wall drew all twenty-two — labelling eleven opponents it had just
        /// deliberately ghosted out. The bodies said "not the subject" and the numbers said the opposite,
        /// in an overlapping pile along the horizon. Putting each number on its own player's wall layer
        /// answers both questions with the rule the bodies already follow.</para>
        ///
        /// <para>Kept defined, and kept inside <see cref="PitchMask"/>, so that anything put here later is
        /// still stripped from the display cameras rather than appearing in the room.</para>
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
        /// <see cref="Players"/> is deliberately <b>absent</b>. Each player carries a second body renderer
        /// on <see cref="RightWall"/> whose material was fixed at build time — own team lit, opponents
        /// ghosted — and if the undressed body on <see cref="Players"/> were visible here as well, both
        /// would draw at the same place and the ghosting would be buried under a fully lit capsule.
        ///
        /// <para>Two renderers rather than one material swapped per camera: both side walls render the
        /// same frame, so a shared material cannot be lit for one and ghosted for the other, and swapping
        /// in <c>onPreCull</c> breaks the SRP batcher this project depends on. The cost is 44 extra
        /// renderers and no per-frame work at all.</para>
        /// </remarks>
        public static int RightWallMask => Mask(Shared, RightWall, Stadium);

        /// <summary>The away-team isolation camera on the left wall. See <see cref="RightWallMask"/>.</summary>
        public static int LeftWallMask => Mask(Shared, LeftWall, Stadium);

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

        /// <summary>
        /// Set a layer on everything under <paramref name="root"/> that is still on the default layer.
        /// </summary>
        /// <remarks>
        /// The catch-all for parts a builder did not place deliberately, without undoing the ones it did.
        /// <see cref="ApplyRecursively"/> stamps everything, which meant every part needing its own layer —
        /// the jersey numbers, and now two dressed bodies and the ball — had to be stamped a second time
        /// after the sweep, in a different method, and stayed correct only as long as nobody forgot.
        /// Skipping anything already assigned makes the builder's own choice the one that survives.
        /// </remarks>
        public static void ApplyToUnassigned(GameObject root, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (root == null || layer < 0) return;

            var children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].gameObject.layer == 0)
                    children[i].gameObject.layer = layer;
            }
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
