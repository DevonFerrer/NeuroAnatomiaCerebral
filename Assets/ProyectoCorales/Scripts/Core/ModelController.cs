using System.Collections.Generic;
using UnityEngine;

namespace ProyectoCorales
{
    /// <summary>
    /// Carga el modelo del encéfalo, lo normaliza (tamaño 1 unidad, centrado) y gestiona:
    /// capas visibles, transparencia, resaltado, aislamiento, rotación y zoom.
    /// Jerarquía: Root (posición/escala) → Spin (rotación del usuario) → Content (modelo centrado).
    /// </summary>
    public class ModelController : MonoBehaviour
    {
        public Transform Root { get; private set; }
        public Transform Spin { get; private set; }
        public Transform Content { get; private set; }
        public bool Loaded => Content != null;

        public float BaseScale = 1f;
        public float UserScale { get; private set; } = 1f;

        readonly List<AnatomyPart> _parts = new List<AnatomyPart>();
        readonly Dictionary<string, List<AnatomyPart>> _byId = new Dictionary<string, List<AnatomyPart>>();
        readonly HashSet<string> _hidden = new HashSet<string>();
        public readonly List<string> StructureIds = new List<string>();

        Material _transparentTpl, _highlightTpl;
        float _transparency;
        AnatomyPart _selected;
        string _isolated;
        Quaternion _initialSpin = Quaternion.identity;

        static readonly string[] ColorProps = { "baseColorFactor", "_BaseColor", "_Color" };
        static readonly string[] TexProps = { "baseColorTexture", "_BaseMap", "_MainTex" };

        // -------------------------------------------------------------- construcción
        public void Build(GameObject prefab, Material transparentTpl, Material highlightTpl)
        {
            _transparentTpl = transparentTpl;
            _highlightTpl = highlightTpl;

            Root = new GameObject("Modelo").transform;
            Root.SetParent(transform, false);
            Spin = new GameObject("Rotacion").transform;
            Spin.SetParent(Root, false);

            if (prefab == null)
            {
                Debug.LogError("[ProyectoCorales] No hay modelo asignado. Ejecuta ProyectoCorales ▸ Configurar proyecto.");
                return;
            }

            var inst = Instantiate(prefab, Spin, false);
            inst.name = "Cerebro";
            Content = inst.transform;

            // Normaliza: centra y escala para que la mayor dimensión mida 1 unidad.
            var b = WorldBounds(inst);
            float max = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float k = max > 1e-5f ? 1f / max : 1f;
            Content.localScale = Content.localScale * k;
            b = WorldBounds(inst);
            Content.position -= b.center - Spin.position;

            CollectParts(inst);
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        void CollectParts(GameObject inst)
        {
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var part = r.GetComponent<AnatomyPart>();
                if (part == null)
                {
                    if (!AnatomyPart.TryResolve(r.transform, inst.transform.parent, out var id, out var right, out var node))
                    {
                        id = "otros";
                        right = true;
                        node = r.name;
                    }
                    part = r.gameObject.AddComponent<AnatomyPart>();
                    part.structureId = id;
                    part.rightSide = right;
                    part.nodeName = node;
                }

                // Colisionador para poder tocar la estructura.
                if (r.GetComponent<Collider>() == null)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                        r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    else
                        r.gameObject.AddComponent<BoxCollider>();
                }

                part.renderers = new[] { r };
                part.originals = new[] { r.sharedMaterials };
                _parts.Add(part);

                if (!_byId.TryGetValue(part.structureId, out var list))
                {
                    list = new List<AnatomyPart>();
                    _byId[part.structureId] = list;
                }
                list.Add(part);
            }

            // Orden según el catálogo
            foreach (var s in StructureCatalog.All)
                if (_byId.ContainsKey(s.Id)) StructureIds.Add(s.Id);
            foreach (var id in _byId.Keys)
                if (!StructureIds.Contains(id)) StructureIds.Add(id);
        }

        // -------------------------------------------------------------- consulta
        public bool IsVisible(string id) => !_hidden.Contains(id) && (_isolated == null || _isolated == id);
        public string Isolated => _isolated;
        public float Transparency => _transparency;
        public AnatomyPart Selected => _selected;

        /// <summary>Punto representativo de una estructura en el mundo (para etiquetas).</summary>
        public bool TryGetAnchor(string id, out Vector3 world)
        {
            world = Vector3.zero;
            if (!_byId.TryGetValue(id, out var list) || list.Count == 0) return false;
            bool any = false;
            Bounds b = default;
            foreach (var p in list)
            {
                if (!p.rightSide && list.Count > 1) continue; // usa un lado para no duplicar
                foreach (var r in p.renderers)
                {
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
            }
            if (!any) return false;
            world = b.center;
            return true;
        }

        public Vector3 Center => Root != null ? Spin.position : Vector3.zero;

        public AnatomyPart Pick(Camera cam, Vector2 screen)
        {
            if (cam == null || !Loaded) return null;
            var ray = cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 100f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var p = h.collider.GetComponentInParent<AnatomyPart>();
                if (p == null || !p.transform.IsChildOf(Root)) continue;
                if (!IsVisible(p.structureId)) continue;
                // si el cerebro es casi transparente, deja "atravesarlo" para tocar lo de adentro
                if (p.structureId == "cerebro" && _transparency > 0.55f && hits.Length > 1) continue;
                return p;
            }
            return null;
        }

        // -------------------------------------------------------------- estado visual
        public void SetVisible(string id, bool visible)
        {
            if (visible) _hidden.Remove(id); else _hidden.Add(id);
            ApplyVisibility();
        }

        public void ShowAll()
        {
            _hidden.Clear();
            _isolated = null;
            ApplyVisibility();
        }

        public void Isolate(string id)
        {
            _isolated = id;
            ApplyVisibility();
        }

        void ApplyVisibility()
        {
            foreach (var p in _parts)
            {
                bool v = IsVisible(p.structureId);
                foreach (var r in p.renderers) r.enabled = v;
                var col = p.GetComponent<Collider>();
                if (col != null) col.enabled = v;
            }
        }

        /// <summary>0 = opaco, 1 = muy transparente. Afecta a los hemisferios cerebrales.</summary>
        public void SetTransparency(float t)
        {
            _transparency = Mathf.Clamp01(t);
            foreach (var p in _parts) RefreshMaterials(p);
        }

        public void Select(AnatomyPart part)
        {
            var prev = _selected;
            _selected = part;
            if (prev != null) foreach (var p in PartsOf(prev.structureId)) RefreshMaterials(p);
            if (part != null) foreach (var p in PartsOf(part.structureId)) RefreshMaterials(p);
        }

        public AnatomyPart FirstPart(string id)
        {
            if (!_byId.TryGetValue(id, out var list) || list.Count == 0) return null;
            foreach (var p in list) if (p.rightSide) return p;
            return list[0];
        }

        List<AnatomyPart> PartsOf(string id) =>
            _byId.TryGetValue(id, out var l) ? l : new List<AnatomyPart>();

        void RefreshMaterials(AnatomyPart p)
        {
            bool selected = _selected != null && _selected.structureId == p.structureId;
            bool fade = p.structureId == "cerebro" && _transparency > 0.01f && !selected;

            for (int i = 0; i < p.renderers.Length; i++)
            {
                var r = p.renderers[i];
                if (selected && _highlightTpl != null)
                {
                    if (p.highlight == null) p.highlight = new Material[p.renderers.Length][];
                    if (p.highlight[i] == null) p.highlight[i] = MakeCopies(p.originals[i], _highlightTpl, true);
                    r.sharedMaterials = p.highlight[i];
                }
                else if (fade && _transparentTpl != null)
                {
                    if (p.transparent == null) p.transparent = new Material[p.renderers.Length][];
                    if (p.transparent[i] == null) p.transparent[i] = MakeCopies(p.originals[i], _transparentTpl, false);
                    float alpha = Mathf.Lerp(1f, 0.12f, _transparency);
                    foreach (var m in p.transparent[i])
                    {
                        var c = m.GetColor("_BaseColor");
                        c.a = alpha;
                        m.SetColor("_BaseColor", c);
                    }
                    r.sharedMaterials = p.transparent[i];
                }
                else
                {
                    r.sharedMaterials = p.originals[i];
                }
            }
        }

        static Material[] MakeCopies(Material[] src, Material tpl, bool highlight)
        {
            var res = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var m = new Material(tpl);
                var s = src[i];
                Color col = Color.white;
                Texture tex = null;
                if (s != null)
                {
                    foreach (var p in ColorProps) if (s.HasProperty(p)) { col = s.GetColor(p); break; }
                    foreach (var p in TexProps) if (s.HasProperty(p)) { tex = s.GetTexture(p); if (tex != null) break; }
                }
                if (tex != null) m.SetTexture("_BaseMap", tex);
                if (highlight)
                {
                    col = Color.Lerp(col, Theme.Accent, 0.35f);
                    m.SetColor("_EmissionColor", Theme.Accent * 0.45f);
                    m.EnableKeyword("_EMISSION");
                }
                col.a = 1f;
                m.SetColor("_BaseColor", col);
                res[i] = m;
            }
            return res;
        }

        // -------------------------------------------------------------- transformaciones
        public void Place(Vector3 position, Quaternion rotation, float baseScale)
        {
            Root.SetPositionAndRotation(position, rotation);
            BaseScale = baseScale;
            ApplyScale();
        }

        public void Rotate(Vector2 dragPixels, Camera cam, bool allowPitch)
        {
            if (!Loaded) return;
            const float k = 0.28f;
            Spin.Rotate(Vector3.up, -dragPixels.x * k, Space.World);
            if (allowPitch && cam != null)
                Spin.Rotate(cam.transform.right, dragPixels.y * k, Space.World);
        }

        public void Zoom(float factor)
        {
            if (Mathf.Approximately(factor, 1f)) return;
            UserScale = Mathf.Clamp(UserScale * factor, 0.45f, 3f);
            ApplyScale();
        }

        public void AutoSpin(float degPerSec)
        {
            if (Loaded) Spin.Rotate(Vector3.up, degPerSec * Time.deltaTime, Space.World);
        }

        public void ResetView()
        {
            UserScale = 1f;
            Spin.localRotation = _initialSpin;
            ApplyScale();
        }

        void ApplyScale() => Root.localScale = Vector3.one * (BaseScale * UserScale);

        public void SetActive(bool active)
        {
            if (Root != null) Root.gameObject.SetActive(active);
        }
    }
}
