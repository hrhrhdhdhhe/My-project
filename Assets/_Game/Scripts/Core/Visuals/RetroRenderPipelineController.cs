// World Crisis | Слой: Core/Visuals | Ответственный: OpenCode
// Настройка рендера в стиле Crisp Retro-PBR по разделу 6 Art Bible:
//   * рендер сцены в низкую RenderTexture 960x540 с FilterMode.Point и без сглаживания;
//   * вывод через полноэкранный RawImage с RetroBunkerDither (дизеринг, постеризация,
//     аутлайн, ACES, контраст, bloom, виньетка, зерно);
//   * густой экспоненциальный туман #0D0E11;
//   * жёсткие тени у всех источников света.
//
// HUD рисуется в отдельном Canvas с большим sortingOrder, поэтому поверх ретро-картинки
// остаётся только прицел-точка и диегетические подсказки.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
// Алиас, а не using на весь namespace: иначе ShadowResolution из URP перебивает
// UnityEngine.ShadowResolution, который нужен для QualitySettings.
using UniversalPipelineAsset = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;

namespace WorldCrisis
{
    /// <summary>
    /// Конструктор ретро-рендера. Вешается на любой объект сцены и настраивает всё сразу:
    /// и камеру, и RenderSettings, и тени, и глобальные параметры материалов.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("World Crisis/Retro Render Pipeline Controller")]
    public class RetroRenderPipelineController : MonoBehaviour
    {
        #region Константы Art Bible

        /// <summary>Цвет тумана бункера #0D0E11.</summary>
        private static readonly Color32 BunkerFogColor = new Color32(0x0D, 0x0E, 0x11, 0xFF);

        /// <summary>Плотность экспоненциального тумана: работает в глубокой шахте зала саммита,
        /// а в кабине бункера не закрашивает стол.</summary>
        private const float BunkerFogDensity = 0.018f;

        /// <summary>Имя шейдера постобработки.</summary>
        private const string BlitShaderName = "WorldCrisis/Retro Bunker Dither";

        /// <summary>Разрешение ретро-вывода по умолчанию (6-колоночный кадр 16:9).</summary>
        private const int DefaultRetroWidth = 960;

        private const int DefaultRetroHeight = 540;

        private const int RetroColorDepth = 24;

        /// <summary>Слой UI: презентер рисует только его, не задевая уже отрисованную 3D-сцену.</summary>
        private const int uiLayerIndex = 5;

        #endregion

        #region Сериализованные параметры

        [Header("Ретро-вывод")]
        [SerializeField] private bool _enableRetroOutput = true;
        [SerializeField] private int _retroWidth = DefaultRetroWidth;
        [SerializeField] private int _retroHeight = DefaultRetroHeight;
        [SerializeField] private bool _pointFiltering = true;

        [Tooltip("Слой Canvas постобработки. HUD должен иметь больший sortingOrder.")]
        [SerializeField] private int _overlaySortingOrder = -100;


        [Header("Туман")]
        [SerializeField] private bool _enableFog = true;
        [SerializeField] private Color32 _fogColor = BunkerFogColor;
        [SerializeField] private float _fogDensity = BunkerFogDensity;

        [Header("Тени")]
        [SerializeField] private bool _forceHardShadows = true;
        [SerializeField] private ShadowResolution _shadowResolution = ShadowResolution.High;

        [Header("Внешний вид материалов (глобалы)")]
        [SerializeField] private bool _pushRetroGlobals = true;
        [SerializeField] private Vector2 _dirtTiling = new Vector2(3f, 3f);
        [SerializeField] private Vector2 _emissionBreakupGrid = new Vector2(14f, 14f);
        [Range(0f, 1f)] [SerializeField] private float _dirtStrength = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float _dirtRoughnessKill = 0.65f;
        [Range(0f, 2f)] [SerializeField] private float _specContrast = 1.35f;
        [Range(0f, 1f)] [SerializeField] private float _smoothnessFloor = 0.12f;
        [Range(0f, 8f)] [SerializeField] private float _emissionBoost = 1.8f;
        [Range(0f, 1f)] [SerializeField] private float _emissionBreakup = 0.15f;
        [SerializeField] private Color _rimColor = new Color(0.55f, 0.72f, 0.85f, 1f);
        [Range(0f, 4f)] [SerializeField] private float _rimStrength = 0.55f;
        [Range(0.5f, 12f)] [SerializeField] private float _rimPower = 3.5f;
        [SerializeField] private Color _globalEmissionTint = Color.black;

        [Tooltip("Общий множитель альбедо. Палитра процедурных текстур уже осветлена "
                 + "по Art Bible, поэтому множитель по умолчанию равен 1.0: при 1.45 "
                 + "бетон и приборы выжигались в чисто белый.")]
        [SerializeField] private float _albedoGain = 1f;

        [Header("Отладка")]
        [SerializeField] private Camera _cameraOverride = null;

        #endregion

        #region Кэш идентификаторов

        // _MainTex, а не _RetroSource: в это свойство UnityEngine.UI.RawImage отдаёт
        // свою текстуру через Graphic.mainTexture.
        private static readonly int RetroSourceId = Shader.PropertyToID("_MainTex");
        private static readonly int RetroSourceTexelSizeId = Shader.PropertyToID("_MainTex_TexelSize");
        private static readonly int RetroDepthId = Shader.PropertyToID("_RetroDepth");
        private static readonly int RetroDirtTilingId = Shader.PropertyToID("_RetroDirtTiling");
        private static readonly int RetroRimColorId = Shader.PropertyToID("_RetroRimColor");
        private static readonly int RetroEmissionColorId = Shader.PropertyToID("_RetroEmissionColor");
        private static readonly int RetroDirtId = Shader.PropertyToID("_RetroDirt");
        private static readonly int RetroSpecId = Shader.PropertyToID("_RetroSpec");
        private static readonly int RetroRimId = Shader.PropertyToID("_RetroRim");
        private static readonly int RetroDirtMapId = Shader.PropertyToID("_RetroDirtMap");
        private static readonly int RetroAlbedoGainId = Shader.PropertyToID("_RetroAlbedoGain");

        #endregion

        private RenderTexture _sceneTexture;
        private Material _blitMaterial;
        private Canvas _overlayCanvas;
        private RawImage _overlayImage;
        private Camera _targetCamera;
        private Camera _presenterCamera;
        private RenderTexture _previousTarget;
        private bool _previousFogEnabled;
        private FogMode _previousFogMode;
        private Color _previousFogColor;
        private float _previousFogDensity;
        private bool _warnedAboutShader;
        private bool _warnedAboutSoftShadows;
        private bool _warnedAboutMissingLightShadow;

        /// <summary>Низкоразрешённая текстура со сценой. Её же видит RawImage.</summary>
        public RenderTexture SceneTexture
        {
            get { return _sceneTexture; }
        }

        /// <summary>Материал постобработки — можно докручивать из инспектора в рантайме.</summary>
        public Material BlitMaterial
        {
            get { return _blitMaterial; }
        }

        #region Unity

        private void Awake()
        {
            if (!_enableRetroOutput && !_pushRetroGlobals)
            {
                enabled = false;
                return;
            }

            ResolveCamera();

            if (_enableRetroOutput)
            {
                CreateSceneTexture();
                CreateOverlay();
                CreatePresenterCamera();
                BindCameraToTexture();
                PushBlitParameters();
            }

            ApplyRenderSettings();
            ApplyRetroGlobals();
            ApplyLightShadows();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnEnable()
        {
            // После перезагрузки домена RenderTexture могла быть уничтожена движком.
            if (_enableRetroOutput && (_sceneTexture == null || !_sceneTexture.IsCreated()))
            {
                CreateSceneTexture();
                CreateOverlay();
                CreatePresenterCamera();
                BindCameraToTexture();
                PushBlitParameters();
            }

            ApplyLightShadows();
        }

        private void Start()
        {
            ApplyLightShadows();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_targetCamera != null && _targetCamera.targetTexture == _sceneTexture)
            {
                _targetCamera.targetTexture = _previousTarget;
            }

            if (_overlayCanvas != null)
            {
                Destroy(_overlayCanvas.gameObject);
                _overlayCanvas = null;
                _overlayImage = null;
            }

            if (_presenterCamera != null)
            {
                Destroy(_presenterCamera.gameObject);
                _presenterCamera = null;
            }

            if (_blitMaterial != null)
            {
                Destroy(_blitMaterial);
                _blitMaterial = null;
            }

            if (_sceneTexture != null)
            {
                _sceneTexture.Release();
                Destroy(_sceneTexture);
                _sceneTexture = null;
            }

            RestoreRenderSettings();
        }

        private void OnValidate()
        {
            _retroWidth = Mathf.Max(160, _retroWidth);
            _retroHeight = Mathf.Max(90, _retroHeight);
        }

        #endregion

        #region Публичные операции

        /// <summary>Пересоздаёт ретро-текстуру и перепривязывает камеру (после смены разрешения).</summary>
        public void RebuildRenderTarget()
        {
            if (!_enableRetroOutput)
            {
                return;
            }

            ReleaseSceneTexture();
            CreateSceneTexture();
            CreatePresenterCamera();
            BindCameraToTexture();
            PushBlitParameters();
        }

        /// <summary>
        ///
        /// ВАЖНО: жёсткие тени НЕ включаются принудительно у каждой лампы.
        /// Один PointLight с тенями занимает сразу 6 слотов в Shadow Atlas, и при
        /// десятке точечных источников URP начинает делить разрешение
        /// («Reduced additional punctual light shadows resolution by 4 to make
        /// 20 shadow maps fit...»), а на поверхностях появляется чёрная «зебра»
        /// от Shadow Acne. Решение: точечные источники всегда без теней,
        /// а единственную теневую лампу выбирает PhaseLightingDirector.
        /// </summary>
        public void ApplyLightShadows()
        {
            if (!_forceHardShadows)
            {
                return;
            }

            QualitySettings.shadowResolution = _shadowResolution;

            if (!TryGetLights(out Light[] lights))
            {
                if (!_warnedAboutMissingLightShadow)
                {
                    _warnedAboutMissingLightShadow = true;
                    Debug.LogWarning("[WorldCrisis] Не удалось получить источники света для настройки теней.");
                }

                return;
            }

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null)
                {
                    continue;
                }

                // PointLight — всегда без теней: шесть карт теней на одну лампу.
                if (lights[i].type == LightType.Point)
                {
                    lights[i].shadows = LightShadows.None;
                }
            }

            WarnAboutSoftShadowsInPipelineAsset();
        }

        #endregion

        #region RenderTexture и вывод

        private void ResolveCamera()
        {
            if (_cameraOverride != null)
            {
                _targetCamera = _cameraOverride;
                return;
            }

            _targetCamera = Camera.main;

            if (_targetCamera == null)
            {
                Debug.LogWarning("[WorldCrisis] RetroRenderPipelineController: Camera.main не найдена, ретро-вывод отключён.");
                _enableRetroOutput = false;
            }
        }

        private void CreateSceneTexture()
        {
            ReleaseSceneTexture();

            _sceneTexture = new RenderTexture(_retroWidth, _retroHeight, RetroColorDepth, RenderTextureFormat.ARGB32)
            {
                name = "RetroSceneRT",
                antiAliasing = 1,
                filterMode = _pointFiltering ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                anisoLevel = 0
            };

            _sceneTexture.Create();
        }

        private void ReleaseSceneTexture()
        {
            if (_sceneTexture == null)
            {
                return;
            }

            _sceneTexture.Release();
            Destroy(_sceneTexture);
            _sceneTexture = null;
        }

        private void BindCameraToTexture()
        {
            if (_targetCamera == null || _sceneTexture == null)
            {
                return;
            }

            _previousTarget = _targetCamera.targetTexture;
            _targetCamera.targetTexture = _sceneTexture;

            // Сглаживание отключаем и на уровне камеры: сама текстура уже без MSAA,
            // но признак MSAA в URP всё равно участвует в настройке сглаживания.
            _targetCamera.allowMSAA = false;
            _targetCamera.allowHDR = true;
        }

        private void CreateOverlay()
        {
            if (_blitMaterial == null)
            {
                Shader shader = Shader.Find(BlitShaderName);

                if (shader == null)
                {
                    if (!_warnedAboutShader)
                    {
                        _warnedAboutShader = true;
                        Debug.LogWarning("[WorldCrisis] Не найден шейдер " + BlitShaderName
                                         + ", ретро-вывод отключён (сцена рендерится напрямую).");
                    }

                    _enableRetroOutput = false;
                    return;
                }

                _blitMaterial = new Material(shader)
                {
                    name = "Mat_RetroBunkerDither",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (_overlayCanvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("RetroOutputOverlay",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            _overlayCanvas = canvasObject.GetComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = _overlaySortingOrder;
            _overlayCanvas.pixelPerfect = false;
            _overlayCanvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 16f;

            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

            GameObject imageObject = new GameObject("RetroOutputImage", typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            imageObject.layer = canvasObject.layer;

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            _overlayImage = imageObject.GetComponent<RawImage>();
            _overlayImage.texture = _sceneTexture;
            _overlayImage.color = Color.white;
            _overlayImage.raycastTarget = false;
            _overlayImage.material = _blitMaterial;

            // 16:9 внутри любого окна: по краям видно очищенный презентером бетонный фон
            // #0D0E11 вместо растянутых пикселей. У RawImage нет preserveAspect,
            // поэтому пропорции держит AspectRatioFitter.
            AspectRatioFitter fitter = imageObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)_retroWidth / _retroHeight;

            canvasObject.hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>
        /// Камера-презентер. Нужна потому, что основная камера уводит рендер в RenderTexture:
        /// без второй камеры в бэкбуфер никто не пишет, и Unity показывает «No cameras rendering».
        /// Она рендерится последней, очищает экран в цвет бункера и рисует только слой UI,
        /// поэтому 3D-сцену не перерисовывает.
        /// </summary>
        private void CreatePresenterCamera()
        {
            if (_presenterCamera != null)
            {
                return;
            }

            GameObject presenterObject = new GameObject("RetroPresenterCamera");
            presenterObject.hideFlags = HideFlags.HideAndDontSave;

            _presenterCamera = presenterObject.AddComponent<Camera>();
            _presenterCamera.clearFlags = CameraClearFlags.SolidColor;
            _presenterCamera.backgroundColor = BunkerFogColor;
            _presenterCamera.cullingMask = 1 << uiLayerIndex;
            _presenterCamera.targetTexture = null;
            _presenterCamera.allowHDR = false;
            _presenterCamera.allowMSAA = false;
            _presenterCamera.useOcclusionCulling = false;
            _presenterCamera.depth = (_targetCamera != null ? _targetCamera.depth : 0f) + 1f;

            // AudioListener здесь намеренно нет: он уже висит на основной камере,
            // а второй выдал бы предупреждение Unity.
        }

        private void PushBlitParameters()
        {
            if (_blitMaterial == null)
            {
                return;
            }

            if (_sceneTexture != null)
            {
                _blitMaterial.SetTexture(RetroSourceId, _sceneTexture);
                _blitMaterial.SetVector(RetroSourceTexelSizeId, new Vector4(
                    1f / _retroWidth, 1f / _retroHeight, (float)_retroWidth / _retroHeight, _retroHeight));
            }


            // Карту глубины в этот путь отдать неоткуда: RawImage выполняется после рендера
            // камеры во внеэкранную текстуру, а не внутри прохода URP. Ключ не включаем —
            // шейдер остаётся на яркостном аутлайне, который даёт тот же силуэт.
            _blitMaterial.DisableKeyword("_RETRO_DEPTH");
            _blitMaterial.SetTexture(RetroDepthId, Texture2D.blackTexture);
        }

        #endregion

        #region RenderSettings и тени

        private void ApplyRenderSettings()
        {
            if (!_enableFog)
            {
                return;
            }

            _previousFogEnabled = RenderSettings.fog;
            _previousFogMode = RenderSettings.fogMode;
            _previousFogColor = RenderSettings.fogColor;
            _previousFogDensity = RenderSettings.fogDensity;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity;
        }

        private void RestoreRenderSettings()
        {
            if (!_enableFog)
            {
                return;
            }

            RenderSettings.fog = _previousFogEnabled;
            RenderSettings.fogMode = _previousFogMode;
            RenderSettings.fogColor = _previousFogColor;
            RenderSettings.fogDensity = _previousFogDensity;
        }

        private void WarnAboutSoftShadowsInPipelineAsset()
        {
            if (_warnedAboutSoftShadows)
            {
                return;
            }

            _warnedAboutSoftShadows = true;

            // supportsSoftShadows имеет internal setter, поэтому из рантайма его не выключить:
            // поле живёт в URP-ассете, который Unity собирает на старте. Мягкие тени
            // перебивают LightShadows.Hard, поэтому предупреждаем ровно один раз.
            if (GraphicsSettings.currentRenderPipeline is UniversalPipelineAsset pipeline
                && pipeline.supportsSoftShadows)
            {
                Debug.LogWarning(
                    "[WorldCrisis] Мягкие тени включены в URP-ассете — они перебивают "
                    + "LightShadows.Hard, и тени будут мягкими вопреки настройке. "
                    + "Выключите Soft Shadows в инспекторе ассета "
                    + "(Assets/Settings/PC_RPAsset.asset и/или Mobile_RPAsset.asset, "
                    + "Quality и Graphics). Из кода это не сделать: сеттер у "
                    + "UniversalRenderPipelineAsset.supportsSoftShadows внутренний.");
            }
        }

        #endregion

        #region Глобальные параметры материалов

        /// <summary>
        /// Расставляет глобальные параметры внешнего вида для CrispRetroLit и WireframeHologram.
        /// Внешний вид — свойство сцены, поэтому он не плодит свойства в каждом материале
        /// и не ломает SRP Batcher.
        /// </summary>
        public void ApplyRetroGlobals()
        {
            if (!_pushRetroGlobals)
            {
                return;
            }

            Shader.SetGlobalVector(RetroDirtTilingId,
                new Vector4(_dirtTiling.x, _dirtTiling.y, _emissionBreakupGrid.x, _emissionBreakupGrid.y));
            Shader.SetGlobalVector(RetroDirtId, new Vector4(_dirtStrength, _dirtRoughnessKill, 0f, 0f));
            Shader.SetGlobalVector(RetroSpecId,
                new Vector4(_specContrast, _smoothnessFloor, _emissionBoost, _emissionBreakup));
            Shader.SetGlobalVector(RetroRimId, new Vector4(_rimStrength, _rimPower, 1f, 0f));
            Shader.SetGlobalColor(RetroRimColorId, _rimColor);
            Shader.SetGlobalColor(RetroEmissionColorId, _globalEmissionTint);
            Shader.SetGlobalFloat(RetroAlbedoGainId, _albedoGain);
            Shader.SetGlobalTexture(RetroDirtMapId, GetRetroGrungeTexture());
        }

        private static Texture2D s_GrungeTexture;

        /// <summary>
        /// Карта грязи и царапин для всех материалов сцены: R — маска грязи,
        /// G — мелкий шум (мерцание голограмм), B — крупные пятна.
        /// Генерируется один раз и живёт до выгрузки сцены.
        /// </summary>
        private static Texture2D GetRetroGrungeTexture()
        {
            if (s_GrungeTexture == null)
            {
                s_GrungeTexture = RetroPbrTextureGenerator.GetRetroGrunge(512).Albedo;
            }

            return s_GrungeTexture;
        }

        #endregion

        #region Сцены и утилиты

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_enableRetroOutput && (_sceneTexture == null || !_sceneTexture.IsCreated()))
            {
                RebuildRenderTarget();
            }

            ApplyRenderSettings();
            ApplyLightShadows();
        }

        private static bool TryGetLights(out Light[] lights)
        {
            // FindObjectsOfType и перегрузка с FindObjectsSortMode помечены устаревшими
            // в Unity 6 (warning CS0618), поэтому берём актуальную версию без сортировки.
            lights = FindObjectsByType<Light>();

            if (lights != null && lights.Length > 0)
            {
                return true;
            }

            lights = null;
            return false;
        }

        #endregion
    }
}