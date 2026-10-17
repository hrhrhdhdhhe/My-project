// World Crisis | Слой: Core/Visuals | Ответственный: OpenCode
// Режиссёр фазового освещения и перемещения камеры. Единственный владелец позиции
// основной камеры и световых сценариев матча.
//
// Единственный источник истины по фазе — MatchManager (AGENTS.md §6): директор
// подписывается на OnPhaseChanged и ничего не знает про UI. Ручное переключение
// обзора по Tab не трогает фазу — это только смена точки обзора.

using System.Collections.Generic;
using UnityEngine;
// Ввод по AGENTS.md §4: Input System с откатом на legacy UnityEngine.Input.
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WorldCrisis
{
    /// <summary>
    /// Переключает камеру между личным бункером и Круглым залом саммита,
    /// прогрывает световые сценарии фаз и вращает красные маяки Часа Расплаты.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("World Crisis/Phase Lighting Director")]
    public class PhaseLightingDirector : MonoBehaviour
    {
        #region Константы Art Bible

        /// <summary>Тёплый свет настольной лампы в бункере.</summary>
        private static readonly Color DeskLampColor = new Color(1f, 0.878f, 0.698f, 1f);

        /// <summary>Янтарное свечение ЭЛТ-монитора.</summary>
        private static readonly Color CrtAmberColor = new Color(1f, 0.69f, 0f, 1f);

        /// <summary>Зелёный сигнал ПВО и подсветки городов.</summary>
        private static readonly Color AegisGreenColor = new Color(0f, 0.902f, 0.463f, 1f);

        /// <summary>Холодный прожектор зала саммита #D8E8EC.</summary>
        private static readonly Color SummitKeyLightColor = new Color(0.847f, 0.91f, 0.925f, 1f);

        /// <summary>Бирюзовый свет голографического глобуса #00F0C0.</summary>
        private static readonly Color GlobeTurquoiseColor = new Color(0f, 0.941f, 0.753f, 1f);

        /// <summary>Красный свет тревоги и пусков.</summary>
        private static readonly Color AlarmRedColor = new Color(1f, 0.09f, 0.267f, 1f);

        /// <summary>Скорость вращения маяков Часа Расплаты (оборотов в секунду).</summary>
        private const float BeaconRevolutionsPerSecond = 0.28f;

        /// <summary>
        /// Плотность тумана. Раньше 0.04 — в пределах кабины глубиной 2-4 м туман
        /// съедал контраст и закрашивал стол; теперь он работает только в глубокой
        /// шахте зала саммита.
        /// </summary>
        public const float FogDensity = 0.018f;

        #endregion

        #region Сериализованные параметры

        [Header("Обзор")]
        [SerializeField] private bool _allowManualToggle = true;

        [Header("Интенсивности")]
        // Лампа в плафоне над столом: тёплый верхний спот, он и есть «настольная лампа»
        // из Art Bible. Значения подобраны так, чтобы выбить светом тёплую ржавую
        // столешницу и корпуса приборов, а не оставить их синими.
        [SerializeField] private float _bunkerLampIntensity = 8f;
        [SerializeField] private float _bunkerFillIntensity = 3.2f;
        [SerializeField] private float _crtIntensity = 1.5f;
        [SerializeField] private float _launchButtonIntensity = 1.4f;
        [SerializeField] private float _summitKeyIntensity = 10f;
        [SerializeField] private float _wellLightIntensity = 3.5f;
        [SerializeField] private float _summitFillIntensity = 3.5f;
        [SerializeField] private float _beaconIntensity = 7.5f;
        [SerializeField] private float _ambientBunker = 0.55f;
        [SerializeField] private float _ambientSummit = 0.42f;
        [SerializeField] private float _ambientAlarm = 0.3f;

        #endregion

        #region Состояние

        private BunkerWorldBuilder _world;
        private Camera _camera;
        private MatchManager _match;

        private bool _warnedAboutCamera;

        private Vector3 _cameraPosition;
        private Quaternion _cameraRotation;

        private Vector3 _bunkerPosition;
        private Vector3 _bunkerLook;
        private float _bunkerFov = BunkerWorldBuilder.BunkerFieldOfView;

        private Vector3 _summitPosition;
        private Quaternion _summitRotation = SummitLayout.PlayerEyeRotation;
        private float _summitFov = BunkerWorldBuilder.SummitFieldOfView;

        private GamePhase _phase = GamePhase.Lobby;

        private GameObject _rigRoot;
        private Light _bunkerDeskLamp;
        private Light _bunkerFill;
        private Light _bunkerCrtGlow;
        private Light _bunkerLaunchGlow;
        private Light _bunkerDioramaFill;
        private Light _summitKeyLight;
        private Light _summitWellLight;

        private readonly Light[] _summitFills = new Light[3];

        /// <summary>
        /// Три заполняющих точечных лампы над головами диктаторов: без них пять фигур
        /// напротив игрока тонут в темноте. Без теней — см. ApplyShadowPolicy.
        /// </summary>
        private Light _summitFillA;
        private Light _summitFillB;
        private Light _summitFillC;
        private readonly Light[] _beacons = new Light[2];
        private Transform _beaconPivotA;
        private Transform _beaconPivotB;
        private Color _ambientBase;

        /// <summary>Текущая фаза, по которой выбран световой сценарий.</summary>
        public GamePhase CurrentPhase
        {
            get { return _phase; }
        }

        /// <summary>true, если камера сейчас в Круглом зале саммита.</summary>
        public bool IsSummitView
        {
            get { return _summitViewActive; }
        }

        private bool _summitViewActive;

        #endregion

        #region Unity

        private void Awake()
        {
            // Первым делом гасим наследие шаблона сцены: солнце из SampleScene светит
            // сквозь бетон бункера, а Global Volume поверх ретро-постобработки выбеливает
            // картинку. Операция идемпотентна — сборщик мира делает то же самое.
            BunkerWorldBuilder.DisableDefaultSceneLightsAndVolumes();

            _world = FindAnyObjectByType<BunkerWorldBuilder>();
            _match = MatchManager.Instance;

            _camera = Camera.main;

            if (_camera == null)
            {
                _camera = FindAnyObjectByType<Camera>();
            }

            if (_camera == null)
            {
                if (!_warnedAboutCamera)
                {
                    _warnedAboutCamera = true;
                    Debug.LogWarning("[WorldCrisis] PhaseLightingDirector: камера не найдена, "
                                     + "переключение локаций отключено.");
                }

                return;
            }

            _ambientBase = RenderSettings.ambientLight;
            ApplyAmbientAndFog();
            CaptureRetroGlobals();
            BuildLightRig();
            ApplyShadowPolicy();
            CacheViewposes();

            // Стартуем в бункере и сразу сажаем камеру в её точку обзора: первый кадр
            // не должен показывать пустую сцену из точки (0,0,0).
            _cameraPosition = _bunkerPosition;
            _cameraRotation = Quaternion.LookRotation(_bunkerLook - _bunkerPosition, Vector3.up);
            ApplyCameraImmediate();
        }

        private void OnEnable()
        {
            if (_match != null)
            {
                _match.OnPhaseChanged += OnPhaseChanged;
                ApplyPhase(_match.Phase, true);
            }
        }

        private void OnDisable()
        {
            if (_match != null)
            {
                _match.OnPhaseChanged -= OnPhaseChanged;
            }
        }

        private void Update()
        {
            if (_camera == null)
            {
                return;
            }

            HandleManualToggle();
            UpdateBeacons();
        }

        /// <summary>
        /// Свет ставится ПО ФАКТИЧЕСКОМУ ПОЛОЖЕНИЮ КАМЕРЫ, а не по фазе. Пока камера
        /// перелетает через всю сцену, а лампы уже погашены сценарием другой комнаты,
        /// в кадре ровно то самое «чёрное окно саммита» из скриншотов. Проверка идёт
        /// по мировой Z: бункер лежит около нуля, зал саммита — около сорока.
        /// </summary>
        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            if (_camera.transform.position.z > SummitRoomPlaneZ != IsCameraInSummit)
            {
                _summitViewActive = _camera.transform.position.z > SummitRoomPlaneZ;
                ApplyLightScenario(_phase);
                ApplyShadowPolicy();
            }
        }

        /// <summary>
        /// Граница между комнатами по мировой Z: всё, что дальше, — зал саммита.
        /// Взаимодействие со светом идёт именно в LateUpdate, чтобы проверка шла
        /// после всех перемещений камеры за кадр.
        /// </summary>
        private const float SummitRoomPlaneZ = 20f;

        private void OnValidate()
        {
            _bunkerLampIntensity = Mathf.Max(0f, _bunkerLampIntensity);
            _summitKeyIntensity = Mathf.Max(0f, _summitKeyIntensity);
        }

        #endregion

        #region Точки обзора

        private void CacheViewposes()
        {
            _bunkerPosition = BunkerWorldBuilder.BunkerEyePosition;
            _bunkerLook = BunkerWorldBuilder.BunkerLookTarget;
            _bunkerFov = BunkerWorldBuilder.BunkerFieldOfView;

            // Позиция и поворот камеры в зале саммита — ЖЁСТКИЕ числа из SummitLayout:
            // (0, 1.42, 37.95) и Euler(12, 0, 0). Никакого чтения мест, углов и радиусов:
            // производная от угла места однажды уводила камеру в ржавую стену шахты
            // (в инспекторе это выглядело как X = −8.523).
            _summitPosition = BunkerWorldBuilder.SummitEyePosition;
            _summitRotation = BunkerWorldBuilder.SummitEyeRotation;
            _summitFov = BunkerWorldBuilder.SummitFieldOfView;
        }

        private void ApplyCameraImmediate()
        {
            if (_camera == null)
            {
                return;
            }

            _camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation);
            _camera.fieldOfView = Mathf.Lerp(_bunkerFov, _summitFov, _summitViewActive ? 1f : 0f);
        }

        private Vector3 TargetPosition()
        {
            return _summitViewActive ? _summitPosition : _bunkerPosition;
        }

        /// <summary>
        /// Целевой поворот камеры. В зале саммита он ЖЁСТКИЙ — Quaternion.Euler(12, 0, 0):
        /// «смотреть в центр стола» через LookRotation работало, пока стол стоял ровно,
        /// и уводило камеру вбок при любом смещении точки обзора.
        /// </summary>
        private Quaternion TargetRotation()
        {
            if (_summitViewActive)
            {
                return _summitRotation;
            }

            return Quaternion.LookRotation(_bunkerLook - _bunkerPosition, Vector3.up);
        }

        private float TargetFov()
        {
            return _summitViewActive ? _summitFov : _bunkerFov;
        }

        /// <summary>
        /// Переносит камеру в целевую локацию МГНОВЕННО. Был «подъём лифта» с дугой
        /// вверх и пролётом через 40 метров сцены: во время пролёта видно пустоту между
        /// локациями, и на фазе «Саммит ООН» это и давало чёрный экран. Параметр
        /// instant сохранён, чтобы вызовы фаз выглядели так же, но игнорируется.
        /// </summary>
        public void GoToView(bool summit, bool instant)
        {
            // Рабочее место перечитываем каждый раз: игрок может сменить державу в лобби,
            // и тогда за столом должна оказаться именно его страна.
            CacheViewposes();

            _summitViewActive = summit;

            // Смена комнаты пересчитывает и свет, и то, какая лампа отбрасывает тени.
            ApplyLightScenario(_phase);
            ApplyShadowPolicy();

            if (_camera == null)
            {
                return;
            }

            _cameraPosition = TargetPosition();
            _cameraRotation = TargetRotation();
            ApplyCameraImmediate();
        }

        private void HandleManualToggle()
        {
            if (!_allowManualToggle || !ToggleViewPressed())
            {
                return;
            }

            // Ручное переключение — это только смена точки обзора: фаза матча не меняется,
            // световой сценарий остаётся прежним. Автоматика вернёт камеру в нужную локацию
            // на ближайшей смене фазы — осмотр «между фазами» не должен ломать драматургию.
            GoToView(!_summitViewActive, true);
        }

        /// <summary>Tab — осмотр обеих зон. Клавиша фиксированная по Art Bible.</summary>
        private static bool ToggleViewPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }

        #endregion

        #region Световой сценарий

        private void OnPhaseChanged(GamePhase phase)
        {
            // Смена фазы всегда возвращает камеру в нужную локацию, даже если игрок
            // только что осматривал другую по Tab: иначе фаза «Саммит» шла бы из бункера.
            ApplyPhase(phase, false);
        }

        /// <summary>Применяет световой сценарий фазы и переносит камеру в её локацию.</summary>
        public void ApplyPhase(GamePhase phase, bool instant)
        {
            _phase = phase;
            GoToView(phaseUsesSummit(phase), instant);
            ApplyLightScenario(phase);
            ApplyRetroGlobals();
        }

        /// <summary>
        /// В каких фазах игрок находится в зале саммита. BunkerDiplomacy и OrderInput —
        /// личный бункер, SummitUN — зал, Reckoning («Час Расплаты») — снова зал,
        /// потому что там вскрываются пакты и летят ракеты.
        /// </summary>
        private static bool phaseUsesSummit(GamePhase phase)
        {
            return phase == GamePhase.SummitUN || phase == GamePhase.Reckoning;
        }

        private void ApplyLightScenario(GamePhase phase)
        {
            if (_bunkerDeskLamp == null)
            {
                return;
            }

            // Свет ставится по КОМНАТЕ, в которой камера находится фактически, а не по фазе.
            // Иначе в фазе «Лобби» после нажатия Tab зал саммита остаётся абсолютно
            // чёрным: лампы зала погашены сценарием фазы, а камера в нём.
            bool inSummit = IsCameraInSummit;
            bool alarm = phase == GamePhase.Reckoning;

            if (inSummit)
            {
                SetBunkerLights(0f, 0f, 0f, 0f);
                SetSummitLights(_summitKeyIntensity, _wellLightIntensity, _summitFillIntensity);
                SetBeaconsActive(alarm);
                RenderSettings.ambientLight = Color.Lerp(_ambientBase,
                    alarm ? AlarmRedColor : SummitKeyLightColor, alarm ? _ambientAlarm : _ambientSummit);
                return;
            }

            SetBunkerLights(_bunkerLampIntensity, _bunkerFillIntensity, _crtIntensity, _launchButtonIntensity);
            SetSummitLights(0f, 0f, 0f);
            SetBeaconsActive(false);
            RenderSettings.ambientLight = Color.Lerp(_ambientBase, DeskLampColor, _ambientBunker);
        }

        /// <summary>true, если камера сейчас физически находится в зале саммита.</summary>
        public bool IsCameraInSummit
        {
            get { return _summitViewActive; }
        }

        /// <summary>
        /// Тени включены ровно у одного источника — верхнего прожектора активной комнаты.
        /// Всё остальное светит без теней: десяток теневых ламп переполняет Shadow Atlas
        /// и даёт чёрную «зебру» от Shadow Acne.
        /// </summary>
        private void ApplyShadowPolicy()
        {
            bool inSummit = IsCameraInSummit;

            SetShadowCasting(_bunkerDeskLamp, !inSummit);
            SetShadowCasting(_summitKeyLight, inSummit);
            SetShadowCasting(_summitFillA, false);
            SetShadowCasting(_summitFillB, false);
            SetShadowCasting(_summitFillC, false);
        }

        private static void SetShadowCasting(Light light, bool allow)
        {
            if (light == null)
            {
                return;
            }

            // PointLight с тенями занимает 6 слотов атласа, спот — один.
            if (light.type == LightType.Point)
            {
                light.shadows = LightShadows.None;
                return;
            }

            light.shadows = allow ? LightShadows.Hard : LightShadows.None;

            if (!allow)
            {
                return;
            }

            // Смещения подобраны так, чтобы убрать Shadow Acne на стенах и столе.
            light.shadowBias = 0.06f;
            light.shadowNormalBias = 0.45f;
            light.shadowNearPlane = 0.25f;
        }

        private void SetBunkerLights(float lamp, float fill, float crt, float launch)
        {
            SetLight(_bunkerDeskLamp, lamp);
            SetLight(_bunkerFill, fill);
            SetLight(_bunkerCrtGlow, crt);
            SetLight(_bunkerLaunchGlow, launch);
            SetLight(_bunkerDioramaFill, fill * 0.4f);
        }

        private void SetSummitLights(float key, float well, float fill)
        {
            SetLight(_summitKeyLight, key);
            SetLight(_summitWellLight, well);
            SetLight(_summitFillA, fill);
            SetLight(_summitFillB, fill);
            SetLight(_summitFillC, fill);
        }

        private static void SetLight(Light light, float intensity)
        {
            if (light != null)
            {
                light.intensity = intensity;
                light.enabled = intensity > 0.01f;
            }
        }

        private void SetBeaconsActive(bool active)
        {
            for (int i = 0; i < _beacons.Length; i++)
            {
                SetLight(_beacons[i], active ? _beaconIntensity : 0f);
            }
        }

        private void UpdateBeacons()
        {
            if (_beaconPivotA == null || _beaconPivotB == null)
            {
                return;
            }

            float angle = Time.time * BeaconRevolutionsPerSecond * Mathf.PI * 2f;

            // Два противоположных маяка: один идёт по часовой, второй против, поэтому
            // по залу бегут длинные расходящиеся тени.
            _beaconPivotA.localRotation = Quaternion.Euler(0f, angle, 0f);
            _beaconPivotB.localRotation = Quaternion.Euler(0f, -angle, 0f);
        }

        /// <summary>
        /// На тревожной фазе поднимает общее свечение материалов: глобалы UnityPerRetro
        /// читаются CrispRetroLit, поэтому одного света не хватает — нужен и материал.
        /// Базовые значения снимаются с глобалов, которые выставил RetroRenderPipelineController:
        /// директор меняет только тревожные слагаемые и не затирает остальные параметры.
        /// </summary>
        private void ApplyRetroGlobals()
        {
            bool alarm = _phase == GamePhase.Reckoning;

            if (alarm == _alarmGlobalsApplied)
            {
                return;
            }

            // Если контроллер ещё не выставил глобалы, нечего модифицировать.
            if (_retroSpecBaseline == Vector4.zero)
            {
                return;
            }

            _alarmGlobalsApplied = alarm;

            Shader.SetGlobalVector(RetroSpecId, new Vector4(
                _retroSpecBaseline.x,
                _retroSpecBaseline.y,
                alarm ? _alarmEmissionBoost : _retroSpecBaseline.z,
                _retroSpecBaseline.w));

            Shader.SetGlobalVector(RetroRimId, new Vector4(
                alarm ? _alarmRimStrength : _retroRimBaseline.x,
                _retroRimBaseline.y,
                _retroRimBaseline.z,
                _retroRimBaseline.w));

            Shader.SetGlobalColor(RetroRimColorId, alarm ? AlarmRedColor : _retroRimBaselineColor);
        }

        private bool _alarmGlobalsApplied;
        private Vector4 _retroSpecBaseline;
        private Vector4 _retroRimBaseline;
        private Color _retroRimBaselineColor;

        [SerializeField] private float _alarmEmissionBoost = 4.2f;
        [SerializeField] private float _alarmRimStrength = 1.9f;

        private static readonly int RetroSpecId = Shader.PropertyToID("_RetroSpec");
        private static readonly int RetroRimId = Shader.PropertyToID("_RetroRim");
        private static readonly int RetroRimColorId = Shader.PropertyToID("_RetroRimColor");

        #endregion

        #region Создание света

        /// <summary>Z дальней стены бункера, читается из планировки сборщика мира.</summary>
        private static float BunkerBackWallZ
        {
            get { return BunkerWorldBuilder.BunkerBackWallZ; }
        }

        /// <summary>
        /// Трёхточечный ambient вместо однотонного: без него кабина уходила в синий
        /// холод и не читались ни трубы, ни кабели на заднем плане.
        /// </summary>
        private void ApplyAmbientAndFog()
        {
            // Trilight, а не «Trilinear»: в Unity так называется трёхзонный ambient
            // (небо / экватор / земля). Именно он и нужен для тёплой заливки кабины.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.19f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.14f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.07f, 0.06f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.35f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = FogDensity;
        }

        /// <summary>Снимает текущие глобалы внешнего вида, чтобы не затирать настройки контроллера.</summary>
        private void CaptureRetroGlobals()
        {
            _retroSpecBaseline = Shader.GetGlobalVector(RetroSpecId);
            _retroRimBaseline = Shader.GetGlobalVector(RetroRimId);
            _retroRimBaselineColor = Shader.GetGlobalColor(RetroRimColorId);
        }

        /// <summary>
        /// Создаёт весь свет сцены. Динамический свет не сериализуется в сцене, поэтому
        /// он собирается кодом и всегда соответствует текущей фазе.
        /// </summary>
        private void BuildLightRig()
        {
            _rigRoot = new GameObject("PhaseLights");
            _rigRoot.transform.SetParent(transform, false);
            _rigRoot.hideFlags = HideFlags.DontSave;

            // --- Бункер ---
            // Верхний спот в потолочном плафоне над центром стола: #FFE0B2, широкий
            // тёплый конус — тот самый световой «зонтик» с концепт-артов. Стоит ровно
            // в узле Bunker_Ceiling_Lamp, поэтому тень от приборов идёт на столешницу.
            _bunkerDeskLamp = CreateSpot("Bunker_DeskLamp",
                SummitLayout.Bunker.CeilingLampPosition, new Vector3(0f, -1f, 0f), new Vector2(96f, 42f),
                DeskLampColor, _bunkerLampIntensity, 5.5f);

            // Заполняющий тёплый точечный свет над приборами, без теней.
            _bunkerFill = CreatePoint("Bunker_FillLight",
                new Vector3(0f, 1.3f, 0.45f), new Color(1f, 0.722f, 0.424f), _bunkerFillIntensity, 3.2f);

            // Зелёное свечение экрана монитора CITY STATUS.
            _bunkerCrtGlow = CreatePoint("Bunker_CrtGlow",
                new Vector3(0.64f, 0.94f, 0.42f), AegisGreenColor, _crtIntensity, 1.4f);

            // Красное свечение кнопки пуска на ядерном пульте.
            _bunkerLaunchGlow = CreatePoint("Bunker_LaunchGlow",
                new Vector3(0.18f, 0.86f, 0.5f), AlarmRedColor, _launchButtonIntensity, 1.2f);

            // Зелёная подсветка диорам городов на табло статуса за столом.
            _bunkerDioramaFill = CreateSpot("Bunker_DioramaFill",
                new Vector3(-1.35f, 1.45f, BunkerBackWallZ - 0.45f), new Vector3(0f, 0f, -1f), new Vector2(80f, 90f),
                AegisGreenColor, _bunkerFillIntensity * 0.5f, 3f);

            // --- Саммит ---
            Vector3 summit = BunkerWorldBuilder.SummitCenter;

            // Верхний прожектор под потолком зала: холодный #D8E8EC вертикально вниз.
            _summitKeyLight = CreateSpot("Summit_KeyLight",
                summit + new Vector3(0f, 2.55f, 0f), new Vector3(0f, -1f, 0f), new Vector2(110f, 40f),
                SummitKeyLightColor, _summitKeyIntensity, 9f);

            // Подсветка из колодца карты мира — снизу вверх бирюзовая.
            _summitWellLight = CreatePoint("Summit_WellLight",
                summit + new Vector3(0f, 0.92f, 0f), GlobeTurquoiseColor, _wellLightIntensity, 4f);

            // Три тёплых заполняющих лампы над местами ИИ-диктаторов. Именно они делают
            // пять фигур напротив игрока читаемыми: одного верхнего прожектора мало.
            for (int i = 0; i < _summitFills.Length; i++)
            {
                Vector3 seat = BunkerMeshFactory.GetSummitSeatPosition(i + 1);
                _summitFills[i] = CreatePoint("Summit_FillLight_" + i,
                    new Vector3(seat.x, 2.15f, BunkerWorldBuilder.SummitCenter.z + seat.z), DeskLampColor,
                    _summitFillIntensity, 4.5f);
            }

            _summitFillA = _summitFills[0];
            _summitFillB = _summitFills[1];
            _summitFillC = _summitFills[2];

            // --- Час Расплаты: два противоположных вращающихся маяка ---
            Transform beaconRig = new GameObject("Beacon_Rig").transform;
            beaconRig.SetParent(_rigRoot.transform, false);

            // Маяки висят под потолком зала (2.70 м), а не в пустоте над старой шахтой 9.2 м.
            Vector3 beaconAnchor = summit + new Vector3(0f, BunkerWorldBuilder.SummitCeilingHeight - 0.05f, 0f);
            beaconRig.localPosition = beaconAnchor;

            _beaconPivotA = MakeBeaconPivot(beaconRig, 0f);
            _beaconPivotB = MakeBeaconPivot(beaconRig, 180f);

            // Пока маяки выключены — их сценарий включает только Час Расплаты.
            SetBeaconsActive(false);
        }

        private Transform MakeBeaconPivot(Transform parent, float yaw)
        {
            GameObject pivotObject = new GameObject("Beacon_Pivot_" + (int)yaw);
            pivotObject.transform.SetParent(parent, false);

            // Стойка от потолка к маяку. Материал берём у сборщика мира: свой кэш
            // означал бы второй экземпляр того же материала в памяти.
            MeshBuilder rig = new MeshBuilder();
            rig.Add(MeshPrimitives.Prism(0.05f, 0.05f, 1.6f, 8, 0.008f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.8f, 0f), Vector3.zero, Vector3.one),
                BunkerWorldBuilder.SharedIron, MeshUvMode.BoxProject);
            SealInto(pivotObject.transform, "Beacon_Stand", rig);

            // Голова прожектора смотрит вниз внутрь зала.
            Light beacon = CreateSpotInto("Beacon_Light", pivotObject.transform,
                new Vector3(0f, -0.15f, 0f), new Vector3(0f, -1f, 0f), new Vector3(34f, 62f),
                AlarmRedColor, 0f, 18f);

            _beacons[(int)(yaw / 180f)] = beacon;

            pivotObject.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return pivotObject.transform;
        }

        private Light CreateSpot(string nodeName, Vector3 position, Vector3 direction, Vector2 cone,
            Color color, float intensity, float range)
        {
            GameObject node = new GameObject(nodeName);
            node.transform.SetParent(_rigRoot.transform, false);
            node.transform.localPosition = position;

            return CreateSpotInto(nodeName + "_Light", node.transform, Vector3.zero, direction, cone, color,
                intensity, range);
        }

        /// <summary>
        /// Точечный источник без направления: подсветка столешницы, экрана монитора
        /// и кнопки пуска. Именно они отвечают за «сочный» читаемый кадр бункера.
        /// </summary>
        private Light CreatePoint(string nodeName, Vector3 position, Color color, float intensity, float range)
        {
            GameObject node = new GameObject(nodeName);
            node.transform.SetParent(_rigRoot.transform, false);
            node.transform.localPosition = position;

            Light light = node.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.enabled = intensity > 0.01f;
            return light;
        }

        private static Light CreateSpotInto(string nodeName, Transform parent, Vector3 position, Vector3 direction,
            Vector2 cone, Color color, float intensity, float range)
        {
            GameObject node = new GameObject(nodeName);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = position;
            node.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            Light light = node.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.spotAngle = cone.x;
            light.innerSpotAngle = cone.y;
            light.range = range;

            // По умолчанию спот теней не отбрасывает; единственную теневую лампу
            // назначает ApplyShadowPolicy — она знает, в какой комнате сейчас камера.
            light.shadows = LightShadows.None;
            light.enabled = intensity > 0.01f;
            return light;
        }

        private void SealInto(Transform parent, string nodeName, MeshBuilder builder)
        {
            GameObject node = new GameObject(nodeName);
            node.transform.SetParent(parent, false);

            Mesh mesh = builder.Build(nodeName + "_Mesh", true);

            if (mesh.vertexCount == 0)
            {
                return;
            }

            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.AddComponent<MeshRenderer>().sharedMaterials = builder.GetMaterials();
        }

        #endregion
    }
}
