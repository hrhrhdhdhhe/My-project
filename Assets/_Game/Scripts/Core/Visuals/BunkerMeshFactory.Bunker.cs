// World Crisis | Слой: Core | Ответственный: OpenCode
// Приборы личного бункера (Локация №1): пульт с тремя дисковыми телефонами, советский ЭЛТ-терминал,
// матричный принтер, ядерный пульт с красной кнопкой «ПУСК» и настенное табло статуса государств.
// Имена узлов — контракт из GeneratedObjectNames, наружу отдаются только Transform-ссылки.

using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    public static partial class BunkerMeshFactory
    {
        // Палитра Art Bible для приборов.
        // Насыщенный карболит: телефоны должны читаться в тёплом свете лампы, а не
        // тонуть в темноте кабины вместе с чугуном. Цвет несёт материал (_BaseColor),
        // альбедо-текстура карболита серая — см. RetroPbrTextureGenerator.BuildBakelite.
        // Концепт-арт 1-в-1: красный #9E241E, жёлтый #C49222, чёрный #1C1E22.
        private static readonly Color PhoneRedColor = new Color(0.62f, 0.14f, 0.12f);
        private static readonly Color PhoneYellowColor = new Color(0.77f, 0.57f, 0.13f);
        private static readonly Color PhoneBlackColor = new Color(0.11f, 0.12f, 0.13f);

        /// <summary>Кремово-бежевый диск набора #C8C0AD — как на телефонах 70-х.</summary>
        private static readonly Color PhoneDialWhite = new Color(0.784f, 0.753f, 0.678f);

        /// <summary>Толстый чёрный жгут трубки и кабелей по столу.</summary>
        private static readonly Color PhoneCordColor = new Color(0.05f, 0.05f, 0.06f);

        private static readonly Color BodyGreyColor = new Color(0.24f, 0.25f, 0.27f);
        private static readonly Color BeigeColor = new Color(0.62f, 0.58f, 0.48f);
        private static readonly Color AlertRedColor = new Color(1f, 0.09f, 0.27f);
        private static readonly Color LampAmberColor = new Color(1f, 0.69f, 0f);
        private static readonly Color LampGreenColor = new Color(0f, 0.9f, 0.46f);
        private static readonly Color CrtGreenColor = new Color(0f, 0.902f, 0.463f);
        private static readonly Color ShieldDomeColor = new Color(0f, 0.902f, 0.463f);

/// <summary>
        /// Пульт связи бункера: БЕЗ плиты-подставки. Три карболитовых телефона стоят
        /// прямо на ржавой столешнице слева направо (красный, жёлтый, чёрный), а позади
        /// них — низкая наклонная панель с шестью лампами LineLamp_0..5.
        ///
        /// Узел Switchboard_Unit — чистый пивот: он держит три телефона вместе и даёт
        /// биндеру единую точку входа, но собственной геометрии не имеет. Раньше здесь
        /// была белая плита-подставка с ножками — на концепт-арте телефоны стоят прямо
        /// на столе, и подставка только отделяла их от ржавчины.
        /// </summary>
        public static GameObject CreateSwitchboardUnit(Transform parent)
        {
            GameObject root = MakeNode(GeneratedObjectNames.SwitchboardUnit, parent, Vector3.zero);

            // Локальные базы телефонов. Мировые точки (-0.66, 0.52), (-0.37, 0.55) и
            // (-0.08, 0.56) из концепт-арта пересчитаны в локальные координаты пивота
            // ПОСЛЕ разворота пульта на 166°, поэтому X отрицательный: разворот
            // зеркалит ряд телефонов вдоль оси стола.
            Vector3[] phoneBases =
            {
                Vector3.zero,
                new Vector3(-0.289f, 0f, 0.041f),
                new Vector3(-0.573f, 0f, 0.102f)
            };

            // Жёлтый и чёрный довёрнуты относительно красного: после разворота пульта
            // на 166° в мире они смотрят на игрока под 174° и 180° — как на арте,
            // где телефоны стоят веером.
            BuildBakelitePhone(root.transform, GeneratedObjectNames.PhoneRed, PhoneRedColor, phoneBases[0],
                false);
            BuildBakelitePhone(root.transform, GeneratedObjectNames.PhoneYellow, PhoneYellowColor, phoneBases[1],
                false).transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
            BuildBakelitePhone(root.transform, GeneratedObjectNames.PhoneBlack, PhoneBlackColor, phoneBases[2],
                true).transform.localRotation = Quaternion.Euler(0f, 14f, 0f);

            BuildLineLampPanel(root.transform, new Vector3(-0.29f, 0f, -0.14f));
            BuildPhoneCords(root.transform, phoneBases);

            return root;
        }

        /// <summary>
        /// Карболитовый дисковый телефон 70-х: корпус своего цвета, кремово-бежевый диск
        /// набора Dial_Wheel на наклонной лицевой грани и — у красного и жёлтого —
        /// трубка Handset_Pivot сверху. У чёрного вместо трубки окно с двумя
        /// магнитофонными катушками Reel_L / Reel_R.
        ///
        /// Диск вынесен на 8 мм от наклонной грани: когда он стоял вплотную, плоскости
        /// совпадали и диск мерцал Z-fighting'ом.
        /// </summary>
        public static GameObject BuildBakelitePhone(Transform parent, string nodeName, Color bodyColor,
            Vector3 position, bool withReels)
        {
            Material body = Mats.Bakelite(bodyColor);
            Material dial = Mats.Bakelite(PhoneDialWhite);
            Material steel = Mats.ColdSteel();
            MeshBuilder shell = new MeshBuilder();

            const float bodyWidth = 0.23f;
            const float bodyHeight = 0.105f;
            const float bodyDepth = 0.19f;

            // Корпус: тяжёлая трапеция 70-х, задняя часть ниже передней.
            shell.Add(MeshPrimitives.BeveledBox(new Vector3(bodyWidth, bodyHeight, bodyDepth), 0.012f),
                MeshBuilder.Trs(new Vector3(0f, bodyHeight * 0.5f, 0f), Vector3.zero, Vector3.one), body,
                MeshUvMode.BoxProject);

            // Наклонная лицевая грань — на ней стоит диск набора.
            const float slopeDegrees = -24f;
            Vector3 slopeCenter = new Vector3(0f, bodyHeight + 0.022f, 0.022f);
            Vector3 slopeNormal = Quaternion.Euler(slopeDegrees, 0f, 0f) * Vector3.up;

            shell.Add(MeshPrimitives.BeveledBox(new Vector3(bodyWidth, 0.05f, 0.13f), 0.01f),
                MeshBuilder.Trs(slopeCenter, new Vector3(slopeDegrees, 0f, 0f), Vector3.one), body,
                MeshUvMode.BoxProject);

            // Ножки-опоры: телефон чуть приподнят над столешницей.
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.085f;
                float z = (i < 2 ? -1f : 1f) * 0.065f;
                shell.Add(MeshPrimitives.Prism(0.012f, 0.012f, 0.012f, 8, 0.003f, false),
                    MeshBuilder.Trs(new Vector3(x, 0.006f, z), Vector3.zero, Vector3.one), steel,
                    MeshUvMode.BoxProject);
            }

            if (withReels)
            {
                // Окно катушек собрано рамкой — ролики видно сквозь проём.
                shell.Add(MeshPrimitives.Frame(new Vector2(0.2f, 0.12f), 0.025f, 0.03f, 0.005f),
                    MeshBuilder.Trs(new Vector3(0f, bodyHeight + 0.055f, -0.02f), Vector3.zero, Vector3.one), body,
                    MeshUvMode.BoxProject);
            }

            GameObject root = Seal(nodeName, parent, position, shell);

            if (withReels)
            {
                // Катушки магнитофона Reel_L / Reel_R — вращаемые узлы.
                for (int i = 0; i < 2; i++)
                {
                    MeshBuilder reel = new MeshBuilder();
                    reel.Add(MeshPrimitives.Prism(0.032f, 0.032f, 0.016f, 14, 0.004f, false),
                        MeshBuilder.Trs(Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one), steel,
                        MeshUvMode.BoxProject);
                    reel.Add(MeshPrimitives.Prism(0.01f, 0.01f, 0.022f, 10, 0.003f, false),
                        MeshBuilder.Trs(Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one), dial,
                        MeshUvMode.BoxProject);

                    for (int s = 0; s < 3; s++)
                    {
                        reel.Add(MeshPrimitives.BeveledBox(new Vector3(0.045f, 0.009f, 0.006f), 0.002f),
                            MeshBuilder.Trs(Vector3.zero, new Vector3(90f, s * 120f, 0f), Vector3.one), steel,
                            MeshUvMode.BoxProject);
                    }

                    Seal(i == 0 ? GeneratedObjectNames.ReelLeft : GeneratedObjectNames.ReelRight, root.transform,
                        new Vector3(i == 0 ? -0.04f : 0.04f, bodyHeight + 0.055f, -0.02f), reel);
                }
            }

            // --- Диск набора: плоский цилиндр на наклонной грани ---
            const float dialRadius = 0.042f;
            const float dialThickness = 0.006f;
            Vector3 dialCenter = slopeCenter + slopeNormal * (0.13f * 0.5f + 0.008f);

            MeshBuilder dialBuilder = new MeshBuilder();
            dialBuilder.Add(MeshPrimitives.Prism(dialRadius, dialRadius, dialThickness, 20, 0.001f, false),
                MeshBuilder.Trs(Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one), dial, MeshUvMode.BoxProject);

            // Тёмный кружок в центре и десять отверстий по кругу — как на настоящем диске.
            dialBuilder.Add(MeshPrimitives.Prism(0.011f, 0.011f, dialThickness + 0.001f, 12, 0f, false),
                MeshBuilder.Trs(Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one), steel, MeshUvMode.BoxProject);

            for (int h = 0; h < 10; h++)
            {
                float angle = h * 36f;
                Vector3 hole = Quaternion.Euler(0f, 0f, angle) * new Vector3(0.031f, 0f, 0f);
                dialBuilder.Add(MeshPrimitives.Prism(0.0055f, 0.0055f, dialThickness + 0.001f, 8, 0f, false),
                    MeshBuilder.Trs(hole, new Vector3(90f, angle, 0f), Vector3.one), steel, MeshUvMode.BoxProject);
            }

            GameObject dialNode = Seal(GeneratedObjectNames.DialWheel, root.transform, dialCenter, dialBuilder);
            dialNode.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, slopeNormal);

            if (withReels)
            {
                return root;
            }

            // Трубка на шарнире Handset_Pivot.
            MeshBuilder handset = new MeshBuilder();
            handset.Add(MeshPrimitives.BeveledBox(new Vector3(0.19f, 0.032f, 0.044f), 0.009f),
                MeshBuilder.Trs(new Vector3(0f, 0.016f, 0f), new Vector3(0f, 0f, 5f), Vector3.one), body,
                MeshUvMode.BoxProject);

            for (int e = 0; e < 2; e++)
            {
                float x = e == 0 ? -0.082f : 0.082f;
                handset.Add(MeshPrimitives.Prism(0.03f, 0.03f, 0.03f, 14, 0.006f, false),
                    MeshBuilder.Trs(new Vector3(x, 0f, 0f), new Vector3(90f, 0f, 0f), Vector3.one), body,
                    MeshUvMode.BoxProject);
                handset.Add(MeshPrimitives.Ring(0.014f, 0.03f, 0.032f, 14),
                    MeshBuilder.Trs(new Vector3(x, 0f, 0f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                    MeshUvMode.BoxProject);
            }

            Seal(GeneratedObjectNames.HandsetPivot, root.transform, new Vector3(0f, bodyHeight + 0.062f, -0.035f),
                handset);

            return root;
        }

        /// <summary>
        /// Витая трубка и жгуты, соединяющие телефоны с распредкоробкой на переднем
        /// краю стола: спираль у корпуса и толстый чёрный жгут, провисающий по столешнице.
        /// Без жгутов стол читался как витрина с тремя болванками.
        /// </summary>
        private static void BuildPhoneCords(Transform parent, Vector3[] phoneBases)
        {
            Material rubber = Mats.Bakelite(PhoneCordColor);
            Vector3 junction = new Vector3(0.1f, 0.016f, 0.3f);

            for (int i = 0; i < phoneBases.Length; i++)
            {
                Vector3 start = phoneBases[i] + new Vector3(0.08f, 0.03f, -0.08f);
                Vector3[] path = new Vector3[8];

                for (int s = 0; s < path.Length; s++)
                {
                    float t = s / (float)(path.Length - 1);

                    // Провисание по столу: дуга вниз и в сторону распредкоробки.
                    Vector3 point = Vector3.Lerp(start, junction, t);
                    point.y = Mathf.Lerp(start.y, junction.y, t) - Mathf.Sin(t * Mathf.PI) * 0.025f;
                    point.z += Mathf.Sin(t * Mathf.PI) * 0.05f * (i == 1 ? -1f : 1f);

                    // Ни одна точка жгута не опускается ниже столешницы: иначе пульт
                    // целиком приподнимается при вписывании в габарит (FitPropToBox
                    // выравнивает нижнюю грань по столешнице) и телефоны зависают в воздухе.
                    point.y = Mathf.Max(point.y, 0.012f);
                    path[s] = point;
                }

                MeshBuilder cord = new MeshBuilder();
                cord.Add(MeshPrimitives.Tube(path, 0.009f, 6), Matrix4x4.identity, rubber,
                    MeshUvMode.BoxProject);
                cord.Add(MeshPrimitives.Helix(0.016f, 0.004f, 4f, 0.06f, 10, 5),
                    MeshBuilder.Trs(start + new Vector3(-0.03f, 0.03f, -0.03f), new Vector3(58f, 0f, 0f),
                        Vector3.one), rubber, MeshUvMode.BoxProject);

                Seal("Phone_Cord_" + i, parent, Vector3.zero, cord);
            }

            // Распредкоробка, к которой сходятся все жгуты.
            MeshBuilder box = new MeshBuilder();
            box.Add(MeshPrimitives.BeveledBox(new Vector3(0.14f, 0.032f, 0.1f), 0.006f),
                MeshBuilder.Trs(junction, Vector3.zero, Vector3.one), Mats.CastIron(), MeshUvMode.BoxProject);
            box.Add(MeshPrimitives.BeveledBox(new Vector3(0.1f, 0.012f, 0.07f), 0.003f),
                MeshBuilder.Trs(junction + new Vector3(0f, 0.022f, 0f), Vector3.zero, Vector3.one),
                Mats.ColdSteel(), MeshUvMode.BoxProject);
            Seal("Phone_Junction_Box", parent, Vector3.zero, box);
        }

        /// <summary>Позади телефонов — невысокая наклонная панель с шестью сигнальными
        /// лампами LineLamp_0..5 и шестью тумблерами. Стоит на низком цоколе и повёрнута
        /// к игроку, поэтому лампы читаются, а не теряются за корпусами телефонов.</summary>
        private static void BuildLineLampPanel(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();
            Material iron = Mats.CastIron();
            Material amber = Mats.Neon(LampAmberColor);
            Material green = Mats.Neon(LampGreenColor);

            const float panelWidth = 0.9f;

            MeshBuilder panel = new MeshBuilder();
            panel.Add(MeshPrimitives.BeveledBox(new Vector3(panelWidth, 0.03f, 0.16f), 0.007f),
                MeshBuilder.Trs(new Vector3(0f, 0.045f, 0f), new Vector3(-18f, 0f, 0f), Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Цоколь стоит на столешнице и поднимает панель: ни одна точка прибора
            // не уходит ниже y = 0, иначе весь пульт приподнимается при вписывании
            // в габарит (FitPropToBox выравнивает нижнюю грань по столешнице).
            panel.Add(MeshPrimitives.BeveledBox(new Vector3(panelWidth, 0.06f, 0.05f), 0.008f),
                MeshBuilder.Trs(new Vector3(0f, 0.03f, -0.045f), new Vector3(-18f, 0f, 0f), Vector3.one), iron,
                MeshUvMode.BoxProject);

            GameObject root = Seal("Line_Panel", parent, position, panel);

            for (int i = 0; i < GeneratedObjectNames.LineLampCount; i++)
            {
                float x = -panelWidth * 0.5f + 0.1f + i * ((panelWidth - 0.2f) / (GeneratedObjectNames.LineLampCount - 1));
                Material lampMaterial = i % 3 == 0 ? green : amber;

                MeshBuilder lamp = new MeshBuilder();
                lamp.Add(MeshPrimitives.Prism(0.017f, 0.017f, 0.024f, 10, 0.004f, false),
                    MeshBuilder.Trs(Vector3.zero, new Vector3(-18f, 0f, 0f), Vector3.one), steel, MeshUvMode.BoxProject);
                lamp.Add(MeshPrimitives.Dome(0.016f, 0.02f, 10, 4, true),
                    MeshBuilder.Trs(new Vector3(0f, 0.021f, -0.007f), new Vector3(-18f, 0f, 0f), Vector3.one),
                    lampMaterial, MeshUvMode.BoxProject);

                Seal(GeneratedObjectNames.LineLamp(i), root.transform, new Vector3(x, 0.082f, 0.044f), lamp);

                MeshBuilder toggle = new MeshBuilder();
                toggle.Add(MeshPrimitives.Prism(0.011f, 0.011f, 0.014f, 8, 0.002f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.012f, 0f), new Vector3(-18f, 0f, 0f), Vector3.one), steel,
                    MeshUvMode.BoxProject);
                toggle.Add(MeshPrimitives.Prism(0.004f, 0.004f, 0.04f, 6, 0.001f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.034f, 0f), new Vector3(-18f + (i % 2 == 0 ? 22f : -22f), 0f, 0f),
                        Vector3.one), steel, MeshUvMode.BoxProject);

                Seal("LineToggle_" + i, root.transform, new Vector3(x, 0.052f, -0.024f), toggle);
            }
        }

/// <summary>
        /// Советский ЭЛТ-монитор с кабинетами: компактный прямоугольный корпус
        /// 0.50 x 0.42 x 0.36, выпуклая рамка, тёмно-зелёный экран Screen_Surface
        /// с CITY STATUS и зелёный индикатор питания.
        ///
        /// Раньше здесь стоял огромный круглый терминал 0.62 x 0.52 с полукуполом
        /// и аркой клавиш — на концепт-арте это плоский монитор, а стекло накрывало
        /// эмиссию и игрок видел тёмный овал вместо радара.
        /// </summary>
        public static GameObject CreateOrdersCrtTerminal(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();

            // Корпус: оливково-серый крашеный металл, чуть светлее бетона.
            Material iron = Mats.CrackedShell();
            Material screen = Mats.CityStatusScreen();

            MeshBuilder body = new MeshBuilder();

            // Массивный корпус монитора: локальная лицевая сторона (+Z) смотрит на игрока.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.5f, 0.42f, 0.34f), 0.022f),
                MeshBuilder.Trs(new Vector3(0f, 0.21f, -0.02f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Косой срез кожуха сзади: прибор уходит к стене, а не стоит кирпичом.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.42f, 0.3f, 0.14f), 0.03f),
                MeshBuilder.Trs(new Vector3(0f, 0.24f, -0.24f), new Vector3(-14f, 0f, 0f), Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Рамка экрана: широкая фаскованная, утоплена в корпус.
            body.Add(MeshPrimitives.Frame(new Vector2(0.47f, 0.35f), 0.05f, 0.055f, 0.012f),
                MeshBuilder.Trs(new Vector3(0f, 0.22f, 0.165f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Узкая клавиатурная полка под экраном с рядом клавиш.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.46f, 0.03f, 0.05f), 0.006f),
                MeshBuilder.Trs(new Vector3(0f, 0.045f, 0.175f), new Vector3(-12f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);

            for (int i = 0; i < 8; i++)
            {
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.035f, 0.022f, 0.016f), 0.004f),
                    MeshBuilder.Trs(new Vector3(-0.175f + i * 0.05f, 0.056f, 0.183f),
                        new Vector3(-12f, 0f, 0f), Vector3.one), steel, MeshUvMode.BoxProject);
            }

            // Заклёпки по нижней кромке рамки.
            AddRivetRow(body, new Vector3(-0.2f, 0.018f, 0.192f), new Vector3(0.2f, 0.018f, 0.192f), 5, 0.007f,
                0.005f, steel, Vector3.forward);

            GameObject root = Seal(GeneratedObjectNames.OrdersCrtTerminal, parent, position, body);

            // Экран: плоскость в проёме рамки. Именно Plane, а не AtlasQuad —
            // CityStatusScreen самостоятельная текстура, Keep-проекция берёт UV 0..1.
            MeshBuilder screenBuilder = new MeshBuilder();
            screenBuilder.Add(MeshPrimitives.Plane(0.4f, 0.28f),
                MeshBuilder.Trs(new Vector3(0f, 0.22f, 0.166f), Vector3.zero, Vector3.one), screen,
                MeshUvMode.Keep);
            Seal(GeneratedObjectNames.ScreenSurface, root.transform, Vector3.zero, screenBuilder);

            // Зелёный индикатор питания под экраном и чёрный тумблер яркости рядом.
            MeshBuilder led = new MeshBuilder();
            led.Add(MeshPrimitives.Dome(0.009f, 0.008f, 10, 3, true),
                MeshBuilder.Trs(new Vector3(0.2f, 0.058f, 0.184f), new Vector3(-78f, 0f, 0f), Vector3.one),
                Mats.Neon(CrtGreenColor), MeshUvMode.BoxProject);
            led.Add(MeshPrimitives.Prism(0.011f, 0.011f, 0.012f, 10, 0.002f, false),
                MeshBuilder.Trs(new Vector3(0.15f, 0.056f, 0.184f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);
            Seal("Terminal_Status_Leds", root.transform, Vector3.zero, led);

            // Ножки-опоры: прибор стоит на столешнице.
            MeshBuilder feet = new MeshBuilder();

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.2f;
                float z = (i < 2 ? -1f : 1f) * 0.13f;
                feet.Add(MeshPrimitives.Prism(0.022f, 0.022f, 0.035f, 8, 0.005f, false),
                    MeshBuilder.Trs(new Vector3(x, 0.018f, z), Vector3.zero, Vector3.one), steel,
                    MeshUvMode.BoxProject);
            }

            Seal("Terminal_Feet", root.transform, Vector3.zero, feet);
            return root;
        }

        /// <summary>Матричный принтер справа от терминала с выезжающей лентой PaperStrip_Pivot.</summary>
        public static GameObject CreateDotMatrixPrinter(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();
            Material paper = Mats.Paper();
            Material plastic = Mats.Bakelite(BodyGreyColor);

            // Корпус: нижняя часть сплошная, верхняя — рамка вокруг щели для бумаги.
            MeshBuilder body = new MeshBuilder();
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.42f, 0.24f, 0.38f), 0.02f),
                MeshBuilder.Trs(new Vector3(0f, 0.12f, 0f), Vector3.zero, Vector3.one), plastic,
                MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.Frame(new Vector2(0.42f, 0.38f), 0.06f, 0.34f, 0.018f),
                MeshBuilder.Trs(new Vector3(0f, 0.17f, 0f), Vector3.zero, Vector3.one), plastic,
                MeshUvMode.BoxProject);

            // Верхняя крышка с вентиляцией.
            Mesh lid = MeshPrimitives.Frame(new Vector2(0.42f, 0.38f), 0.1f, 0.05f, 0.012f);
            body.Add(lid, MeshBuilder.Trs(new Vector3(0f, 0.365f, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Вал подачи бумаги и кнопка включения.
            body.Add(MeshPrimitives.Prism(0.022f, 0.022f, 0.32f, 10, 0.005f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.31f, 0.05f), new Vector3(0f, 0f, 90f), Vector3.one), steel,
                MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.Prism(0.018f, 0.018f, 0.02f, 12, 0.004f, false),
                MeshBuilder.Trs(new Vector3(0.15f, 0.31f, 0.16f), new Vector3(90f, 0f, 0f), Vector3.one),
                Mats.Neon(LampGreenColor), MeshUvMode.BoxProject);

            AddRivetRow(body, new Vector3(-0.18f, 0.06f, 0.191f), new Vector3(0.18f, 0.06f, 0.191f), 5, 0.008f,
                0.005f, steel, Vector3.forward);

            GameObject root = Seal(GeneratedObjectNames.DotMatrixPrinter, parent, position, body);

            // Выезжающая лента: шарнир у щели, полотно уходит вниз и наружу.
            MeshBuilder strip = new MeshBuilder();
            Vector2[] outline =
            {
                new Vector2(-0.155f, 0f),
                new Vector2(0.155f, 0f),
                new Vector2(0.14f, -0.26f),
                new Vector2(-0.14f, -0.24f)
            };

            strip.Add(MeshPrimitives.ExtrudePolygon(outline, 0.004f, 1f),
                MeshBuilder.Trs(new Vector3(0f, 0f, 0f), new Vector3(78f, 0f, 0f), Vector3.one), paper,
                MeshUvMode.BoxProject);

            Seal(GeneratedObjectNames.PaperStripPivot, root.transform, new Vector3(0f, 0.295f, 0.12f), strip);
            return root;
        }

/// <summary>
        /// Ядерный пульт: компактная стальная коробка 0.21 x 0.08 x 0.21 в чёрно-жёлтую
        /// разметку, откидная крышка Safety_Cover_Pivot, поворотный ключ Launch_Key_Pivot,
        /// светящаяся красная кнопка «ПУСК» Red_Button_Plunger на металлическом кольце.
        /// Тумблеры приказов вынесены на стол отдельно — см. CreateDeskOrderSwitches.
        ///
        /// Геометрия намеренно компактная и БЕЗ разрывов по вертикали: корпус,
        /// кольцо и кнопка идут вплотную друг к другу (0.00 → 0.08 → 0.10).
        /// Габарит дополнительно принудительно вписывается в бокс вызовом
        /// BunkerWorldBuilder.FitPropToBox.
        /// </summary>
        public static GameObject CreateNuclearLaunchBox(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();
            Material hazard = Mats.Hazard();
            Material glass = Mats.SmokedGlass();
            Material red = Mats.Neon(AlertRedColor);

            const float width = 0.21f;
            const float depth = 0.21f;
            const float baseHeight = 0.08f;
            const float frameTop = 0.095f;
            const float buttonRadius = 0.042f;
            const float buttonHeight = 0.03f;

            MeshBuilder body = new MeshBuilder();

            // Нижний корпус в чёрно-жёлтой диагональной разметке hazard.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(width, baseHeight, depth), 0.01f),
                MeshBuilder.Trs(new Vector3(0f, baseHeight * 0.5f, 0f), Vector3.zero, Vector3.one), hazard,
                MeshUvMode.BoxProject);

            // Верхняя стальная рамка КОЛЬЦОМ: кнопка «ПУСК» стоит посередине верхней
            // грани корпуса (y = 0.08), а не на сплошной пластине.
            body.Add(MeshPrimitives.Ring(0.062f, width * 0.5f, 0.009f, 24),
                MeshBuilder.Trs(new Vector3(0f, baseHeight + 0.004f, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Замок и поворотный ключ на передней стенке.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.055f, 0.05f, 0.018f), 0.005f),
                MeshBuilder.Trs(new Vector3(0f, 0.03f, depth * 0.5f + 0.009f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            AddRivetRow(body,
                new Vector3(-width * 0.5f + 0.025f, 0.012f, depth * 0.5f + 0.001f),
                new Vector3(width * 0.5f - 0.025f, 0.012f, depth * 0.5f + 0.001f),
                5, 0.005f, 0.003f, steel, Vector3.forward);

            GameObject root = Seal(GeneratedObjectNames.NuclearLaunchBox, parent, position, body);

            // Откидная крышка 0.20 x 0.20: серая рамка, дымчатое стекло и слабое
            // красное отражение кнопки в стекле. Шарнир — на ЗАДНЕЙ кромке (локальный -Z),
            // то есть дальше от игрока, и открывается вверх-назад на 70°: иначе крышка
            // встаёт между камерой и кнопкой.
            const float coverSize = 0.2f;
            MeshBuilder cover = new MeshBuilder();
            cover.Add(MeshPrimitives.Frame(new Vector2(coverSize, coverSize), 0.018f, 0.012f, 0.004f),
                MeshBuilder.Trs(Vector3.zero, Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
            cover.Add(MeshPrimitives.BeveledBox(new Vector3(coverSize - 0.03f, 0.006f, coverSize - 0.03f), 0.002f),
                MeshBuilder.Trs(Vector3.zero, Vector3.zero, Vector3.one), glass, MeshUvMode.BoxProject);

            // Отражение красной кнопки в стекле: слабый красный диск, чтобы крышка
            // не читалась пустой чёрной пластиной.
            cover.Add(MeshPrimitives.Dome(buttonRadius * 0.8f, 0.002f, 14, 2, true),
                MeshBuilder.Trs(new Vector3(0f, 0.004f, coverSize * 0.18f), Vector3.zero, Vector3.one),
                Mats.Neon(new Color(0.35f, 0.04f, 0.07f)), MeshUvMode.BoxProject);

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.078f : 0.078f;
                cover.Add(MeshPrimitives.Prism(0.008f, 0.008f, 0.03f, 8, 0.002f, false),
                    MeshBuilder.Align(new Vector3(x, 0f, -0.006f), Vector3.left), steel, MeshUvMode.BoxProject);
            }

            Seal(GeneratedObjectNames.SafetyCoverPivot, root.transform,
                new Vector3(0f, frameTop, -depth * 0.46f), cover).transform.localRotation =
                Quaternion.Euler(-70f, 0f, 0f);

            // Поворотный ключ: стержень с двумя лопастями, торчит из замка.
            MeshBuilder key = new MeshBuilder();
            key.Add(MeshPrimitives.Prism(0.004f, 0.004f, 0.04f, 8, 0f, false),
                MeshBuilder.Trs(new Vector3(0f, 0f, 0.02f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);
            key.Add(MeshPrimitives.BeveledBox(new Vector3(0.016f, 0.004f, 0.016f), 0.001f),
                MeshBuilder.Trs(new Vector3(0f, 0f, 0.038f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
            key.Add(MeshPrimitives.BeveledBox(new Vector3(0.011f, 0.004f, 0.011f), 0.001f),
                MeshBuilder.Trs(new Vector3(0f, 0f, 0.03f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            Seal(GeneratedObjectNames.LaunchKeyPivot, root.transform,
                new Vector3(0f, 0.03f, depth * 0.5f + 0.026f), key);

            // Красная кнопка «ПУСК»: посередине верхней грани, ровно над центром кольца.
            MeshBuilder plunger = new MeshBuilder();
            plunger.Add(MeshPrimitives.Prism(buttonRadius + 0.014f, buttonRadius + 0.014f, 0.01f, 16, 0.003f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.005f, 0f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
            plunger.Add(MeshPrimitives.Prism(buttonRadius, buttonRadius, buttonHeight, 16, 0.004f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.01f + buttonHeight * 0.5f, 0f), Vector3.zero, Vector3.one), red,
                MeshUvMode.BoxProject);
            plunger.Add(MeshPrimitives.Dome(buttonRadius, 0.01f, 16, 5, true),
                MeshBuilder.Trs(new Vector3(0f, 0.01f + buttonHeight, 0f), Vector3.zero, Vector3.one), red,
                MeshUvMode.BoxProject);

            Seal(GeneratedObjectNames.RedButtonPlunger, root.transform,
                new Vector3(0f, baseHeight, 0f), plunger);

            // Канонический алиас Nuke_Button — тот же меш и материал, чтобы биндер нашёл оба имени.
            GameObject alias = MakeNode(GeneratedObjectNames.NukeButton, root.transform,
                new Vector3(0f, baseHeight, 0f));
            alias.AddComponent<MeshFilter>().sharedMesh = plunger.Build("NukeButton_Mesh", false);
            alias.AddComponent<MeshRenderer>().sharedMaterials = new[] { steel, red };

            return root;
        }

        /// <summary>
        /// Три тумблера приказов Switch_01..03, стоящие прямо на столешнице рядом с
        /// ядерным пультом: стальная пластина, качающийся рычаг и светящийся индикатор.
        /// Раньше они были приварены к передней стенке пульта и тонули в его тени.
        /// </summary>
        public static void CreateDeskOrderSwitches(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();

            MeshBuilder plate = new MeshBuilder();
            plate.Add(MeshPrimitives.BeveledBox(new Vector3(0.24f, 0.012f, 0.09f), 0.004f),
                MeshBuilder.Trs(new Vector3(0f, 0.006f, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);
            Seal("Desk_Switch_Plate", parent, position, plate);

            for (int i = 0; i < GeneratedObjectNames.OrderSwitchCount; i++)
            {
                float x = -0.08f + i * 0.08f;

                MeshBuilder toggle = new MeshBuilder();
                toggle.Add(MeshPrimitives.Prism(0.014f, 0.014f, 0.014f, 10, 0.003f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.019f, 0f), Vector3.zero, Vector3.one), steel,
                    MeshUvMode.BoxProject);
                toggle.Add(MeshPrimitives.Prism(0.0045f, 0.0045f, 0.042f, 8, 0.001f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.046f, 0f),
                        new Vector3(i % 2 == 0 ? 30f : -30f, 0f, 0f), Vector3.one), steel, MeshUvMode.BoxProject);
                toggle.Add(MeshPrimitives.Sphere(0.009f, 8, 6),
                    MeshBuilder.Trs(new Vector3(0f, 0.067f, 0.01f), Vector3.zero, Vector3.one),
                    Mats.Neon(i == 0 ? AlertRedColor : LampAmberColor), MeshUvMode.BoxProject);

                Seal(GeneratedObjectNames.OrderSwitch(i), parent, position + new Vector3(x, 0f, 0f), toggle);
            }
        }

        /// <summary>
        /// Настенное бронированное табло «СТАТУС ГОСУДАРСТВА» из четырёх застеклённых секций-диорам.
        /// Внутри каждой — макет живого города City_State_Model (высота зданий растёт с
        /// DevelopmentLevel), проволочный купол ПВО AAShield_Dome и отдельный узел
        /// обугленных руин Ruins_State, который на старте выключен: города целые.
        /// Секции-диорамы лежат на ЛИЦЕВОЙ (+Z) стороне табло — в комнату, к игроку.
        /// </summary>
        public static GameObject CreateStateStatusBoard(Transform parent, Vector3 position,
            IReadOnlyList<CityState> cities)
        {
            Material iron = Mats.CastIron();
            Material steel = Mats.ColdSteel();
            Material glass = Mats.SmokedGlass();
            Material cityBody = Mats.Cloth(new Color(0.32f, 0.34f, 0.38f));
            Material ruinBody = Mats.Bakelite(new Color(0.12f, 0.1f, 0.09f));
            Material ember = Mats.Neon(new Color(1f, 0.35f, 0.08f));
            Material domeMaterial = Mats.Neon(ShieldDomeColor);

            const float boardWidth = 4.4f;
            const float boardHeight = 2.6f;
            const float cellWidth = 1.0f;
            const float cellHeight = 1.1f;

            MeshBuilder frame = new MeshBuilder();
            frame.Add(MeshPrimitives.BeveledBox(new Vector3(boardWidth, boardHeight, 0.16f), 0.03f),
                MeshBuilder.Trs(new Vector3(0f, boardHeight * 0.5f, -0.08f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Рама-уголки и заклёпки по периметру.
            AddRivetRow(frame, new Vector3(-boardWidth * 0.5f + 0.1f, 0.1f, 0.005f),
                new Vector3(boardWidth * 0.5f - 0.1f, 0.1f, 0.005f), 13, 0.014f, 0.01f, steel, Vector3.forward);
            AddRivetRow(frame, new Vector3(-boardWidth * 0.5f + 0.1f, boardHeight - 0.1f, 0.005f),
                new Vector3(boardWidth * 0.5f - 0.1f, boardHeight - 0.1f, 0.005f), 13, 0.014f, 0.01f, steel,
                Vector3.forward);

            // Верхняя табличка со шрифтовым атласом (белая подпись на чёрном).
            frame.Add(AtlasQuad(2.4f, 0.3f, new Rect(0.25f, 0f, 0.25f, 0.25f)),
                MeshBuilder.Trs(new Vector3(0f, boardHeight - 0.14f, 0.085f), Vector3.zero, Vector3.one),
                Mats.Segment(new Color(1f, 0.69f, 0f)), MeshUvMode.Keep);

            GameObject root = Seal(GeneratedObjectNames.StateStatusBoard, parent, position, frame);

            for (int i = 0; i < GeneratedObjectNames.CityDioramaCount; i++)
            {
                CityState city = cities != null && i < cities.Count ? cities[i] : null;
                int development = city != null ? city.DevelopmentLevel : 0;
                bool destroyed = city != null && city.IsDestroyed;

                float x = (i - 1.5f) * cellWidth;
                float y = boardHeight * 0.5f;

                MeshBuilder cell = new MeshBuilder();

                // Ниша секции: задняя стенка и рамка.
                cell.Add(MeshPrimitives.BeveledBox(new Vector3(cellWidth - 0.08f, cellHeight - 0.08f, 0.02f), 0.006f),
                    MeshBuilder.Trs(new Vector3(0f, 0f, -0.18f), Vector3.zero, Vector3.one), steel,
                    MeshUvMode.BoxProject);

                // Стекло-витрина.
                cell.Add(MeshPrimitives.BeveledBox(new Vector3(cellWidth - 0.06f, cellHeight - 0.06f, 0.012f), 0.004f),
                    MeshBuilder.Trs(new Vector3(0f, 0f, 0.04f), Vector3.zero, Vector3.one), glass, MeshUvMode.BoxProject);

                Seal(GeneratedObjectNames.CityDiorama(i), root.transform, new Vector3(x, y, 0.08f), cell);

                BuildCityModel(root.transform, GeneratedObjectNames.CityDiorama(i), i, development, destroyed,
                    cityBody, ruinBody, ember, domeMaterial, steel);
            }

            return root;
        }

        /// <summary>
        /// Содержимое секции-диорамы. Оба состояния города строятся всегда и живут
        /// ОТДЕЛЬНЫМИ узлами внутри секции: City_State_Model показывается, пока город
        /// цел, Ruins_State — когда уничтожен.
        ///
        /// Раньше руины и макет города собирались в ОДИН узел с именем Ruins_State, а
        /// родителем считалась несуществующая ветка «CityDiorama_i/CityDiorama_i».
        /// Из-за этого все четыре Ruins_State оставались в корне сцены отдельными
        /// объектами, а куполы ПВО и зелёные макеты городов не появлялись вообще.
        /// </summary>
        private static void BuildCityModel(Transform board, string cellName, int index, int development, bool destroyed,
            Material cityBody, Material ruinBody, Material ember, Material domeMaterial, Material steel)
        {
            Transform cell = board.Find(cellName);

            if (cell == null)
            {
                return;
            }

            DetRandom random = new DetRandom(0xC17 * (index + 1) + 7);
            DetRandom ruinRandom = new DetRandom(0x51E * (index + 1) + 3);

            // --- МАКЕТ ЖИВОГО ГОРОДА ---
            MeshBuilder city = new MeshBuilder();

            // Основание-подставка.
            city.Add(MeshPrimitives.BeveledBox(new Vector3(0.82f, 0.02f, 0.16f), 0.006f),
                MeshBuilder.Trs(new Vector3(0f, -0.5f, -0.08f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            // Число и высота зданий растут с уровнем развития.
            int buildings = Mathf.Clamp(4 + development, 4, 14);

            for (int i = 0; i < buildings; i++)
            {
                float rx = random.Range(-0.34f, 0.34f);
                float rz = random.Range(-0.15f, -0.03f);
                float levelBoost = 0.02f * Mathf.Max(0, development);
                float height = random.Range(0.06f, 0.16f + levelBoost);
                Vector3 size = new Vector3(random.Range(0.045f, 0.1f), height, random.Range(0.045f, 0.1f));

                city.Add(MeshPrimitives.BeveledBox(size, 0.006f),
                    MeshBuilder.Trs(new Vector3(rx, -0.5f + height * 0.5f, rz), Vector3.zero, Vector3.one), cityBody,
                    MeshUvMode.BoxProject);
            }

            // Светящиеся окна этажей.
            int windows = Mathf.Clamp(buildings * 2, 6, 26);

            for (int i = 0; i < windows; i++)
            {
                city.Add(MeshPrimitives.BeveledBox(new Vector3(0.008f, 0.006f, 0.008f), 0f),
                    MeshBuilder.Trs(new Vector3(random.Range(-0.32f, 0.32f),
                        -0.5f + random.Range(0.04f, 0.32f), random.Range(-0.03f, 0f)), Vector3.zero, Vector3.one),
                    steel, MeshUvMode.BoxProject);
            }

            GameObject cityNode = Seal(GeneratedObjectNames.CityStateModel, cell, Vector3.zero, city);
            cityNode.SetActive(!destroyed);

            // --- ОБУГЛЕННЫЕ РУИНЫ ---
            MeshBuilder ruins = new MeshBuilder();

            ruins.Add(MeshPrimitives.BeveledBox(new Vector3(0.82f, 0.02f, 0.16f), 0.006f),
                MeshBuilder.Trs(new Vector3(0f, -0.5f, -0.08f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            int debris = 7;

            for (int i = 0; i < debris; i++)
            {
                float rx = ruinRandom.Range(-0.34f, 0.34f);
                float rz = ruinRandom.Range(-0.14f, -0.03f);
                float height = ruinRandom.Range(0.05f, 0.2f);
                Vector3 size = new Vector3(ruinRandom.Range(0.05f, 0.14f), height, ruinRandom.Range(0.05f, 0.12f));

                ruins.Add(MeshPrimitives.BeveledBox(size, 0.008f),
                    MeshBuilder.Trs(new Vector3(rx, -0.5f + height * 0.5f, rz),
                        new Vector3(0f, ruinRandom.Range(0f, 60f), ruinRandom.Range(-8f, 8f)), Vector3.one),
                    ruinBody, MeshUvMode.BoxProject);

                if (i % 3 == 0)
                {
                    ruins.Add(MeshPrimitives.Sphere(0.018f, 6, 4),
                        MeshBuilder.Trs(new Vector3(rx, -0.5f + height, rz), Vector3.zero, Vector3.one), ember,
                        MeshUvMode.BoxProject);
                }
            }

            // Тлеющее пламя: сгустки света между обломками.
            for (int i = 0; i < 5; i++)
            {
                ruins.Add(MeshPrimitives.Sphere(ruinRandom.Range(0.02f, 0.05f), 6, 4),
                    MeshBuilder.Trs(new Vector3(ruinRandom.Range(-0.3f, 0.3f), -0.5f + ruinRandom.Range(0f, 0.08f),
                        ruinRandom.Range(-0.14f, -0.02f)), Vector3.zero, Vector3.one), ember, MeshUvMode.BoxProject);
            }

            GameObject ruinsNode = Seal(GeneratedObjectNames.RuinsState, cell, Vector3.zero, ruins);

            // Города на старте целые: руины выключены сразу при сборке, иначе в корне
            // сцены болтались бы обломки без всякого отношения к секциям-диорамам.
            ruinsNode.SetActive(destroyed);

            // --- КУПОЛ ПВО ---
            // Живой город под куполом, уничтоженный — без щита (узел остаётся пустым).
            MeshBuilder dome = new MeshBuilder();
            float domeRadius = 0.36f + 0.02f * Mathf.Max(0, development);

            dome.Add(MeshPrimitives.DomeShell(domeRadius, 0.012f, 18, 7),
                MeshBuilder.Trs(new Vector3(0f, -0.48f, -0.08f), Vector3.zero, Vector3.one), domeMaterial,
                MeshUvMode.BoxProject);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                dome.Add(MeshPrimitives.Prism(0.008f, 0.008f, domeRadius * 2f, 5, 0f, false),
                    MeshBuilder.Align(new Vector3(0f, -0.48f + domeRadius * 0.1f, -0.08f) + dir * domeRadius,
                        dir), steel, MeshUvMode.BoxProject);
            }

            GameObject domeNode = Seal(GeneratedObjectNames.AaShieldDome, cell, Vector3.zero, dome);
            domeNode.SetActive(!destroyed);
        }
    }
}
