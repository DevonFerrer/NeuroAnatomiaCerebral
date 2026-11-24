using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProyectoCorales
{
    /// <summary>Paleta y tipografía de la app (basada en el prototipo de Figma, versión oscura moderna).</summary>
    public static class Theme
    {
        public static readonly Color Bg = Hex("#0A0E15");
        public static readonly Color Surface = Hex("#131A25");
        public static readonly Color Surface2 = Hex("#1C2533");
        public static readonly Color Stroke = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color Accent = Hex("#E8173A");
        public static readonly Color AccentSoft = new Color(0.91f, 0.09f, 0.23f, 0.16f);
        public static readonly Color Text = Hex("#F3F5F9");
        public static readonly Color TextDim = Hex("#A3ADBD");
        public static readonly Color TextFaint = Hex("#6B7585");
        public static readonly Color Glass = new Color(0.07f, 0.09f, 0.13f, 0.78f);

        static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }
    }

    public enum BtnStyle { Primary, Secondary, Ghost, Danger }

    /// <summary>Fábrica de UI procedimental (uGUI) — sin assets externos.</summary>
    public static class UIKit
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // ------------------------------------------------------------------ sprites
        /// <summary>Rectángulo redondeado 9-slice. radius en unidades de UI.</summary>
        public static Sprite Rounded(int radius, float feather = 1f)
        {
            string key = "r" + radius + "_" + feather;
            if (Cache.TryGetValue(key, out var s)) return s;
            int r = Mathf.Max(2, radius * 2); // doble resolución para bordes nítidos
            float soft = feather * 2f;
            int size = r * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = key
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, r, size - r);
                float cy = Mathf.Clamp(fy, r, size - r);
                float d = Vector2.Distance(new Vector2(fx, fy), new Vector2(cx, cy));
                float a = soft <= 2f
                    ? Mathf.Clamp01(r - d + 0.5f)
                    : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - d) / soft));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            s.name = key;
            Cache[key] = s;
            return s;
        }

        /// <summary>Gradiente radial (centro opaco → borde transparente) para brillos.</summary>
        public static Sprite RadialGlow()
        {
            const string key = "glow";
            if (Cache.TryGetValue(key, out var s)) return s;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = s;
            return s;
        }

        /// <summary>Degradado vertical (transparente arriba → opaco abajo).</summary>
        public static Sprite VerticalFade()
        {
            const string key = "vfade";
            if (Cache.TryGetValue(key, out var s)) return s;
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.SmoothStep(1f, 0f, y / (h - 1f));
                tex.SetPixel(0, y, new Color(1, 1, 1, a));
            }
            tex.Apply(false, true);
            s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = s;
            return s;
        }

        // ------------------------------------------------------------------ rect helpers
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Ancla a la parte superior con altura fija.</summary>
        public static RectTransform Top(this RectTransform rt, float height, float y = 0, float side = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(side, -y - height);
            rt.offsetMax = new Vector2(-side, -y);
            return rt;
        }

        /// <summary>Ancla a la parte inferior con altura fija.</summary>
        public static RectTransform Bottom(this RectTransform rt, float height, float y = 0, float side = 0)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(side, y);
            rt.offsetMax = new Vector2(-side, y + height);
            return rt;
        }

        public static RectTransform Center(this RectTransform rt, Vector2 size, Vector2 offset = default)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        public static T Pref<T>(this T c, float height = -1, float width = -1, float flexW = -1, float flexH = -1) where T : Component
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (height >= 0) { le.preferredHeight = height; le.minHeight = height; }
            if (width >= 0) { le.preferredWidth = width; le.minWidth = width; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return c;
        }

        // ------------------------------------------------------------------ primitives
        public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 2f;
            }
            return img;
        }

        public static Image Card(Transform parent, string name, int radius = 22, Color? color = null, bool raycast = true)
        {
            return Img(parent, name, Rounded(radius), color ?? Theme.Surface, raycast);
        }

        public static Text Txt(Transform parent, string text, int size, Color color,
            FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft, string name = "Texto")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Theme.Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.12f;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, RectOffset pad = null,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = pad ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, RectOffset pad = null,
            TextAnchor align = TextAnchor.MiddleLeft, bool expandWidth = true)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = pad ?? new RectOffset(0, 0, 0, 0);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = true;
            return h;
        }

        public static ContentSizeFitter FitHeight(GameObject go)
        {
            var f = go.GetComponent<ContentSizeFitter>();
            if (f == null) f = go.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return f;
        }

        public static RectOffset Pad(int all) => new RectOffset(all, all, all, all);
        public static RectOffset Pad(int h, int v) => new RectOffset(h, h, v, v);

        // ------------------------------------------------------------------ controls
        public static Button Btn(Transform parent, string label, BtnStyle style, Action onClick, float height = 54, int fontSize = 17)
        {
            Color bg, fg;
            switch (style)
            {
                case BtnStyle.Primary: bg = Theme.Accent; fg = Color.white; break;
                case BtnStyle.Secondary: bg = Theme.Surface2; fg = Theme.Text; break;
                case BtnStyle.Danger: bg = Theme.AccentSoft; fg = Theme.Accent; break;
                default: bg = new Color(1, 1, 1, 0.0f); fg = Theme.TextDim; break;
            }

            // contenedor (sin gráfico) → brillo detrás → fondo → texto
            var root = Rect("Btn_" + label, parent);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            root.GetComponent<LayoutElement>().minHeight = height;

            if (style == BtnStyle.Primary)
            {
                var glow = Img(root, "Brillo", Rounded(22, 12), Theme.Accent.WithAlpha(0.45f));
                glow.rectTransform.Stretch(-4, -4, 6, -12);
            }
            else if (style == BtnStyle.Secondary)
            {
                var o = Img(root, "Contorno", Rounded(18), new Color(1, 1, 1, 0.09f));
                o.rectTransform.Stretch(-1, -1, -1, -1);
            }

            var img = Img(root, "Fondo", Rounded(18), bg, true);
            img.rectTransform.Stretch();
            if (style == BtnStyle.Ghost) img.color = new Color(1, 1, 1, 0.001f); // sigue recibiendo toques

            var t = Txt(img.transform, label, fontSize, fg, FontStyle.Bold, TextAnchor.MiddleCenter, "Etiqueta");
            t.rectTransform.Stretch(12, 12, 0, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;

            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) b.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressScale>();
            return b;
        }

        /// <summary>Contenedor del botón (para ocultar/mostrar o reordenar en layouts).</summary>
        public static GameObject Root(this Button b) => b.transform.parent.gameObject;

        public static void SetLabel(this Button b, string label)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        public static Button IconBtn(Transform parent, string glyph, Action onClick, float size = 40, int fontSize = 20, Color? bg = null)
        {
            var img = Img(parent, "Icono_" + glyph, Rounded(Mathf.RoundToInt(size / 2)), bg ?? Theme.Surface2, true);
            img.Pref(size, size);
            var t = Txt(img.transform, glyph, fontSize, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter, "Glifo");
            t.rectTransform.Stretch();
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) b.onClick.AddListener(() => onClick());
            img.gameObject.AddComponent<PressScale>();
            return b;
        }

        /// <summary>Pastilla/chip de texto.</summary>
        public static Image Chip(Transform parent, string text, Color bg, Color fg, int fontSize = 12)
        {
            var img = Img(parent, "Chip", Rounded(12), bg);
            var t = Txt(img.transform, text, fontSize, fg, FontStyle.Bold, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch(10, 10, 0, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var fitter = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            fitter.padding = new RectOffset(12, 12, 5, 5);
            fitter.childControlWidth = true;
            fitter.childControlHeight = true;
            fitter.childForceExpandWidth = false;
            var csf = img.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return img;
        }

        /// <summary>Slider moderno.</summary>
        public static Slider MakeSlider(Transform parent, float value, Action<float> onChange)
        {
            var root = Rect("Slider", parent);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

            var track = Img(root, "Pista", Rounded(4), Theme.Surface2);
            track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0, 8);

            var fillArea = Rect("ZonaRelleno", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-16, 8);
            var fill = Img(fillArea, "Relleno", Rounded(4), Theme.Accent);
            fill.rectTransform.sizeDelta = new Vector2(16, 0);

            var handleArea = Rect("ZonaManija", root);
            handleArea.Stretch(10, 10, 0, 0);
            var handle = Img(handleArea, "Manija", Rounded(12), Color.white, true);
            handle.rectTransform.sizeDelta = new Vector2(24, 24);
            var ring = Img(handle.transform, "Anillo", Rounded(6), Theme.Accent);
            ring.rectTransform.Center(new Vector2(10, 10));

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0;
            s.maxValue = 1;
            s.value = value;
            s.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onChange != null) s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        // ------------------------------------------------------------------ animation
        public static IEnumerator Fade(CanvasGroup g, float to, float dur)
        {
            float from = g.alpha;
            float t = 0;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                g.alpha = Mathf.Lerp(from, to, Ease(t / dur));
                yield return null;
            }
            g.alpha = to;
        }

        public static IEnumerator SlideY(RectTransform rt, float from, float to, float dur)
        {
            float t = 0;
            var p = rt.anchoredPosition;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                p.y = Mathf.Lerp(from, to, Ease(t / dur));
                rt.anchoredPosition = p;
                yield return null;
            }
            p.y = to;
            rt.anchoredPosition = p;
        }

        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }
    }

    /// <summary>Pequeña animación de pulsación para botones.</summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Vector3 _target = Vector3.one;

        public void OnPointerDown(PointerEventData e) => _target = Vector3.one * 0.96f;
        public void OnPointerUp(PointerEventData e) => _target = Vector3.one;
        public void OnPointerExit(PointerEventData e) => _target = Vector3.one;

        void OnDisable()
        {
            _target = Vector3.one;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 25f));
        }
    }

    /// <summary>Ajusta el contenido al área segura (notch, barra de gestos).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect _last;
        RectTransform _rt;

        void Awake() { _rt = (RectTransform)transform; Apply(); }
        void Update() { if (Screen.safeArea != _last) Apply(); }

        void Apply()
        {
            _last = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var min = _last.position;
            var max = _last.position + _last.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = _rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Rota suavemente un elemento (indicadores de carga).</summary>
    public class Spin : MonoBehaviour
    {
        public float speed = -180f;
        void Update() => transform.Rotate(0, 0, speed * Time.unscaledDeltaTime);
    }

    /// <summary>Pulso de escala/alpha (marco de escaneo).</summary>
    public class Pulse : MonoBehaviour
    {
        public float amount = 0.05f;
        public float speed = 2.2f;
        void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * speed) * amount;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
