using System.Collections.Generic;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using MiddleVR;             // MVR, vrCameraStereo
using MiddleVR.Unity;       // MVRNodesMapper
#endif

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// The MiddleVR per-eye display cameras — the head-tracked CAVE wall projections — enumerated exactly as
    /// <c>MVRTools.UpdateCameraProperties</c> does (name-agnostic; skips the stereo container and the offscreen
    /// pitch RT cameras, which are not in <c>DisplayMgr.Cameras</c>). Used to flip a per-layer culling bit
    /// for reveals and for hiding the pitch from the room (the front-wall portal, pitch isolation): MVR only
    /// rewrites near/far/projection each
    /// frame, never the culling mask, so a one-shot flip persists. No-ops off the MiddleVR platform.
    /// </summary>
    public static class MvrDisplay
    {
        public static IEnumerable<Camera> Cameras()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            if (!MVRNodesMapper.HasInstance)
                yield break;
            var displayMgr = MVR.DisplayMgr;
            if (displayMgr == null)
                yield break;

            foreach (var vc in displayMgr.Cameras)
            {
                if (vc is vrCameraStereo)
                    continue;
                var go = MVRNodesMapper.Instance.GetNode(vc);
                var cam = go != null ? go.GetComponent<Camera>() : null;
                if (cam != null)
                    yield return cam;
            }
#else
            yield break;
#endif
        }
    }
}
