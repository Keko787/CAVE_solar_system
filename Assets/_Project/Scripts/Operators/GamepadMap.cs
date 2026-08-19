using UnityEngine;

namespace Stadium3D.Operators
{
    /// <summary>
    /// A pad's button/axis translation as a config asset — the same "data, not code" treatment
    /// <c>CaveRegionMap</c> gives the CAVE surfaces, and for the same reason: which raw index is the A
    /// button depends on the pad, the driver, and the machine, so it must be tunable without a rebuild.
    /// Nothing here references scene objects, so it stays a pure asset.
    ///
    /// You normally do not need one of these for the Unity Input System backend
    /// (<see cref="UnityGamepadOperatorInput"/> reports controls in canonical order already). You <em>do</em>
    /// need one for the MiddleVR/DirectInput backend, filled in from what <see cref="GamepadProbe"/> logs
    /// on the target machine.
    /// </summary>
    [CreateAssetMenu(fileName = "GamepadMap", menuName = "Stadium3D/Gamepad Map", order = 1)]
    public class GamepadMap : ScriptableObject
    {
        [SerializeField, Tooltip("Raw device indices translated into the canonical PadControl/PadAxis space.")]
        PadMapping m_Mapping = PadMapping.UnityDefault();

        /// <summary>The mapping this asset carries.</summary>
        public PadMapping Mapping => m_Mapping;

        /// <summary>
        /// Reset the asset to the identity mapping used by the Unity Input System backend. Shared by the
        /// editor builder and the tests so "what a default map looks like" is verified, matching how
        /// <c>CaveRegionMap.PopulateDefault</c> is shared.
        /// </summary>
        public static void PopulateUnityDefault(GamepadMap map)
        {
            if (map != null)
                map.m_Mapping = PadMapping.UnityDefault();
        }

        /// <summary>
        /// Reset the asset to the common DirectInput layout for an Xbox-style pad — a starting point for
        /// the MiddleVR backend that still has to be confirmed against the probe, particularly the POV-hat
        /// D-pad and the shared trigger axis.
        /// </summary>
        public static void PopulateDirectInputXboxDefault(GamepadMap map)
        {
            if (map != null)
                map.m_Mapping = PadMapping.DirectInputXboxDefault();
        }
    }
}
