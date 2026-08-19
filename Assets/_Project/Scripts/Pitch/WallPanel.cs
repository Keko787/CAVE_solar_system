using UnityEngine;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// One CAVE surface's picture: a flat quad hung at the wall, showing a pitch camera's render target.
    /// </summary>
    /// <remarks>
    /// <b>The aspect is driven from the render texture, in code, every time.</b> The inherited
    /// <c>DroneFeedWindow</c> prefab showed a 1.333 target inside a 2.256 rectangle with no fitter — a 1.69x
    /// horizontal stretch that survived review because a stretched map still looks like a map. A stretched
    /// football field would be unmissable, and a stretched player is the one thing that would destroy the
    /// life-size framing the side walls exist for, so the panel refuses to take its proportions from
    /// anywhere but the texture it is showing.
    ///
    /// <para>The panel is unlit and writes no depth beyond its own quad: it is a picture of somewhere else,
    /// not a surface in the room, and lighting it would make the room's key light visible as a sheen across
    /// a stadium.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class WallPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("The pitch camera whose view this panel shows.")]
        PitchCameraBase m_Source;

        [SerializeField, Tooltip("Physical size of the surface this panel covers, metres (width, height).")]
        Vector2 m_SurfaceMeters = new Vector2(3.50f, 2.19f);

        [SerializeField, Tooltip("The quad. Created by the builder if left empty.")]
        Renderer m_Screen;

        Material m_Material;
        Texture m_Bound;

        /// <summary>Physical size of this panel, metres.</summary>
        public Vector2 SurfaceMeters => m_SurfaceMeters;

        /// <summary>The camera feeding it.</summary>
        public PitchCameraBase Source { get => m_Source; set => m_Source = value; }

        void LateUpdate()
        {
            if (m_Source == null || m_Screen == null) return;

            var view = m_Source.View;
            if (view == null || view == m_Bound) return;

            Bind(view);
        }

        /// <summary>
        /// Show a render target, sizing the quad so the image is never distorted.
        /// </summary>
        /// <remarks>
        /// The surface is a fixed physical rectangle and the image has its own aspect, so unless the two
        /// agree exactly something has to give. Letterboxing inside the surface is the only option that
        /// preserves proportion — the alternative, filling the surface, is the stretch this class exists to
        /// prevent. In practice the render targets are matched to their projector viewports, so the
        /// correction is a fraction of a millimetre and the letterbox never actually appears.
        /// </remarks>
        public void Bind(Texture view)
        {
            m_Bound = view;

            if (m_Material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
                m_Material = new Material(shader) { name = name + "_panel" };
            }

            m_Material.mainTexture = view;
            if (m_Material.HasProperty("_BaseMap")) m_Material.SetTexture("_BaseMap", view);
            if (m_Material.HasProperty("_BaseColor")) m_Material.SetColor("_BaseColor", Color.white);
            m_Screen.sharedMaterial = m_Material;

            float imageAspect = view.height <= 0 ? 1f : (float)view.width / view.height;
            float surfaceAspect = m_SurfaceMeters.y <= 0f ? 1f : m_SurfaceMeters.x / m_SurfaceMeters.y;

            float w = m_SurfaceMeters.x, h = m_SurfaceMeters.y;
            if (imageAspect > surfaceAspect) h = w / imageAspect;   // image is wider: bars top and bottom
            else                             w = h * imageAspect;   // image is taller: bars at the sides

            m_Screen.transform.localScale = new Vector3(w, h, 1f);
        }

        /// <summary>Wire the quad from a builder.</summary>
        public void Configure(PitchCameraBase source, Renderer screen, Vector2 surfaceMeters)
        {
            m_Source = source;
            m_Screen = screen;
            m_SurfaceMeters = surfaceMeters;
        }

        void OnDestroy()
        {
            if (m_Material != null) Destroy(m_Material);
        }
    }
}
