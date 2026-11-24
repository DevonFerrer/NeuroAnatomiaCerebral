using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
#if AR_FOUNDATION
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif
#if XR_MANAGEMENT
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
#endif

namespace ProyectoCorales.EditorTools
{
    /// <summary>
    /// Configura automáticamente el proyecto la primera vez que se abre:
    ///  - prefab interactivo del cerebro (colisionadores + metadatos por estructura)
    ///  - materiales (transparente, resaltado, planos RA)
    ///  - escena AnatomiaRA con App, cámaras, AR Session y XR Origin
    ///  - Player Settings para Android (IL2CPP, ARM64, vertical, OpenGLES3)
    ///  - XR Plug-in Management con ARCore y AR Background Renderer Feature en URP
    /// Se puede volver a ejecutar desde el menú ProyectoCorales.
    /// </summary>
    [InitializeOnLoad]
    public static class ProyectoCoralesSetup
    {
        const string Root = "Assets/ProyectoCorales";
        const string GlbPath = Root + "/Modelos/Cerebro.glb";
        const string PrefabPath = Root + "/Modelos/CerebroInteractivo.prefab";
        const string PlanePrefabPath = Root + "/Modelos/PlanoRA.prefab";
        const string MatDir = Root + "/Materiales";
        const string SceneDir = Root + "/Escenas";
        const string ScenePath = SceneDir + "/AnatomiaRA.unity";
        const string Marker = "ProjectSettings/ProyectoCorales.setup";

        static int _tries;

        static ProyectoCoralesSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (File.Exists(Marker)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Retry();
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath) == null)
            {
                if (_tries++ < 40) Retry();
                else Debug.LogWarning("[ProyectoCorales] El modelo Cerebro.glb aún no se importa. Verifica que el paquete glTFast esté instalado y usa ProyectoCorales ▸ Configurar proyecto.");
                return;
            }
            Run(false);
        }

        static void Retry()
        {
            double at = EditorApplication.timeSinceStartup + 3.0;
            EditorApplication.CallbackFunction cb = null;
            cb = () =>
            {
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= cb;
                AutoSetup();
            };
            EditorApplication.update += cb;
        }

        [MenuItem("ProyectoCorales/Configurar proyecto (Android + RA)", priority = 0)]
        public static void MenuSetup() => Run(true);

        [MenuItem("ProyectoCorales/Abrir escena principal", priority = 1)]
        public static void MenuOpenScene()
        {
            if (File.Exists(ScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        static void Run(bool interactive)
        {
            try
            {
                if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

                EnsureFolder(MatDir);
                EnsureFolder(SceneDir);

                var transparent = MakeMat("M_Transparente", "Universal Render Pipeline/Lit", m =>
                {
                    MakeTransparent(m);
                    m.SetColor("_BaseColor", new Color(1, 1, 1, 0.5f));
                    m.SetFloat("_Smoothness", 0.45f);
                });
                var highlight = MakeMat("M_Resaltado", "Universal Render Pipeline/Lit", m =>
                {
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_Smoothness", 0.5f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.91f, 0.09f, 0.23f) * 0.4f);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                });
                var planeMat = MakeMat("M_PlanoRA", "Universal Render Pipeline/Unlit", m =>
                {
                    MakeTransparent(m);
                    m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.10f));
                });
                var lineMat = MakeMat("M_Linea", "Universal Render Pipeline/Particles/Unlit", m =>
                {
                    m.SetColor("_BaseColor", Color.white);
                });
                AssetDatabase.SaveAssets();

                var model = BuildModelPrefab();
                if (model == null)
                {
                    const string msg = "No se pudo importar Assets/ProyectoCorales/Modelos/Cerebro.glb.\n\nInstala el paquete glTFast (com.unity.cloud.gltfast) desde Package Manager y vuelve a ejecutar ProyectoCorales ▸ Configurar proyecto.";
                    if (interactive) EditorUtility.DisplayDialog("Anatomía RA", msg, "OK");
                    else Debug.LogWarning("[ProyectoCorales] " + msg);
                    return;
                }
                var planePrefab = BuildPlanePrefab(planeMat, lineMat);

                ConfigurePlayer();
                ConfigureXR();
                AddARBackgroundFeature();
                BuildScene(model, transparent, highlight, lineMat, planePrefab);

                File.WriteAllText(Marker, DateTime.Now.ToString("s"));
                Debug.Log("[ProyectoCorales] Proyecto configurado. Escena: " + ScenePath);
                if (interactive)
                    EditorUtility.DisplayDialog("Anatomía RA",
                        "Listo. Se creó la escena AnatomiaRA y se configuró Android + ARCore.\n\nPulsa Play para probar la vista 3D en el editor, o cambia la plataforma a Android en Build Profiles para compilar.",
                        "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ------------------------------------------------------------------ utilidades
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static Material MakeMat(string name, string shaderName, Action<Material> setup)
        {
            string path = MatDir + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        // ------------------------------------------------------------------ modelo
        static GameObject BuildModelPrefab()
        {
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (glb == null) return null;

            var inst = (GameObject)UnityEngine.Object.Instantiate(glb);
            inst.name = "CerebroInteractivo";
            try
            {
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    if (!AnatomyPart.TryResolve(r.transform, null, out var id, out var right, out var node))
                    {
                        id = "otros";
                        right = true;
                        node = r.name;
                    }
                    var part = GetOrAdd<AnatomyPart>(r.gameObject);
                    part.structureId = id;
                    part.rightSide = right;
                    part.nodeName = node;

                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && r.GetComponent<Collider>() == null)
                        r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                return PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        static GameObject BuildPlanePrefab(Material planeMat, Material lineMat)
        {
            var go = new GameObject("PlanoRA");
            try
            {
#if AR_FOUNDATION
                GetOrAdd<ARPlane>(go);
                GetOrAdd<ARPlaneMeshVisualizer>(go);
#endif
                GetOrAdd<MeshFilter>(go);
                var mr = GetOrAdd<MeshRenderer>(go);
                mr.sharedMaterial = planeMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                var lr = GetOrAdd<LineRenderer>(go);
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.widthMultiplier = 0.004f;
                lr.positionCount = 0;
                lr.sharedMaterial = lineMat;
                lr.startColor = lr.endColor = new Color(1f, 1f, 1f, 0.8f);
                return PrefabUtility.SaveAsPrefabAsset(go, PlanePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------ player / XR / URP
        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Anatomía RA";
            PlayerSettings.companyName = "InnoMed";
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.innomed.anatomiara");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.bakeCollisionMeshes = true;
        }

        static void ConfigureXR()
        {
#if XR_MANAGEMENT
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                EnsureFolder("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null)
            {
                general = ScriptableObject.CreateInstance<XRGeneralSettings>();
                general.name = "Android Settings";
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
                AssetDatabase.AddObjectToAsset(general, perTarget);
            }

            var so = new SerializedObject(general);
            var mgrProp = so.FindProperty("m_Manager") ?? so.FindProperty("m_LoaderManagerInstance");
            var manager = mgrProp != null ? mgrProp.objectReferenceValue as XRManagerSettings : null;
            if (manager == null)
            {
                manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = "Android Providers";
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                if (mgrProp != null) mgrProp.objectReferenceValue = manager;
            }
            var initProp = so.FindProperty("m_InitManagerOnStart");
            if (initProp != null) initProp.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool ok = XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android);
            if (!ok) Debug.LogWarning("[ProyectoCorales] No se pudo asignar ARCore automáticamente. Actívalo en Project Settings ▸ XR Plug-in Management ▸ Android ▸ Google ARCore.");

            EditorUtility.SetDirty(perTarget);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
#else
            Debug.LogWarning("[ProyectoCorales] XR Plug-in Management no está instalado; la app funcionará solo en modo 3D.");
#endif
        }

        static void AddARBackgroundFeature()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == "ARBackgroundRendererFeature" && typeof(ScriptableObject).IsAssignableFrom(t) && !t.IsAbstract);
            if (type == null) return;

            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (data == null) continue;

                var so = new SerializedObject(data);
                var feats = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                if (feats == null) continue;

                bool has = false;
                for (int i = 0; i < feats.arraySize; i++)
                {
                    var o = feats.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (o != null && o.GetType() == type) has = true;
                }
                if (has) continue;

                var feature = ScriptableObject.CreateInstance(type);
                feature.name = "ARBackgroundRendererFeature";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

                so.Update();
                int n = feats.arraySize;
                feats.InsertArrayElementAtIndex(n);
                feats.GetArrayElementAtIndex(n).objectReferenceValue = feature;
                if (map != null)
                {
                    int m = map.arraySize;
                    map.InsertArrayElementAtIndex(m);
                    map.GetArrayElementAtIndex(m).longValue = localId;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                Debug.Log("[ProyectoCorales] AR Background Renderer Feature añadido a " + path);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ escena
        static void BuildScene(GameObject model, Material transparent, Material highlight, Material line, GameObject planePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);

            var light = new GameObject("Luz direccional").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            var fill = new GameObject("Luz de relleno").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(-20f, 150f, 0f);

            var viewerGo = new GameObject("Vista3D Camera");
            var viewer = viewerGo.AddComponent<Camera>();
            viewerGo.AddComponent<AudioListener>();
            viewer.clearFlags = CameraClearFlags.SolidColor;
            viewer.backgroundColor = new Color(0.04f, 0.055f, 0.08f);
            viewer.fieldOfView = 30f;
            viewer.transform.position = new Vector3(0, 0, -2.6f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var appGo = new GameObject("App");
            var app = appGo.AddComponent<AppController>();
            app.modelPrefab = model;
            app.transparentMaterial = transparent;
            app.highlightMaterial = highlight;
            app.lineMaterial = line;
            app.viewerCamera = viewer;

#if AR_FOUNDATION
            var sessionGo = new GameObject("AR Session");
            var session = sessionGo.AddComponent<ARSession>();
            sessionGo.AddComponent<ARInputManager>();
            session.enabled = false;

            var originGo = new GameObject("XR Origin (RA)");
            var origin = originGo.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);

            var camGo = new GameObject("AR Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset.transform, false);
            var arCam = camGo.AddComponent<Camera>();
            arCam.clearFlags = CameraClearFlags.SolidColor;
            arCam.backgroundColor = Color.black;
            arCam.nearClipPlane = 0.05f;
            arCam.farClipPlane = 40f;
            GetOrAdd<ARCameraManager>(camGo);
            GetOrAdd<ARCameraBackground>(camGo);
            camGo.AddComponent<AudioListener>();

            var tpd = camGo.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            var pos = new InputAction("Posicion", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            pos.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rot = new InputAction("Rotacion", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rot.AddBinding("<HandheldARInputDevice>/deviceRotation");
            tpd.positionInput = new InputActionProperty(pos);
            tpd.rotationInput = new InputActionProperty(rot);

            origin.Camera = arCam;
            origin.CameraFloorOffsetObject = offset;

            var planes = originGo.AddComponent<ARPlaneManager>();
            planes.planePrefab = planePrefab;
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planes.enabled = false;
            var raycaster = originGo.AddComponent<ARRaycastManager>();
            originGo.SetActive(false);

            var bridge = appGo.AddComponent<ARBridge>();
            bridge.session = session;
            bridge.origin = origin;
            bridge.planeManager = planes;
            bridge.raycastManager = raycaster;
            bridge.arCamera = arCam;
            app.ar = bridge;
#else
            app.ar = appGo.AddComponent<ARBridge>();
#endif

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
