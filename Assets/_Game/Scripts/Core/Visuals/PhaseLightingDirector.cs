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

        /// <summary>Холодный прожектор зала саммита.</summary>
        private static readonly Color SummitKeyLightColor = new Color(0.72f, 0.83f, 0.95f, 1f);

        /// <summary>Красный свет тревоги и пусков.</summary>
        private static readonly Color AlarmRedColor = new Color(1f, 0.09f, 0.267f, 1f);

        /// <summary>Скорость вращения маяков Часа Расплаты (оборотов в секунду).</summary>
        private const float BeaconRevolutionsPerSecond = 0.28f;

        /// <summary>Длительность «подъёма лифта» при переносе в зал саммита.</summary>
        private const float LiftTransitionSeconds = 1.1f;

        /// <summary>Высота, на которую камера поднимается при переносе в зал саммита.</summary>
        private const float LiftRiseHeight = 2.2f;

        #endregion

        #region Сериализованные параметры

        [Header("Обзор")]
        [SerializeField] private bool _allowManualToggle = true;

        [Tooltip("Скорость перехода между локациями, м/с и рад/с.")]
        [SerializeField] private float _moveSpeed = 6f;

        [SerializeField] private float _turnSpeed = 240f;

        [Header("Интенсивности")]
        [SerializeField] private float _bunkerLampIntensity = 3.2f;
        [SerializeField] private float _crtIntensity = 2.4f;
        [SerializeField] private float _summitKeyIntensity = 4.5f;
        [SerializeField] private float _wellLightIntensity = 2.2f;
        [SerializeField] private float _beaconIntensity = 7.5f;
        [SerializeField] private float _ambientBunker = 0.14f;
        [SerializeField] private float _ambientSummit = 0.1f;
        [SerializeField] private float _ambientAlarm = 0.06f;

        #endregion

        #region Состояние

        private BunkerWorldBuilder _world;
        private Camera _camera;
        private MatchManager _match;

        private bool _warnedAboutCamera;

        private Vector3 _cameraPosition;
        private Quaternion _cameraRotation;
        private Vector3 _positionFrom;
        private Vector3 _positionTo;
        private Quaternion _rotationFrom;
        private Quaternion _rotationTo;
        private float _transitionLeft;
        private float _transitionTotal;

        private Vector3 _bunkerPosition;
        private Vector3 _bunkerLook;
        private float _bunkerFov = BunkerWorldBuilder.BunkerFieldOfView;

        private Vector3 _summitPosition;
        private Vector3 _summitLook;
        private float _summitFov = BunkerWorldBuilder.SummitFieldOfView;

        private GamePhase _phase = GamePhase.Lobby;

        private GameObject _rigRoot;
        private Light _bunkerDeskLamp;
        private Light _bunkerCrtGlow;
        private Light _bunkerDioramaFill;
        private Light _summitKeyLight;
        private Light _summitWellLight;
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

        private Transform _summitPlayerSeat;
        private bool _summitViewActive;

        #endregion

        #region Unity

        private void Awake()
        {
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
            CaptureRetroGlobals();
            BuildLightRig();
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
            UpdateTransition();
            UpdateBeacons();
        }

        private void OnValidate()
        {
            _moveSpeed = Mathf.Max(0.5f, _moveSpeed);
            _turnSpeed = Mathf.Max(10f, _turnSpeed);
        }

        #endregion

        #region Точки обзора

        private void CacheViewposes()
        {
            _bunkerPosition = BunkerWorldBuilder.BunkerEyePosition;
            _bunkerLook = BunkerWorldBuilder.BunkerLookTarget;
            _bunkerFov = BunkerWorldBuilder.BunkerFieldOfView;

            ResolvePlayerSeat();

            _summitPosition = _world != null
                ? _world.GetSummitEyePosition(_summitPlayerSeat)
                : BunkerWorldBuilder.SummitCenter + Vector3.up * BunkerWorldBuilder.SummitEyeHeight;

            // Взгляд игрока за столом — в центр стола, с небольшим наклоном вниз.
            _summitLook = _world != null
                ? BunkerWorldBuilder.SummitTableCenter
                : BunkerWorldBuilder.SummitCenter + Vector3.up;

            _summitFov = BunkerWorldBuilder.SummitFieldOfView;
        }

        /// <summary>Выбирает рабочее место локального игрока среди шести слотов зала.</summary>
        private void ResolvePlayerSeat()
        {
            if (_world == null)
            {
                _summitPlayerSeat = null;
                return;
            }

            IReadOnlyList<Transform> seats = _world.SummitSeats;

            if (seats == null || seats.Count == 0)
            {
                _summitPlayerSeat = null;
                return;
            }

            _summitPlayerSeat = null;

            if (_match != null)
            {
                CountryId local = _match.LocalPlayer;

                for (int i = 0; i < seats.Count && i < GameDefinitions.AllCountryIds.Length; i++)
                {
                    if (GameDefinitions.AllCountryIds[i] == local && seats[i] != null)
                    {
                        _summitPlayerSeat = seats[i];
                        break;
                    }
                }
            }

            if (_summitPlayerSeat == null)
            {
                _summitPlayerSeat = seats[0];
            }
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

        private void UpdateTransition()
        {
            if (_transitionLeft > 0f)
            {
                _transitionLeft -= Time.deltaTime;

                float t = _transitionTotal > 0f
                    ? Mathf.Clamp01(1f - _transitionLeft / _transitionTotal)
                    : 1f;

                // Плавный ease-out: «подъём лифта» затухает, а не останавливается рывком.
                t = 1f - (1f - t) * (1f - t) * (1f - t);

                Vector3 position = Vector3.Lerp(_positionFrom, _positionTo, t);

                // Небольшая дуга вверх по ходу перехода — ощущение подъёма лифта.
                position.y += Mathf.Sin(t * Mathf.PI) * LiftRiseHeight;

                _cameraPosition = position;
                _cameraRotation = Quaternion.Slerp(_rotationFrom, _rotationTo, t);

                if (_camera != null)
                {
                    _camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation);
                    _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, TargetFov(), Time.deltaTime * 4f);
                }

                if (_transitionLeft <= 0f)
                {
                    _transitionLeft = 0f;
                }

                return;
            }

            // Покой: мягко держим целевую позицию, чтобы дыхание камеры не ломало кадр.
            if (_camera != null)
            {
                _camera.transform.position = Vector3.Lerp(_camera.transform.position, TargetPosition(),
                    1f - Mathf.Exp(-_moveSpeed * Time.deltaTime));

                Quaternion wanted = Quaternion.LookRotation(TargetLook() - TargetPosition(), Vector3.up);
                _camera.transform.rotation = Quaternion.RotateTowards(_camera.transform.rotation, wanted,
                    _turnSpeed * Time.deltaTime);
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, TargetFov(), 1f - Mathf.Exp(-6f * Time.deltaTime));
            }
        }

        private Vector3 TargetPosition()
        {
            return _summitViewActive ? _summitPosition : _bunkerPosition;
        }

        private Vector3 TargetLook()
        {
            return _summitViewActive ? _summitLook : _bunkerLook;
        }

        private float TargetFov()
        {
            return _summitViewActive ? _summitFov : _bunkerFov;
        }

        /// <summary>Переносит камеру в целевую локацию с эффектом подъёма лифта.</summary>
        public void GoToView(bool summit, bool instant)
        {
            // Рабочее место перечитываем каждый раз: игрок может сменить державу в лобби,
            // и тогда за столом должна оказаться именно его страна.
            CacheViewposes();

            _summitViewActive = summit;

            if (_camera == null)
            {
                return;
            }

            if (instant)
            {
                _cameraPosition = TargetPosition();
                _cameraRotation = Quaternion.LookRotation(TargetLook() - _cameraPosition, Vector3.up);
                _transitionLeft = 0f;
                ApplyCameraImmediate();
                return;
            }

            _positionFrom = _camera.transform.position;
            _rotationFrom = _camera.transform.rotation;
            _positionTo = TargetPosition();
            _rotationTo = Quaternion.LookRotation(_positionTo - _positionFrom, Vector3.up);

            // Из кресла бункера взгляд направлен вниз-вперёд, из зала — в центр стола.
            _rotationTo = Quaternion.LookRotation(TargetLook() - _positionTo, Vector3.up);

            _transitionTotal = Vector3.Distance(_positionFrom, _positionTo) / Mathf.Max(0.5f, _moveSpeed);
            _transitionLeft = _transitionTotal;
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
            _summitViewActive = !_summitViewActive;
            GoToView(_summitViewActive, false);
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

            switch (phase)
            {
                case GamePhase.Reckoning:
                    SetBunkerLights(0f, 0f, 0f);
                    SetSummitLights(0f, _wellLightIntensity * 0.4f);
                    SetBeaconsActive(true);
                    RenderSettings.ambientLight = Color.Lerp(_ambientBase, AlarmRedColor, _ambientAlarm);
                    break;

                case GamePhase.SummitUN:
                    SetBunkerLights(0f, 0f, 0f);
                    SetSummitLights(_summitKeyIntensity, _wellLightIntensity);
                    SetBeaconsActive(false);
                    RenderSettings.ambientLight = Color.Lerp(_ambientBase, SummitKeyLightColor, _ambientSummit);
                    break;

                default:
                    SetBunkerLights(_bunkerLampIntensity, _crtIntensity, _bunkerLampIntensity * 0.35f);
                    SetSummitLights(0f, 0f);
                    SetBeaconsActive(false);
                    RenderSettings.ambientLight = Color.Lerp(_ambientBase, DeskLampColor, _ambientBunker);
                    break;
            }
        }

        private void SetBunkerLights(float lamp, float crt, float diorama)
        {
            SetLight(_bunkerDeskLamp, lamp);
            SetLight(_bunkerCrtGlow, crt);
            SetLight(_bunkerDioramaFill, diorama);
        }

        private void SetSummitLights(float key, float well)
        {
            SetLight(_summitKeyLight, key);
            SetLight(_summitWellLight, well);
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
            // Камерный тёплый спот настольной лампы над пультом.
            _bunkerDeskLamp = CreateSpot("Bunker_DeskLamp",
                new Vector3(-0.3f, BunkerWorldBuilder.BunkerRoomHeight - 0.35f, 1.35f),
                new Vector3(0f, 1.5f, 2.2f), new Vector3(38f, 55f), DeskLampColor, _bunkerLampIntensity);

            // Подсветка ЭЛТ-монитора — янтарная, бьёт от терминала вверх и назад.
            _bunkerCrtGlow = CreateSpot("Bunker_CrtGlow",
                new Vector3(-0.05f, BunkerWorldBuilder.BunkerConsoleTopY + 0.45f, 0.85f),
                new Vector3(0f, -0.25f, 1f), new Vector3(50f, 70f), CrtAmberColor, _crtIntensity);

            // Общий зелёный подсвет четырёх диорам городов на табло.
            _bunkerDioramaFill = CreateSpot("Bunker_DioramaFill",
                new Vector3(0f, 1.4f, -2.6f), new Vector3(0f, 0f, 1f), new Vector3(80f, 90f),
                AegisGreenColor, _bunkerLampIntensity * 0.35f);

            // --- Саммит ---
            Vector3 summit = BunkerWorldBuilder.SummitCenter;

            // Холодный вертикальный прожектор сверху на стол.
            _summitKeyLight = CreateSpot("Summit_KeyLight",
                summit + new Vector3(0f, 8.6f, 0f), new Vector3(0f, -1f, 0f), new Vector3(26f, 60f),
                SummitKeyLightColor, _summitKeyIntensity);

            // Подсветка из колодца карты мира — снизу вверх зелёно-янтарная.
            _summitWellLight = CreateSpot("Summit_WellLight",
                summit + new Vector3(0f, BunkerMeshFactory.SummitTableSurfaceY - 0.35f, 0f),
                new Vector3(0f, 1f, 0f), new Vector3(95f, 100f), AegisGreenColor, _wellLightIntensity);

            // --- Час Расплаты: два противоположных вращающихся маяка ---
            Transform beaconRig = new GameObject("Beacon_Rig").transform;
            beaconRig.SetParent(_rigRoot.transform, false);

            Vector3 beaconAnchor = summit + new Vector3(0f, 6.4f, 0f);
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
                AlarmRedColor, 0f);

            _beacons[(int)(yaw / 180f)] = beacon;

            pivotObject.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return pivotObject.transform;
        }

        private Light CreateSpot(string nodeName, Vector3 position, Vector3 direction, Vector2 cone,
            Color color, float intensity)
        {
            GameObject node = new GameObject(nodeName);
            node.transform.SetParent(_rigRoot.transform, false);
            node.transform.localPosition = position;

            return CreateSpotInto(nodeName + "_Light", node.transform, Vector3.zero, direction, cone, color,
                intensity);
        }

        private static Light CreateSpotInto(string nodeName, Transform parent, Vector3 position, Vector3 direction,
            Vector2 cone, Color color, float intensity)
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
            light.range = 18f;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 1f;

            // Свет не должен бликовать: в кадре 960x540 пересвеченный источник
            // съедает читаемость индикаторов.
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