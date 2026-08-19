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
        /// Reserved for M8's 3D miniature — the one pitch thing the display cameras are allowed to see,
        /// because it is real geometry in the room rather than a picture of somewhere else.
        /// </summary>
        public const string Miniature = "PitchMiniature";

        /// <summary>Every pitch layer. What the MiddleVR display cameras must strip.</summary>
        public static int PitchMask => Mask(Shared, Players, RightWall, LeftWall, Tactical, Stadium);

        /// <summary>The broadcast camera on the front wall: the whole field, both teams, no tactical overlay.</summary>
        public static int BroadcastMask => Mask(Shared, Players, Stadium);

        /// <summary>The home-team isolation camera on the right wall.</summary>
        public static int RightWallMask => Mask(Shared, RightWall, Stadium);

        /// <summary>The away-team isolation camera on the left wall.</summary>
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
