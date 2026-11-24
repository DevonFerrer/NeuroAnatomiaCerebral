using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ProyectoCorales
{
    /// <summary>
    /// Controlador principal de Anatomía RA: construye toda la interfaz (inspirada en el prototipo de Figma),
    /// maneja la navegación entre pantallas, el flujo de RA (permiso → escaneo → colocación) y la vista 3D.
    /// </summary>
    public class AppController : MonoBehaviour
    {
        [Header("Asignado por: ProyectoCorales ▸ Configurar proyecto")]
        public GameObject modelPrefab;
        public Material transparentMaterial;
        public Material highlightMaterial;
        public Material lineMaterial;
        public Camera viewerCamera;
        public ARBridge ar;

        [Header("Ajustes")]
        [Tooltip("Tamaño real del modelo en RA (metros).")]
        public float arModelSize = 0.22f;
        [Tooltip("Segundos sin detectar superficie antes de mostrar el error.")]
        public float scanTimeout = 25f;

        enum Page { Splash, Home, Instructions, Scanning, Viewer, HowTo, About, CameraDenied }

        // ----------------------------------------------------------------- estado
        readonly Dictionary<Page, CanvasGroup> _pages = new Dictionary<Page, CanvasGroup>();
        Page _current = Page.Splash;
        Page _howToReturn = Page.Home;
        bool _arMode;
        bool _placed;
        bool _labelsOn = true;
        float _scanStart;
        bool _busy;

        ModelController _model;
        GestureInput _gestures;
        LabelOverlay _labels;
        Transform _reticle;
        Canvas _canvas;
        RectTransform _overlayRoot;
        readonly Queue<Action> _mainThread = new Queue<Action>();

        // ----------------------------------------------------------------- referencias UI
        Image _splashFill;
        Text _scanStatus, _scanTitle, _scanSub, _modeChipText;
        GameObject _placeBtn, _scanError, _scanFrame, _hint, _viewerBottom;
        Button _permBtn, _btnLayers, _btnLabels, _btnIsolate;
        Text _toastText;
        CanvasGroup _toast;
        Coroutine _toastCo;

        Sheet _layersSheet, _infoSheet, _listSheet;
        Text _infoTitle, _infoSide, _infoDesc, _infoRel;
        Image _infoDot;
        Slider _transparency;
        Text _transparencyValue;
        readonly Dictionary<string, Image> _layerBoxes = new Dictionary<string, Image>();
        readonly Dictionary<string, GameObject> _layerTicks = new Dictionary<string, GameObject>();

        class Sheet
        {
            public RectTransform Rt;
            public bool Open;
            public Coroutine Anim;
        }

        // ================================================================= ciclo de vida
        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void Start()
        {
            EnsureEventSystem();
            EnsureViewerCamera();

            _model = gameObject.AddComponent<ModelController>();
            _model.Build(modelPrefab, transparentMaterial, highlightMaterial);
            _gestures = new GestureInput();

            BuildBackground();
            BuildUI();
            BuildReticle();

            Go(Page.Splash);
            StartCoroutine(SplashSequence());
        }

        void Update()
        {
            lock (_mainThread)
                while (_mainThread.Count > 0) _mainThread.Dequeue()?.Invoke();

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();

            _gestures.Update();

            switch (_current)
            {
                case Page.Splash:
                case Page.Home:
                    _model.AutoSpin(16f);
                    break;
                case Page.Scanning:
                    UpdateScanning();
                    break;
                case Page.Viewer:
                    UpdateViewer();
                    break;
            }
        }

        Camera ActiveCamera => _arMode && ar != null && ar.Running ? ar.Camera : viewerCamera;

        // ================================================================= escena
        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void EnsureViewerCamera()
        {
            if (viewerCamera == null)
            {
                var go = new GameObject("Vista3D Camera");
                viewerCamera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            viewerCamera.clearFlags = CameraClearFlags.SolidColor;
            viewerCamera.backgroundColor = Theme.Bg;
            viewerCamera.fieldOfView = 30f;
            viewerCamera.nearClipPlane = 0.05f;
            viewerCamera.farClipPlane = 60f;
            viewerCamera.transform.SetPositionAndRotation(new Vector3(0, 0, -2.6f), Quaternion.identity);

            if (FindAnyObjectByType<Light>() == null)
            {
                var l = new GameObject("Luz").AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.1f;
                l.transform.rotation = Quaternion.Euler(40, -30, 0);
            }
        }

        void BuildReticle()
        {
            var go = new GameObject("Reticula");
            _reticle = go.transform;
            AddRing(go.transform, 0.07f, 0.004f);
            AddRing(go.transform, 0.012f, 0.006f);
            go.SetActive(false);
        }

        void AddRing(Transform parent, float radius, float width)
        {
            var go = new GameObject("Anillo");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.widthMultiplier = width;
            lr.numCornerVertices = 2;
            const int n = 64;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.002f, Mathf.Sin(a) * radius));
            }
            if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
            lr.startColor = lr.endColor = Theme.Accent;
        }

        /// <summary>Fondo degradado detrás del modelo (canvas en espacio de cámara, solo para la vista 3D).</summary>
        void BuildBackground()
        {
            var go = new GameObject("Fondo3D", typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = viewerCamera;
            c.planeDistance = 40f;
            c.sortingOrder = -100;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(390, 844);
            sc.matchWidthOrHeight = 0.5f;

            var bg = UIKit.Img(go.transform, "Base", null, Theme.Bg);
            bg.rectTransform.Stretch();
            var g1 = UIKit.Img(go.transform, "BrilloRojo", UIKit.RadialGlow(), Theme.Accent.WithAlpha(0.22f));
            g1.rectTransform.Center(new Vector2(720, 720), new Vector2(0, 40));
            var g2 = UIKit.Img(go.transform, "BrilloAzul", UIKit.RadialGlow(), Theme.Hex("#3B82F6").WithAlpha(0.10f));
            g2.rectTransform.Center(new Vector2(640, 640), new Vector2(-220, -360));
        }

        // ================================================================= navegación
        void Go(Page page)
        {
            var prev = _current;
            _current = page;
            foreach (var kv in _pages)
            {
                bool on = kv.Key == page;
                if (on && !kv.Value.gameObject.activeSelf)
                {
                    kv.Value.gameObject.SetActive(true);
                    kv.Value.alpha = 0f;
                    StartCoroutine(UIKit.Fade(kv.Value, 1f, 0.22f));
                }
                else if (!on) kv.Value.gameObject.SetActive(false);
            }
            OnEnter(page, prev);
        }

        void OnEnter(Page page, Page prev)
        {
            bool viewer3D = page != Page.Scanning && !(page == Page.Viewer && _arMode);
            if (viewer3D && ar != null && ar.Running) ar.End();
            viewerCamera.gameObject.SetActive(viewer3D);
            _reticle.gameObject.SetActive(false);
            _labels.gameObject.SetActive(page == Page.Viewer && _labelsOn);

            switch (page)
            {
                case Page.Splash:
                case Page.Home:
                    _model.SetActive(true);
                    _model.ResetView();
                    _model.ShowAll();
                    _model.Select(null);
                    _model.SetTransparency(0f);
                    _model.Place(new Vector3(0, page == Page.Home ? 0.02f : 0.05f, 0), Quaternion.Euler(0, 200, 0), 0.62f);
                    break;

                case Page.Scanning:
                    _placed = false;
                    _model.SetActive(false);
                    _scanStart = Time.time;
                    _scanError.SetActive(false);
                    _scanFrame.SetActive(true);
                    _placeBtn.SetActive(false);
                    ar.Begin();
                    break;

                case Page.Viewer:
                    CloseAllSheets(true);
                    _modeChipText.text = _arMode ? "RA" : "3D";
                    if (!_arMode)
                    {
                        _model.SetActive(true);
                        _model.ResetView();
                        _model.Place(new Vector3(0, 0.12f, 0), Quaternion.Euler(0, 200, 0), 0.78f);
                    }
                    ShowHint();
                    SetToggle(_btnLabels, _labelsOn);
                    SetToggle(_btnLayers, false);
                    break;
            }
        }

        void Back()
        {
            switch (_current)
            {
                case Page.Home:
                    Application.Quit();
                    break;
                case Page.Instructions:
                case Page.CameraDenied:
                    Go(Page.Home);
                    break;
                case Page.Scanning:
                    if (_scanError.activeSelf) _scanError.SetActive(false);
                    else Go(Page.Home);
                    break;
                case Page.Viewer:
                    if (AnySheetOpen()) CloseAllSheets(false);
                    else Go(Page.Home);
                    break;
                case Page.HowTo:
                    Go(_howToReturn);
                    break;
                case Page.About:
                    Go(Page.Home);
                    break;
            }
        }

        IEnumerator SplashSequence()
        {
            float t = 0;
            const float dur = 2.0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _splashFill.rectTransform.anchorMax = new Vector2(UIKit.Ease(t / dur), 1);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.15f);
            Go(Page.Home);
        }

        // ================================================================= flujo RA
        void StartExperience()
        {
            if (_busy) return;
            _busy = true;
            _permBtn.SetLabel("Comprobando…");
            RequestCamera(granted =>
            {
                if (!granted)
                {
                    _busy = false;
                    _permBtn.SetLabel("Permitir cámara");
                    Go(Page.CameraDenied);
                    return;
                }
                if (ar == null)
                {
                    FinishCheck(false);
                    return;
                }
                StartCoroutine(ar.CheckAvailability(FinishCheck));
            });
        }

        void FinishCheck(bool arOk)
        {
            _busy = false;
            _permBtn.SetLabel("Permitir cámara");
            _arMode = arOk;
            if (arOk) Go(Page.Scanning);
            else
            {
                Go(Page.Viewer);
                Toast("Este dispositivo no admite RA. Te mostramos el modelo en 3D.");
            }
        }

        void Open3D()
        {
            _arMode = false;
            Go(Page.Viewer);
        }

        void RequestCamera(Action<bool> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Camera)) { done(true); return; }
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += _ => Enqueue(() => done(true));
            cb.PermissionDenied += _ => Enqueue(() => done(false));
            Permission.RequestUserPermission(Permission.Camera, cb);
#else
            done(true);
#endif
        }

        void Enqueue(Action a)
        {
            lock (_mainThread) _mainThread.Enqueue(a);
        }

        void OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                {
                    string pkg = activity.Call<string>("getPackageName");
                    using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + pkg))
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri))
                    {
                        intent.Call<AndroidJavaObject>("addFlags", 0x10000000);
                        activity.Call("startActivity", intent);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("No se pudo abrir ajustes: " + e.Message);
            }
#else
            Toast("Abre los ajustes del dispositivo y habilita la cámara.");
#endif
        }

        void UpdateScanning()
        {
            if (_scanError.activeSelf) return;

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool hit = ar.TryRaycast(center, out var pose);
            _reticle.gameObject.SetActive(hit);
            if (hit)
            {
                _reticle.SetPositionAndRotation(pose.position, pose.rotation);
                _scanFrame.SetActive(false);
            }
            else _scanFrame.SetActive(true);

            _placeBtn.SetActive(hit);

            if (!ar.Tracking)
            {
                _scanStatus.text = "Iniciando cámara…";
            }
            else if (ar.PlaneCount == 0)
            {
                _scanStatus.text = "Detectando superficie…";
                _scanTitle.text = "Mueve el celular lentamente";
                _scanSub.text = "Busca una zona plana y bien iluminada";
            }
            else if (!hit)
            {
                _scanStatus.text = "Superficie encontrada";
                _scanTitle.text = "Apunta al área detectada";
                _scanSub.text = "Centra la retícula sobre la superficie";
            }
            else
            {
                _scanStatus.text = "Listo para colocar";
                _scanTitle.text = "Toca para colocar el modelo";
                _scanSub.text = "Podrás rotarlo, ampliarlo y explorar sus estructuras";
            }

            if (_gestures.Tap && hit)
            {
                PlaceModel(pose);
                return;
            }

            if (ar.PlaneCount == 0 && Time.time - _scanStart > scanTimeout)
                _scanError.SetActive(true);
        }

        void PlaceCenter()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (ar.TryRaycast(center, out var pose)) PlaceModel(pose);
        }

        void PlaceModel(Pose pose)
        {
            var cam = ar.Camera;
            var toCam = cam.transform.position - pose.position;
            toCam.y = 0;
            var rot = toCam.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toCam.normalized, Vector3.up) : Quaternion.identity;

            _model.SetActive(true);
            _model.ResetView();
            _model.ShowAll();
            _model.SetTransparency(0f);
            _model.Place(pose.position + Vector3.up * (arModelSize * 0.45f), rot, arModelSize);
            _placed = true;
            _reticle.gameObject.SetActive(false);
            ar.FreezePlanes(true);
            Go(Page.Viewer);
        }

        void RescanAR()
        {
            _scanError.SetActive(false);
            _scanStart = Time.time;
            ar.Restart();
        }

        // ================================================================= visor
        void UpdateViewer()
        {
            if (_arMode && !_placed) return;
            var cam = ActiveCamera;

            if (_gestures.Drag != Vector2.zero) _model.Rotate(_gestures.Drag, cam, true);
            _model.Zoom(_gestures.Pinch);

            if (_gestures.Tap)
            {
                var part = _model.Pick(cam, _gestures.TapPosition);
                if (part != null) ShowInfo(part.structureId, part.rightSide);
                else if (_infoSheet.Open)
                {
                    CloseSheet(_infoSheet);
                    _model.Select(null);
                    ShowBottomBar(true);
                }
            }
        }

        void ShowInfo(string id, bool right)
        {
            var info = StructureCatalog.Get(id);
            _model.Select(_model.FirstPart(id));
            _infoTitle.text = info.Name;
            _infoSide.text = id == "cerebro" || id == "cerebelo" ? "Hemisferio " + (right ? "derecho" : "izquierdo") : StructureCatalog.SideName(right);
            _infoDot.color = info.Color;
            _infoDesc.text = info.Description;
            _infoRel.text = info.Relations;
            _btnIsolate.SetLabel(_model.Isolated == id ? "Mostrar todo" : "Aislar estructura");
            CloseSheet(_layersSheet);
            CloseSheet(_listSheet);
            SetToggle(_btnLayers, false);
            OpenSheet(_infoSheet);
            ShowBottomBar(false);
            HideHint();
        }

        void ToggleIsolate()
        {
            var sel = _model.Selected;
            if (sel == null) return;
            if (_model.Isolated == sel.structureId)
            {
                _model.ShowAll();
                RefreshLayerChecks();
                _btnIsolate.SetLabel("Aislar estructura");
            }
            else
            {
                _model.Isolate(sel.structureId);
                _btnIsolate.SetLabel("Mostrar todo");
            }
        }

        void CloseInfo()
        {
            CloseSheet(_infoSheet);
            _model.Select(null);
            ShowBottomBar(true);
        }

        void ToggleLayers()
        {
            bool open = !_layersSheet.Open;
            CloseSheet(_infoSheet);
            CloseSheet(_listSheet);
            _model.Select(null);
            if (open) { RefreshLayerChecks(); OpenSheet(_layersSheet); }
            else CloseSheet(_layersSheet);
            SetToggle(_btnLayers, open);
            ShowBottomBar(!open);
        }

        void ToggleLabels()
        {
            _labelsOn = !_labelsOn;
            _labels.gameObject.SetActive(_labelsOn);
            SetToggle(_btnLabels, _labelsOn);
        }

        void ResetViewer()
        {
            _model.ResetView();
            _model.ShowAll();
            _model.SetTransparency(0f);
            _model.Select(null);
            if (_transparency != null) _transparency.SetValueWithoutNotify(0f);
            if (_transparencyValue != null) _transparencyValue.text = "0%";
            RefreshLayerChecks();
            CloseAllSheets(false);
            Toast("Vista restablecida");
        }

        void ShowBottomBar(bool show)
        {
            if (_viewerBottom != null) _viewerBottom.SetActive(show);
        }

        void ShowHint()
        {
            if (_hint == null) return;
            _hint.SetActive(true);
            CancelInvoke(nameof(HideHint));
            Invoke(nameof(HideHint), 5f);
        }

        void HideHint()
        {
            if (_hint != null) _hint.SetActive(false);
        }

        void RefreshLayerChecks()
        {
            foreach (var id in _model.StructureIds)
            {
                bool on = _model.IsVisible(id);
                if (_layerBoxes.TryGetValue(id, out var box)) box.color = on ? Theme.Accent : new Color(1, 1, 1, 0.10f);
                if (_layerTicks.TryGetValue(id, out var tick)) tick.SetActive(on);
            }
        }

        // ================================================================= hojas inferiores
        bool AnySheetOpen() => _layersSheet.Open || _infoSheet.Open || _listSheet.Open;

        void OpenSheet(Sheet s)
        {
            if (s.Open) return;
            s.Open = true;
            s.Rt.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(s.Rt);
            float h = s.Rt.rect.height + 60f;
            if (s.Anim != null) StopCoroutine(s.Anim);
            s.Anim = StartCoroutine(UIKit.SlideY(s.Rt, -h, 0f, 0.28f));
        }

        void CloseSheet(Sheet s, bool instant = false)
        {
            if (!s.Open && !instant) return;
            s.Open = false;
            if (s.Anim != null) StopCoroutine(s.Anim);
            if (instant || !s.Rt.gameObject.activeInHierarchy)
            {
                s.Rt.gameObject.SetActive(false);
                return;
            }
            s.Anim = StartCoroutine(CloseRoutine(s));
        }

        IEnumerator CloseRoutine(Sheet s)
        {
            float h = s.Rt.rect.height + 60f;
            yield return UIKit.SlideY(s.Rt, s.Rt.anchoredPosition.y, -h, 0.2f);
            s.Rt.gameObject.SetActive(false);
        }

        void CloseAllSheets(bool instant)
        {
            CloseSheet(_layersSheet, instant);
            CloseSheet(_infoSheet, instant);
            CloseSheet(_listSheet, instant);
            _model.Select(null);
            SetToggle(_btnLayers, false);
            ShowBottomBar(true);
        }

        // ================================================================= toast
        void Toast(string msg)
        {
            _toastText.text = msg;
            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            _toast.gameObject.SetActive(true);
            yield return UIKit.Fade(_toast, 1f, 0.2f);
            yield return new WaitForSecondsRealtime(2.8f);
            yield return UIKit.Fade(_toast, 0f, 0.3f);
            _toast.gameObject.SetActive(false);
        }

        // ================================================================= construcción de UI
        void BuildUI()
        {
            var go = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(390, 844);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            _overlayRoot = (RectTransform)go.transform;

            // Capa de etiquetas (debajo de las pantallas para no tapar botones)
            var labelsRt = UIKit.Rect("Etiquetas", _overlayRoot);
            labelsRt.Stretch();
            _labels = labelsRt.gameObject.AddComponent<LabelOverlay>();
            _labels.Setup(_model, () => ActiveCamera);
            labelsRt.gameObject.SetActive(false);

            BuildSplash();
            BuildHome();
            BuildInstructions();
            BuildScanning();
            BuildViewer();
            BuildHowTo();
            BuildAbout();
            BuildCameraDenied();
            BuildToast();
        }

        RectTransform NewPage(Page page, bool opaque)
        {
            var root = UIKit.Rect("Pantalla_" + page, _overlayRoot);
            root.Stretch();
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            if (opaque)
            {
                var bg = UIKit.Img(root, "Fondo", null, Theme.Bg, true);
                bg.rectTransform.Stretch();
                var glow = UIKit.Img(root, "Brillo", UIKit.RadialGlow(), Theme.Accent.WithAlpha(0.10f));
                glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                glow.rectTransform.sizeDelta = new Vector2(700, 700);
                glow.rectTransform.anchoredPosition = new Vector2(120, -60);
            }
            var safe = UIKit.Rect("AreaSegura", root);
            safe.Stretch();
            safe.gameObject.AddComponent<SafeArea>();
            root.gameObject.SetActive(false);
            _pages[page] = cg;
            return safe;
        }

        RectTransform TopBar(RectTransform page, string title, Action back, string rightLabel = null, Action right = null)
        {
            var bar = UIKit.Rect("BarraSuperior", page);
            bar.Top(60, 4, 16);
            UIKit.HStack(bar, 12, null, TextAnchor.MiddleLeft, false).childForceExpandHeight = false;

            if (back != null) UIKit.IconBtn(bar, "‹", back, 42, 28);
            var t = UIKit.Txt(bar, title, 19, Theme.Text, FontStyle.Bold, TextAnchor.MiddleLeft, "Titulo");
            t.Pref(-1, -1, 1);
            if (rightLabel != null)
            {
                if (rightLabel.Length <= 1) UIKit.IconBtn(bar, rightLabel, right, 42, 18);
                else
                {
                    var b = UIKit.Btn(bar, rightLabel, BtnStyle.Secondary, right, 38, 13);
                    b.Root().GetComponent<LayoutElement>().preferredWidth = 108;
                }
            }
            return bar;
        }

        RectTransform BottomStack(RectTransform page, float spacing = 12, int side = 20, int bottom = 18)
        {
            var rt = UIKit.Rect("Inferior", page);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            UIKit.VStack(rt, spacing, new RectOffset(side, side, 8, bottom));
            UIKit.FitHeight(rt.gameObject);
            return rt;
        }

        RectTransform ContentStack(RectTransform page, float top, float spacing = 14, int side = 20)
        {
            var rt = UIKit.Rect("Contenido", page);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -top);
            rt.sizeDelta = Vector2.zero;
            UIKit.VStack(rt, spacing, new RectOffset(side, side, 0, 0));
            UIKit.FitHeight(rt.gameObject);
            return rt;
        }

        RectTransform Emblem(Transform parent, float size)
        {
            var holder = UIKit.Rect("Emblema", parent);
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = size;
            var glow = UIKit.Img(holder, "Halo", UIKit.RadialGlow(), Theme.Accent.WithAlpha(0.45f));
            glow.rectTransform.Center(new Vector2(size * 2f, size * 2f));
            var ring = UIKit.Img(holder, "Anillo", UIKit.Rounded(Mathf.RoundToInt(size / 2)), Theme.Accent);
            ring.rectTransform.Center(new Vector2(size, size));
            var inner = UIKit.Img(holder, "Interior", UIKit.Rounded(Mathf.RoundToInt(size / 2 - 4)), Theme.Surface);
            inner.rectTransform.Center(new Vector2(size - 8, size - 8));
            var t = UIKit.Txt(holder, "RA", Mathf.RoundToInt(size * 0.34f), Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            t.rectTransform.Center(new Vector2(size, size));
            var sub = UIKit.Txt(holder, "ANATOMÍA", Mathf.RoundToInt(size * 0.085f), Theme.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
            sub.rectTransform.Center(new Vector2(size, 20), new Vector2(0, -size * 0.26f));
            return holder;
        }

        Text Para(Transform parent, string text, int size = 14, Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
            => UIKit.Txt(parent, text, size, color ?? Theme.TextDim, style, anchor);

        // ----------------------------------------------------------------- 01 Carga
        void BuildSplash()
        {
            var p = NewPage(Page.Splash, true);
            var mid = UIKit.Rect("Centro", p);
            mid.Center(new Vector2(320, 360), new Vector2(0, 30));
            UIKit.VStack(mid, 14, null, TextAnchor.MiddleCenter);
            var em = Emblem(mid, 132);
            em.gameObject.AddComponent<Pulse>().amount = 0.025f;
            UIKit.Rect("Espacio", mid).gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
            UIKit.Txt(mid, "Anatomía RA", 32, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Para(mid, StructureCatalog.ModelSubtitle, 14, Theme.TextDim, TextAnchor.MiddleCenter);

            var barHolder = UIKit.Rect("Progreso", mid);
            barHolder.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var track = UIKit.Img(barHolder, "Pista", UIKit.Rounded(3), Theme.Surface2);
            track.rectTransform.Center(new Vector2(190, 6), new Vector2(0, -6));
            _splashFill = UIKit.Img(track.transform, "Relleno", UIKit.Rounded(3), Theme.Accent);
            _splashFill.rectTransform.anchorMin = Vector2.zero;
            _splashFill.rectTransform.anchorMax = new Vector2(0, 1);
            _splashFill.rectTransform.offsetMin = _splashFill.rectTransform.offsetMax = Vector2.zero;
            Para(mid, "Preparando la experiencia…", 13, Theme.TextFaint, TextAnchor.MiddleCenter);

            var foot = UIKit.Txt(p, "UNS · Medicina Humana", 12, Theme.TextFaint, FontStyle.Normal, TextAnchor.MiddleCenter);
            foot.rectTransform.Bottom(30, 24);
        }

        // ----------------------------------------------------------------- 02 Inicio
        void BuildHome()
        {
            var p = NewPage(Page.Home, false);

            // degradado inferior para legibilidad
            var fade = UIKit.Img(p.parent, "Degradado", UIKit.VerticalFade(), Theme.Bg.WithAlpha(0.95f));
            fade.rectTransform.Bottom(360);
            fade.transform.SetSiblingIndex(0);

            var bar = TopBar(p, "Anatomía RA", null, "?", () => { _howToReturn = Page.Home; Go(Page.HowTo); });
            var dot = UIKit.Img(bar, "Punto", UIKit.Rounded(5), Theme.Accent);
            dot.Pref(10, 10);
            dot.transform.SetAsFirstSibling();

            var c = ContentStack(p, 76, 10);
            var chipRow = UIKit.Rect("FilaChip", c);
            UIKit.HStack(chipRow, 0, null, TextAnchor.MiddleLeft, false);
            UIKit.Chip(chipRow, "NEUROANATOMÍA · RA", Theme.AccentSoft, Theme.Accent, 11);
            UIKit.Txt(c, "Explora el\nencéfalo humano", 31, Theme.Text, FontStyle.Bold);
            Para(c, "Visualiza sus estructuras en realidad aumentada y descubre cómo se relacionan entre sí.", 15);

            var b = BottomStack(p);
            UIKit.Btn(b, "Iniciar experiencia RA", BtnStyle.Primary, () => Go(Page.Instructions), 56, 17);
            var row = UIKit.Rect("Fila", b);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            UIKit.HStack(row, 10);
            UIKit.Btn(row, "Ver en 3D", BtnStyle.Secondary, Open3D, 48, 14);
            UIKit.Btn(row, "¿Cómo usar?", BtnStyle.Secondary, () => { _howToReturn = Page.Home; Go(Page.HowTo); }, 48, 14);
            UIKit.Btn(b, "Acerca del proyecto", BtnStyle.Ghost, () => Go(Page.About), 36, 14);
            Para(b, "Escanea · Explora · Aprende", 12, Theme.TextFaint, TextAnchor.MiddleCenter);
        }

        // ----------------------------------------------------------------- 03 Instrucciones
        void BuildInstructions()
        {
            var p = NewPage(Page.Instructions, true);
            TopBar(p, "Antes de empezar", () => Go(Page.Home));

            var c = ContentStack(p, 84, 12);
            UIKit.Txt(c, "Prepara tu espacio", 28, Theme.Text, FontStyle.Bold);
            Para(c, "Sigue estos 3 pasos para colocar el modelo.", 15);
            UIKit.Rect("Espacio", c).gameObject.AddComponent<LayoutElement>().preferredHeight = 6;

            Step(c, "01", "Busca una mesa o superficie plana", "Usa un lugar con buena iluminación.");
            Step(c, "02", "Mueve el celular lentamente", "Recorre la superficie hasta que sea detectada.");
            Step(c, "03", "Toca para colocar el modelo", "El encéfalo aparecerá en realidad aumentada.");

            var b = BottomStack(p);
            _permBtn = UIKit.Btn(b, "Permitir cámara", BtnStyle.Primary, StartExperience, 56);
            Para(b, "La cámara se usa solo para detectar la superficie.", 12, Theme.TextFaint, TextAnchor.MiddleCenter);
        }

        void Step(Transform parent, string num, string title, string desc)
        {
            var card = UIKit.Card(parent, "Paso" + num, 20);
            UIKit.HStack(card.rectTransform, 14, UIKit.Pad(16, 16), TextAnchor.MiddleLeft, false).childForceExpandHeight = false;
            UIKit.FitHeight(card.gameObject);

            var circ = UIKit.Img(card.transform, "Numero", UIKit.Rounded(19), Theme.Accent);
            circ.Pref(38, 38);
            var n = UIKit.Txt(circ.transform, num, 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            n.rectTransform.Stretch();

            var col = UIKit.Rect("Textos", card.transform);
            UIKit.VStack(col, 4);
            col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            UIKit.Txt(col, title, 16, Theme.Text, FontStyle.Bold);
            Para(col, desc, 13);
        }

        // ----------------------------------------------------------------- 04 Escaneo
        void BuildScanning()
        {
            var p = NewPage(Page.Scanning, false);
            TopBar(p, "Escanear superficie", () => Go(Page.Home), "?",
                () => Toast("Apunta a una mesa o al piso y muévete despacio de lado a lado."));

            // estado
            var status = UIKit.Card(p, "Estado", 18, Theme.Glass, false);
            status.rectTransform.Top(46, 72, 60);
            var sh = UIKit.HStack(status.rectTransform, 10, UIKit.Pad(16, 0), TextAnchor.MiddleCenter, false);
            sh.childForceExpandHeight = false;
            var spin = UIKit.Img(status.transform, "Indicador", UIKit.Rounded(4), Theme.Accent);
            spin.Pref(8, 8);
            spin.gameObject.AddComponent<Pulse>().amount = 0.35f;
            _scanStatus = UIKit.Txt(status.transform, "Detectando superficie…", 14, Theme.Text, FontStyle.Bold, TextAnchor.MiddleLeft);

            // marco central
            var frame = UIKit.Rect("Marco", p);
            frame.Center(new Vector2(230, 230), new Vector2(0, 10));
            frame.gameObject.AddComponent<Pulse>();
            Corner(frame, new Vector2(0, 1), 1, -1);
            Corner(frame, new Vector2(1, 1), -1, -1);
            Corner(frame, new Vector2(0, 0), 1, 1);
            Corner(frame, new Vector2(1, 0), -1, 1);
            _scanFrame = frame.gameObject;

            // panel inferior
            var b = BottomStack(p);
            var info = UIKit.Card(b, "Indicaciones", 22, Theme.Glass, false);
            UIKit.VStack(info.rectTransform, 4, UIKit.Pad(18, 14), TextAnchor.UpperCenter);
            UIKit.FitHeight(info.gameObject);
            _scanTitle = UIKit.Txt(info.transform, "Mueve el celular lentamente", 16, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            _scanSub = Para(info.transform, "Busca una zona plana y bien iluminada", 13, Theme.TextDim, TextAnchor.MiddleCenter);
            _placeBtn = UIKit.Btn(b, "Colocar modelo aquí", BtnStyle.Primary, PlaceCenter, 54).Root();
            UIKit.Btn(b, "Cancelar", BtnStyle.Secondary, () => Go(Page.Home), 48, 15);

            // 04.1 Error de escaneo
            var err = UIKit.Img(p.parent, "ErrorEscaneo", null, new Color(0, 0, 0, 0.6f), true);
            err.rectTransform.Stretch();
            _scanError = err.gameObject;
            var card = UIKit.Card(err.transform, "Tarjeta", 26);
            card.rectTransform.Center(new Vector2(330, 0));
            UIKit.VStack(card.rectTransform, 12, UIKit.Pad(22, 24), TextAnchor.UpperCenter);
            UIKit.FitHeight(card.gameObject);
            UIKit.Txt(card.transform, "Superficie no detectada", 20, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);

            var icon = UIKit.Rect("Icono", card.transform);
            icon.gameObject.AddComponent<LayoutElement>().preferredHeight = 130;
            var ring = UIKit.Img(icon, "Anillo", UIKit.Rounded(60), Theme.Accent.WithAlpha(0.35f));
            ring.rectTransform.Center(new Vector2(120, 120));
            var hole = UIKit.Img(icon, "Hueco", UIKit.Rounded(57), Theme.Surface);
            hole.rectTransform.Center(new Vector2(114, 114));
            var stick = UIKit.Img(icon, "Linea", null, Theme.TextDim);
            stick.rectTransform.Center(new Vector2(2, 44), new Vector2(0, 16));
            var ball = UIKit.Img(icon, "Punto", UIKit.Rounded(10), Theme.Accent);
            ball.rectTransform.Center(new Vector2(20, 20), new Vector2(0, -16));

            Para(card.transform, "Vuelve a escanear", 15, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Para(card.transform, "Busca una zona plana, con textura y bien iluminada. Evita superficies brillantes o de vidrio.", 13, Theme.TextDim, TextAnchor.MiddleCenter);
            var row = UIKit.Rect("Botones", card.transform);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
            UIKit.HStack(row, 10);
            UIKit.Btn(row, "Cancelar", BtnStyle.Secondary, () => Go(Page.Home), 50, 15);
            UIKit.Btn(row, "Escanear de nuevo", BtnStyle.Primary, RescanAR, 50, 15);
            _scanError.SetActive(false);
        }

        void Corner(RectTransform frame, Vector2 anchor, int sx, int sy)
        {
            const float len = 44f, th = 5f;
            var h = UIKit.Img(frame, "H", UIKit.Rounded(2), Color.white);
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = anchor;
            h.rectTransform.pivot = new Vector2(sx > 0 ? 0 : 1, sy > 0 ? 0 : 1);
            h.rectTransform.sizeDelta = new Vector2(len, th);
            h.rectTransform.anchoredPosition = Vector2.zero;
            var v = UIKit.Img(frame, "V", UIKit.Rounded(2), Color.white);
            v.rectTransform.anchorMin = v.rectTransform.anchorMax = anchor;
            v.rectTransform.pivot = new Vector2(sx > 0 ? 0 : 1, sy > 0 ? 0 : 1);
            v.rectTransform.sizeDelta = new Vector2(th, len);
            v.rectTransform.anchoredPosition = Vector2.zero;
        }

        // ----------------------------------------------------------------- 05/06/07 Visor
        void BuildViewer()
        {
            var p = NewPage(Page.Viewer, false);
            var bar = TopBar(p, StructureCatalog.ModelTitle, () => Go(Page.Home), "Restablecer", ResetViewer);
            var chip = UIKit.Chip(bar, "RA", Theme.AccentSoft, Theme.Accent, 11);
            chip.transform.SetSiblingIndex(2);
            _modeChipText = chip.GetComponentInChildren<Text>();

            // pista
            var hint = UIKit.Chip(p, "Toca una estructura para ver su información", Theme.Glass, Theme.Text, 12);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 1);
            hint.rectTransform.pivot = new Vector2(0.5f, 1);
            hint.rectTransform.anchoredPosition = new Vector2(0, -74);
            _hint = hint.gameObject;

            // barra inferior: Capas | Etiquetas + Explorar estructura
            var b = BottomStack(p);
            _viewerBottom = b.gameObject;
            var seg = UIKit.Card(b, "Segmentado", 20, Theme.Glass, false);
            seg.Pref(58);
            UIKit.HStack(seg.rectTransform, 6, UIKit.Pad(6));
            _btnLayers = UIKit.Btn(seg.transform, "Capas", BtnStyle.Secondary, ToggleLayers, 46, 15);
            _btnLabels = UIKit.Btn(seg.transform, "Etiquetas", BtnStyle.Secondary, ToggleLabels, 46, 15);
            UIKit.Btn(b, "Explorar estructura", BtnStyle.Primary, OpenList, 56);

            BuildLayersSheet(p);
            BuildInfoSheet(p);
            BuildListSheet(p);
        }

        void SetToggle(Button b, bool on)
        {
            if (b == null) return;
            var img = b.targetGraphic as Image;
            if (img != null) img.color = on ? Theme.AccentSoft : Theme.Surface2;
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.color = on ? Theme.Accent : Theme.Text;
        }

        Sheet MakeSheet(RectTransform page, string name, out RectTransform content)
        {
            var rt = UIKit.Rect(name, page);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var bg = UIKit.Img(rt, "Fondo", UIKit.Rounded(28), Theme.Surface, true);
            bg.rectTransform.Stretch(0, 0, 0, -60);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            UIKit.VStack(rt, 12, new RectOffset(20, 20, 12, 22));
            UIKit.FitHeight(rt.gameObject);

            var handle = UIKit.Rect("Asa", rt);
            handle.gameObject.AddComponent<LayoutElement>().preferredHeight = 8;
            var hb = UIKit.Img(handle, "Barra", UIKit.Rounded(3), new Color(1, 1, 1, 0.18f));
            hb.rectTransform.Center(new Vector2(42, 5));

            content = rt;
            rt.gameObject.SetActive(false);
            return new Sheet { Rt = rt };
        }

        RectTransform SheetHeader(RectTransform content, string title, string sub, Action close, out Text titleText, out Text subText)
        {
            var row = UIKit.Rect("Cabecera", content);
            UIKit.HStack(row, 10, null, TextAnchor.MiddleLeft, false).childForceExpandHeight = false;
            var col = UIKit.Rect("Textos", row);
            UIKit.VStack(col, 2);
            col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            titleText = UIKit.Txt(col, title, 19, Theme.Text, FontStyle.Bold);
            subText = Para(col, sub, 13);
            UIKit.IconBtn(row, "×", close, 38, 22);
            return row;
        }

        void BuildLayersSheet(RectTransform page)
        {
            _layersSheet = MakeSheet(page, "HojaCapas", out var c);
            SheetHeader(c, "Capas anatómicas", "Selecciona qué estructuras visualizar", ToggleLayers, out _, out _);

            var ids = _model.StructureIds;
            for (int i = 0; i < ids.Count; i += 2)
            {
                var row = UIKit.Rect("Fila", c);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
                UIKit.HStack(row, 8);
                LayerItem(row, ids[i]);
                if (i + 1 < ids.Count) LayerItem(row, ids[i + 1]);
                else UIKit.Rect("Vacio", row).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            }

            var tr = UIKit.Rect("FilaTransparencia", c);
            UIKit.HStack(tr, 8, new RectOffset(2, 2, 6, 0)).childForceExpandWidth = false;
            var tl = UIKit.Txt(tr, "Transparencia del cerebro", 14, Theme.Text, FontStyle.Bold);
            tl.Pref(-1, -1, 1);
            _transparencyValue = UIKit.Txt(tr, "0%", 14, Theme.Accent, FontStyle.Bold, TextAnchor.MiddleRight);
            _transparency = UIKit.MakeSlider(c, 0f, v =>
            {
                _model.SetTransparency(v);
                _transparencyValue.text = Mathf.RoundToInt(v * 100) + "%";
            });

            UIKit.Btn(c, "Mostrar todo", BtnStyle.Secondary, () =>
            {
                _model.ShowAll();
                RefreshLayerChecks();
            }, 46, 15);
        }

        void LayerItem(RectTransform row, string id)
        {
            var info = StructureCatalog.Get(id);
            var item = UIKit.Img(row, "Capa_" + id, UIKit.Rounded(14), Theme.Surface2, true);
            item.Pref(-1, 0, 1);
            UIKit.HStack(item.rectTransform, 8, new RectOffset(10, 8, 0, 0), TextAnchor.MiddleLeft, false).childForceExpandHeight = false;

            var box = UIKit.Img(item.transform, "Casilla", UIKit.Rounded(7), Theme.Accent);
            box.Pref(22, 22);
            var tick = UIKit.Img(box.transform, "Marca", UIKit.Rounded(3), Color.white);
            tick.rectTransform.Center(new Vector2(8, 8));
            _layerBoxes[id] = box;
            _layerTicks[id] = tick.gameObject;

            var dot = UIKit.Img(item.transform, "Color", UIKit.Rounded(4), info.Color);
            dot.Pref(8, 8);
            var t = UIKit.Txt(item.transform, info.Name, 12, Theme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
            t.lineSpacing = 0.95f;
            t.Pref(-1, 0, 1);

            var btn = item.gameObject.AddComponent<Button>();
            btn.targetGraphic = item;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() =>
            {
                if (_model.Isolated != null) _model.ShowAll();
                _model.SetVisible(id, !_model.IsVisible(id));
                RefreshLayerChecks();
            });
        }

        void BuildInfoSheet(RectTransform page)
        {
            _infoSheet = MakeSheet(page, "HojaInfo", out var c);
            var header = SheetHeader(c, "Estructura", "Lado", CloseInfo, out _infoTitle, out _infoSide);
            _infoDot = UIKit.Img(header, "Color", UIKit.Rounded(7), Color.white);
            _infoDot.Pref(14, 14);
            _infoDot.transform.SetAsFirstSibling();

            _infoDesc = Para(c, "", 14, Theme.Text);
            UIKit.Txt(c, "UBICACIÓN Y RELACIONES", 12, Theme.Accent, FontStyle.Bold);
            _infoRel = Para(c, "", 14);

            var row = UIKit.Rect("Botones", c);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
            UIKit.HStack(row, 10);
            _btnIsolate = UIKit.Btn(row, "Aislar estructura", BtnStyle.Secondary, ToggleIsolate, 50, 15);
            UIKit.Btn(row, "Cerrar", BtnStyle.Primary, CloseInfo, 50, 15);
            Para(c, "Toca otra estructura para consultar su información.", 12, Theme.TextFaint, TextAnchor.MiddleCenter);
        }

        void BuildListSheet(RectTransform page)
        {
            _listSheet = MakeSheet(page, "HojaEstructuras", out var c);
            SheetHeader(c, "Estructuras", StructureCatalog.ModelSubtitle, () =>
            {
                CloseSheet(_listSheet);
                ShowBottomBar(true);
            }, out _, out _);

            foreach (var id in _model.StructureIds)
            {
                var info = StructureCatalog.Get(id);
                var item = UIKit.Img(c, "Item_" + id, UIKit.Rounded(14), Theme.Surface2, true);
                item.Pref(44);
                UIKit.HStack(item.rectTransform, 10, new RectOffset(14, 14, 0, 0), TextAnchor.MiddleLeft, false).childForceExpandHeight = false;
                var dot = UIKit.Img(item.transform, "Color", UIKit.Rounded(5), info.Color);
                dot.Pref(10, 10);
                var t = UIKit.Txt(item.transform, info.Name, 15, Theme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
                t.Pref(-1, -1, 1);
                UIKit.Txt(item.transform, "›", 20, Theme.TextFaint, FontStyle.Bold, TextAnchor.MiddleRight).Pref(-1, 14);

                var btn = item.gameObject.AddComponent<Button>();
                btn.targetGraphic = item;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                string captured = id;
                btn.onClick.AddListener(() =>
                {
                    if (_model.Isolated != null && _model.Isolated != captured) _model.ShowAll();
                    if (!_model.IsVisible(captured)) _model.SetVisible(captured, true);
                    ShowInfo(captured, true);
                });
            }
        }

        void OpenList()
        {
            CloseSheet(_layersSheet);
            CloseSheet(_infoSheet);
            _model.Select(null);
            SetToggle(_btnLayers, false);
            OpenSheet(_listSheet);
            ShowBottomBar(false);
            HideHint();
        }

        // ----------------------------------------------------------------- 08 Cómo usar
        void BuildHowTo()
        {
            var p = NewPage(Page.HowTo, true);
            TopBar(p, "Cómo usar", () => Go(_howToReturn));
            var c = ContentStack(p, 84, 10);
            Gesture(c, "1", "Un dedo: rotar", "Arrastra sobre el modelo para girarlo.");
            Gesture(c, "2", "Dos dedos: zoom", "Pellizca para acercar o alejar.");
            Gesture(c, "•", "Toca una estructura", "Verás su nombre, función y relaciones.");
            Gesture(c, "≡", "Capas y transparencia", "Oculta estructuras o vuelve translúcido el cerebro para ver su interior.");
            Gesture(c, "R", "Restablecer la vista", "Vuelve a la posición y tamaño iniciales.");

            var b = BottomStack(p);
            UIKit.Btn(b, "Entendido", BtnStyle.Primary, () => Go(_howToReturn), 56);
            Para(b, "Puedes volver a consultar estas instrucciones desde el inicio.", 12, Theme.TextFaint, TextAnchor.MiddleCenter);
        }

        void Gesture(Transform parent, string glyph, string title, string desc)
        {
            var card = UIKit.Card(parent, "Gesto", 18);
            UIKit.HStack(card.rectTransform, 14, UIKit.Pad(14, 12), TextAnchor.MiddleLeft, false).childForceExpandHeight = false;
            UIKit.FitHeight(card.gameObject);
            var ic = UIKit.Img(card.transform, "Icono", UIKit.Rounded(19), Theme.AccentSoft);
            ic.Pref(38, 38);
            var g = UIKit.Txt(ic.transform, glyph, 17, Theme.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
            g.rectTransform.Stretch();
            var col = UIKit.Rect("Textos", card.transform);
            UIKit.VStack(col, 3);
            col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            UIKit.Txt(col, title, 15, Theme.Text, FontStyle.Bold);
            Para(col, desc, 13);
        }

        // ----------------------------------------------------------------- 09 Acerca del proyecto
        void BuildAbout()
        {
            var p = NewPage(Page.About, true);
            TopBar(p, "Acerca del proyecto", () => Go(Page.Home));

            var card = UIKit.Card(p, "Tarjeta", 26);
            card.rectTransform.anchorMin = new Vector2(0, 1);
            card.rectTransform.anchorMax = new Vector2(1, 1);
            card.rectTransform.pivot = new Vector2(0.5f, 1);
            card.rectTransform.offsetMin = new Vector2(20, 0);
            card.rectTransform.offsetMax = new Vector2(-20, -84);
            UIKit.VStack(card.rectTransform, 8, UIKit.Pad(22, 26), TextAnchor.UpperCenter);
            UIKit.FitHeight(card.gameObject);

            Emblem(card.transform, 96);
            UIKit.Rect("Espacio", card.transform).gameObject.AddComponent<LayoutElement>().preferredHeight = 6;
            Para(card.transform, "PY001", 12, Theme.TextFaint, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Txt(card.transform, "Anatomía RA", 26, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Divider(card.transform);
            Para(card.transform, "Ingeniería de Software II", 14, Theme.Text, TextAnchor.MiddleCenter);
            Para(card.transform, "Universidad Nacional del Santa", 14, Theme.TextDim, TextAnchor.MiddleCenter);
            Divider(card.transform);
            Para(card.transform, "Equipo InnoMed", 15, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Para(card.transform, "Modelo: encéfalo · 9 estructuras en ambos lados", 13, Theme.TextDim, TextAnchor.MiddleCenter);
            Para(card.transform, "Versión 1.0", 13, Theme.TextDim, TextAnchor.MiddleCenter);
            Para(card.transform, "Prototipo académico · 2026", 12, Theme.TextFaint, TextAnchor.MiddleCenter);

            var b = BottomStack(p);
            UIKit.Btn(b, "Volver al inicio", BtnStyle.Primary, () => Go(Page.Home), 56);
        }

        void Divider(Transform parent)
        {
            var d = UIKit.Img(parent, "Separador", null, new Color(1, 1, 1, 0.07f));
            d.Pref(1);
        }

        // ----------------------------------------------------------------- 10 Cámara denegada
        void BuildCameraDenied()
        {
            var p = NewPage(Page.CameraDenied, true);
            TopBar(p, "Cámara", () => Go(Page.Home));

            var card = UIKit.Card(p, "Tarjeta", 26);
            card.rectTransform.Center(new Vector2(340, 0), new Vector2(0, 60));
            UIKit.VStack(card.rectTransform, 12, UIKit.Pad(24, 28), TextAnchor.UpperCenter);
            UIKit.FitHeight(card.gameObject);

            var icon = UIKit.Rect("Icono", card.transform);
            icon.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;
            var circle = UIKit.Img(icon, "Circulo", UIKit.Rounded(40), Theme.AccentSoft);
            circle.rectTransform.Center(new Vector2(80, 80));
            var body = UIKit.Img(icon, "Camara", UIKit.Rounded(6), Theme.Accent);
            body.rectTransform.Center(new Vector2(38, 26), new Vector2(0, -2));
            var lens = UIKit.Img(icon, "Lente", UIKit.Rounded(6), Theme.Surface);
            lens.rectTransform.Center(new Vector2(12, 12), new Vector2(0, -2));
            var slash = UIKit.Img(icon, "Tachado", UIKit.Rounded(2), Theme.Text);
            slash.rectTransform.Center(new Vector2(58, 4));
            slash.rectTransform.localRotation = Quaternion.Euler(0, 0, -40);

            UIKit.Txt(card.transform, "Se necesita acceso a la cámara para usar la realidad aumentada", 18, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Para(card.transform, "Habilita el permiso desde los ajustes del dispositivo o explora el modelo en 3D.", 13, Theme.TextDim, TextAnchor.MiddleCenter);

            var b = BottomStack(p);
            UIKit.Btn(b, "Abrir ajustes", BtnStyle.Primary, OpenAppSettings, 56);
            UIKit.Btn(b, "Ver en 3D sin cámara", BtnStyle.Secondary, Open3D, 48, 15);
            UIKit.Btn(b, "Volver", BtnStyle.Ghost, () => Go(Page.Home), 40, 14);
        }

        // ----------------------------------------------------------------- toast
        void BuildToast()
        {
            var holder = UIKit.Rect("Aviso", _overlayRoot);
            holder.Stretch();
            holder.gameObject.AddComponent<SafeArea>();
            var chip = UIKit.Card(holder, "Toast", 18, Theme.Glass, false);
            chip.rectTransform.anchorMin = new Vector2(0, 1);
            chip.rectTransform.anchorMax = new Vector2(1, 1);
            chip.rectTransform.pivot = new Vector2(0.5f, 1);
            chip.rectTransform.offsetMin = new Vector2(24, 0);
            chip.rectTransform.offsetMax = new Vector2(-24, -120);
            UIKit.VStack(chip.rectTransform, 0, UIKit.Pad(16, 12), TextAnchor.MiddleCenter);
            UIKit.FitHeight(chip.gameObject);
            _toastText = UIKit.Txt(chip.transform, "", 14, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            _toast = chip.gameObject.AddComponent<CanvasGroup>();
            _toast.blocksRaycasts = false;
            _toast.alpha = 0;
            chip.gameObject.SetActive(false);
        }
    }
}
