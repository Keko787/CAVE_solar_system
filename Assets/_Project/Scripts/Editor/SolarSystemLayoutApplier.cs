using System;
using System.Collections.Generic;
using System.Text;
using Stadium3D.Space;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Stamps <see cref="SolarSystemLayout"/> onto whatever solar system is already in the open scene: sizes every
    /// body, puts it on its orbit, and hands the lot to a single <see cref="SolarSystemDriver"/>.
    /// </summary>
    /// <remarks>
    /// <para>It matches bodies <em>by name</em> and never creates or deletes one. The planets carry their own
    /// meshes, materials and click handlers, and a builder that recreated them would throw all of that away every
    /// time someone re-ran it. So a missing body is reported, not manufactured, and re-running is safe.</para>
    ///
    /// <para>Nothing is reparented either. <see cref="SolarSystemDriver"/> finds orbits scene-wide and sorts them
    /// itself, so the layout does not care how the hierarchy is arranged — which matters because parenting a moon
    /// under a 2.7 cm Earth would multiply its orbit radius by 0.027 and hide it inside the planet.</para>
    ///
    /// <para>Any legacy <c>RotateAround</c> on a body it touches is removed. Two things writing one transform is
    /// the kind of fight that looks like a physics bug for an afternoon.</para>
    /// </remarks>
    public static class SolarSystemLayoutApplier
    {
        const string RootName = "SolarSystem";
        const string RoomName = "CAVE_Room";
        const string UndoLabel = "Apply Solar System CAVE Layout";

        [MenuItem("Stadium3D/Build/Solar System CAVE Layout", priority = 70)]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Stadium3D] No open scene to lay the solar system out in.");
                return;
            }

            Dictionary<string, Transform> byName = IndexScene(scene);
            Transform root = EnsureRoot(byName);

            // The Sun anchors everything. Without one the root stands in, so the planets still get a sane ring
            // rather than collapsing onto the world origin.
            Transform anchor = root;
            if (byName.TryGetValue("Sun", out Transform sun))
                anchor = sun;

            var applied = new List<string>();
            var missing = new List<string>();
            var warnings = new List<string>();
            int legacyRemoved = 0;

            for (int i = 0; i < SolarSystemLayout.Bodies.Length; i++)
            {
                BodySpec spec = SolarSystemLayout.Bodies[i];

                if (!byName.TryGetValue(spec.Name, out Transform body))
                {
                    missing.Add(spec.Name);
                    continue;
                }

                Undo.RecordObject(body, UndoLabel);

                // localScale is a diameter only when no ancestor is scaled. Warn rather than silently compensate:
                // a scaled parent is nearly always an accident, and quietly dividing it out hides it.
                if (body.parent != null && !Approximately(body.parent.lossyScale, Vector3.one))
                {
                    warnings.Add($"{spec.Name} sits under \"{body.parent.name}\" which has a lossy scale of " +
                                 $"{body.parent.lossyScale} — its rendered size will not be {spec.DiameterMeters:0.###} m.");
                }

                body.localScale = Vector3.one * spec.DiameterMeters;

                legacyRemoved += StripLegacyRotator(body);

                if (spec.IsCentre)
                {
                    body.position = root.position;

                    // The Sun does not orbit, so a leftover orbit component from a previous layout has to go or
                    // the driver will fling the centre of the system around its own anchor.
                    var stale = body.GetComponent<PlanetOrbit>();
                    if (stale != null)
                        Undo.DestroyObjectImmediate(stale);

                    applied.Add(spec.Name);
                    continue;
                }

                Transform centre = anchor;
                if (spec.Orbits != "Sun")
                {
                    if (byName.TryGetValue(spec.Orbits, out Transform host))
                    {
                        centre = host;
                    }
                    else
                    {
                        warnings.Add($"{spec.Name} orbits {spec.Orbits}, which is not in the scene — " +
                                     "it has been put on the Sun instead.");
                    }
                }

                var orbit = body.GetComponent<PlanetOrbit>();
                if (orbit == null)
                    orbit = Undo.AddComponent<PlanetOrbit>(body.gameObject);
                else
                    Undo.RecordObject(orbit, UndoLabel);

                orbit.Centre = centre;
                orbit.RadiusMeters = spec.OrbitRadiusMeters;
                orbit.PeriodSeconds = SolarSystemLayout.DisplayPeriodSeconds(spec.SiderealPeriodDays);
                orbit.PhaseDegrees = SolarSystemLayout.PhaseDegrees(i);
                orbit.Axis = Vector3.up;

                // Place it now so the scene reads correctly without entering play mode. The table is ordered
                // inside-out, so a moon's host is always already where it belongs by the time we reach the moon.
                orbit.Evaluate(0d);

                applied.Add(spec.Name);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root.gameObject;

            Report(applied, missing, warnings, legacyRemoved, root);
        }

        /// <summary>
        /// Finds or creates the object that carries the clock and marks the centre of the system: eye height in
        /// the middle of the room, parented to CAVE_Room where there is one so it travels with the room.
        /// </summary>
        static Transform EnsureRoot(Dictionary<string, Transform> byName)
        {
            byName.TryGetValue(RoomName, out Transform room);

            if (byName.TryGetValue(RootName, out Transform existing))
            {
                Undo.RecordObject(existing, UndoLabel);

                if (room != null && existing.parent != room)
                    Undo.SetTransformParent(existing, room, UndoLabel);

                existing.localPosition = SolarSystemLayout.Centre;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;

                if (existing.GetComponent<SolarSystemDriver>() == null)
                    Undo.AddComponent<SolarSystemDriver>(existing.gameObject);

                return existing;
            }

            var go = new GameObject(RootName, typeof(SolarSystemDriver));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);

            if (room != null)
                Undo.SetTransformParent(go.transform, room, UndoLabel);

            go.transform.localPosition = SolarSystemLayout.Centre;
            byName[RootName] = go.transform;
            return go.transform;
        }

        /// <summary>Removes the frame-integrating rotator this layout replaces. Returns how many it took off.</summary>
        static int StripLegacyRotator(Transform body)
        {
            var legacy = body.GetComponents<global::RotateAround>();
            foreach (var component in legacy)
                Undo.DestroyObjectImmediate(component);

            return legacy.Length;
        }

        /// <summary>
        /// Every transform in the scene, keyed by name. First one wins on a tie, which is reported, because two
        /// objects called "Earth" is exactly the situation where a silent pick produces a baffling result.
        /// </summary>
        static Dictionary<string, Transform> IndexScene(Scene scene)
        {
            var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new HashSet<string>();

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                foreach (var transform in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (!map.ContainsKey(transform.name))
                        map[transform.name] = transform;
                    else if (SolarSystemLayout.TryGet(transform.name, out _))
                        duplicates.Add(transform.name);
                }
            }

            if (duplicates.Count > 0)
            {
                Debug.LogWarning($"[Stadium3D] More than one object shares each of these names: " +
                                 $"{string.Join(", ", duplicates)}. The layout used the first of each.");
            }

            return map;
        }

        static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-6f;

        static void Report(List<string> applied, List<string> missing, List<string> warnings,
                           int legacyRemoved, Transform root)
        {
            var message = new StringBuilder();
            message.Append("[Stadium3D] Solar system laid out for the CAVE: ");
            message.Append(applied.Count).Append(" of ").Append(SolarSystemLayout.Bodies.Length);
            message.Append(" bodies sized and put on orbit. Outermost edge reaches ");
            message.Append(SolarSystemLayout.OuterExtentMeters.ToString("0.000"));
            message.Append(" m against a half-depth of ");
            message.Append((SolarSystemLayout.RoomDepthMeters * 0.5f).ToString("0.000")).Append(" m. ");
            message.Append("Press play to see it turn, or scrub Seconds on the SolarSystem object.");

            if (legacyRemoved > 0)
                message.Append($"\nRemoved {legacyRemoved} RotateAround component(s) that would have fought the driver.");

            if (missing.Count > 0)
                message.Append($"\nNot in the scene, so skipped: {string.Join(", ", missing)}.");

            foreach (string warning in warnings)
                message.Append("\n").Append(warning);

            if (missing.Count > 0 || warnings.Count > 0)
                Debug.LogWarning(message.ToString(), root);
            else
                Debug.Log(message.ToString(), root);
        }
    }
}
