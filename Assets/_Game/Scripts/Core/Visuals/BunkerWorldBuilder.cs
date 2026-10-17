// World Crisis | Слой: Core/Visuals | Ответственный: OpenCode
// Процедурная сборка обеих локаций сцены: «Личный бункер диктатора» (№1) и
// «Круглый зал саммита» (№2). Окружение (бетонные оболочки, трубы, кабели, пульт-стол,
// круглая шахта, грузовые платформы) строится здесь, а предметы обстановки берутся
// из BunkerMeshFactory — так фабрика остаётся чистой сборщиком моделей, а планировка
// живёт в одном месте.
//
// Ни одной ссылки на графику из сериализованных полей: всё ищется поимённо.

using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    // Вся планировка бункера и зала саммита живёт в SummitLayout: там она чистая
    // математика без Unity-объектов, которую проверяет headless-стенд geotest.
    // Здесь только удобные обёртки для читаемости остального кода.

    /// <summary>
    /// Собирает сцену целиком и хранит узлы, которые нужны PhaseLightingDirector:
    /// точки обзора, рабочие места саммита и подвесной магнитофон.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("World Crisis/Bunker World Builder")]
    public class BunkerWorldBuilder : MonoBehaviour
    {
        #region Планировка (контракт с PhaseLightingDirector)

        /// <summary>Высота потолка личного бункера.</summary>
        public const float BunkerRoomHeight = 4.3f;

        /// <summary>Высота столешницы пульта: на неё встают все приборы.</summary>
        public const float BunkerConsoleTopY = SummitLayout.Bunker.DeskTopY;

        /// <summary>
        /// Разворот оборудования лицом к игроку. Локальная «лицевая» сторона приборов
        /// смотрит в +Z, а игрок находится в -Z и смотрит в +Z, поэтому всё разворачивается
        /// на 180 градусов. Забыть про это — значит показать игроку тыл пульта.
        /// </summary>
        public const float BunkerFacingYaw = SummitLayout.Bunker.FacingYaw;

        /// <summary>Глаз игрока в бункере: сидит в кресле, спинка за спиной.</summary>
        public static readonly Vector3 BunkerEyePosition = SummitLayout.Bunker.EyePosition;

        /// <summary>
        /// Точка, на которую смотрит игрок. Считается из позиции глаза и наклона
        /// BunkerPitchDegrees, поэтому режиссёр освещения не дублирует углы.
        /// </summary>
        public static Vector3 BunkerLookTarget
        {
            get
            {
                float radians = BunkerPitchDegrees * Mathf.Deg2Rad;
                return BunkerEyePosition + new Vector3(0f, -Mathf.Sin(radians), Mathf.Cos(radians));
            }
        }

        /// <summary>Наклон взгляда в бункере вниз, градусы.</summary>
        public const float BunkerPitchDegrees = SummitLayout.Bunker.PitchDegrees;

        /// <summary>Поле зрения камеры бункера.</summary>
        public const float BunkerFieldOfView = SummitLayout.Bunker.FieldOfView;

        /// <summary>Ближняя плоскость отсечения камеры бункера.</summary>
        public const float BunkerNearClip = SummitLayout.Bunker.NearClip;

        /// <summary>Z дальней стены бункера — тот «задний план», что виден за столом.</summary>
        public const float BunkerBackWallZ = SummitLayout.Bunker.BackWallZ;

        /// <summary>Мировая координата центра Круглого зала (разнесено с бункером по Z).</summary>
        public static readonly Vector3 SummitCenter = SummitLayout.Center;

        /// <summary>Наклон взгляда за столом: игрок смотрит через колодец с картой мира.</summary>
        public const float SummitPitchDegrees = SummitLayout.PlayerPitchDegrees;

        /// <summary>Поле зрения камеры саммита.</summary>
        public const float SummitFieldOfView = SummitLayout.PlayerFieldOfView;

        /// <summary>Радиус бетонной шахты зала.</summary>
        public const float SummitShaftRadius = 11f;

        #endregion

        #region Состояние сборки

        private bool _built;
        private bool _warnedAboutHologramShader;

        private Transform _worldRoot;
        private Transform _bunkerRoot;
        private Transform _summitRoot;
        private Transform _lieDetectorTrolley;
        private readonly Transform[] _summitSeats = new Transform[GeneratedObjectNames.CountryCount];

        /// <summary>Корень локации №1. null до вызова Build.</summary>
        public Transform BunkerRoot
        {
            get { return _bunkerRoot; }
        }

        /// <summary>Корень локации №2. null до вызова Build.</summary>
        public Transform SummitRoot
        {
            get { return _summitRoot; }
        }

        /// <summary>
        /// Общий корень обеих локаций. Живёт отдельно от узла компонента и всегда с
        /// тождественным преобразованием, поэтому мировые координаты раскладки
        /// (0, 0, 40) для зала саммита не могут «уехать» вместе с камерой.
        /// </summary>
        public Transform WorldRoot
        {
            get { return _worldRoot; }
        }

        /// <summary>
        /// Подвесной катушечный магнитофон над центром стола. В покое поднят к потолку,
        /// на фазе компромата опускается — двигает его PhaseLightingDirector.
        /// </summary>
        public Transform LieDetectorTrolley
        {
            get { return _lieDetectorTrolley; }
        }

        /// <summary>Рабочие места саммита по индексу слота. Может содержать null до Build.</summary>
        public IReadOnlyList<Transform> SummitSeats
        {
            get { return _summitSeats; }
        }

        /// <summary>
        /// Мировая позиция глаза игрока за столом саммита. Жёсткое число из SummitLayout:
        /// (0, 1.42, 37.95). Места за столом НЕ участвуют в расчёте: производная от угла
        /// места или радиуса кольца однажды уводила камеру в ржавую стену шахты
        /// (как раз на скриншоте инспектора: X = −8.523).
        /// </summary>
        public static Vector3 SummitEyePosition
        {
            get { return SummitLayout.PlayerEyePosition; }
        }

        /// <summary>Поворот камеры в зале саммита: жёстко Quaternion.Euler(12, 0, 0).</summary>
        public static Quaternion SummitEyeRotation
        {
            get { return SummitLayout.PlayerEyeRotation; }
        }

        /// <summary>Центр стола саммита — цель взгляда игрока и всех прожекторов.</summary>
        public static Vector3 SummitTableCenter
        {
            get { return SummitCenter + new Vector3(0f, BunkerMeshFactory.SummitTableSurfaceY + 0.1f, 0f); }
        }

        #endregion

        #region Публичный вход

        /// <summary>
        /// Единая точка входа для автозапуска из GameBootstrap. Если в сцене уже есть
        /// сборщик, ничего не делаем — ручная сборка всегда побеждает.
        /// </summary>
        public static BunkerWorldBuilder EnsureBuilt(GameObject host)
        {
            BunkerWorldBuilder existing = FindAnyObjectByType<BunkerWorldBuilder>();
            if (existing != null)
            {
                return existing;
            }

            return host.AddComponent<BunkerWorldBuilder>();
        }

        /// <summary>Собирает обе локации. Повторный вызов безопасно игнорируется.</summary>
        public void Build(IReadOnlyList<CityState> cities)
        {
            if (_built)
            {
                return;
            }

            _built = true;

            // Дефолтные источники солнца и глобальные Volume выключаются ДО сборки:
            // солнце светит сквозь стены бункера, а Global Volume из шаблона сцены
            // добавляет поверх постобработки ещё одну выбеливающую обработку.
            DisableDefaultSceneLightsAndVolumes();

            // Мир собирается в собственном узле с тождественным преобразованием, а не
            // в узле компонента. Иначе положение локаций наследовалось от камеры или
            // любого другого хост-объекта: камера сама едет в зал саммита — и корень
            // зала уезжает вместе с ней, добавляя к своим координатам её смещение.
            GameObject worldNode = new GameObject("WorldCrisis_World");
            _worldRoot = worldNode.transform;

            GameObject bunkerNode = MakeNode("Bunker_Zone", _worldRoot, Vector3.zero);
            _bunkerRoot = bunkerNode.transform;
            BuildBunkerRoom(_bunkerRoot);
            BuildBunkerConsole(_bunkerRoot);
            PopulateBunkerEquipment(_bunkerRoot, cities);

            GameObject summitNode = MakeNode("Summit_Zone", _worldRoot, SummitCenter);
            _summitRoot = BunkerMeshFactory.CreateLocationTwoSummit(summitNode.transform).transform;
            BuildSummitShell(_summitRoot);
            BuildSummitSeats(_summitRoot);
            BuildLieDetector(_summitRoot);

            // Шейдеры голограмм назначаются последними: узлы куполов к этому моменту созданы.
            ApplyHologramShaders();
        }

        /// <summary>
        /// Гасит дефолтные Directional Light и глобальные Volume сцены.
        ///
        /// Подземный бункер освещается только локальными лампами, а Directional Light
        /// из шаблона SampleScene бьёт сквозь бетонные стены и вместе с ambient
        /// превращает тёмную кабину в светло-бежевую кашу. Глобальный Volume из
        /// шаблона поверх ретро-постобработки даёт то же самое.
        /// </summary>
        public static void DisableDefaultSceneLightsAndVolumes()
        {
            // Перегрузка без FindObjectsSortMode: с сортировкой она помечена устаревшей
            // в Unity 6 (warning CS0618), а порядок обхода здесь не важен.
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];

                if (light == null || light.type != LightType.Directional)
                {
                    continue;
                }

                light.shadows = LightShadows.None;
                light.intensity = 0f;
                light.enabled = false;

                if (light.gameObject != null)
                {
                    light.gameObject.SetActive(false);
                }
            }

            UnityEngine.Rendering.Volume[] volumes =
                Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include);

            for (int i = 0; i < volumes.Length; i++)
            {
                if (volumes[i] == null)
                {
                    continue;
                }

                volumes[i].enabled = false;

                if (volumes[i].gameObject != null)
                {
                    volumes[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Сборка идёт в Awake, а не в Start: PhaseLightingDirector добавляется следом
        /// на тот же объект, и его Awake выполняется немедленно — раньше, чем Start.
        /// Без этого директор увидел бы пустой мир и встал бы не в то место.
        /// </summary>
        private void Awake()
        {
            // Компонент, добавленный в сцену вручную, тоже обязан собрать мир.
            if (!_built)
            {
                BuildCitiesFallback();
            }
        }

        /// <summary>
        /// Стартовый набор городов для ручной сборки без MatchManager. Позже статусы
        /// обновляются заново — сцена не зависит от порядка инициализации.
        /// </summary>
        private void BuildCitiesFallback()
        {
            IReadOnlyList<PlayerState> players = GetPlayers();

            if (players != null && players.Count > 0)
            {
                PlayerState first = players[0];
                if (first != null)
                {
                    Build(first.Cities);
                    return;
                }
            }

            Build(null);
        }

        private static IReadOnlyList<PlayerState> GetPlayers()
        {
            return MatchManager.Instance != null ? MatchManager.Instance.AllPlayers : null;
        }

        #endregion

        #region Узлы

        private GameObject MakeNode(string nodeName, Transform parent, Vector3 localPosition)
        {
            GameObject node = new GameObject(nodeName);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPosition;
            return node;
        }

        private GameObject Seal(string nodeName, Transform parent, MeshBuilder builder)
        {
            GameObject node = MakeNode(nodeName, parent, Vector3.zero);
            Mesh mesh = builder.Build(nodeName + "_Mesh", true);

            if (mesh.vertexCount == 0)
            {
                Debug.LogWarning("[WorldCrisis] BunkerWorldBuilder: узел " + nodeName + " собран без геометрии.");
                return node;
            }

            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.AddComponent<MeshRenderer>().sharedMaterials = builder.GetMaterials();
            return node;
        }

        #endregion

/// <summary>
        /// Принудительно вписывает прибор в заданный габаритный бокс и ставит его
        /// нижней гранью на указанную точку поверхности.
        ///
        /// Зачем это нужно: размеры процедурных моделей задаются в метрах внутри
        /// фабрики, и одна ошибка в паре констант даёт прибор во весь экран. Здесь
        /// размер берётся не из справочника, а из РЕАЛЬНОГО объединённого Bounds
        /// всех мешей прибора, поэтому цифры не могут разойтись с геометрией.
        /// </summary>
        public static void FitPropToBox(GameObject prop, Vector3 maxSizeMeters, Vector3 bottomCenterWorldPos,
            float yawDegrees)
        {
            if (prop == null)
            {
                return;
            }

            // Родитель запоминается и возвращается в конце: расчёт идёт в мировых
            // координатах (узел временно отцепляется), но прибор обязан остаться внутри
            // своей локации. Раньше он навсегда уезжал в корень сцены, и обходы по
            // _bunkerRoot переставали его видеть.
            Transform owner = prop.transform.parent;

            // Сброс идемпотентен: повторный вызов не накапливает масштаб и сдвиг.
            prop.transform.SetParent(null, false);
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one;

            MeshFilter[] filters = prop.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                return;
            }

            Bounds localBounds;
            if (!ComputeWorldMeshBounds(prop, out localBounds))
            {
                return;
            }

            Vector3 size = localBounds.size;

            float sx = size.x > 0.0001f ? maxSizeMeters.x / size.x : 1f;
            float sy = size.y > 0.0001f ? maxSizeMeters.y / size.y : 1f;
            float szFactor = size.z > 0.0001f ? maxSizeMeters.z / size.z : 1f;

            // Равномерный масштаб по наименьшему коэффициенту: прибор пропорционально
            // ужимается целиком и ничего не растягивается по одной оси.
            float uniformScale = Mathf.Min(sx, Mathf.Min(sy, szFactor));

            prop.transform.localScale = Vector3.one * uniformScale;
            prop.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            // После масштаба и поворота границы надо пересчитать заново.
            Bounds worldBounds;
            if (!ComputeWorldMeshBounds(prop, out worldBounds))
            {
                return;
            }

            Vector3 shift = new Vector3(
                bottomCenterWorldPos.x - worldBounds.center.x,
                bottomCenterWorldPos.y - worldBounds.min.y,
                bottomCenterWorldPos.z - worldBounds.center.z);

            prop.transform.position += shift;

            if (owner != null)
            {
                // Мировая позиция уже посчитана, поэтому возвращаем узел в свою локацию
                // без пересчёта: scale остаётся прежним, меняется только локальное смещение.
                prop.transform.SetParent(owner, true);
            }
        }

        /// <summary>
        /// Объединённые мировые границы всех мешей под объектом. Считается по восьми
        /// углам каждого Mesh.bounds, трансформированным в мир: так корректно
        /// учитываются и поворот, и масштаб, и вложенность узлов.
        /// </summary>
        public static bool ComputeWorldMeshBounds(GameObject root, out Bounds worldBounds)
        {
            worldBounds = new Bounds();
            bool hasBounds = false;

            if (root == null)
            {
                return false;
            }

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter meshFilter = filters[i];

                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Mesh mesh = meshFilter.sharedMesh;
                mesh.RecalculateBounds();
                Bounds meshBounds = mesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;

                for (int ix = 0; ix < 2; ix++)
                {
                    for (int iy = 0; iy < 2; iy++)
                    {
                        for (int iz = 0; iz < 2; iz++)
                        {
                            Vector3 corner = new Vector3(
                                ix == 0 ? min.x : max.x,
                                iy == 0 ? min.y : max.y,
                                iz == 0 ? min.z : max.z);

                            Vector3 world = meshFilter.transform.TransformPoint(corner);

                            if (!hasBounds)
                            {
                                worldBounds = new Bounds(world, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                worldBounds.Encapsulate(world);
                            }
                        }
                    }
                }
            }

            return hasBounds;
        }

        /// <summary>
        /// Автоматический разворот прибора ЛИЦОМ к игроку.
        ///
        /// Приборы собираются в локальных координатах, где «лицо» смотрит в +Z, и
        /// поворачиваются на 180° (FacingYaw). Ошибка в одном углу — и игрок видит
        /// тыл пульта: круглый кожух кинескопа вместо экрана, крышку вместо кнопки,
        /// задник табло вместо городов. Поэтому после вписывания в габарит лицевая
        /// деталь проверяется ещё раз, уже в мировых координатах.
        ///
        /// Учитываются ВСЕ найденные лицевые узлы и берётся среднее смещение от центра
        /// прибора: у пульта с тремя телефонами первый диск смещён вбок, и по одному
        /// узлу разворот определялся бы неверно.
        /// </summary>
        public static void EnsureFrontChildFacesCamera(GameObject propRoot, string frontChildNameSubstring,
            Vector3 cameraWorldPos)
        {
            if (propRoot == null || string.IsNullOrEmpty(frontChildNameSubstring))
            {
                return;
            }

            Transform[] children = propRoot.GetComponentsInChildren<Transform>(true);
            Vector3 offsetSum = Vector3.zero;
            int found = 0;

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name.IndexOf(frontChildNameSubstring, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Bounds frontBounds;

                if (!ComputeWorldMeshBounds(children[i].gameObject, out frontBounds))
                {
                    continue;
                }

                offsetSum += frontBounds.center;
                found++;
            }

            if (found == 0)
            {
                return;
            }

            Bounds rootBounds;

            if (!ComputeWorldMeshBounds(propRoot, out rootBounds))
            {
                return;
            }

            Vector3 toCam = cameraWorldPos - rootBounds.center;
            toCam.y = 0f;

            Vector3 toFront = offsetSum / found - rootBounds.center;
            toFront.y = 0f;

            if (toCam.sqrMagnitude < 0.000001f || toFront.sqrMagnitude < 0.000001f)
            {
                return;
            }

            // Лицевая деталь смотрит ОТ камеры — разворачиваем весь прибор на 180°
            // вокруг вертикальной оси через его собственный центр габарита.
            if (Vector3.Dot(toCam.normalized, toFront.normalized) < 0f)
            {
                propRoot.transform.RotateAround(rootBounds.center, Vector3.up, 180f);
            }
        }


#region Локация №1 — личный бункер

        // Раскладка задана прямо в метрах и проверяется headless-стендом: каждый прибор
        // пропускается через FitPropToBox, поэтому его реальные мировые границы не могут
        // разойтись с числами ниже.
        private const float BunkerRoomHalfWidth = 2.4f;
        private const float BunkerFrontWallZ = -1.2f;

        /// <summary>Толщина бетонной стены кабины.</summary>
        private const float WallThickness = 0.3f;

        /// <summary>Высота столешницы. Все приборы ставятся нижней гранью на неё.</summary>
        private const float DeskTopY = SummitLayout.Bunker.DeskTopY;

        /// <summary>Ширина столешницы.</summary>
        private const float DeskWidth = SummitLayout.Bunker.DeskWidth;

        /// <summary>Глубина столешницы.</summary>
        private const float DeskDepth = SummitLayout.Bunker.DeskDepth;

        /// <summary>Центр столешницы по Z.</summary>
        private const float DeskCenterZ = SummitLayout.Bunker.DeskCenterZ;

        /// <summary>Позиция кресла игрока — строго за камерой.</summary>
        private const float ChairZ = SummitLayout.Bunker.ChairZ;

        /// <summary>Высота плафона лампы над столом. Совпадает с константой режиссёра освещения:
        /// и узел Bunker_PendantLamp, и SpotLight висят в одной точке (0, 2.05, 0.55).</summary>
        private const float PendantLampHeight = 2.05f;

        /// <summary>
        /// Бетонная оболочка кабины. Ось +Z — направление взгляда игрока, поэтому
        /// дальняя стена стоит на BunkerBackWallZ и именно её видно за столом.
        /// </summary>
        private void BuildBunkerRoom(Transform parent)
        {
            Material concrete = Mat.Concrete();
            Material iron = Mat.CastIron();
            Material steel = Mat.ColdSteel();
            MeshBuilder shell = new MeshBuilder();
            float roomWidth = BunkerRoomHalfWidth * 2f;
            float roomDepth = BunkerBackWallZ - BunkerFrontWallZ;
            float centerZ = (BunkerBackWallZ + BunkerFrontWallZ) * 0.5f;

            shell.Add(MeshPrimitives.BeveledBox(new Vector3(roomWidth + 0.6f, 0.3f, roomDepth + 0.6f), 0.04f),
                MeshBuilder.Trs(new Vector3(0f, -0.15f, centerZ), Vector3.zero, Vector3.one), concrete,
                MeshUvMode.BoxProject);
            shell.Add(MeshPrimitives.BeveledBox(new Vector3(roomWidth + 0.6f, 0.36f, roomDepth + 0.6f), 0.05f),
                MeshBuilder.Trs(new Vector3(0f, BunkerRoomHeight + 0.18f, centerZ), Vector3.zero, Vector3.one),
                concrete, MeshUvMode.BoxProject);

            // Дальняя стена: её ВНУТРЕННЯЯ плоскость обязана быть ровно на BunkerBackWallZ,
            // иначе от этой цифры отталкиваются гермодверь и табло статуса, а стена
            // перекрывает их: и гермодверь, и табло статуса уходили в бетон.
            AddWall(shell, concrete, new Vector3(0f, BunkerRoomHeight * 0.5f, BunkerBackWallZ + WallThickness * 0.5f),
                new Vector3(roomWidth + 0.6f, BunkerRoomHeight, WallThickness));
            AddWall(shell, concrete, new Vector3(0f, BunkerRoomHeight * 0.5f, BunkerFrontWallZ),
                new Vector3(roomWidth + 0.6f, BunkerRoomHeight, 0.3f));
            AddWall(shell, concrete, new Vector3(-BunkerRoomHalfWidth, BunkerRoomHeight * 0.5f, centerZ),
                new Vector3(0.3f, BunkerRoomHeight, roomDepth + 0.6f));
            AddWall(shell, concrete, new Vector3(BunkerRoomHalfWidth, BunkerRoomHeight * 0.5f, centerZ),
                new Vector3(0.3f, BunkerRoomHeight, roomDepth + 0.6f));

            int ribCount = 4;
            for (int i = 0; i < ribCount; i++)
            {
                float z = BunkerFrontWallZ + 0.3f + i * (roomDepth - 0.6f) / (ribCount - 1);

                shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.26f, BunkerRoomHeight, 0.22f), 0.03f),
                    MeshBuilder.Trs(new Vector3(-BunkerRoomHalfWidth + 0.13f, BunkerRoomHeight * 0.5f, z),
                        Vector3.zero, Vector3.one), concrete, MeshUvMode.BoxProject);
                shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.26f, BunkerRoomHeight, 0.22f), 0.03f),
                    MeshBuilder.Trs(new Vector3(BunkerRoomHalfWidth - 0.13f, BunkerRoomHeight * 0.5f, z),
                        Vector3.zero, Vector3.one), concrete, MeshUvMode.BoxProject);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (BunkerRoomHalfWidth - 0.05f);
                for (int i = 0; i < 8; i++)
                {
                    float z = BunkerFrontWallZ + 0.3f + i * (roomDepth - 0.6f) / 7f;
                    shell.Add(MeshPrimitives.RivetDome(0.016f, 0.012f),
                        MeshBuilder.Align(new Vector3(x, 0.22f, z), new Vector3(-side, 0f, 0f)), steel,
                        MeshUvMode.BoxProject);
                }
            }

            Seal("Bunker_Shell", parent, shell);

            BuildBunkerWallServices(parent, iron, steel);
            BuildHermeticDoor(parent);
            BuildPendantLamp(parent);
        }

        private static void AddWall(MeshBuilder builder, Material material, Vector3 center, Vector3 size)
        {
            builder.Add(MeshPrimitives.BeveledBox(size, 0.04f),
                MeshBuilder.Trs(center, Vector3.zero, Vector3.one), material, MeshUvMode.BoxProject);
        }

        /// <summary>Трубы с вентилями, распределительные коробки и пучки кабелей по стенам.</summary>
        private void BuildBunkerWallServices(Transform parent, Material iron, Material steel)
        {
            MeshBuilder services = new MeshBuilder();
            BunkerMeshFactory.DetRandom random = new BunkerMeshFactory.DetRandom(0x9C1);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (BunkerRoomHalfWidth - 0.22f);

                for (int line = 0; line < 2; line++)
                {
                    float y = BunkerRoomHeight - 0.55f - line * 0.3f;
                    float radius = line == 0 ? 0.085f : 0.055f;

                    Vector3[] path = new Vector3[6];
                    for (int i = 0; i < path.Length; i++)
                    {
                        float t = i / (float)(path.Length - 1);
                        path[i] = new Vector3(x, y + Mathf.Sin(t * Mathf.PI) * 0.03f,
                            Mathf.Lerp(BunkerFrontWallZ + 0.4f, BunkerBackWallZ - 0.3f, t));
                    }

                    services.Add(MeshPrimitives.Tube(path, radius, 8), Matrix4x4.identity,
                        line == 0 ? iron : steel, MeshUvMode.BoxProject);

                    for (int i = 1; i < path.Length - 1; i++)
                    {
                        services.Add(MeshPrimitives.Torus(radius + 0.014f, 0.018f, 10, 5),
                            MeshBuilder.Align(path[i], Vector3.forward), steel, MeshUvMode.BoxProject);
                    }
                }

                services.Add(MeshPrimitives.Prism(0.05f, 0.05f, 0.22f, 8, 0.008f, false),
                    MeshBuilder.Trs(new Vector3(x - side * 0.14f, BunkerRoomHeight - 0.85f,
                        BunkerBackWallZ - 1.1f), new Vector3(0f, 0f, 90f * -side), Vector3.one), iron,
                    MeshUvMode.BoxProject);
                services.Add(MeshPrimitives.Torus(0.13f, 0.024f, 12, 6),
                    MeshBuilder.Align(new Vector3(x - side * 0.26f, BunkerRoomHeight - 0.85f,
                        BunkerBackWallZ - 1.1f), new Vector3(-side, 0f, 0f)), steel, MeshUvMode.BoxProject);

                services.Add(MeshPrimitives.BeveledBox(new Vector3(0.2f, 0.3f, 0.26f), 0.02f),
                    MeshBuilder.Trs(new Vector3(x - side * 0.12f, 1.75f, BunkerBackWallZ - 1.6f),
                        Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);

                const int bundle = 4;
                for (int i = 0; i < bundle; i++)
                {
                    float offset = random.Range(-0.06f, 0.06f);
                    Vector3[] path = new Vector3[7];

                    for (int k = 0; k < path.Length; k++)
                    {
                        float t = k / (float)(path.Length - 1);
                        path[k] = Vector3.Lerp(
                                new Vector3(x - side * 0.12f + offset, 1.9f, BunkerBackWallZ - 1.6f),
                                new Vector3(x - side * 0.3f + offset, BunkerRoomHeight - 0.3f,
                                    BunkerBackWallZ - 0.7f),
                                t)
                            + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.18f;
                    }

                    services.Add(MeshPrimitives.Tube(path, 0.018f, 6), Matrix4x4.identity,
                        i % 2 == 0 ? steel : iron, MeshUvMode.BoxProject);
                }
            }
        }

        /// <summary>Рельефная стальная гермодверь по центру дальней стены.</summary>
        private void BuildHermeticDoor(Transform parent)
        {
            Material iron = Mat.CastIron();
            Material steel = Mat.ColdSteel();
            Material hazard = Mat.Hazard();

            MeshBuilder door = new MeshBuilder();

            // Полотно двери целиком ПЕРЕД внутренней плоскостью стены (z = 2.30):
            // раньше оно стояло по центру стены и проваливалось в бетон — из комнаты
            // гермодверь была не видна вообще.
            const float doorZ = 2.235f;
            Vector3 z = new Vector3(0f, 0f, doorZ);

            const float width = 1.30f;
            const float height = 2.10f;
            const float bar = 0.16f;

            // Рама-обвязка вокруг полотна, тоже перед стеной (2.145..2.265).
            door.Add(MeshPrimitives.Frame(new Vector2(width + bar * 2f, height + bar), bar, 0.12f, 0.03f),
                MeshBuilder.Trs(z + new Vector3(0f, height * 0.5f, -0.03f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Полотно: 0.11 м по Z, то есть z = 2.18..2.29 — целиком в комнате.
            door.Add(MeshPrimitives.BeveledBox(new Vector3(width, height, 0.11f), 0.022f),
                MeshBuilder.Trs(z + new Vector3(0f, height * 0.5f, 0f), Vector3.zero, Vector3.one),
                iron, MeshUvMode.BoxProject);

            door.Add(MeshPrimitives.BeveledBox(new Vector3(width - 0.14f, 0.2f, 0.03f), 0.01f),
                MeshBuilder.Trs(z + new Vector3(0f, 0.24f, -0.06f), Vector3.zero, Vector3.one), hazard,
                MeshUvMode.BoxProject);

            // Маховик выступает в комнату — он и читается как ручка двери.
            Vector3 wheel = z + new Vector3(0.34f, 1.0f, -0.09f);
            door.Add(MeshPrimitives.Prism(0.055f, 0.055f, 0.16f, 10, 0.01f, false),
                MeshBuilder.Align(wheel, Vector3.back), steel, MeshUvMode.BoxProject);
            door.Add(MeshPrimitives.Torus(0.2f, 0.026f, 16, 6),
                MeshBuilder.Align(wheel + Vector3.back * 0.08f, Vector3.back), steel, MeshUvMode.BoxProject);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f + 0.4f;
                Vector3 spoke = wheel + Vector3.back * 0.08f;
                door.Add(MeshPrimitives.Tube(new[]
                {
                    spoke,
                    spoke + new Vector3(Mathf.Cos(angle) * 0.2f, Mathf.Sin(angle) * 0.2f, 0f)
                }, 0.016f, 6), Matrix4x4.identity, steel, MeshUvMode.BoxProject);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -width * 0.5f + 0.14f + i * (width - 0.28f) / 5f;
                door.Add(MeshPrimitives.RivetDome(0.016f, 0.012f),
                    MeshBuilder.Align(z + new Vector3(x, height - 0.12f, -0.06f), Vector3.back), steel,
                    MeshUvMode.BoxProject);
            }

            Seal("Bunker_HermeticDoor", parent, door);
        }

        /// <summary>Железный плафон на кабеле над столом с отдельным узлом лампы накаливания.</summary>
        private void BuildPendantLamp(Transform parent)
        {
            Material iron = Mat.CastIron();
            Material bulb = Mat.Neon(new Color(1f, 0.878f, 0.698f));

            Vector3 anchor = new Vector3(0f, PendantLampHeight, 0.55f);

            MeshBuilder lamp = new MeshBuilder();

            lamp.Add(MeshPrimitives.Tube(new[]
            {
                new Vector3(anchor.x, BunkerRoomHeight - 0.02f, anchor.z),
                new Vector3(anchor.x, anchor.y + 0.28f, anchor.z)
            }, 0.012f, 6), Matrix4x4.identity, iron, MeshUvMode.BoxProject);

            lamp.Add(MeshPrimitives.Prism(0.06f, 0.24f, 0.22f, 14, 0.02f, false),
                MeshBuilder.Trs(anchor + new Vector3(0f, 0.12f, 0f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);
            lamp.Add(MeshPrimitives.Torus(0.235f, 0.018f, 16, 6),
                MeshBuilder.Align(anchor, Vector3.down), iron, MeshUvMode.BoxProject);

            Seal("Bunker_PendantLamp", parent, lamp);

            MeshBuilder filament = new MeshBuilder();
            filament.Add(MeshPrimitives.Sphere(0.085f, 14, 8),
                MeshBuilder.Trs(anchor, Vector3.zero, Vector3.one), bulb, MeshUvMode.BoxProject);
            Seal("Bunker_Bulb", parent, filament);
        }

        /// <summary>
        /// Единый стол управления. Плита 0.06 м толщиной (0.70..0.76), тумбы только
        /// по краям — над столешницей ничего нет, иначе приборы оказываются в стене.
        /// </summary>
        private void BuildBunkerConsole(Transform parent)
        {
            Material steel = Mat.ColdSteel();
            Material iron = Mat.CastIron();
            Material top = Mat.ConsoleTop();

            MeshBuilder desk = new MeshBuilder();

            desk.Add(MeshPrimitives.BeveledBox(new Vector3(DeskWidth, 0.06f, DeskDepth), 0.014f),
                MeshBuilder.Trs(new Vector3(0f, DeskTopY - 0.03f, DeskCenterZ), Vector3.zero, Vector3.one),
                top, MeshUvMode.BoxProject);

            // Кант по ближней кромке — к нему и прижимается прибор.
            desk.Add(MeshPrimitives.BeveledBox(new Vector3(DeskWidth, 0.1f, 0.08f), 0.014f),
                MeshBuilder.Trs(new Vector3(0f, DeskTopY - 0.07f, 0.0f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 1.15f;
                desk.Add(MeshPrimitives.BeveledBox(new Vector3(0.34f, DeskTopY - 0.06f, DeskDepth - 0.1f), 0.018f),
                    MeshBuilder.Trs(new Vector3(x, (DeskTopY - 0.06f) * 0.5f, DeskCenterZ), Vector3.zero,
                        Vector3.one), steel, MeshUvMode.BoxProject);
                desk.Add(MeshPrimitives.BeveledBox(new Vector3(0.42f, 0.06f, DeskDepth), 0.012f),
                    MeshBuilder.Trs(new Vector3(x, 0.03f, DeskCenterZ), Vector3.zero, Vector3.one), iron,
                    MeshUvMode.BoxProject);
            }

            for (int i = 0; i < 13; i++)
            {
                float x = -DeskWidth * 0.5f + 0.16f + i * (DeskWidth - 0.32f) / 12f;
                desk.Add(MeshPrimitives.RivetDome(0.013f, 0.009f),
                    MeshBuilder.Align(new Vector3(x, DeskTopY - 0.07f, -0.045f), Vector3.back), steel,
                    MeshUvMode.BoxProject);
            }

            Seal("Bunker_Desk", parent, desk);

            BuildPlayerChair(parent);
        }

        /// <summary>
        /// Кресло игрока позади камеры (z = -0.85 при камере z = -0.55). Вся геометрия
        /// держится не ближе z = -0.65, поэтому спинка физически не может попасть в кадр.
        /// </summary>
        private void BuildPlayerChair(Transform parent)
        {
            Material iron = Mat.CastIron();
            Material cloth = Mat.Cloth(new Color(0.19f, 0.17f, 0.15f));

            MeshBuilder chair = new MeshBuilder();

            chair.Add(MeshPrimitives.BeveledBox(new Vector3(0.56f, 0.12f, 0.52f), 0.03f),
                MeshBuilder.Trs(new Vector3(0f, 0.46f, ChairZ), Vector3.zero, Vector3.one), cloth,
                MeshUvMode.BoxProject);
            chair.Add(MeshPrimitives.BeveledBox(new Vector3(0.56f, 0.7f, 0.12f), 0.03f),
                MeshBuilder.Trs(new Vector3(0f, 0.87f, ChairZ - 0.22f), new Vector3(-8f, 0f, 0f), Vector3.one),
                cloth, MeshUvMode.BoxProject);

            for (int side = -1; side <= 1; side += 2)
            {
                chair.Add(MeshPrimitives.BeveledBox(new Vector3(0.08f, 0.07f, 0.44f), 0.02f),
                    MeshBuilder.Trs(new Vector3(side * 0.28f, 0.66f, ChairZ), Vector3.zero, Vector3.one),
                    iron, MeshUvMode.BoxProject);
            }

            chair.Add(MeshPrimitives.Prism(0.06f, 0.06f, 0.4f, 10, 0.01f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.22f, ChairZ), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);
            chair.Add(MeshPrimitives.Prism(0.28f, 0.28f, 0.05f, 14, 0.01f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.025f, ChairZ), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            Seal("Bunker_PlayerChair", parent, chair);
        }

        /// <summary>
        /// Приборы на столе. Каждый пропускается через FitPropToBox с явным габаритом
        /// и точкой посадки на столешницу — это единственный способ гарантировать,
        /// что прибор не окажется во весь экран и не будет висеть в воздухе.
        /// </summary>
        private void PopulateBunkerEquipment(Transform parent, IReadOnlyList<CityState> cities)
        {
            float top = DeskTopY;
            Vector3 camPos = BunkerEyePosition;

            // Слева: узел связи с тремя дисковыми телефонами. Диски набора — «лицо»
            // прибора, по ним и определяется разворот.
            GameObject switchboard = BunkerMeshFactory.CreateSwitchboardUnit(parent);
            FitPropToBox(switchboard, new Vector3(0.78f, 0.24f, 0.38f),
                new Vector3(-0.62f, top, 0.52f), 170f);
            EnsureFrontChildFacesCamera(switchboard, GeneratedObjectNames.DialWheel, camPos);

            // Слева-спереди: планшет с бумагами.
            GameObject clipboard = BuildClipboard(parent);
            FitPropToBox(clipboard, new Vector3(0.26f, 0.03f, 0.32f),
                new Vector3(-0.92f, top, 0.24f), 162f);

            // По центру: ядерный пульт. Лицо — ряд тумблеров Switch_01..03 на передней
            // планке: крышка Safety_Cover_Pivot висит сзади и кнопку не закрывает.
            GameObject nuclearBox = BunkerMeshFactory.CreateNuclearLaunchBox(parent, Vector3.zero);
            FitPropToBox(nuclearBox, new Vector3(0.28f, 0.24f, 0.28f),
                new Vector3(0.08f, top, 0.48f), 180f);
            EnsureFrontChildFacesCamera(nuclearBox, GeneratedObjectNames.OrderSwitch(0), camPos);

            // Справа: ЭЛТ-монитор CITY STATUS и принтер правее него. Лицо — экран.
            GameObject crtTerminal = BunkerMeshFactory.CreateOrdersCrtTerminal(parent, Vector3.zero);
            FitPropToBox(crtTerminal, new Vector3(0.54f, 0.46f, 0.42f),
                new Vector3(0.66f, top, 0.56f), 202f);
            EnsureFrontChildFacesCamera(crtTerminal, GeneratedObjectNames.ScreenSurface, camPos);

            GameObject printer = BunkerMeshFactory.CreateDotMatrixPrinter(parent, Vector3.zero);
            FitPropToBox(printer, new Vector3(0.32f, 0.16f, 0.26f),
                new Vector3(1.06f, top, 0.44f), 205f);

            // Слева на задней стене: табло статуса государств. Секции-диорамы должны
            // смотреть ВНУТРЬ комнаты (их кромка локально +Z), а не в бетон.
            GameObject statusBoard = BunkerMeshFactory.CreateStateStatusBoard(parent, Vector3.zero, cities);
            FitPropToBox(statusBoard, new Vector3(1.05f, 0.58f, 0.14f),
                new Vector3(StatusBoardX, 1.15f, StatusBoardZ), 180f);
            EnsureFrontChildFacesCamera(statusBoard, GeneratedObjectNames.CityDiorama(0), camPos);
        }

        /// <summary>
        /// Табло статуса стоит ПЕРЕД задней стеной (внутренняя плоскость стены —
        /// BunkerBackWallZ) и слева от гермодвери, чтобы рама двери не задевала секции:
        /// раньше задняя плита табло уходила в бетон, и игрок видел четыре серых
        /// квадрата задника вместо городов.
        /// </summary>
        private const float StatusBoardZ = 2.12f;
        private const float StatusBoardX = -1.34f;

        /// <summary>Планшет с бумагами и витой провод по столу.</summary>
        private GameObject BuildClipboard(Transform parent)
        {
            Material steel = Mat.ColdSteel();
            Material paper = Mat.Paper();

            MeshBuilder clipboard = new MeshBuilder();

            clipboard.Add(MeshPrimitives.BeveledBox(new Vector3(0.3f, 0.02f, 0.22f), 0.006f),
                MeshBuilder.Trs(new Vector3(0f, 0.01f, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            for (int i = 0; i < 5; i++)
            {
                clipboard.Add(MeshPrimitives.Plate(new Vector2(0.26f, 0.19f), 0.004f, 0.001f),
                    MeshBuilder.Trs(new Vector3(i * 0.005f, 0.024f + i * 0.005f, i * 0.004f),
                        new Vector3(0f, i * 1.6f, 0f), Vector3.one), paper, MeshUvMode.BoxProject);
            }

            clipboard.Add(MeshPrimitives.Prism(0.014f, 0.014f, 0.012f, 10, 0.003f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.026f, 0.11f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            return Seal("Bunker_Clipboard", parent, clipboard);
        }

        #endregion
        #region Локация №2 — Круглый зал саммита

        /// <summary>
        /// Круглая бетонная шахта: стена уходит вверх и вниз в туман, вокруг стола кольцо
        /// пола, по периметру — грузовые платформы рабочих мест и поручни.
        /// </summary>
        private void BuildSummitShell(Transform parent)
        {
            Material concrete = Mat.Concrete();
            Material iron = Mat.CastIron();
            Material steel = Mat.ColdSteel();

            MeshBuilder shell = new MeshBuilder();

            // Профиль шахты замкнут: внутренняя стена вверх, кромка наружу, внешняя стена вниз.
            // Замкнутость нужна, чтобы тело вращения не поставило заглушек и вверх, и вниз:
            // иначе шахта выглядела бы крышкой, а не провалом в туман.
            Vector2[] shaftProfile =
            {
                new Vector2(SummitShaftRadius, -8f),
                new Vector2(SummitShaftRadius, 9.2f),
                new Vector2(SummitShaftRadius + 1.1f, 9.2f),
                new Vector2(SummitShaftRadius + 1.1f, -8f),
                new Vector2(SummitShaftRadius, -8f)
            };

            shell.Add(MeshPrimitives.Lathe(shaftProfile, 40, false, 0.6f), Matrix4x4.identity, concrete,
                MeshUvMode.BoxProject);

            // Кольцо пола вокруг стола.
            shell.Add(MeshPrimitives.Ring(3.5f, SummitShaftRadius, 0.3f, 40),
                MeshBuilder.Trs(new Vector3(0f, -0.15f, 0f), Vector3.zero, Vector3.one), concrete,
                MeshUvMode.BoxProject);

            // Потолочная плита с кольцевым вырезом над столом.
            shell.Add(MeshPrimitives.Ring(3.2f, SummitShaftRadius + 1.1f, 0.5f, 40),
                MeshBuilder.Trs(new Vector3(0f, 9.2f, 0f), Vector3.zero, Vector3.one), concrete,
                MeshUvMode.BoxProject);

            // Поручни по периметру: стойки и оба кольца.
            const int postCount = 24;
            for (int i = 0; i < postCount; i++)
            {
                float angle = i * Mathf.PI * 2f / postCount;
                float radius = SummitShaftRadius - 0.35f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                shell.Add(MeshPrimitives.Prism(0.035f, 0.035f, 1.05f, 8, 0.006f, false),
                    MeshBuilder.Trs(point + new Vector3(0f, 0.52f, 0f), Vector3.zero, Vector3.one), iron,
                    MeshUvMode.BoxProject);
            }

            shell.Add(MeshPrimitives.Torus(SummitShaftRadius - 0.35f, 0.045f, 40, 8),
                MeshBuilder.Trs(new Vector3(0f, 1.04f, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);
            shell.Add(MeshPrimitives.Torus(SummitShaftRadius - 0.35f, 0.03f, 40, 6),
                MeshBuilder.Trs(new Vector3(0f, 0.58f, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            Seal("Summit_Shell", parent, shell);

            BuildCargoPlatforms(parent);
        }

        /// <summary>Шесть грузовых платформ-лифтов по кругу стола. Платформа игрока (место №0)
        /// собирается без кабины: её направляющие и кромка попадали прямо в кадр.</summary>
        private void BuildCargoPlatforms(Transform parent)
        {
            Material steel = Mat.ColdSteel();
            Material iron = Mat.CastIron();
            Material hazard = Mat.Hazard();

            for (int i = 0; i < GeneratedObjectNames.CountryCount; i++)
            {
                Vector3 position = BunkerMeshFactory.GetSummitSeatPosition(i);
                float angle = BunkerMeshFactory.GetSummitSeatAngle(i);

                // Место игрока стоит в мёртвой зоне камеры: ни настил с кромкой,
                // ни направляющие лифта там не строятся — иначе первый же полигон
                // перекрывает обзор от (0, 1.42, 37.95) к столу.
                bool inPlayerClearance = SummitLayout.IsInPlayerClearance(SummitCenter + position);

                MeshBuilder platform = new MeshBuilder();

                if (!inPlayerClearance)
                {
                    // Настил с жёлто-чёрной кромкой — грузовая платформа, а не подиум.
                    platform.Add(MeshPrimitives.BeveledBox(new Vector3(2.6f, 0.14f, 1.9f), 0.025f),
                        MeshBuilder.Trs(new Vector3(0f, 0.07f, 0f), Vector3.zero, Vector3.one), steel,
                        MeshUvMode.BoxProject);
                    platform.Add(MeshPrimitives.BeveledBox(new Vector3(2.6f, 0.2f, 0.16f), 0.02f),
                        MeshBuilder.Trs(new Vector3(0f, 0.12f, 0.88f), Vector3.zero, Vector3.one), hazard,
                        MeshUvMode.BoxProject);

                    // Четыре опоры и направляющие — видно, что платформа ездит.
                    for (int leg = 0; leg < 4; leg++)
                    {
                        float x = (leg % 2 == 0 ? -1f : 1f) * 1.1f;
                        float z = (leg < 2 ? -1f : 1f) * 0.72f;

                        platform.Add(MeshPrimitives.Prism(0.06f, 0.06f, 0.16f, 8, 0.008f, false),
                            MeshBuilder.Trs(new Vector3(x, 0.08f, z), Vector3.zero, Vector3.one), iron,
                            MeshUvMode.BoxProject);
                    }

                    platform.Add(MeshPrimitives.Prism(0.055f, 0.055f, 1.9f, 8, 0.008f, false),
                        MeshBuilder.Trs(new Vector3(-1.22f, -0.85f, 0.7f), Vector3.zero, Vector3.one), iron,
                        MeshUvMode.BoxProject);
                    platform.Add(MeshPrimitives.Prism(0.055f, 0.055f, 1.9f, 8, 0.008f, false),
                        MeshBuilder.Trs(new Vector3(1.22f, -0.85f, 0.7f), Vector3.zero, Vector3.one), iron,
                        MeshUvMode.BoxProject);
                }

                GameObject node = inPlayerClearance
                    ? MakeNode("Cargo_Platform_" + i, parent, position)
                    : Seal("Cargo_Platform_" + i, parent, platform);

                node.transform.localPosition = position;

                // Локальная +Z платформы смотрит к центру стола — как у сидящего диктатора.
                node.transform.localRotation = Quaternion.Euler(0f, 270f - angle * Mathf.Rad2Deg, 0f);
            }
        }

        /// <summary>Пустые узлы рабочих мест за столом: ориентированы лицом к центру стола.</summary>
        private void BuildSummitSeats(Transform parent)
        {
            for (int i = 0; i < GeneratedObjectNames.CountryCount; i++)
            {
                GameObject node = MakeNode("Summit_Seat_" + i, parent, BunkerMeshFactory.GetSummitSeatPosition(i));
                float angle = BunkerMeshFactory.GetSummitSeatAngle(i);
                node.transform.localRotation = Quaternion.Euler(0f, 270f - angle * Mathf.Rad2Deg, 0f);
                _summitSeats[i] = node.transform;
            }
        }

        /// <summary>
        /// Подвесной катушечный магнитофон над центром стола на тросе. Стартовая высота —
        /// под потолком; директор опускает его на фазе компромата.
        /// </summary>
        private void BuildLieDetector(Transform parent)
        {
            Material iron = Mat.CastIron();
            Material steel = Mat.ColdSteel();
            Material neon = Mat.Neon(new Color(1f, 0.09f, 0.27f));

            GameObject trolley = MakeNode("LieDetector_Trolley", parent, LieDetectorRestHeight);

            MeshBuilder rig = new MeshBuilder();

            // Кронштейн с тросом.
            rig.Add(MeshPrimitives.Prism(0.09f, 0.09f, 0.5f, 10, 0.01f, false),
                MeshBuilder.Trs(new Vector3(0f, 2.05f, 0f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);
            rig.Add(MeshPrimitives.Tube(new[]
            {
                new Vector3(0f, 1.82f, 0f),
                new Vector3(0f, 1.2f, 0f)
            }, 0.012f, 6), Matrix4x4.identity, steel, MeshUvMode.BoxProject);

            Seal("LieDetector_Mount", trolley.transform, rig);

            // Сам магнитофон собирается фабрикой как отдельный прибор стола, но здесь он
            // висит над столом — поэтому строим свою коробку с двумя катушками.
            MeshBuilder recorder = new MeshBuilder();
            recorder.Add(MeshPrimitives.BeveledBox(new Vector3(0.46f, 0.24f, 0.32f), 0.02f),
                MeshBuilder.Trs(new Vector3(0f, 1.06f, 0f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);

            recorder.Add(MeshPrimitives.Prism(0.1f, 0.1f, 0.045f, 16, 0.006f, true),
                MeshBuilder.Trs(new Vector3(-0.12f, 1.06f, 0.17f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);
            recorder.Add(MeshPrimitives.Prism(0.1f, 0.1f, 0.045f, 16, 0.006f, true),
                MeshBuilder.Trs(new Vector3(0.12f, 1.06f, 0.17f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Красные индикаторы записи на передней панели.
            for (int i = 0; i < 3; i++)
            {
                recorder.Add(MeshPrimitives.Prism(0.018f, 0.018f, 0.02f, 8, 0.004f, true),
                    MeshBuilder.Trs(new Vector3(-0.1f + i * 0.1f, 1.06f, -0.17f), new Vector3(90f, 0f, 0f),
                        Vector3.one), neon, MeshUvMode.BoxProject);
            }

            Seal(GeneratedObjectNames.LieDetectorRecorder, trolley.transform, recorder);

            _lieDetectorTrolley = trolley.transform;
        }

        /// <summary>Высота подвеса магнитофона в покое — у самого потолка.</summary>
        public Vector3 LieDetectorRestHeight
        {
            get { return new Vector3(0f, 5.4f, 0f); }
        }

        /// <summary>Высота, на которую магнитофон опускается над столом на фазе компромата.</summary>
        public Vector3 LieDetectorActiveHeight
        {
            get { return new Vector3(0f, 2.35f, 0f); }
        }

        #endregion

        #region Материалы

        /// <summary>Чугун оболочки. Открыт наружу: директор освещения ставит на него стойки маяков.</summary>
        public static Material SharedIron
        {
            get { return Mat.CastIron(); }
        }

        /// <summary>Холодная сталь оболочки.</summary>
        public static Material SharedSteel
        {
            get { return Mat.ColdSteel(); }
        }

        /// <summary>
        /// Ленивый кэш материалов сборщика. Отдельный от BunkerMeshFactory: фабрика
        /// отвечает за приборы, сборщик — за оболочку зданий.
        /// </summary>
        private static class Mat
        {
            private static readonly Dictionary<string, Material> s_Cache = new Dictionary<string, Material>();

            private static Material Get(string key, System.Func<Material> factory)
            {
                Material material;
                if (s_Cache.TryGetValue(key, out material) && material != null)
                {
                    return material;
                }

                material = factory();
                s_Cache.Add(key, material);
                return material;
            }

            public static Material Concrete()
            {
                return Get("console_concrete", delegate
                {
                    return RetroPbrTextureGenerator.GetBunkerConcrete()
                        .CreateMaterial("Mat_World_Concrete", RetroBlendMode.Opaque);
                });
            }

            public static Material CastIron()
            {
                return Get("console_iron", delegate
                {
                    return RetroPbrTextureGenerator.GetCastIron()
                        .CreateMaterial("Mat_World_CastIron", RetroBlendMode.Opaque);
                });
            }

            public static Material ColdSteel()
            {
                return Get("console_steel", delegate
                {
                    return RetroPbrTextureGenerator.GetColdSteel()
                        .CreateMaterial("Mat_World_ColdSteel", RetroBlendMode.Opaque);
                });
            }

            public static Material Hazard()
            {
                return Get("console_hazard", delegate
                {
                    return RetroPbrTextureGenerator.GetHazardStripes()
                        .CreateMaterial("Mat_World_Hazard", RetroBlendMode.Opaque);
                });
            }

            /// <summary>Столешница пульта: тёплая потёртая сталь с ржавчиной и царапинами.</summary>
            public static Material ConsoleTop()
            {
                return Get("console_top", delegate
                {
                    return RetroPbrTextureGenerator.GetBunkerConsoleTop()
                        .CreateMaterial("Mat_World_ConsoleTop", RetroBlendMode.Opaque);
                });
            }

            /// <summary>Ткань обивки кресла игрока.</summary>
            public static Material Cloth(Color color)
            {
                string key = "console_cloth_" + ColorUtility.ToHtmlStringRGB(color);
                return Get(key, delegate
                {
                    return RetroPbrTextureGenerator.GetCloth(color)
                        .CreateMaterial("Mat_World_Cloth_" + key, RetroBlendMode.Opaque);
                });
            }

            /// <summary>Бумага: пачки приказов в планшете на столе.</summary>
            public static Material Paper()
            {
                return Get("console_paper", delegate
                {
                    return RetroPbrTextureGenerator.GetTreatyPaper()
                        .CreateMaterial("Mat_World_Paper", RetroBlendMode.Opaque);
                });
            }

            public static Material Neon(Color color)
            {
                string key = "console_neon_" + ColorUtility.ToHtmlStringRGB(color);
                return Get(key, delegate
                {
                    return RetroPbrTextureGenerator.GetNeon(color)
                        .CreateMaterial("Mat_World_Neon_" + key, RetroBlendMode.Additive);
                });
            }
        }

        #endregion

        #region Проводка голограмм

        /// <summary>
        /// Подключает шейдер WireframeHologram к узлам куполов ПВО и архипелагов.
        /// Вызывается после сборки локаций: узлы уже созданы фабрикой.
        /// </summary>
        public void ApplyHologramShaders()
        {
            Shader shader = Shader.Find(HologramShaderName);

            if (shader == null)
            {
                if (!_warnedAboutHologramShader)
                {
                    _warnedAboutHologramShader = true;
                    Debug.LogWarning("[WorldCrisis] BunkerWorldBuilder: не найден шейдер " + HologramShaderName
                                     + ", купола ПВО останутся обычными материалами.");
                }

                return;
            }

            Color shield = new Color(0f, 0.9f, 0.46f, 1f);
            Color territory = new Color(1f, 0.69f, 0f, 1f);

            if (_bunkerRoot != null)
            {
                ApplyHologram(_bunkerRoot, GeneratedObjectNames.AaShieldDome, shield, 0.5f);
            }

            if (_summitRoot != null)
            {
                for (int i = 0; i < GeneratedObjectNames.CountryCount; i++)
                {
                    ApplyHologram(_summitRoot, GeneratedObjectNames.Archipelago(GameDefinitions.AllCountryIds[i]),
                        territory, 0.35f);
                }
            }
        }

        private const string HologramShaderName = "WorldCrisis/Wireframe Hologram";

        private static void ApplyHologram(Transform root, string nodeName, Color glow, float intensity)
        {
            List<Transform> nodes = FindAllDeep(root, nodeName);

            for (int i = 0; i < nodes.Count; i++)
            {
                Transform node = nodes[i];

                if (node == null)
                {
                    continue;
                }

                MeshRenderer renderer = node.GetComponent<MeshRenderer>();

                if (renderer == null)
                {
                    continue;
                }

                // Один материал на узел: иначе трипланарная UV-развёртка всех субмешей
                // начнёт рисовать сетку поверх непрозрачных деталей купола.
                Material material = new Material(FindHologramShader())
                {
                    name = "Mat_Hologram_" + nodeName,
                    hideFlags = HideFlags.HideAndDontSave
                };

                material.SetColor("_GlowColor", glow);
                material.SetColor("_GlowColorCore", new Color(1f, 1f, 1f, 1f));
                material.SetFloat("_Intensity", intensity);

                renderer.sharedMaterials = new[] { material };
            }
        }

        private static Shader s_HologramShader;

        private static Shader FindHologramShader()
        {
            if (s_HologramShader == null)
            {
                s_HologramShader = Shader.Find(HologramShaderName);
            }

            return s_HologramShader;
        }

        /// <summary>
        /// Все узлы с таким именем вглубь иерархии. Обход в ширину, а не поиск только
        /// прямых детей: купол лежит внутри секции-диорамы, архипелаг — внутри стола.
        /// Купол ПВО AAShield_Dome по одному на каждую из четырёх секций-диорам,
        /// поэтому «первый найденный» не годится: три купола так и остались обычным
        /// материалом.
        /// </summary>
        private static List<Transform> FindAllDeep(Transform root, string nodeName)
        {
            List<Transform> found = new List<Transform>();

            if (root == null || string.IsNullOrEmpty(nodeName))
            {
                return found;
            }

            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();

                if (current.name == nodeName)
                {
                    found.Add(current);
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    queue.Enqueue(current.GetChild(i));
                }
            }

            return found;
        }

        #endregion
    }
}
