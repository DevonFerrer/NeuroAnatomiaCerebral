using System.Collections.Generic;
using UnityEngine;

namespace ProyectoCorales
{
    public class StructureInfo
    {
        public string Id;
        public string Name;
        public string Description;
        public string Relations;
        public Color Color;
    }

    /// <summary>Contenido anatómico de las estructuras del modelo de encéfalo (Cerebro.glb).</summary>
    public static class StructureCatalog
    {
        public const string ModelTitle = "Encéfalo";
        public const string ModelSubtitle = "Neuroanatomía · Sistema nervioso central";

        public static readonly List<StructureInfo> All = new List<StructureInfo>
        {
            new StructureInfo
            {
                Id = "cerebro", Name = "Cerebro", Color = Theme.Hex("#FF6B8A"),
                Description = "Es la porción más grande del encéfalo. Su corteza, organizada en los lóbulos frontal, parietal, temporal y occipital, integra la sensibilidad, el movimiento voluntario, el lenguaje, la memoria y el pensamiento.",
                Relations = "Ocupa las fosas craneales anterior y media. Los dos hemisferios están separados por la fisura longitudinal y unidos en profundidad por el cuerpo calloso; la tienda del cerebelo lo separa del cerebelo."
            },
            new StructureInfo
            {
                Id = "cerebelo", Name = "Cerebelo", Color = Theme.Hex("#FFB020"),
                Description = "Coordina el movimiento, el equilibrio y el tono muscular, y participa en el aprendizaje de habilidades motoras.",
                Relations = "Se ubica en la fosa craneal posterior, detrás del tronco encefálico, al que se une mediante los pedúnculos cerebelosos superior, medio e inferior."
            },
            new StructureInfo
            {
                Id = "tronco_encefalico", Name = "Tronco encefálico", Color = Theme.Hex("#4ADE80"),
                Description = "Formado por mesencéfalo, puente y bulbo raquídeo. Conecta el cerebro con la médula espinal y contiene núcleos de los nervios craneales y centros vitales de la respiración y del ritmo cardíaco.",
                Relations = "Desciende desde el diencéfalo hasta el foramen magno, donde continúa como médula espinal. Por detrás se relaciona con el cerebelo y el cuarto ventrículo."
            },
            new StructureInfo
            {
                Id = "talamo", Name = "Tálamo", Color = Theme.Hex("#38BDF8"),
                Description = "Masa ovoide de sustancia gris del diencéfalo. Es la estación de relevo de casi toda la información sensitiva hacia la corteza y participa en la atención y la conciencia.",
                Relations = "Forma la pared lateral del tercer ventrículo. Se sitúa por debajo del cuerpo calloso y del fórnix, y medial a la cápsula interna."
            },
            new StructureInfo
            {
                Id = "cuerpo_calloso", Name = "Cuerpo calloso", Color = Theme.Hex("#A78BFA"),
                Description = "Es la mayor comisura del encéfalo: un gran haz de fibras blancas que comunica ambos hemisferios cerebrales para que compartan información.",
                Relations = "Se encuentra en el fondo de la fisura longitudinal y forma el techo de los ventrículos laterales. Se divide en rostro, rodilla, tronco y esplenio."
            },
            new StructureInfo
            {
                Id = "fornix", Name = "Fórnix", Color = Theme.Hex("#2DD4BF"),
                Description = "Haz de fibras en forma de arco que lleva información del hipocampo a los cuerpos mamilares. Forma parte del circuito de Papez, relacionado con la memoria.",
                Relations = "Se curva por debajo del cuerpo calloso y por encima del tálamo y del tercer ventrículo; sus columnas descienden por delante de la comisura anterior."
            },
            new StructureInfo
            {
                Id = "comisura_anterior", Name = "Comisura anterior", Color = Theme.Hex("#E879F9"),
                Description = "Pequeño haz de fibras que cruza la línea media y conecta los lóbulos temporales y las estructuras olfatorias de ambos hemisferios.",
                Relations = "Se sitúa en la lámina terminal, en la pared anterior del tercer ventrículo, por delante de las columnas del fórnix."
            },
            new StructureInfo
            {
                Id = "glandula_pituitaria", Name = "Glándula pituitaria", Color = Theme.Hex("#FDE047"),
                Description = "También llamada hipófisis. Es la glándula endocrina «maestra»: regula el crecimiento, la tiroides, las glándulas suprarrenales y la reproducción mediante sus hormonas.",
                Relations = "Se aloja en la silla turca del hueso esfenoides y se une al hipotálamo por el tallo hipofisario, por debajo del quiasma óptico."
            },
            new StructureInfo
            {
                Id = "quiasmaoptico", Name = "Quiasma óptico", Color = Theme.Hex("#F97316"),
                Description = "Punto donde se cruzan parcialmente las fibras de los nervios ópticos: las que vienen de la mitad nasal de cada retina pasan al lado opuesto.",
                Relations = "Está en la base del encéfalo, por delante del tallo hipofisario y por encima de la hipófisis. Hacia atrás se continúa con los tractos ópticos."
            },
        };

        static Dictionary<string, StructureInfo> _map;

        public static StructureInfo Get(string id)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, StructureInfo>();
                foreach (var s in All) _map[s.Id] = s;
            }
            if (id != null && _map.TryGetValue(id, out var info)) return info;
            return new StructureInfo
            {
                Id = id, Name = Prettify(id), Color = Color.white,
                Description = "Estructura del modelo anatómico.", Relations = "—"
            };
        }

        static string Prettify(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Estructura";
            var s = id.Replace('_', ' ');
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        public static string SideName(bool right) => right ? "Lado derecho" : "Lado izquierdo";
    }
}
