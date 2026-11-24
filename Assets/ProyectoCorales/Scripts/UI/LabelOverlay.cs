using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProyectoCorales
{
    /// <summary>Etiquetas flotantes con línea guía que siguen a cada estructura del modelo.</summary>
    public class LabelOverlay : MonoBehaviour
    {
        class Label
        {
            public string Id;
            public RectTransform Dot, Line, Chip;
            public CanvasGroup Group;
        }

        readonly List<Label> _labels = new List<Label>();
        ModelController _model;
        Func<Camera> _camera;
        RectTransform _rt;
        Canvas _canvas;

        public void Setup(ModelController model, Func<Camera> camera)
        {
            _model = model;
            _camera = camera;
            _rt = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();

            foreach (var id in model.StructureIds)
            {
                var info = StructureCatalog.Get(id);
                var l = new Label { Id = id };

                var holder = UIKit.Rect("Etiqueta_" + id, transform);
                holder.Stretch();
                l.Group = holder.gameObject.AddComponent<CanvasGroup>();
                l.Group.blocksRaycasts = false;
                l.Group.interactable = false;

                var line = UIKit.Img(holder, "Linea", null, Color.white.WithAlpha(0.75f));
                line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                line.rectTransform.pivot = new Vector2(0f, 0.5f);
                l.Line = line.rectTransform;

                var dot = UIKit.Img(holder, "Punto", UIKit.Rounded(6), info.Color);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dot.rectTransform.sizeDelta = new Vector2(10, 10);
                l.Dot = dot.rectTransform;

                var chip = UIKit.Chip(holder, info.Name, Theme.Glass, Theme.Text, 12);
                chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                l.Chip = chip.rectTransform;

                _labels.Add(l);
            }
        }

        void LateUpdate()
        {
            if (_model == null || !_model.Loaded) return;
            var cam = _camera?.Invoke();
            if (cam == null) return;

            Vector2 center;
            if (!ToLocal(cam, _model.Center, out center)) center = Vector2.zero;

            for (int i = 0; i < _labels.Count; i++)
            {
                var l = _labels[i];
                bool show = _model.IsVisible(l.Id) && _model.TryGetAnchor(l.Id, out var world);
                Vector2 p = Vector2.zero;
                if (show)
                {
                    _model.TryGetAnchor(l.Id, out world);
                    show = ToLocal(cam, world, out p);
                }
                l.Group.alpha = Mathf.MoveTowards(l.Group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
                if (!show) continue;

                // Abre las etiquetas hacia afuera del centro del modelo, alternando por altura para evitar choques.
                var dir = p - center;
                if (dir.sqrMagnitude < 4f) dir = new Vector2(i % 2 == 0 ? 1 : -1, 0.3f);
                dir.Normalize();
                float side = Mathf.Sign(dir.x == 0 ? 1 : dir.x);
                var elbow = p + dir * (46f + (i % 3) * 14f);
                var end = new Vector2(elbow.x + side * 18f, elbow.y);

                l.Dot.anchoredPosition = p;
                SetLine(l.Line, p, end);
                l.Chip.pivot = new Vector2(side > 0 ? 0f : 1f, 0.5f);
                l.Chip.anchoredPosition = end + new Vector2(side * 4f, 0);
            }
        }

        static void SetLine(RectTransform line, Vector2 a, Vector2 b)
        {
            var d = b - a;
            line.anchoredPosition = a;
            line.sizeDelta = new Vector2(d.magnitude, 1.5f);
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        bool ToLocal(Camera cam, Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0) return false;
            var uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, sp, uiCam, out local);
        }
    }
}
