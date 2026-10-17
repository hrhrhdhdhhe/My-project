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
    /// <summary>
    /// Собирает сцену целиком и хранит узлы, которые нужны PhaseLightingDirector:
    /// точки обзора, рабочие места саммита и подвесной магнитофон.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("World Crisis/Bunker World Builder")]
    public class BunkerWorldBuilder : MonoBehaviour
    {
        #region Планировка (контракт с PhaseLightingDirector)

        /// <summary>Высота столешницы пульта бункера.</summary>
        public const float BunkerConsoleTopY = 0.72f;

        /// <summary>Ширина пульта бункера.</summary>
        public const float BunkerConsoleWidth = 5.4f;

        /// <summary>Глубина пульта бункера.</summary>
        public const float BunkerConsoleDepth = 1.5f;

        /// <summary>Высота потолка личного бункера.</summary>
        public const float BunkerRoomHeight = 4.3f;

        /// <summary>Смещение пульта от начала координат по Z (пульт ближе к игроку).</summary>
        public const float BunkerConsoleCenterZ = 0.9f;

        /// <summary>Глаз игрока в бункере: сидит за пультом, смотрит на приборы и табло.</summary>
        public static readonly Vector3 BunkerEyePosition = new Vector3(0f, 1.62f, 1.98f);

        /// <summary>Точка, на которую смотрит игрок в бункере.</summary>
        public static readonly Vector3 BunkerLookTarget = new Vector3(0f, 1.1f, -0.6f);

        /// <summary>Поле зрения камеры бункера (Art Bible: 65°).</summary>
        public const float BunkerFieldOfView = 65f;

        /// <summary>Мировая координата центра Круглого зала (разнесено с бункером по Z).</summary>
        public static readonly Vector3 SummitCenter = SummitLayout.Center;

        /// <summary>Высота глаза за столом саммита.</summary>
        public const float SummitEyeHeight = 1.34f;

        /// <summary>Поле зрения камеры саммита: зал просторный, но кадр должен остаться кадром.</summary>
        public const float SummitFieldOfView = 58f;

        /// <summary>Радиус бетонной шахты зала.</summary>
        public const float SummitShaftRadius = 11f;

        #endregion

        #region Состояние сборки

        private bool _built;
        private bool _warnedAboutHologramShader;

        private Transform _bunkerRoot;
        private Transform _summitRoot;
        private Transform _lieDetectorTrolley;
        private readonly Transform[] _summitSeats = new Transform[GeneratedObjectNames.CountryCount];
        private Transform _summitPlayerSeat;

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
        /// Подвесной катушечный магнитофон над центром стола. В покое поднят к потолку,
        /// на фазе компромата опускается — двигает его PhaseLightingDirector.
        /// </summary>
        public Transform LieDetectorTrolley
        {
            get { return _lieDetectorTrolley; }
        }

        /// <summary>Рабочее место игрока за столом саммита (задаётся директором по LocalPlayer).</summary>
        public Transform SummitPlayerSeat
        {
            get { return _summitPlayerSeat; }
        }

        /// <summary>Рабочие места саммита по индексу слота. Может содержать null до Build.</summary>
        public IReadOnlyList<Transform> SummitSeats
        {
            get { return _summitSeats; }
        }

        /// <summary>Мировая позиция глаза игрока в выбранном саммит-месте.</summary>
        public Vector3 GetSummitEyePosition(Transform seat)
        {
            if (seat == null)
            {
                return SummitCenter + Vector3.up * SummitEyeHeight;
            }

            Vector3 world = seat.position;
            world.y = SummitCenter.y + SummitEyeHeight;
            return world;
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

            GameObject bunkerNode = MakeNode("Bunker_Zone", transform, Vector3.zero);
            _bunkerRoot = bunkerNode.transform;
            BuildBunkerRoom(_bunkerRoot);
            BuildBunkerConsole(_bunkerRoot);
            PopulateBunkerEquipment(_bunkerRoot, cities);

            GameObject summitNode = MakeNode("Summit_Zone", transform, SummitCenter);
            _summitRoot = BunkerMeshFactory.CreateLocationTwoSummit(summitNode.transform).transform;
            BuildSummitShell(_summitRoot);
            BuildSummitSeats(_summitRoot);
            BuildLieDetector(_summitRoot);

            // Шейдеры голограмм назначаются последними: узлы куполов к этому моменту созданы.
            ApplyHologramShaders();
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

        #region Локация №1 — личный бункер

        private const float BunkerRoomHalfWidth = 3.9f;
        private const float BunkerBackWallZ = -3.7f;
        private const float BunkerFrontWallZ = 2.9f;

        /// <summary>Бетонная оболочка: пол, стены, потолок, пилоны и вентиляционные прорези.</summary>
        private void BuildBunkerRoom(Transform parent)
        {
            Material concrete = Mat.Concrete();
            Material iron = Mat.CastIron();
            Material steel = Mat.ColdSteel();

            MeshBuilder shell = new MeshBuilder();

            float roomWidth = BunkerRoomHalfWidth * 2f;
            float roomDepth = BunkerFrontWallZ - BunkerBackWallZ;
            float centerZ = (BunkerFrontWallZ + BunkerBackWallZ) * 0.5f;

            // Пол и потолок — плиты с фаской, пол утоплен в грунт.
            shell.Add(MeshPrimitives.BeveledBox(new Vector3(roomWidth + 0.6f, 0.3f, roomDepth + 0.6f), 0.04f),
                MeshBuilder.Trs(new Vector3(0f, -0.15f, centerZ), Vector3.zero, Vector3.one), concrete,
                MeshUvMode.BoxProject);
            shell.Add(MeshPrimitives.BeveledBox(new Vector3(roomWidth + 0.6f, 0.36f, roomDepth + 0.6f), 0.05f),
                MeshBuilder.Trs(new Vector3(0f, BunkerRoomHeight + 0.18f, centerZ), Vector3.zero, Vector3.one),
                concrete, MeshUvMode.BoxProject);

            // Четыре стены. Собираются наружу от кабины, чтобы игрок видел бетон изнутри.
            AddWall(shell, concrete, new Vector3(0f, BunkerRoomHeight * 0.5f, BunkerBackWallZ),
                new Vector3(roomWidth + 0.6f, BunkerRoomHeight, 0.3f));
            AddWall(shell, concrete, new Vector3(0f, BunkerRoomHeight * 0.5f, BunkerFrontWallZ),
                new Vector3(roomWidth + 0.6f, BunkerRoomHeight, 0.3f));
            AddWall(shell, concrete, new Vector3(-BunkerRoomHalfWidth, BunkerRoomHeight * 0.5f, centerZ),
                new Vector3(0.3f, BunkerRoomHeight, roomDepth + 0.6f));
            AddWall(shell, concrete, new Vector3(BunkerRoomHalfWidth, BunkerRoomHeight * 0.5f, centerZ),
                new Vector3(0.3f, BunkerRoomHeight, roomDepth + 0.6f));

            // Пилоны брутализма: вертикальные рёбра через каждые 1.3 м на боковых стенах.
            int ribCount = 6;
            for (int i = 0; i < ribCount; i++)
            {
                float z = BunkerBackWallZ + 0.4f + i * (roomDepth - 0.8f) / (ribCount - 1);

                shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.26f, BunkerRoomHeight, 0.22f), 0.03f),
                    MeshBuilder.Trs(new Vector3(-BunkerRoomHalfWidth + 0.13f, BunkerRoomHeight * 0.5f, z),
                        Vector3.zero, Vector3.one), concrete, MeshUvMode.BoxProject);
                shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.26f, BunkerRoomHeight, 0.22f), 0.03f),
                    MeshBuilder.Trs(new Vector3(BunkerRoomHalfWidth - 0.13f, BunkerRoomHeight * 0.5f, z),
                        Vector3.zero, Vector3.one), concrete, MeshUvMode.BoxProject);
            }

            // Заклёпки вдоль стыка пола и стен — фирменный приём, повторяет стиль фабрики.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (BunkerRoomHalfWidth - 0.05f);
                for (int i = 0; i < 9; i++)
                {
                    float z = BunkerBackWallZ + 0.3f + i * (roomDepth - 0.6f) / 8f;
                    shell.Add(MeshPrimitives.RivetDome(0.016f, 0.012f),
                        MeshBuilder.Align(new Vector3(x, 0.22f, z), new Vector3(-side, 0f, 0f)), steel,
                        MeshUvMode.BoxProject);
                }
            }

            Seal("Bunker_Shell", parent, shell);

            BuildBunkerPipes(parent, iron, steel);
        }

        private static void AddWall(MeshBuilder builder, Material material, Vector3 center, Vector3 size)
        {
            builder.Add(MeshPrimitives.BeveledBox(size, 0.04f),
                MeshBuilder.Trs(center, Vector3.zero, Vector3.one), material, MeshUvMode.BoxProject);
        }

        /// <summary>Трубы и провисающие кабели вдоль стен — «жилой» вид кабины.</summary>
        private void BuildBunkerPipes(Transform parent, Material iron, Material steel)
        {
            MeshBuilder pipes = new MeshBuilder();
            BunkerMeshFactory.DetRandom random = new BunkerMeshFactory.DetRandom(0x9C1);

            // Две горизонтальные трубы под потолком вдоль правой стены.
            for (int line = 0; line < 2; line++)
            {
                float y = BunkerRoomHeight - 0.45f - line * 0.28f;
                float radius = line == 0 ? 0.075f : 0.05f;

                Vector3[] path = new Vector3[6];
                for (int i = 0; i < path.Length; i++)
                {
                    float t = i / (float)(path.Length - 1);
                    path[i] = new Vector3(
                        BunkerRoomHalfWidth - 0.18f,
                        y + Mathf.Sin(t * Mathf.PI) * 0.03f,
                        Mathf.Lerp(BunkerBackWallZ + 0.3f, BunkerFrontWallZ - 0.3f, t));
                }

                pipes.Add(MeshPrimitives.Tube(path, radius, 8), Matrix4x4.identity,
                    line == 0 ? iron : steel, MeshUvMode.BoxProject);

                // Хомуты трубы.
                for (int i = 1; i < path.Length - 1; i++)
                {
                    pipes.Add(MeshPrimitives.Torus(radius + 0.012f, 0.016f, 10, 5),
                        MeshBuilder.Align(path[i], Vector3.forward), steel, MeshUvMode.BoxProject);
                }
            }

            // Вертикальный стояк у задней стены.
            pipes.Add(MeshPrimitives.Prism(0.085f, 0.085f, BunkerRoomHeight - 0.2f, 10, 0.012f, false),
                MeshBuilder.Trs(new Vector3(-BunkerRoomHalfWidth + 0.34f, (BunkerRoomHeight - 0.2f) * 0.5f,
                    BunkerBackWallZ + 0.3f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);

            // Кабели: провисающие нитки между потолочными кронштейнами.
            const int cableCount = 5;
            for (int i = 0; i < cableCount; i++)
            {
                Vector3 from = new Vector3(
                    random.Range(-BunkerRoomHalfWidth + 0.6f, BunkerRoomHalfWidth - 0.6f),
                    BunkerRoomHeight - 0.22f,
                    BunkerBackWallZ + random.Range(0.6f, 1.4f));
                Vector3 to = new Vector3(
                    random.Range(-BunkerRoomHalfWidth + 0.6f, BunkerRoomHalfWidth - 0.6f),
                    BunkerRoomHeight - 0.22f,
                    BunkerBackWallZ + random.Range(2.2f, 3.2f));

                Vector3[] path = new Vector3[7];
                for (int k = 0; k < path.Length; k++)
                {
                    float t = k / (float)(path.Length - 1);
                    path[k] = Vector3.Lerp(from, to, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.32f;
                }

                pipes.Add(MeshPrimitives.Tube(path, 0.022f, 6), Matrix4x4.identity,
                    random.Next01() > 0.5f ? steel : iron, MeshUvMode.BoxProject);
            }

            Seal("Bunker_Pipes", parent, pipes);
        }

        /// <summary>Тяжёлый стальной пульт-стол перед креслом игрока.</summary>
        private void BuildBunkerConsole(Transform parent)
        {
            Material steel = Mat.ColdSteel();
            Material iron = Mat.CastIron();
            Material concrete = Mat.Concrete();

            MeshBuilder console = new MeshBuilder();

            Vector3 center = new Vector3(-0.3f, 0f, BunkerConsoleCenterZ);
            float halfWidth = BunkerConsoleWidth * 0.5f;
            float halfDepth = BunkerConsoleDepth * 0.5f;
            float topY = BunkerConsoleTopY;

            // Столешница с толстым передним кантом.
            console.Add(MeshPrimitives.BeveledBox(new Vector3(BunkerConsoleWidth, 0.09f, BunkerConsoleDepth), 0.02f),
                MeshBuilder.Trs(center + new Vector3(0f, topY, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);
            console.Add(MeshPrimitives.BeveledBox(new Vector3(BunkerConsoleWidth, 0.13f, 0.12f), 0.02f),
                MeshBuilder.Trs(center + new Vector3(0f, topY - 0.09f, halfDepth), Vector3.zero, Vector3.one),
                iron, MeshUvMode.BoxProject);

            // Фартук и две тумбы: под пультом остаётся ниша для ног игрока по центру.
            console.Add(MeshPrimitives.BeveledBox(new Vector3(BunkerConsoleWidth, 0.34f, 0.09f), 0.015f),
                MeshBuilder.Trs(center + new Vector3(0f, topY - 0.24f, -halfDepth + 0.05f), Vector3.zero,
                    Vector3.one), iron, MeshUvMode.BoxProject);

            AddPedestal(console, concrete, iron, center + new Vector3(-halfWidth + 0.42f, 0f, 0f));
            AddPedestal(console, concrete, iron, center + new Vector3(halfWidth - 0.42f, 0f, 0f));

            // Заклёпки по переднему канту — пульт выглядит клёпаным, а не литым.
            for (int i = 0; i < 17; i++)
            {
                float x = -halfWidth + 0.2f + i * (BunkerConsoleWidth - 0.4f) / 16f;
                console.Add(MeshPrimitives.RivetDome(0.014f, 0.01f),
                    MeshBuilder.Align(center + new Vector3(x, topY - 0.09f, halfDepth + 0.055f), Vector3.forward),
                    steel, MeshUvMode.BoxProject);
            }

            Seal("Bunker_Console", parent, console);
        }

        private static void AddPedestal(MeshBuilder builder, Material body, Material trim, Vector3 center)
        {
            builder.Add(MeshPrimitives.BeveledBox(new Vector3(0.8f, BunkerConsoleTopY - 0.14f, 1.36f), 0.025f),
                MeshBuilder.Trs(center + new Vector3(0f, (BunkerConsoleTopY - 0.14f) * 0.5f, 0f), Vector3.zero,
                    Vector3.one), body, MeshUvMode.BoxProject);

            // Цоколь и тяжёлая обвязка основания.
            builder.Add(MeshPrimitives.BeveledBox(new Vector3(0.9f, 0.08f, 1.46f), 0.015f),
                MeshBuilder.Trs(center + new Vector3(0f, 0.04f, 0f), Vector3.zero, Vector3.one), trim,
                MeshUvMode.BoxProject);
        }

        /// <summary>
        /// Расстановка приборов по зонам пульта: слева узел связи, в центре терминал с
        /// принтером, справа ядерный пульт. Настенное табло — на дальней стене.
        /// </summary>
        private void PopulateBunkerEquipment(Transform parent, IReadOnlyList<CityState> cities)
        {
            float topY = BunkerConsoleTopY;
            Vector3 center = new Vector3(-0.3f, 0f, BunkerConsoleCenterZ);

            // Левая зона — узел связи с тремя дисковыми телефонами. Поворот 0, а не 180:
            // лицевая сторона пульта (диски, трубки, табличка) смотрит в +Z, то есть
            // к игроку, который сидит в +Z и смотрит в -Z.
            GameObject switchboard = BunkerMeshFactory.CreateSwitchboardUnit(parent);
            switchboard.transform.localPosition = new Vector3(center.x - 1.32f, topY, center.z - 0.18f);
            switchboard.transform.localRotation = Quaternion.identity;

            // Центральная зона — терминал ввода приказов и принтер справа от него.
            GameObject terminal = BunkerMeshFactory.CreateOrdersCrtTerminal(parent,
                new Vector3(center.x + 0.25f, topY, center.z - 0.06f));
            terminal.transform.localRotation = Quaternion.Euler(0f, -14f, 0f);

            GameObject printer = BunkerMeshFactory.CreateDotMatrixPrinter(parent,
                new Vector3(center.x + 0.95f, topY, center.z - 0.02f));
            printer.transform.localRotation = Quaternion.Euler(0f, -14f, 0f);

            // Правая зона — ядерный пульт на собственной тумбе.
            MeshBuilder pedestal = new MeshBuilder();
            pedestal.Add(MeshPrimitives.BeveledBox(new Vector3(1.0f, 0.62f, 0.8f), 0.025f),
                MeshBuilder.Trs(new Vector3(0f, 0.31f, 0f), Vector3.zero, Vector3.one), Mat.Concrete(),
                MeshUvMode.BoxProject);

            GameObject pedestalNode = Seal("Launch_Pedestal", parent, pedestal);
            pedestalNode.transform.localPosition = new Vector3(center.x + 1.85f, topY, center.z - 0.02f);
            pedestalNode.transform.localRotation = Quaternion.Euler(0f, -22f, 0f);

            GameObject launchBox = BunkerMeshFactory.CreateNuclearLaunchBox(parent,
                pedestalNode.transform.position + new Vector3(0f, 0.62f, 0f));
            launchBox.transform.localRotation = Quaternion.Euler(0f, -22f, 0f);

            // Настенное табло «СТАТУС ГОСУДАРСТВА» над пультом. Диорамы и табличка лежат
            // на +Z-стороне узла, поэтому разворот не нужен.
            GameObject board = BunkerMeshFactory.CreateStateStatusBoard(parent,
                new Vector3(0f, 1.25f, BunkerBackWallZ + 0.17f), cities);
            board.transform.localRotation = Quaternion.identity;
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

        /// <summary>Шесть грузовых платформ-лифтов по кругу стола.</summary>
        private void BuildCargoPlatforms(Transform parent)
        {
            Material steel = Mat.ColdSteel();
            Material iron = Mat.CastIron();
            Material hazard = Mat.Hazard();

            for (int i = 0; i < GeneratedObjectNames.CountryCount; i++)
            {
                Vector3 position = BunkerMeshFactory.GetSummitSeatPosition(i);
                float angle = BunkerMeshFactory.GetSummitSeatAngle(i);

                MeshBuilder platform = new MeshBuilder();

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

                GameObject node = Seal("Cargo_Platform_" + i, parent, platform);
                node.transform.localPosition = position;
                node.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 180f, 0f);
            }
        }

        /// <summary>Пустые узлы рабочих мест: на них директор ставит камеру игрока.</summary>
        private void BuildSummitSeats(Transform parent)
        {
            for (int i = 0; i < GeneratedObjectNames.CountryCount; i++)
            {
                GameObject node = MakeNode("Summit_Seat_" + i, parent, BunkerMeshFactory.GetSummitSeatPosition(i));
                float angle = BunkerMeshFactory.GetSummitSeatAngle(i);
                node.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 180f, 0f);
                _summitSeats[i] = node.transform;
            }

            _summitPlayerSeat = _summitSeats[0];
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
            Transform node = FindDeep(root, nodeName);

            if (node == null)
            {
                return;
            }

            MeshRenderer renderer = node.GetComponent<MeshRenderer>();

            if (renderer == null)
            {
                return;
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
        /// Ищет узел по имени вглубь иерархии. Обход в ширину, а не поиск только прямых
        /// детей: купол лежит внутри секции-диорамы, архипелаг — внутри стола.
        /// </summary>
        private static Transform FindDeep(Transform root, string nodeName)
        {
            if (root == null || string.IsNullOrEmpty(nodeName))
            {
                return null;
            }

            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();

                if (current.name == nodeName)
                {
                    return current;
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    queue.Enqueue(current.GetChild(i));
                }
            }

            return null;
        }

        #endregion
    }
}