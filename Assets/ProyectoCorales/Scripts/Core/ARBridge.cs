using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if AR_FOUNDATION
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace ProyectoCorales
{
    /// <summary>
    /// Capa delgada sobre AR Foundation (ARCore). Si el paquete no está instalado o el
    /// dispositivo no es compatible, la app usa automáticamente la vista 3D.
    /// </summary>
    public class ARBridge : MonoBehaviour
    {
#if AR_FOUNDATION
        public ARSession session;
        public XROrigin origin;
        public ARPlaneManager planeManager;
        public ARRaycastManager raycastManager;
        public Camera arCamera;

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();
#endif

        public bool Available { get; private set; }
        public bool Running { get; private set; }

        public Camera Camera
        {
            get
            {
#if AR_FOUNDATION
                return arCamera;
#else
                return null;
#endif
            }
        }

        public IEnumerator CheckAvailability(Action<bool> done)
        {
#if AR_FOUNDATION
            if (session == null || arCamera == null)
            {
                Available = false;
                done(false);
                yield break;
            }

            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                yield return ARSession.CheckAvailability();

            if (ARSession.state == ARSessionState.NeedsInstall)
                yield return ARSession.Install();

            Available = ARSession.state >= ARSessionState.Ready;
            done(Available);
#else
            Available = false;
            done(false);
            yield break;
#endif
        }

        public void Begin()
        {
#if AR_FOUNDATION
            if (!Available) return;
            origin.gameObject.SetActive(true);
            session.enabled = true;
            planeManager.enabled = true;
            SetPlanesVisible(true);
            Running = true;
#endif
        }

        public void Restart()
        {
#if AR_FOUNDATION
            if (!Running) return;
            session.Reset();
#endif
        }

        public void End()
        {
#if AR_FOUNDATION
            if (!Running) return;
            SetPlanesVisible(false);
            planeManager.enabled = false;
            session.enabled = false;
            origin.gameObject.SetActive(false);
            Running = false;
#endif
        }

        public bool Tracking
        {
            get
            {
#if AR_FOUNDATION
                return Running && ARSession.state == ARSessionState.SessionTracking;
#else
                return false;
#endif
            }
        }

        public int PlaneCount
        {
            get
            {
#if AR_FOUNDATION
                return Running && planeManager != null ? planeManager.trackables.count : 0;
#else
                return 0;
#endif
            }
        }

        public bool TryRaycast(Vector2 screen, out Pose pose)
        {
            pose = Pose.identity;
#if AR_FOUNDATION
            if (!Running || raycastManager == null) return false;
            if (raycastManager.Raycast(screen, Hits, TrackableType.PlaneWithinPolygon))
            {
                pose = Hits[0].pose;
                return true;
            }
#endif
            return false;
        }

        public void SetPlanesVisible(bool visible)
        {
#if AR_FOUNDATION
            if (planeManager == null) return;
            foreach (var p in planeManager.trackables) p.gameObject.SetActive(visible);
#endif
        }

        /// <summary>Detiene la detección de nuevos planos (tras colocar el modelo).</summary>
        public void FreezePlanes(bool frozen)
        {
#if AR_FOUNDATION
            if (planeManager == null || !Running) return;
            planeManager.enabled = !frozen;
            SetPlanesVisible(!frozen);
#endif
        }
    }
}
