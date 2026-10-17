// World Crisis | Слой: Core | Ответственный: OpenCode
// Круглый зал саммита (Локация №2): шестиметровый стол с гидравлической опорой и колодцем
// чёрной воды, подвесной неоновый шестигранный куб часов и набор предметов на столе.

using UnityEngine;

namespace WorldCrisis
{
    public static partial class BunkerMeshFactory
    {
        private static readonly Color TableMetalColor = new Color(0.3f, 0.32f, 0.35f);
        private static readonly Color WaterGlassColor = new Color(0.75f, 0.85f, 0.92f);
        private static readonly Color ClockRedColor = new Color(1f, 0.09f, 0.27f);

        /// <summary>
        /// Массивный круглый стол саммита: столешница с булевым вырезанным колодцем,
        /// чёрная вода в углублении, гидравлическая опора с гофрой и шесть бронзовых
        /// голографических архипелагов Archipelago_&lt;CountryId&gt; со светящимися маркерами городов.
        /// </summary>
        public static GameObject CreateSummitRoundTable(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();
            Material iron = Mats.CastIron();
            Material water = Mats.BlackWater();
            Material bronze = Mats.Bronze();

            const float tableRadius = 3f;
            const float topThickness = 0.16f;
            const float wellRadius = 1f;
            const float wellDepth = 0.4f;
            const float tableHeight = 0.78f;

            // Столешница с колодцем: профиль тела вращения сразу задаёт кромку, стенку и дно углубления.
            Mesh top = MeshPrimitives.TableWithWell(tableRadius, 0.09f, wellRadius, wellDepth, topThickness, 32);

            MeshBuilder body = new MeshBuilder();
            body.Add(top, MeshBuilder.Trs(new Vector3(0f, tableHeight + topThickness * 0.5f, 0f),
                Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            // Обод стола и заклёпки по краю.
            body.Add(MeshPrimitives.Torus(tableRadius - 0.015f, 0.045f, 32, 8),
                MeshBuilder.Trs(new Vector3(0f, tableHeight, 0f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);

            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f * Mathf.Deg2Rad;
                Vector3 point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (tableRadius - 0.06f);
                body.Add(MeshPrimitives.RivetDome(0.014f, 0.01f),
                    MeshBuilder.Trs(new Vector3(0f, tableHeight + topThickness * 0.5f, 0f) + point, Vector3.zero,
                        Vector3.one), steel, MeshUvMode.BoxProject);
            }

            // Гидравлическая опора: башмак, колонна, гофра-гармошка, верхний конус.
            body.Add(MeshPrimitives.Prism(0.62f, 0.62f, 0.09f, 20, 0.02f, true),
                MeshBuilder.Trs(new Vector3(0f, 0.045f, 0f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.Prism(0.2f, 0.2f, 0.36f, 16, 0.02f, true),
                MeshBuilder.Trs(new Vector3(0f, 0.27f, 0f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            for (int i = 0; i < 6; i++)
            {
                float y = 0.45f + i * 0.05f;
                float radius = i % 2 == 0 ? 0.27f : 0.23f;
                body.Add(MeshPrimitives.Torus(radius, 0.035f, 16, 6),
                    MeshBuilder.Trs(new Vector3(0f, y, 0f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);
            }

            body.Add(MeshPrimitives.Prism(0.3f, 0.16f, 0.18f, 16, 0.02f, true),
                MeshBuilder.Trs(new Vector3(0f, tableHeight - 0.09f, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Чёрная вода в колодце — на уровне ниже столешницы.
            body.Add(MeshPrimitives.Prism(wellRadius - 0.02f, wellRadius - 0.02f, 0.02f, 24, 0f, true),
                MeshBuilder.Trs(new Vector3(0f, tableHeight - wellDepth * 0.5f, 0f), Vector3.zero, Vector3.one), water,
                MeshUvMode.BoxProject);

            GameObject root = Seal(GeneratedObjectNames.SummitRoundTable, parent, position, body);

            // Шесть голографических архипелагов по кругу от колодца.
            for (int i = 0; i < GameDefinitions.AllCountryIds.Length; i++)
            {
                CountryId id = GameDefinitions.AllCountryIds[i];
                float angle = i * Mathf.PI * 2f / GameDefinitions.AllCountryIds.Length + Mathf.PI * 0.5f;
                Vector3 islandPosition = new Vector3(Mathf.Cos(angle), tableHeight + 0.09f, Mathf.Sin(angle))
                                         * (wellRadius + 0.22f);

                BuildArchipelago(root.transform, id, islandPosition, -angle * Mathf.Rad2Deg, bronze, steel);
            }

            return root;
        }

        /// <summary>Бронзовый голографический архипелаг державы с маркерами её городов.</summary>
        private static void BuildArchipelago(Transform parent, CountryId id, Vector3 position, float yaw,
            Material bronze, Material steel)
        {
            CountryDefinition definition = GameDefinitions.GetConfig().GetOrCreateCountry(id);
            Material markerMaterial = Mats.Neon(definition != null ? definition.AccentColor : Color.white);

            DetRandom random = new DetRandom(0xA9 * ((int)id + 3) + 11);
            MeshBuilder island = new MeshBuilder();

            // Неровный контур острова.
            const int outlineCount = 12;
            Vector2[] outline = new Vector2[outlineCount];
            float islandRadius = random.Range(0.17f, 0.21f);

            for (int i = 0; i < outlineCount; i++)
            {
                float angle = Mathf.PI * 2f * i / outlineCount;
                float radius = islandRadius * random.Range(0.7f, 1.25f);
                outline[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.8f);
            }

            island.Add(MeshPrimitives.ExtrudePolygon(outline, 0.035f, 4f),
                MeshBuilder.Trs(new Vector3(0f, 0.02f, 0f), new Vector3(-90f, 0f, 0f), Vector3.one), bronze,
                MeshUvMode.BoxProject);

            // Холмы-конусы острова.
            for (int i = 0; i < 3; i++)
            {
                float h = random.Range(0.03f, 0.08f);
                island.Add(MeshPrimitives.Prism(random.Range(0.02f, 0.05f), 0f, h, 7, 0f, false),
                    MeshBuilder.Trs(new Vector3(random.Range(-0.1f, 0.1f), 0.04f + h * 0.5f, random.Range(-0.08f, 0.08f)),
                        Vector3.zero, Vector3.one), bronze, MeshUvMode.BoxProject);
            }

            GameObject root = Seal(GeneratedObjectNames.Archipelago(id), parent, position, island);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Маркеры городов: вертикальные штыри со светящейся головкой.
            for (int c = 0; c < EconomyConstants.StartingCitiesPerCountry; c++)
            {
                float angle = Mathf.PI * 2f * c / EconomyConstants.StartingCitiesPerCountry + random.Range(0f, 0.7f);
                float radius = islandRadius * random.Range(0.35f, 0.9f);

                MeshBuilder marker = new MeshBuilder();
                marker.Add(MeshPrimitives.Prism(0.006f, 0.006f, 0.07f, 6, 0f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.035f, 0f), Vector3.zero, Vector3.one), steel,
                    MeshUvMode.BoxProject);
                marker.Add(MeshPrimitives.Sphere(0.018f, 8, 6),
                    MeshBuilder.Trs(new Vector3(0f, 0.085f, 0f), Vector3.zero, Vector3.one), markerMaterial,
                    MeshUvMode.BoxProject);

                Seal(GeneratedObjectNames.CityMarkerPrefix + c, root.transform,
                    new Vector3(Mathf.Cos(angle) * radius, 0.03f, Mathf.Sin(angle) * radius), marker);
            }
        }

        /// <summary>
        /// Подвесной под потолком неоновый шестигранный куб с крупными красными
        /// 7-сегментными цифрами экологии (100% .. 0%). Сам купол — пересечение двух шестигранных призм.
        /// </summary>
        public static GameObject CreateDoomsdayHexClock(Transform parent, Vector3 position)
        {
            Material frame = Mats.CastIron();
            Material digits = Mats.Segment(ClockRedColor);
            Material neon = Mats.Neon(ClockRedColor);

            const float size = 1.25f;

            // Шестигранный блок-«куб» часов: тяжёлый гранёный ящик с фаской.
            Mesh cube = MeshPrimitives.HexBlock(new Vector3(size, size, size), 0.08f);

            MeshBuilder body = new MeshBuilder();
            body.Add(cube, MeshBuilder.Trs(Vector3.zero, new Vector3(0f, 30f, 0f), Vector3.one), frame,
                MeshUvMode.BoxProject);

            // Неоновые трубки вдоль шести рёбер куба.
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad + 30f * Mathf.Deg2Rad;
                Vector3 edge = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * size * 0.5f;
                body.Add(MeshPrimitives.Prism(0.018f, 0.018f, size * 0.98f, 6, 0f, false),
                    MeshBuilder.Trs(edge, new Vector3(90f, 0f, i * 60f), Vector3.one), neon, MeshUvMode.BoxProject);
            }

            // Подвесные тросы к потолку.
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                Vector3 anchor = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * size * 0.36f;
                body.Add(MeshPrimitives.Prism(0.012f, 0.012f, 1.4f, 6, 0f, false),
                    MeshBuilder.Trs(anchor + new Vector3(0f, 0.7f, 0f), Vector3.zero, Vector3.one), frame,
                    MeshUvMode.BoxProject);
            }

            GameObject root = Seal(GeneratedObjectNames.DoomsdayHexClock, parent, position, body);

            // Цифры экологии на лицевой грани: «1 0 0 %», каждая — свой квад атласа.
            MeshBuilder readout = new MeshBuilder();
            float faceOffset = size * 0.5f + 0.01f;

            int[] cells = { 1, 0, 0 };
            for (int i = 0; i < cells.Length; i++)
            {
                readout.Add(AtlasQuad(size * 0.26f, size * 0.34f,
                        RetroPbrTextureGenerator.GetSevenSegmentCell(cells[i])),
                    MeshBuilder.Trs(new Vector3((i - 1f) * size * 0.24f, size * 0.04f, faceOffset), Vector3.zero,
                        Vector3.one), digits, MeshUvMode.Keep);
            }

            readout.Add(AtlasQuad(size * 0.2f, size * 0.28f, RetroPbrTextureGenerator.GetSevenSegmentCell(13)),
                MeshBuilder.Trs(new Vector3(size * 0.42f, size * 0.04f, faceOffset), Vector3.zero, Vector3.one),
                digits, MeshUvMode.Keep);

            // Подсветка лицевой панели.
            readout.Add(MeshPrimitives.BeveledBox(new Vector3(size * 0.92f, size * 0.48f, 0.02f), 0.01f),
                MeshBuilder.Trs(new Vector3(0f, size * 0.04f, faceOffset - 0.012f), Vector3.zero, Vector3.one), frame,
                MeshUvMode.BoxProject);

            Seal("HexClock_Readout", root.transform, Vector3.zero, readout);
            return root;
        }

        /// <summary>
        /// Настольный набор Круглого зала: микрофон на гусиной шее с красным кольцом,
        /// мини-шредер с двумя зубчатыми валами, лист договора, стакан с водой,
        /// папка с бланками пактов и катушечный магнитофон с прожектором фазы «Хроники Лжи».
        /// </summary>
        public static GameObject CreateSummitPersonalSet(Transform parent, Vector3 position, float yaw)
        {
            MeshBuilder table = new MeshBuilder();
            GameObject root = Seal("Summit_Personal_Set", parent, position, table);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Material steel = Mats.ColdSteel();
            Material iron = Mats.CastIron();
            Material paper = Mats.Paper();
            Material glass = Mats.Lens(WaterGlassColor, 0.96f, 0f);
            Material neonRed = Mats.Neon(ClockRedColor);

            BuildGooseneckMic(root.transform, new Vector3(-0.62f, 0f, 0.12f), steel, iron, neonRed);
            BuildDesktopShredder(root.transform, new Vector3(0.66f, 0f, 0.14f), iron, steel);
            BuildTreatySheet(root.transform, new Vector3(0.06f, 0.002f, 0.3f), -12f, paper);
            BuildWaterGlass(root.transform, new Vector3(-0.2f, 0f, 0.34f), glass, steel);
            BuildPactFolder(root.transform, new Vector3(0.46f, 0.002f, -0.16f), 24f, paper, steel);
            BuildLieDetectorRecorder(root.transform, new Vector3(-0.92f, 0f, -0.3f), -24f, iron, steel, neonRed);

            return root;
        }

        /// <summary>Чугунный микрофон на гусиной шее с красным светящимся кольцом.</summary>
        private static void BuildGooseneckMic(Transform parent, Vector3 position, Material steel, Material iron,
            Material neon)
        {
            MeshBuilder baseBuilder = new MeshBuilder();
            baseBuilder.Add(MeshPrimitives.Prism(0.075f, 0.06f, 0.028f, 16, 0.006f, true),
                MeshBuilder.Trs(new Vector3(0f, 0.014f, 0f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);
            baseBuilder.Add(MeshPrimitives.Prism(0.03f, 0.03f, 0.02f, 12, 0.004f, true),
                MeshBuilder.Trs(new Vector3(0f, 0.035f, 0f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            GameObject root = Seal(GeneratedObjectNames.GooseneckMic, parent, position, baseBuilder);

            // Гусиная шея: дуга из трубы по точкам.
            Vector3[] path = new Vector3[10];
            for (int i = 0; i < path.Length; i++)
            {
                float t = i / (float)(path.Length - 1);
                path[i] = new Vector3(
                    0f,
                    0.05f + t * 0.34f - Mathf.Pow(t, 2.4f) * 0.11f,
                    -0.02f + Mathf.Pow(t, 2f) * 0.05f);
            }

            MeshBuilder neck = new MeshBuilder();
            neck.Add(MeshPrimitives.Tube(path, 0.008f, 8), Matrix4x4.identity, steel, MeshUvMode.BoxProject);

            // Капсула микрофона и красное кольцо.
            Vector3 head = path[path.Length - 1] + new Vector3(0f, 0.01f, -0.035f);
            neck.Add(MeshPrimitives.Prism(0.022f, 0.022f, 0.08f, 12, 0.006f, true),
                MeshBuilder.Align(head, new Vector3(0f, 0.35f, -1f).normalized), iron, MeshUvMode.BoxProject);
            neck.Add(MeshPrimitives.Torus(0.024f, 0.006f, 14, 6),
                MeshBuilder.Align(head + new Vector3(0f, 0.012f, -0.03f), new Vector3(0f, 0.35f, -1f).normalized),
                neon, MeshUvMode.BoxProject);

            Seal("Mic_Neck", root.transform, Vector3.zero, neck);
        }

        /// <summary>Настольный мини-шредер с двумя вращающимися зубчатыми валами.</summary>
        private static void BuildDesktopShredder(Transform parent, Vector3 position, Material iron, Material steel)
        {
            // Корпус шредера: рамка вокруг приёмного лотка плюс глухая задняя стенка.
            MeshBuilder body = new MeshBuilder();
            body.Add(MeshPrimitives.Frame(new Vector2(0.3f, 0.2f), 0.06f, 0.28f, 0.016f),
                MeshBuilder.Trs(new Vector3(0f, 0.14f, 0f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.3f, 0.28f, 0.06f), 0.016f),
                MeshBuilder.Trs(new Vector3(0f, 0.14f, -0.07f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Приёмный лоток и бак для бумаг.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.26f, 0.02f, 0.1f), 0.005f),
                MeshBuilder.Trs(new Vector3(0f, 0.02f, 0.16f), new Vector3(-16f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.24f, 0.12f, 0.16f), 0.012f),
                MeshBuilder.Trs(new Vector3(0f, 0.06f, -0.06f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            AddRivetRow(body, new Vector3(-0.12f, 0.05f, 0.101f), new Vector3(0.12f, 0.05f, 0.101f), 5, 0.008f,
                0.005f, steel, Vector3.forward);

            GameObject root = Seal(GeneratedObjectNames.DesktopShredder, parent, position, body);

            // Два зубчатых вала — отдельные узлы, чтобы Core крутил их при шредере.
            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? -0.03f : 0.03f;

                MeshBuilder cutter = new MeshBuilder();
                cutter.Add(MeshPrimitives.Prism(0.022f, 0.022f, 0.24f, 12, 0.004f, false),
                    MeshBuilder.Trs(Vector3.zero, new Vector3(0f, 0f, 90f), Vector3.one), steel, MeshUvMode.BoxProject);

                for (int t = 0; t < 9; t++)
                {
                    float angle = t * 40f;
                    Vector3 offset = Quaternion.Euler(0f, 0f, angle) * new Vector3(0.027f, 0f, 0f);
                    cutter.Add(MeshPrimitives.BeveledBox(new Vector3(0.016f, 0.014f, 0.23f), 0.002f),
                        MeshBuilder.Trs(offset, new Vector3(0f, 0f, angle), Vector3.one), steel, MeshUvMode.BoxProject);
                }

                Seal("Cutter_" + (i == 0 ? "L" : "R"), root.transform, new Vector3(0f, 0.19f, z), cutter);
            }
        }

        /// <summary>Лист мирного договора: слегка волнистый прямоугольник с печатью на текстуре.</summary>
        private static void BuildTreatySheet(Transform parent, Vector3 position, float yaw, Material paper)
        {
            const int gridX = 5;
            const int gridY = 7;
            MeshBuilder sheet = new MeshBuilder();

            for (int gy = 0; gy < gridY; gy++)
            {
                for (int gx = 0; gx < gridX; gx++)
                {
                    float u0 = gx / (float)gridX;
                    float u1 = (gx + 1) / (float)gridX;
                    float v0 = gy / (float)gridY;
                    float v1 = (gy + 1) / (float)gridY;

                    // Лёгкая волнистость бумаги — по краям лист загибается.
                    float wave = Mathf.Sin(u0 * 7.1f + v0 * 4.3f) * 0.004f
                                 + Mathf.Sin(v0 * 5.7f - u0 * 2.9f) * 0.003f;

                    Vector3 a = new Vector3((u0 - 0.5f) * 0.29f, wave, (v0 - 0.5f) * 0.4f);
                    Vector3 b = new Vector3((u1 - 0.5f) * 0.29f, wave, (v0 - 0.5f) * 0.4f);
                    Vector3 c = new Vector3((u1 - 0.5f) * 0.29f, wave, (v1 - 0.5f) * 0.4f);
                    Vector3 d = new Vector3((u0 - 0.5f) * 0.29f, wave, (v1 - 0.5f) * 0.4f);

                    sheet.AddQuad(a, b, c, d, Vector3.up, paper, MeshUvMode.BoxProject);
                }
            }

            // Обратная сторона листа.
            for (int gy = 0; gy < gridY; gy++)
            {
                for (int gx = 0; gx < gridX; gx++)
                {
                    float u0 = gx / (float)gridX;
                    float u1 = (gx + 1) / (float)gridX;
                    float v0 = gy / (float)gridY;
                    float v1 = (gy + 1) / (float)gridY;

                    Vector3 a = new Vector3((u0 - 0.5f) * 0.29f, -0.0015f, (v0 - 0.5f) * 0.4f);
                    Vector3 b = new Vector3((u1 - 0.5f) * 0.29f, -0.0015f, (v0 - 0.5f) * 0.4f);
                    Vector3 c = new Vector3((u1 - 0.5f) * 0.29f, -0.0015f, (v1 - 0.5f) * 0.4f);
                    Vector3 d = new Vector3((u0 - 0.5f) * 0.29f, -0.0015f, (v1 - 0.5f) * 0.4f);

                    sheet.AddQuad(d, c, b, a, Vector3.down, paper, MeshUvMode.BoxProject);
                }
            }

            GameObject root = Seal(GeneratedObjectNames.TreatyMesh, parent, position, sheet);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Гранёный стакан с водой.</summary>
        private static void BuildWaterGlass(Transform parent, Vector3 position, Material glass, Material steel)
        {
            Vector2[] profile =
            {
                new Vector2(0.0f, 0f),
                new Vector2(0.034f, 0f),
                new Vector2(0.038f, 0.012f),
                new Vector2(0.042f, 0.11f),
                new Vector2(0.038f, 0.113f),
                new Vector2(0.036f, 0.014f),
                new Vector2(0.0f, 0.012f)
            };

            MeshBuilder builder = new MeshBuilder();
            builder.Add(MeshPrimitives.Lathe(profile, 10, false, 1f), Matrix4x4.identity, glass, MeshUvMode.BoxProject);

            // Вода внутри — до половины.
            builder.Add(MeshPrimitives.Prism(0.035f, 0.039f, 0.06f, 10, 0f, true),
                MeshBuilder.Trs(new Vector3(0f, 0.04f, 0f), Vector3.zero, Vector3.one),
                Mats.Lens(new Color(0.55f, 0.75f, 0.9f, 1f), 0.98f, 0f), MeshUvMode.BoxProject);

            builder.Add(MeshPrimitives.Ring(0.034f, 0.042f, 0.004f, 10),
                MeshBuilder.Trs(new Vector3(0f, 0.112f, 0f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            Seal(GeneratedObjectNames.WaterGlass, parent, position, builder);
        }

        /// <summary>Папка с бланками пактов.</summary>
        private static void BuildPactFolder(Transform parent, Vector3 position, float yaw, Material paper,
            Material steel)
        {
            MeshBuilder folder = new MeshBuilder();

            // Нижняя и верхняя крышки.
            folder.Add(MeshPrimitives.BeveledBox(new Vector3(0.24f, 0.006f, 0.32f), 0.003f),
                MeshBuilder.Trs(new Vector3(0f, 0.004f, 0f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
            folder.Add(MeshPrimitives.BeveledBox(new Vector3(0.24f, 0.006f, 0.32f), 0.003f),
                MeshBuilder.Trs(new Vector3(0f, 0.019f, 0.012f), new Vector3(-6f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Стопка бланков с небольшим смещением.
            DetRandom random = new DetRandom(0xFAC7);
            for (int i = 0; i < 7; i++)
            {
                folder.Add(MeshPrimitives.BeveledBox(new Vector3(0.21f, 0.0015f, 0.29f), 0f),
                    MeshBuilder.Trs(new Vector3(random.Range(-0.006f, 0.006f), 0.008f + i * 0.0018f,
                        random.Range(-0.006f, 0.006f)), Vector3.zero, Vector3.one), paper, MeshUvMode.BoxProject);
            }

            // Кольцевая пружина скрепки.
            folder.Add(MeshPrimitives.Torus(0.012f, 0.0025f, 10, 5),
                MeshBuilder.Trs(new Vector3(0f, 0.012f, -0.14f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);

            GameObject root = Seal(GeneratedObjectNames.PactFolder, parent, position, folder);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Катушечный магнитофон на тросе с красным прожектором фазы «Хроники Лжи».</summary>
        private static void BuildLieDetectorRecorder(Transform parent, Vector3 position, float yaw, Material iron,
            Material steel, Material neon)
        {
            MeshBuilder body = new MeshBuilder();

            // Корпус-чемодан: рамка вокруг окна с лентой плюс глухое дно.
            body.Add(MeshPrimitives.Frame(new Vector2(0.42f, 0.24f), 0.07f, 0.26f, 0.018f),
                MeshBuilder.Trs(new Vector3(0f, 0.13f, 0f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.42f, 0.26f, 0.04f), 0.018f),
                MeshBuilder.Trs(new Vector3(0f, 0.13f, -0.1f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Ручка на дуге.
            body.Add(MeshPrimitives.Arc(0.08f, 180f, 0.008f, 10),
                MeshBuilder.Trs(new Vector3(0f, 0.26f, 0f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Ножки.
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.17f;
                float z = (i < 2 ? -1f : 1f) * 0.09f;
                body.Add(MeshPrimitives.Prism(0.016f, 0.016f, 0.02f, 8, 0.004f, false),
                    MeshBuilder.Trs(new Vector3(x, 0.01f, z), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
            }

            GameObject root = Seal(GeneratedObjectNames.LieDetectorRecorder, parent, position, body);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Две катушки с лентой между ними.
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.1f : 0.1f;

                MeshBuilder reel = new MeshBuilder();
                reel.Add(MeshPrimitives.Prism(0.055f, 0.055f, 0.02f, 14, 0.004f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0f, 0.01f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                    MeshUvMode.BoxProject);
                reel.Add(MeshPrimitives.Prism(0.018f, 0.018f, 0.028f, 10, 0.004f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0f, 0.01f), new Vector3(90f, 0f, 0f), Vector3.one), iron,
                    MeshUvMode.BoxProject);

                for (int s = 0; s < 4; s++)
                {
                    reel.Add(MeshPrimitives.BeveledBox(new Vector3(0.075f, 0.01f, 0.008f), 0.002f),
                        MeshBuilder.Trs(Vector3.zero, new Vector3(90f, s * 45f, 0f), Vector3.one), steel,
                        MeshUvMode.BoxProject);
                }

                Seal("Recorder_Reel_" + (i == 0 ? "L" : "R"), root.transform, new Vector3(x, 0.2f, 0.1f), reel);
            }

            // Лента между катушками.
            MeshBuilder tape = new MeshBuilder();
            Vector3[] tapePath =
            {
                new Vector3(-0.045f, 0.2f, 0.12f),
                new Vector3(-0.02f, 0.195f, 0.12f),
                new Vector3(0.02f, 0.195f, 0.12f),
                new Vector3(0.045f, 0.2f, 0.12f)
            };

            tape.Add(MeshPrimitives.Tube(tapePath, 0.004f, 4), Matrix4x4.identity, iron, MeshUvMode.BoxProject);

            // Красный прожектор на штативе — указатель на лжеца.
            tape.Add(MeshPrimitives.Prism(0.012f, 0.012f, 0.22f, 8, 0.003f, false),
                MeshBuilder.Trs(new Vector3(0.2f, 0.36f, -0.04f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);
            tape.Add(MeshPrimitives.Prism(0.038f, 0.03f, 0.06f, 12, 0.008f, true),
                MeshBuilder.Trs(new Vector3(0.2f, 0.48f, -0.04f), new Vector3(28f, 0f, 0f), Vector3.one), iron,
                MeshUvMode.BoxProject);
            tape.Add(MeshPrimitives.Dome(0.03f, 0.012f, 12, 4, true),
                MeshBuilder.Trs(new Vector3(0.2f, 0.5f, -0.018f), new Vector3(28f, 0f, 0f), Vector3.one), neon,
                MeshUvMode.BoxProject);

            Seal("Recorder_Spotlight", root.transform, Vector3.zero, tape);
        }
    }
}