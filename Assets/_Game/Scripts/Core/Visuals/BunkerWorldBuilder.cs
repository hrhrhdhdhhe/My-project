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

        /// <summary>
        /// Высота потолка личного бункера. Низкая, как в настоящем бункерном отсеке:
        /// при 4.3 м кабина читалась бы как ангар, а лампа под потолком и гирлянда
        /// кабелей висели бы в недосягаемой высоте. Над головой игрока остаётся
        /// почти метр, и плафон с кабелями попадают в верхнюю часть кадра.
        /// </summary>
        public const float BunkerRoomHeight = 2.05f;

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

        /// <summary>Радиус круглого зала саммита: концепт-арт 1-в-1, 3.80 м.</summary>
        public const float SummitShaftRadius = 3.8f;

        /// <summary>Высота потолка зала саммита.</summary>
        public const float SummitCeilingHeight = 2.7f;

        /// <summary>Число колонн по периметру зала.</summary>
        public const int SummitColumnCount = 8;

        /// <summary>Число люминесцентных светильников под потолком зала.</summary>
        public const int SummitLampCount = 3;

        /// <summary>Радиус стола саммита — общий контракт раскладки.</summary>
        public const float SummitTableRadius = SummitLayout.TableRadius;

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
/// <summary>
        /// Бетонная оболочка кабины. Ось +Z — направление взгляда игрока, поэтому
        /// дальняя стена стоит на BunkerBackWallZ (1.68 м) и именно её видно за столом.
        /// Комната низкая и узкая: стол, приборы и ниша двери читаются в одном кадре.
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
            // иначе ниша двери и щитки уходят в бетон.
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
            BuildDoorNiche(parent);
            BuildCeilingLamp(parent);
            BuildCeilingCables(parent);
        }

        private static void AddWall(MeshBuilder builder, Material material, Vector3 center, Vector3 size)
        {
            builder.Add(MeshPrimitives.BeveledBox(size, 0.04f),
                MeshBuilder.Trs(center, Vector3.zero, Vector3.one), material, MeshUvMode.BoxProject);
        }

        /// <summary>
        /// Ниша гермодвери в дальней стене: 0.90 x 1.50 м (x от -0.45 до +0.45), полотно
        /// уходит в бетон до z = 1.65. Полотно тёмно-стальное #121417 — обязано читаться
        /// как дверь, но не засвечиваться в кадре. Снаружи — рама и маховик аварийного
        /// открывания, СТРОГО по центру полотна (0, 0.98, 1.60) — как в спецификации:
        /// раньше колесо красовалось справа от двери, как герб на брошенном контейнере.
        /// </summary>
        private void BuildDoorNiche(Transform parent)
        {
            Material iron = Mat.CastIron();
            Material steel = Mat.ColdSteel();
            Material dark = Mat.Plastic(RetroPbrTextureGenerator.DoorNicheColor);

            MeshBuilder niche = new MeshBuilder();

            const float halfWidth = SummitLayout.Bunker.DoorHalfWidth;
            const float height = SummitLayout.Bunker.DoorHeight;
            const float nicheZ = SummitLayout.Bunker.DoorNicheZ;

            // Тёмное полотно двери, чуть утопленное в стену.
            niche.Add(MeshPrimitives.BeveledBox(new Vector3(halfWidth * 2f, height, 0.12f), 0.01f),
                MeshBuilder.Trs(new Vector3(0f, height * 0.5f, nicheZ + 0.06f), Vector3.zero, Vector3.one), dark,
                MeshUvMode.BoxProject);

            // Стальная рама проёма по периметру: вертикальные стойки и перемычка.
            const float bar = 0.1f;
            niche.Add(MeshPrimitives.Frame(new Vector2(halfWidth * 2f + bar * 2f, height + bar), bar, 0.1f, 0.02f),
                MeshBuilder.Trs(new Vector3(0f, height * 0.5f, nicheZ + 0.02f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Заклёпки по раме — ниша выглядит врезанной в бетон, а не нарисованной.
            for (int i = 0; i < 5; i++)
            {
                float y = 0.14f + i * (height - 0.28f) / 4f;
                niche.Add(MeshPrimitives.RivetDome(0.014f, 0.01f),
                    MeshBuilder.Align(new Vector3(-halfWidth - bar * 0.5f, y, nicheZ - 0.03f), Vector3.back), steel,
                    MeshUvMode.BoxProject);
                niche.Add(MeshPrimitives.RivetDome(0.014f, 0.01f),
                    MeshBuilder.Align(new Vector3(halfWidth + bar * 0.5f, y, nicheZ - 0.03f), Vector3.back), steel,
                    MeshUvMode.BoxProject);
            }

            // Маховик аварийного открывания: по центру полотна, 1-в-1 из спецификации.
            Vector3 wheel = new Vector3(0f, 0.98f, 1.60f);
            niche.Add(MeshPrimitives.Prism(0.05f, 0.05f, 0.14f, 10, 0.008f, false),
                MeshBuilder.Align(wheel, Vector3.back), steel, MeshUvMode.BoxProject);
            niche.Add(MeshPrimitives.Torus(0.14f, 0.022f, 14, 6),
                MeshBuilder.Align(wheel + Vector3.back * 0.07f, Vector3.back), steel, MeshUvMode.BoxProject);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f + 0.4f;
                Vector3 spoke = wheel + Vector3.back * 0.07f;
                niche.Add(MeshPrimitives.Tube(new[]
                {
                    spoke,
                    spoke + new Vector3(Mathf.Cos(angle) * 0.14f, Mathf.Sin(angle) * 0.14f, 0f)
                }, 0.013f, 6), Matrix4x4.identity, steel, MeshUvMode.BoxProject);
            }

            Seal("Bunker_DoorNiche", parent, niche);
        }

        /// <summary>
        /// Трубные банки с вентилями и гофрами, распределительные щитки с индикаторами
        /// и жгуты кабелей вдоль дальней стены. Всё это стоит за столом и заполняет
        /// верхнюю треть кадра, которая до переделки была пустым бетоном.
        /// </summary>
        private void BuildBunkerWallServices(Transform parent, Material iron, Material steel)
        {
            MeshBuilder services = new MeshBuilder();
            BunkerMeshFactory.DetRandom random = new BunkerMeshFactory.DetRandom(0x9C1);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (BunkerRoomHalfWidth - 0.22f);

                // Две магистрали под потолком.
                for (int line = 0; line < 2; line++)
                {
                    float y = BunkerRoomHeight - 0.28f - line * 0.24f;
                    float radius = line == 0 ? 0.075f : 0.05f;

                    Vector3[] path = new Vector3[6];
                    for (int i = 0; i < path.Length; i++)
                    {
                        float t = i / (float)(path.Length - 1);
                        path[i] = new Vector3(x, y + Mathf.Sin(t * Mathf.PI) * 0.02f,
                            Mathf.Lerp(BunkerFrontWallZ + 0.4f, BunkerBackWallZ - 0.16f, t));
                    }

                    services.Add(MeshPrimitives.Tube(path, radius, 8), Matrix4x4.identity,
                        line == 0 ? iron : steel, MeshUvMode.BoxProject);

                    for (int i = 1; i < path.Length - 1; i++)
                    {
                        services.Add(MeshPrimitives.Torus(radius + 0.012f, 0.015f, 10, 5),
                            MeshBuilder.Align(path[i], Vector3.forward), steel, MeshUvMode.BoxProject);
                    }
                }

                // Вентиль на магистрали.
                float valveZ = BunkerBackWallZ - 0.62f;
                services.Add(MeshPrimitives.Prism(0.045f, 0.045f, 0.2f, 8, 0.008f, false),
                    MeshBuilder.Trs(new Vector3(x - side * 0.13f, BunkerRoomHeight - 0.52f, valveZ),
                        new Vector3(0f, 0f, 90f * -side), Vector3.one), iron, MeshUvMode.BoxProject);
                services.Add(MeshPrimitives.Torus(0.115f, 0.02f, 12, 6),
                    MeshBuilder.Align(new Vector3(x - side * 0.24f, BunkerRoomHeight - 0.52f, valveZ),
                        new Vector3(-side, 0f, 0f)), steel, MeshUvMode.BoxProject);

                // Гофра на нижней ветке: серия колец на трубе.
                for (int i = 0; i < 6; i++)
                {
                    float z = BunkerBackWallZ - 0.24f - i * 0.09f;
                    services.Add(MeshPrimitives.Torus(0.07f, 0.016f, 10, 5),
                        MeshBuilder.Align(new Vector3(x, BunkerRoomHeight - 0.52f, z), Vector3.forward), steel,
                        MeshUvMode.BoxProject);
                }

                // Жгут из четырёх кабелей, уходящий под потолок.
                const int bundle = 4;
                for (int i = 0; i < bundle; i++)
                {
                    float offset = random.Range(-0.05f, 0.05f);
                    Vector3[] path = new Vector3[7];

                    for (int k = 0; k < path.Length; k++)
                    {
                        float t = k / (float)(path.Length - 1);
                        path[k] = Vector3.Lerp(
                                new Vector3(x - side * 0.12f + offset, 1.72f, BunkerBackWallZ - 0.08f),
                                new Vector3(x - side * 0.3f + offset, BunkerRoomHeight - 0.14f,
                                    BunkerBackWallZ - 0.5f),
                                t)
                            + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.1f;
                    }

                    services.Add(MeshPrimitives.Tube(path, 0.016f, 6), Matrix4x4.identity,
                        i % 2 == 0 ? steel : iron, MeshUvMode.BoxProject);
                }
            }

            // Ржавые трубы вдоль дальней стены по обе стороны от двери: 1-в-1 из
            // спецификации — x от ±0.55 до ±1.35, цвет #201915, радиусы 0.04..0.07.
            Material rusty = Mat.Plastic(new Color(0.125f, 0.098f, 0.082f));
            float pipeBackZ = BunkerBackWallZ - 0.04f;
            float[][] pipeRunsX =
            {
                new[] { -1.35f, -0.55f },
                new[] { 0.55f, 1.35f }
            };
            float[] pipeCenterY = { 1.06f, 1.34f, 1.62f };
            float[] pipeRadius = { 0.04f, 0.06f, 0.05f };

            for (int run = 0; run < pipeRunsX.Length; run++)
            {
                for (int line = 0; line < pipeRadius.Length; line++)
                {
                    Vector3[] pipePath =
                    {
                        new Vector3(pipeRunsX[run][0], pipeCenterY[line], pipeBackZ),
                        new Vector3(pipeRunsX[run][1], pipeCenterY[line], pipeBackZ)
                    };

                    services.Add(MeshPrimitives.Tube(pipePath, pipeRadius[line], 8), Matrix4x4.identity,
                        rusty, MeshUvMode.BoxProject);
                }
            }

            Seal("Bunker_WallServices", parent, services);
        }

        /// <summary>
        /// Потолочный плафон над столом, 1-в-1 из спецификации: узел Bunker_Ceiling_Lamp
        /// с коническим абажуром (радиус 0.15, высота 0.10) в точке (0, 1.48, 0.52) и
        /// отдельная лампа накаливания радиусом 0.038, цвет #FFB74D. Раньше абажур был
        /// в три раза шире и вешал на 1.52 — кук образовался оранжевый балун во весь кадр.
        /// </summary>
        private void BuildCeilingLamp(Transform parent)
        {
            Material iron = Mat.CastIron();
            Material bulb = Mat.Neon(new Color(1f, 0.718f, 0.302f));
            Vector3 anchor = SummitLayout.Bunker.CeilingLampPosition;

            MeshBuilder lamp = new MeshBuilder();

            // Конический абажур: узкий верх наверху (0.06), широкий край снизу (0.15),
            // нижняя кромка ровно на точке подвеса.
            lamp.Add(MeshPrimitives.Prism(0.06f, 0.15f, 0.10f, 14, 0.006f, false),
                MeshBuilder.Trs(anchor + new Vector3(0f, 0.05f, 0f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);
            lamp.Add(MeshPrimitives.Torus(0.147f, 0.008f, 16, 6),
                MeshBuilder.Align(anchor, Vector3.down), iron, MeshUvMode.BoxProject);

            // Короткая подвеска от абажура к потолку.
            lamp.Add(MeshPrimitives.Tube(new[]
            {
                new Vector3(anchor.x, anchor.y + 0.10f, anchor.z),
                new Vector3(anchor.x, BunkerRoomHeight - 0.02f, anchor.z)
            }, 0.008f, 6), Matrix4x4.identity, iron, MeshUvMode.BoxProject);

            Seal("Bunker_Ceiling_Lamp", parent, lamp);

            MeshBuilder filament = new MeshBuilder();
            filament.Add(MeshPrimitives.Sphere(0.038f, 12, 7),
                MeshBuilder.Trs(anchor + new Vector3(0f, -0.018f, 0f), Vector3.zero, Vector3.one), bulb,
                MeshUvMode.BoxProject);
            Seal("Bunker_Ceiling_Bulb", parent, filament);
        }

        /// <summary>
        /// Гирлянда из десяти провисающих чёрных кабелей под потолком: точки крепления
        /// заданы по дуге от (0, 1.62, 0.60), провис считается через CreateCableCurve.
        /// Кабели дают верхней части кадара «грязную» графику, которой не хватало
        /// после уменьшения высоты потолка.
        /// </summary>
        private void BuildCeilingCables(Transform parent)
        {
            Material rubber = Mat.Plastic(new Color(0.035f, 0.035f, 0.04f));
            MeshBuilder cables = new MeshBuilder();
            Vector3 anchor = SummitLayout.Bunker.CableAnchorPosition;

            for (int i = 0; i < CeilingCableCount; i++)
            {
                float t = i / (float)(CeilingCableCount - 1);
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 start = anchor + new Vector3(side * (0.12f + t * 0.95f), 0f, -0.06f - t * 0.12f);
                Vector3 end = start + new Vector3(side * 0.34f, -0.02f, -0.5f - t * 0.22f);

                cables.Add(MeshPrimitives.Tube(CreateCableCurve(start, end, 0.06f + t * 0.02f), 0.012f, 5),
                    Matrix4x4.identity, rubber, MeshUvMode.BoxProject);
            }

            // Крепёжные площадки на потолке, из которых кабели выходят.
            for (int i = 0; i < CeilingCableCount; i += 2)
            {
                float t = i / (float)(CeilingCableCount - 1);
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 start = anchor + new Vector3(side * (0.12f + t * 0.95f), 0f, -0.06f - t * 0.12f);
                cables.Add(MeshPrimitives.BeveledBox(new Vector3(0.05f, 0.02f, 0.05f), 0.004f),
                    MeshBuilder.Trs(start + new Vector3(0f, (BunkerRoomHeight - start.y) * 0.5f, 0f),
                        Vector3.zero, Vector3.one), Mat.ColdSteel(), MeshUvMode.BoxProject);
            }

            Seal("Bunker_Ceiling_Cables", parent, cables);
        }

        /// <summary>Число кабелей в гирлянде под потолком.</summary>
        private const int CeilingCableCount = 10;

        /// <summary>
        /// Провисающая кривая кабеля: прямая между точками со сдвигом вниз по параболе,
        /// плюс мелкая волна по X, чтобы десять кабелей под потолком не совпали.
        /// </summary>
        private static Vector3[] CreateCableCurve(Vector3 start, Vector3 end, float sag)
        {
            const int steps = 9;
            Vector3[] path = new Vector3[steps];

            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * sag;
                point.x += Mathf.Sin(t * Mathf.PI * 2f) * sag * 0.18f;
                path[i] = point;
            }

            return path;
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
            float top = SummitLayout.Bunker.DeskTopY;
            Vector3 camPos = BunkerEyePosition;

            // Слева: узел связи с три карболитовых телефона, стоящие прямо на столе,
            // и ламповая планка позади них. Диски набора — «лицо» прибора, по ним
            // и определяется разворот; телефоны построены фронтом в -Z, поэтому
            // пивот ставится без разворота. Пульт НЕ вписывается в габарит через
            // FitPropToBox: единый масштаб разъехался бы на координаты телефонов.
            GameObject switchboard = BunkerMeshFactory.CreateSwitchboardUnit(parent);
            switchboard.transform.localPosition = SummitLayout.Bunker.DeskPropAnchor[0];
            switchboard.transform.localRotation = Quaternion.Euler(0f, SummitLayout.Bunker.DeskPropYaw[0], 0f);
            EnsureFrontChildFacesCamera(switchboard, GeneratedObjectNames.DialWheel, camPos);

            // Слева-спереди: планшет с бумагами.
            GameObject clipboard = BuildClipboard(parent);
            FitPropToBox(clipboard, SummitLayout.Bunker.DeskPropMaxSize[1], SummitLayout.Bunker.DeskPropAnchor[1],
                SummitLayout.Bunker.DeskPropYaw[1]);

            // По центру: ядерный пульт с красной кнопкой. Лицо (замок и ключ) построено
            // в -Z, сажается без разворота и без EnsureFrontChildFacesCamera: ключ,
            // торчащий вперёд, смещает центр габарита к камере и мог бы ложно
            // развернуть пульт на 180°.
            GameObject nuclearBox = BunkerMeshFactory.CreateNuclearLaunchBox(parent, Vector3.zero);
            FitPropToBox(nuclearBox, SummitLayout.Bunker.DeskPropMaxSize[2], SummitLayout.Bunker.DeskPropAnchor[2],
                SummitLayout.Bunker.DeskPropYaw[2]);

            // Тумблеры приказов Switch_01..03 стоят на столешнице слева от пульта,
            // шаг 0.07: узлы ложатся на 0.34 / 0.41 / 0.48 по X.
            BunkerMeshFactory.CreateDeskOrderSwitches(parent, new Vector3(0.41f, top, 0.28f));

            // Справа: ЭЛТ-монитор CITY STATUS и принтер правее него. Лицо — экран.
            GameObject crtTerminal = BunkerMeshFactory.CreateOrdersCrtTerminal(parent, Vector3.zero);
            FitPropToBox(crtTerminal, SummitLayout.Bunker.DeskPropMaxSize[3], SummitLayout.Bunker.DeskPropAnchor[3],
                SummitLayout.Bunker.DeskPropYaw[3]);
            EnsureFrontChildFacesCamera(crtTerminal, GeneratedObjectNames.ScreenSurface, camPos);

            GameObject printer = BunkerMeshFactory.CreateDotMatrixPrinter(parent, Vector3.zero);
            FitPropToBox(printer, SummitLayout.Bunker.DeskPropMaxSize[4], SummitLayout.Bunker.DeskPropAnchor[4],
                SummitLayout.Bunker.DeskPropYaw[4]);

            // Техническая документация к монитору: лист на столе у правого края.
            Seal("Bunker_Tech_Doc", parent, BuildTechDoc());

            // Слева на боковой стене: табло статуса государств. Секции-диорамы должны
            // смотреть ВНУТРЬ комнаты (их кромка локально +Z), а не в бетон.
            GameObject statusBoard = BunkerMeshFactory.CreateStateStatusBoard(parent, Vector3.zero, cities);
            FitPropToBox(statusBoard, new Vector3(1.05f, 0.58f, 0.14f),
                new Vector3(StatusBoardX, StatusBoardBottomY, StatusBoardZ), StatusBoardYaw);
            EnsureFrontChildFacesCamera(statusBoard, GeneratedObjectNames.CityDiorama(0), camPos);
        }

        /// <summary>
        /// Табло статуса стоит ПЕРЕД задней стеной (внутренняя плоскость стены —
        /// BunkerBackWallZ) и слева от ниши гермодвери, справа от трубных щитков:
        /// раньше задняя плита табло уходила в бетон, и игрок видел четыре серых
        /// квадрата задника вместо городов.
        /// </summary>
        private const float StatusBoardZ = 1.54f;
        private const float StatusBoardX = -1.35f;
        private const float StatusBoardYaw = 180f;
        private const float StatusBoardBottomY = 1f;

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

        /// <summary>
        /// Техническая документация к ЭЛТ-монитору: два листа с таблицей, заложенные
        /// под стеклянную пластину, и карандаш рядом. Стоит у правого края стола,
        /// где на концепт-арте лежат бумаги с печатной схемой.
        /// </summary>
        private static MeshBuilder BuildTechDoc()
        {
            Material paper = Mat.Paper();
            Material steel = Mat.ColdSteel();

            MeshBuilder doc = new MeshBuilder();

            doc.Add(MeshPrimitives.Plate(new Vector2(0.28f, 0.2f), 0.004f, 0.001f),
                MeshBuilder.Trs(new Vector3(0.72f, DeskTopY + 0.004f, 0.22f), new Vector3(0f, -14f, 0f),
                    Vector3.one), paper, MeshUvMode.BoxProject);
            doc.Add(MeshPrimitives.Plate(new Vector2(0.28f, 0.2f), 0.004f, 0.001f),
                MeshBuilder.Trs(new Vector3(0.74f, DeskTopY + 0.008f, 0.24f), new Vector3(0f, 4f, 0f),
                    Vector3.one), paper, MeshUvMode.BoxProject);

            // Строки печатной таблицы на верхнем листе.
            for (int i = 0; i < 6; i++)
            {
                doc.Add(MeshPrimitives.BeveledBox(new Vector3(0.2f - i * 0.012f, 0.001f, 0.006f), 0f),
                    MeshBuilder.Trs(new Vector3(0.72f, DeskTopY + 0.011f, 0.16f + i * 0.026f),
                        new Vector3(0f, -14f, 0f), Vector3.one), steel, MeshUvMode.BoxProject);
            }

            // Карандаш.
            doc.Add(MeshPrimitives.Prism(0.005f, 0.005f, 0.16f, 6, 0f, false),
                MeshBuilder.Trs(new Vector3(0.88f, DeskTopY + 0.006f, 0.2f), new Vector3(0f, 0f, 90f),
                    Vector3.one), Mat.Cloth(new Color(0.6f, 0.5f, 0.2f)), MeshUvMode.BoxProject);

            return doc;
        }

        #endregion
        #region Локация №2 — Круглый зал саммита

        /// <summary>
        /// Круглая бетонная шахта: стена уходит вверх и вниз в туман, вокруг стола кольцо
        /// пола, по периметру — грузовые платформы рабочих мест и поручни.
/// <summary>
        /// Круглый зал саммита: каменный цилиндр радиусом 3.80 м и высотой 2.70 м,
        /// восемь колонн по периметру с трубами и кабелями между ними, три
        /// люминесцентных светильника под потолком и низкий круглый подиум стола.
        ///
        /// Раньше здесь стояла бетонная шахта радиусом 11 м и высотой 9.2 м: игрок
        /// терял диктаторов в тумане, а стол выглядел игрушечной на дне колодца.
        /// </summary>
        private void BuildSummitShell(Transform parent)
        {
            Material concrete = Mat.Concrete();
            Material iron = Mat.CastIron();
            Material steel = Mat.ColdSteel();

            MeshBuilder shell = new MeshBuilder();

            // Стены зала: замкнутый профиль тела вращения от пола до потолка и обратно.
            Vector2[] wallProfile =
            {
                new Vector2(SummitShaftRadius, -0.4f),
                new Vector2(SummitShaftRadius, SummitCeilingHeight),
                new Vector2(SummitShaftRadius + 0.5f, SummitCeilingHeight),
                new Vector2(SummitShaftRadius + 0.5f, -0.4f),
                new Vector2(SummitShaftRadius, -0.4f)
            };

            shell.Add(MeshPrimitives.Lathe(wallProfile, 48, false, 0.7f), Matrix4x4.identity, concrete,
                MeshUvMode.BoxProject);

            // Потолочная плита.
            shell.Add(MeshPrimitives.Prism(SummitShaftRadius + 0.5f, SummitShaftRadius + 0.5f, 0.3f, 48, 0.05f,
                    false),
                MeshBuilder.Trs(new Vector3(0f, SummitCeilingHeight + 0.15f, 0f), Vector3.zero, Vector3.one),
                concrete, MeshUvMode.BoxProject);

            // Кольцо пола вокруг стола — ступень подиума.
            shell.Add(MeshPrimitives.Ring(SummitTableRadius + 0.25f, SummitShaftRadius, 0.18f, 48),
                MeshBuilder.Trs(new Vector3(0f, -0.09f, 0f), Vector3.zero, Vector3.one), concrete,
                MeshUvMode.BoxProject);

            // Восемь колонн у стены: они ломают ровный цилиндр и держат трубы.
            for (int i = 0; i < SummitColumnCount; i++)
            {
                float angle = i * Mathf.PI * 2f / SummitColumnCount + Mathf.PI / SummitColumnCount;
                Vector3 position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))
                    * (SummitShaftRadius - 0.22f);

                shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.3f, SummitCeilingHeight, 0.3f), 0.03f),
                    MeshBuilder.Trs(position + new Vector3(0f, SummitCeilingHeight * 0.5f, 0f),
                        new Vector3(0f, -angle * Mathf.Rad2Deg, 0f), Vector3.one), concrete, MeshUvMode.BoxProject);

                // Капитель и цоколь колонны.
                shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.38f, 0.12f, 0.38f), 0.02f),
                    MeshBuilder.Trs(position + new Vector3(0f, SummitCeilingHeight - 0.06f, 0f),
                        new Vector3(0f, -angle * Mathf.Rad2Deg, 0f), Vector3.one), iron, MeshUvMode.BoxProject);
                shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.38f, 0.1f, 0.38f), 0.02f),
                    MeshBuilder.Trs(position + new Vector3(0f, 0.05f, 0f),
                        new Vector3(0f, -angle * Mathf.Rad2Deg, 0f), Vector3.one), iron, MeshUvMode.BoxProject);
            }

            // Вертикальные трубы и кабели между колоннами — стена перестаёт быть пустой.
            for (int i = 0; i < SummitColumnCount; i++)
            {
                float angle = i * Mathf.PI * 2f / SummitColumnCount + Mathf.PI / SummitColumnCount;
                Vector3 basePoint = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))
                    * (SummitShaftRadius - 0.4f);

                for (int line = 0; line < 2; line++)
                {
                    float radius = line == 0 ? 0.055f : 0.032f;
                    float x = basePoint.x + Mathf.Cos(angle + Mathf.PI * 0.5f) * (0.08f + line * 0.11f);
                    float z = basePoint.z + Mathf.Sin(angle + Mathf.PI * 0.5f) * (0.08f + line * 0.11f);

                    shell.Add(MeshPrimitives.Tube(new[]
                    {
                        new Vector3(x, 0.12f, z),
                        new Vector3(x, SummitCeilingHeight - 0.12f, z)
                    }, radius, 8), Matrix4x4.identity, line == 0 ? iron : steel, MeshUvMode.BoxProject);

                    for (int b = 1; b < 4; b++)
                    {
                        shell.Add(MeshPrimitives.Torus(radius + 0.012f, 0.014f, 10, 5),
                            MeshBuilder.Align(new Vector3(x, 0.12f + b * (SummitCeilingHeight - 0.24f) / 4f, z),
                                Vector3.right), steel, MeshUvMode.BoxProject);
                    }
                }
            }

            Seal("Summit_Shell", parent, shell);

            BuildCeilingLights(parent);
            BuildSeatChairs(parent);
        }

        /// <summary>Три люминесцентных светильника под потолком зала: холодный белый #D8EAE6.</summary>
        private void BuildCeilingLights(Transform parent)
        {
            Material steel = Mat.ColdSteel();
            Material tube = Mat.Neon(new Color(0.847f, 0.918f, 0.902f));

            MeshBuilder lamps = new MeshBuilder();

            for (int i = 0; i < SummitLampCount; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / SummitLampCount;
                Vector3 position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2.1f;

                // Корпус светильника и две подвески к потолку.
                lamps.Add(MeshPrimitives.BeveledBox(new Vector3(1.3f, 0.1f, 0.24f), 0.02f),
                    MeshBuilder.Trs(position + new Vector3(0f, SummitCeilingHeight - 0.12f, 0f),
                        new Vector3(0f, -angle * Mathf.Rad2Deg, 0f), Vector3.one), steel, MeshUvMode.BoxProject);

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 hang = position + new Vector3(Mathf.Cos(angle) * 0f, SummitCeilingHeight - 0.02f, 0f);
                    hang += Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f) * new Vector3(s * 0.5f, 0f, 0f);
                    lamps.Add(MeshPrimitives.Tube(new[] { hang, hang + Vector3.down * 0.06f }, 0.012f, 6),
                        Matrix4x4.identity, steel, MeshUvMode.BoxProject);
                }

                // Светящаяся трубка снизу — именно она даёт холодный свет на лица.
                lamps.Add(MeshPrimitives.BeveledBox(new Vector3(1.22f, 0.05f, 0.15f), 0.012f),
                    MeshBuilder.Trs(position + new Vector3(0f, SummitCeilingHeight - 0.19f, 0f),
                        new Vector3(0f, -angle * Mathf.Rad2Deg, 0f), Vector3.one), tube, MeshUvMode.BoxProject);
            }

            Seal("Summit_Fluorescent_Fixtures", parent, lamps);
        }

        /// <summary>
        /// Кресла дипломатов за круглым столом: тяжёлое дерево, кожаная спинка и
        /// подлокотники. Раньше на этих местах стояли грузовые платформы-лифты
        /// 2.6 x 1.9 м — в зале радиусом 3.8 м они перекрывали стол целиком.
        /// </summary>
        private void BuildSeatChairs(Transform parent)
        {
            Material wood = Mat.CastIron();
            Material leather = Mat.Cloth(new Color(0.16f, 0.13f, 0.12f));

            for (int i = 0; i < GameDefinitions.AllCountryIds.Length; i++)
            {
                // Место игрока остаётся пустым: кресло в камере закрывает весь кадр.
                if (i == 0)
                {
                    continue;
                }

                Vector3 position = SummitLayout.GetSeatPosition(i, GameDefinitions.AllCountryIds.Length);

                MeshBuilder chair = new MeshBuilder();

                chair.Add(MeshPrimitives.BeveledBox(new Vector3(0.52f, 0.1f, 0.48f), 0.02f),
                    MeshBuilder.Trs(new Vector3(0f, 0.45f, 0f), Vector3.zero, Vector3.one), wood,
                    MeshUvMode.BoxProject);
                chair.Add(MeshPrimitives.BeveledBox(new Vector3(0.5f, 0.56f, 0.09f), 0.02f),
                    MeshBuilder.Trs(new Vector3(0f, 0.77f, -0.2f), new Vector3(-8f, 0f, 0f), Vector3.one), leather,
                    MeshUvMode.BoxProject);
                chair.Add(MeshPrimitives.BeveledBox(new Vector3(0.5f, 0.09f, 0.08f), 0.02f),
                    MeshBuilder.Trs(new Vector3(0f, 1.02f, -0.24f), new Vector3(-8f, 0f, 0f), Vector3.one), wood,
                    MeshUvMode.BoxProject);

                for (int side = -1; side <= 1; side += 2)
                {
                    chair.Add(MeshPrimitives.BeveledBox(new Vector3(0.07f, 0.06f, 0.4f), 0.015f),
                        MeshBuilder.Trs(new Vector3(side * 0.24f, 0.63f, 0f), Vector3.zero, Vector3.one), wood,
                        MeshUvMode.BoxProject);
                    chair.Add(MeshPrimitives.BeveledBox(new Vector3(0.06f, 0.45f, 0.06f), 0.015f),
                        MeshBuilder.Trs(new Vector3(side * 0.24f, 0.22f, -0.2f), Vector3.zero, Vector3.one), wood,
                        MeshUvMode.BoxProject);
                    chair.Add(MeshPrimitives.BeveledBox(new Vector3(0.06f, 0.45f, 0.06f), 0.015f),
                        MeshBuilder.Trs(new Vector3(side * 0.24f, 0.22f, 0.2f), Vector3.zero, Vector3.one), wood,
                        MeshUvMode.BoxProject);
                }

GameObject chairNode = Seal("Summit_Chair_" + i, parent, chair);
                chairNode.transform.localPosition = position;
                chairNode.transform.localRotation =
                    Quaternion.Euler(0f, SummitLayout.GetSeatYawDegrees(i, GameDefinitions.AllCountryIds.Length), 0f);
            }
        }
        /// <summary>Пустые узлы рабочих мест за столом: ориентированы лицом к центру стола.</summary>
        private void BuildSummitSeats(Transform parent)
        {
            for (int i = 0; i < GeneratedObjectNames.CountryCount; i++)
            {
                GameObject node = MakeNode("Summit_Seat_" + i, parent, BunkerMeshFactory.GetSummitSeatPosition(i));
                node.transform.localRotation =
                    Quaternion.Euler(0f, SummitLayout.GetSeatYawDegrees(i, GeneratedObjectNames.CountryCount), 0f);
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

        /// <summary>Высота подвеса магнитофона в покое — у самого потолка (зал 2.7 м).</summary>
        public Vector3 LieDetectorRestHeight
        {
            get { return new Vector3(0f, 3.5f, 0f); }
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

            /// <summary>
            /// Окрашенный пластик/резина: почти чёрная ниша двери, жгуты кабелей и
            /// трубки телефонов. Цвет несёт материал, альбедо у карболита серое.
            /// </summary>
            public static Material Plastic(Color color)
            {
                string key = "console_plastic_" + ColorUtility.ToHtmlStringRGB(color);
                return Get(key, delegate
                {
                    return RetroPbrTextureGenerator.GetBakelite(color)
                        .CreateMaterial("Mat_World_Plastic_" + key, RetroBlendMode.Opaque);
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
                    return RetroPbrTextureGenerator.GetDeskDocumentPaper()
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
