using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProyectoCorales.EditorTools
{
    /// <summary>Compila el APK de Android sin necesidad de un celular conectado.</summary>
    public static class ProyectoCoralesBuild
    {
        const string Scene = "Assets/ProyectoCorales/Escenas/AnatomiaRA.unity";
        const string Output = "Builds/AnatomiaRA.apk";

        [MenuItem("ProyectoCorales/Compilar APK (Android)", priority = 20)]
        public static void BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    EditorUtility.DisplayDialog("Anatomía RA", "No se pudo cambiar a Android. Instala el módulo Android Build Support en Unity Hub.", "OK");
                    return;
                }
            }

            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                string full = Path.GetFullPath(Output);
                Debug.Log($"[ProyectoCorales] APK listo: {full} ({summary.totalSize / (1024 * 1024)} MB)");
                EditorUtility.RevealInFinder(full);
            }
            else
            {
                Debug.LogError($"[ProyectoCorales] La compilación falló: {summary.result} ({summary.totalErrors} errores). Revisa la consola.");
            }
        }
    }
}
