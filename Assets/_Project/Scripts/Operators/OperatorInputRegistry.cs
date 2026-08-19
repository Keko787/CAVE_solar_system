using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Operators
{
    /// <summary>
    /// Where consumers look up "the input for operator X". Sources register themselves on enable, so a
    /// consumer holds an <see cref="OperatorRole"/> in the inspector instead of a cross-scene object
    /// reference — the same reason the CAVE panels talk to each other through public methods rather than
    /// serialized links.
    ///
    /// Both roles are held at once and neither shadows the other: a wand source and a pad source can be
    /// registered simultaneously and are read independently every frame. That is the whole point of the
    /// secondary-operator feature.
    /// </summary>
    /// <remarks>
    /// Plain static state rather than a scene singleton, so there is no "which GameObject owns input?"
    /// lifecycle question and no execution-order dependency on a hub existing. Cleared on subsystem
    /// registration so it survives Unity's "Enter Play Mode without domain reload" option, which would
    /// otherwise carry dead sources from the previous play session into the next one.
    /// </remarks>
    public static class OperatorInputRegistry
    {
        static readonly Dictionary<OperatorRole, IOperatorInput> s_Sources = new();

        /// <summary>Roles that currently have a source registered.</summary>
        public static IEnumerable<OperatorRole> Roles => s_Sources.Keys;

        /// <summary>How many operators are currently driving the room.</summary>
        public static int Count => s_Sources.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Sources.Clear();

        /// <summary>
        /// Claim a role for a source. Registering a second source for a role replaces the first and warns —
        /// silently dropping one would present as "the pad does nothing", which is miserable to diagnose in
        /// a CAVE session.
        /// </summary>
        public static void Register(OperatorRole role, IOperatorInput source)
        {
            if (source == null)
                return;

            if (s_Sources.TryGetValue(role, out var existing) && !ReferenceEquals(existing, source))
                Debug.LogWarning($"[Operators] {role} already had input source '{existing.DeviceName}'; " +
                                 $"replacing it with '{source.DeviceName}'. Only one source per role is read.");

            s_Sources[role] = source;
        }

        /// <summary>Release a role, but only if <paramref name="source"/> is the one still holding it.</summary>
        public static void Unregister(OperatorRole role, IOperatorInput source)
        {
            if (source != null && s_Sources.TryGetValue(role, out var existing) && ReferenceEquals(existing, source))
                s_Sources.Remove(role);
        }

        /// <summary>The source driving this role, or null if nobody is.</summary>
        public static IOperatorInput Get(OperatorRole role) =>
            s_Sources.TryGetValue(role, out var source) ? source : null;

        /// <summary>The source driving this role, if any.</summary>
        public static bool TryGet(OperatorRole role, out IOperatorInput source) =>
            s_Sources.TryGetValue(role, out source);

        /// <summary>True if this role has a source and that source's device is actually present.</summary>
        public static bool IsConnected(OperatorRole role) =>
            s_Sources.TryGetValue(role, out var source) && source != null && source.IsConnected;

        /// <summary>Drop every registration. For tests and teardown.</summary>
        public static void Clear() => s_Sources.Clear();
    }
}
