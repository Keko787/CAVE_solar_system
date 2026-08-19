using UnityEngine;

namespace Stadium3D.Operators
{
    /// <summary>
    /// Claims an <see cref="OperatorRole"/> for the MiddleVR wand and drives its per-frame sampling. The
    /// wand read itself lives in <see cref="WandInput"/>; this component only puts it on the register, so
    /// a consumer can ask for "operator Primary" instead of holding a scene reference.
    ///
    /// Optional in practice — a cursor falls back to <see cref="WandInput.Shared"/> when the primary role
    /// is unclaimed, so existing scenes keep working untouched. Add it when you want the wand to appear
    /// in the registry alongside the pad, which is what the probe and the interaction log read.
    /// </summary>
    [DefaultExecutionOrder(-1000)]   // sample before any consumer's Update reads an edge
    public class WandOperatorInput : MonoBehaviour
    {
        [SerializeField, Tooltip("Which operator this device drives. The wand is the primary operator.")]
        OperatorRole m_Role = OperatorRole.Primary;

        [SerializeField, Min(0), Tooltip("Buttons this wand reports. The VisBox Flystick exposes 0-5, " +
            "which is what the .vrx Wand0 entry declares (Button0..Button5).")]
        int m_ButtonCount = WandInput.DefaultButtonCount;

        /// <summary>Which operator this device drives.</summary>
        public OperatorRole Role => m_Role;

        /// <summary>The wand read this component registers.</summary>
        public IOperatorInput Input => WandInput.Shared;

        void OnEnable()
        {
            WandInput.Shared.ButtonCount = m_ButtonCount;
            OperatorInputRegistry.Register(m_Role, WandInput.Shared);
        }

        void OnDisable() => OperatorInputRegistry.Unregister(m_Role, WandInput.Shared);

        void Update() => WandInput.Shared.Sample();
    }
}
