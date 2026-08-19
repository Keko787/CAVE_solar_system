using System.Collections;
using UnityEngine;
using Stadium3D.CaveBox;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// Strips every pitch layer from the MiddleVR display cameras, so 110 metres of stadium never renders
    /// inside a 3.5 metre room.
    /// </summary>
    /// <remarks>
    /// This works for exactly one reason, and it is worth stating precisely because everything visual in the
    /// demo depends on it: <c>MVRTools.UpdateCameraProperties</c> rewrites each display camera's near plane,
    /// far plane and projection matrix every frame, and <b>never touches <c>cullingMask</c></b>. So a
    /// one-shot flip persists. <see cref="FrontWallPortal"/> already proved the technique in the source
    /// project; this is the same move applied to the whole pitch instead of one reveal layer.
    ///
    /// <para>The coroutine start is not defensive padding. MiddleVR builds its display cameras during
    /// <c>MVRManager</c>'s own <c>Awake</c>, and script execution order does not guarantee that happens
    /// before ours — so the mask has to be applied once they exist, not once we do.</para>
    ///
    /// <para><b>Belt and braces.</b> The pitch also sits 1500 m below the room, beyond the <c>Far="1000"</c>
    /// the <c>.vrx</c> gives every display camera. If this component fails to run, or a camera appears late
    /// and is missed, the far plane still hides the field. Two independent guarantees, because the failure
    /// mode — a stadium suddenly drawn across the walls of the room — is the worst-looking one available.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class PitchIsolation : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds to wait for MiddleVR to build its display cameras before giving up.")]
        float m_Timeout = 10f;

        [SerializeField, Tooltip("Re-apply periodically. Off by default — the flip persists, and a repeating " +
            "sweep would mask a real ordering bug rather than surface it.")]
        bool m_Reapply;

        [SerializeField, Min(0.5f)] float m_ReapplyInterval = 2f;

        /// <summary>True once the mask has been applied to at least one display camera.</summary>
        public bool Applied { get; private set; }

        /// <summary>How many display cameras were stripped on the last pass.</summary>
        public int CameraCount { get; private set; }

        IEnumerator Start()
        {
            float timeout = m_Timeout;
            while (timeout > 0f && !HasDisplayCameras())
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Apply();

            if (!Applied)
            {
                // Off the MiddleVR platform this is expected and harmless — the sandbox scene has no display
                // cameras at all. In the CAVE it means the pitch is only hidden by the far plane.
                Debug.Log("[Stadium3D] PitchIsolation found no MiddleVR display cameras. Expected outside " +
                          "the CAVE; in the CAVE, the pitch is relying on the far plane alone.", this);
                yield break;
            }

            while (m_Reapply)
            {
                yield return new WaitForSeconds(m_ReapplyInterval);
                Apply();
            }
        }

        /// <summary>Strip the pitch layers from every display camera. Idempotent.</summary>
        public void Apply()
        {
            int mask = PitchLayers.PitchMask;
            int n = 0;

            foreach (var cam in MvrDisplay.Cameras())
            {
                if (cam == null) continue;
                cam.cullingMask &= ~mask;
                n++;
            }

            CameraCount = n;
            if (n > 0) Applied = true;
        }

        static bool HasDisplayCameras()
        {
            foreach (var cam in MvrDisplay.Cameras())
                if (cam != null) return true;
            return false;
        }
    }
}
