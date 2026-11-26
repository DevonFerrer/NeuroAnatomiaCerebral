using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ProyectoCorales.EditorTools
{
    /// <summary>
    /// Al abrir el proyecto (por ejemplo, recién clonado de GitHub) Unity muestra una escena vacía
    /// "Untitled". Este script abre automáticamente la escena principal de la app.
    /// </summary>
    [InitializeOnLoad]
    public static class AbrirEscenaPrincipal
    {
        const string ScenePath = "Assets/ProyectoCorales/Escenas/AnatomiaRA.unity";
        const string SessionKey = "ProyectoCorales.EscenaAbierta";

        static AbrirEscenaPrincipal()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += Abrir;
        }

        static void Abrir()
        {
            SessionState.SetBool(SessionKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var activa = SceneManager.GetActiveScene();
            bool vacia = string.IsNullOrEmpty(activa.path) || activa.path.EndsWith("SampleScene.unity");
            if (!vacia) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
