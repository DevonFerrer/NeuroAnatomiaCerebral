using System.Text.RegularExpressions;
using UnityEngine;

namespace ProyectoCorales
{
    /// <summary>
    /// Marca una pieza del modelo. Los nodos del GLB se llaman "ed_talamo", "ei_cerebelo.001", etc.
    /// ed = lado derecho, ei = lado izquierdo.
    /// </summary>
    public class AnatomyPart : MonoBehaviour
    {
        public string structureId;
        public bool rightSide;
        public string nodeName;

        [System.NonSerialized] public Renderer[] renderers;
        [System.NonSerialized] public Material[][] originals;
        [System.NonSerialized] public Material[][] transparent;
        [System.NonSerialized] public Material[][] highlight;

        static readonly Regex Suffix = new Regex(@"\.\d+$");

        /// <summary>Busca hacia arriba el nodo con prefijo ed_/ei_ y devuelve (id, lado).</summary>
        public static bool TryResolve(Transform t, Transform stopAt, out string id, out bool right, out string node)
        {
            for (var cur = t; cur != null && cur != stopAt; cur = cur.parent)
            {
                var n = cur.name.ToLowerInvariant();
                if (n.StartsWith("ed_") || n.StartsWith("ei_"))
                {
                    right = n.StartsWith("ed_");
                    id = Suffix.Replace(n.Substring(3), "");
                    node = cur.name;
                    return true;
                }
            }
            id = null;
            right = false;
            node = null;
            return false;
        }
    }
}
