using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ProyectoCorales
{
    /// <summary>
    /// Gestos con el nuevo Input System: 1 dedo = arrastrar/rotar, 2 dedos = pellizcar/zoom, toque = seleccionar.
    /// En el editor funciona con el mouse (arrastrar + rueda).
    /// </summary>
    public class GestureInput
    {
        public Vector2 Drag;      // píxeles este frame
        public float Pinch = 1f;  // factor de escala este frame
        public bool Tap;
        public Vector2 TapPosition;

        Vector2 _start;
        float _startTime;
        bool _startedOverUI;
        bool _multi;
        bool _mouseDown;

        static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        public GestureInput()
        {
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }

        public static bool IsOverUI(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = pos };
            Hits.Clear();
            es.RaycastAll(ped, Hits);
            return Hits.Count > 0;
        }

        public void Update()
        {
            Drag = Vector2.zero;
            Pinch = 1f;
            Tap = false;

            var touches = ETouch.activeTouches;
            if (touches.Count > 0)
            {
                if (touches.Count >= 2)
                {
                    _multi = true;
                    var a = touches[0];
                    var b = touches[1];
                    if (_startedOverUI) return;
                    float cur = Vector2.Distance(a.screenPosition, b.screenPosition);
                    float prev = Vector2.Distance(a.screenPosition - a.delta, b.screenPosition - b.delta);
                    if (prev > 10f) Pinch = Mathf.Clamp(cur / prev, 0.8f, 1.25f);
                    return;
                }

                var t = touches[0];
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        _start = t.screenPosition;
                        _startTime = Time.unscaledTime;
                        _startedOverUI = IsOverUI(t.screenPosition);
                        _multi = false;
                        break;
                    case TouchPhase.Moved:
                        if (!_startedOverUI && !_multi) Drag = t.delta;
                        break;
                    case TouchPhase.Ended:
                        if (!_startedOverUI && !_multi &&
                            Time.unscaledTime - _startTime < 0.35f &&
                            Vector2.Distance(t.screenPosition, _start) < 18f)
                        {
                            Tap = true;
                            TapPosition = t.screenPosition;
                        }
                        break;
                }
                return;
            }

            _multi = false;

            // ---- mouse (editor / escritorio)
            var mouse = Mouse.current;
            if (mouse == null) return;
            var pos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _mouseDown = true;
                _start = pos;
                _startTime = Time.unscaledTime;
                _startedOverUI = IsOverUI(pos);
            }
            else if (_mouseDown && mouse.leftButton.isPressed)
            {
                if (!_startedOverUI) Drag = mouse.delta.ReadValue();
            }
            else if (_mouseDown && mouse.leftButton.wasReleasedThisFrame)
            {
                _mouseDown = false;
                if (!_startedOverUI && Time.unscaledTime - _startTime < 0.35f && Vector2.Distance(pos, _start) < 8f)
                {
                    Tap = true;
                    TapPosition = pos;
                }
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUI(pos))
                Pinch = Mathf.Clamp(1f + Mathf.Sign(scroll) * 0.08f, 0.8f, 1.25f);
        }
    }
}
