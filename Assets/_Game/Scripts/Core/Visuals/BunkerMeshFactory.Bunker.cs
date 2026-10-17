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
        // Значения 1-в-1 из спецификации: красный #941C1A, жёлтый #B88724, чёрный #1C1F21.
        private static readonly Color PhoneRedColor = new Color(0.58f, 0.11f, 0.10f);
        private static readonly Color PhoneYellowColor = new Color(0.72f, 0.53f, 0.14f);
        private static readonly Color PhoneBlackColor = new Color(0.11f, 0.12f, 0.13f);

        /// <summary>Серо-бежевый диск набора #C2B8A3 — един на всех трёх телефонах.</summary>
        private static readonly Color PhoneDialWhite = new Color(0.7608f, 0.7216f, 0.6392f);

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
        /// них — плоская ламповая планка Line_Panel с метками LineLamp_0..5.
        ///
        /// Узел Switchboard_Unit — чистый пивот без собственной геометрии. Лицевая
        /// сторона телефонов (диск набора) смотрит в -Z, игрок сидит в -Z от стола,
        /// поэтому пивот сажается на стол БЕЗ разворота: внешний вид полностью
        /// определяется локальной геометорией самих телефонов.
        /// </summary>
        public static GameObject CreateSwitchboardUnit(Transform parent)
        {
            GameObject root = MakeNode(GeneratedObjectNames.SwitchboardUnit, parent, Vector3.zero);

            // Локальные базы телефонов 1-в-1 из спецификации: узкий ряд с шагом 28 см,
            // красный слева (-0.28), жёлтый в центре, чёрный справа.
            Vector3[] phoneBases =
            {
                new Vector3(-0.28f, 0f, 0f),
                Vector3.zero,
                new Vector3(0.28f, 0f, 0f)
            };

            // Жёлтый и чёрный слегка довёрнуты (8° и 14°): ряд не читается мёртво
            // строго по прямой, телефоны стоят чуть веером, как на концепт-арте.
            BuildBakelitePhone(root.transform, GeneratedObjectNames.PhoneRed, PhoneRedColor, phoneBases[0]);
            BuildBakelitePhone(root.transform, GeneratedObjectNames.PhoneYellow, PhoneYellowColor,
                phoneBases[1]).transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
            BuildBakelitePhone(root.transform, GeneratedObjectNames.PhoneBlack, PhoneBlackColor,
                phoneBases[2]).transform.localRotation = Quaternion.Euler(0f, 14f, 0f);

            BuildLineLampPanel(root.transform, new Vector3(0f, 0f, 0.22f));
            BuildPhoneCords(root.transform, phoneBases);

            return root;
        }

        /// <summary>
        /// Карболитовый дисковый телефон 70-х, геометрия 1-в-1 из спецификации:
        /// основание 0.16 x 0.045 x 0.18 стоит ровно на столешнице (низ y = 0),
        /// наклонная верхняя грань 22°, диск набора Dial_Wheel тонким диском
        /// (диаметр 7.5 см — в спецификации Prism(0.075, 0.075, 0.004) задан
        /// ДИАМЕТР, у Prism это радиусы) на переднем свесе и трубка
        /// Handset_Pivot сверху. Все три телефона идентичны по форме, цвет
        /// передаёт материал корпуса.
        /// </summary>
        public static GameObject BuildBakelitePhone(Transform parent, string nodeName, Color bodyColor,
            Vector3 position)
        {
            Material body = Mats.Bakelite(bodyColor);
            Material dial = Mats.Bakelite(PhoneDialWhite);
            Material steel = Mats.ColdSteel();
            MeshBuilder shell = new MeshBuilder();

            const float bodyWidth = 0.16f;
            const float bodyHeight = 0.045f;
            const float bodyDepth = 0.18f;

            // Основание: ровный короб, без ножек — нижняя плоскость в y = 0.
            shell.Add(MeshPrimitives.BeveledBox(new Vector3(bodyWidth, bodyHeight, bodyDepth), 0.012f),
                MeshBuilder.Trs(new Vector3(0f, bodyHeight * 0.5f, 0f), Vector3.zero, Vector3.one), body,
                MeshUvMode.BoxProject);

            // Наклонная грань 22°: короб 0.15 x 0.045 x 0.12 с центром (0, 0.055, 0.015).
            shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.15f, 0.045f, 0.12f), 0.01f),
                MeshBuilder.Trs(new Vector3(0f, 0.055f, 0.015f), new Vector3(22f, 0f, 0f), Vector3.one), body,
                MeshUvMode.BoxProject);

            // Стойки-гнёзда под трубку по бокам ската.
            for (int e = 0; e < 2; e++)
            {
                float x = e == 0 ? -0.04f : 0.04f;
                shell.Add(MeshPrimitives.Prism(0.008f, 0.008f, 0.02f, 8, 0.001f, false),
                    MeshBuilder.Trs(new Vector3(x, 0.085f, 0.03f), Vector3.zero, Vector3.one), steel,
                    MeshUvMode.BoxProject);
            }

            GameObject root = Seal(nodeName, parent, position, shell);

            // --- Диск набора: тонкий диск Prism (диаметр 0.075), узел в (0, 0.062, -0.028),
            // наклон -68°. У Prism тонкая ось - локальный Y, поэтому разворот диска
            // делает именно узел: его нормаль (0, 0.375, -0.927) смотрит прямо в камеру.
            const float dialRadius = 0.0375f;
            const float dialThickness = 0.004f;

            MeshBuilder dialBuilder = new MeshBuilder();
            dialBuilder.Add(MeshPrimitives.Prism(dialRadius, dialRadius, dialThickness, 24, 0.001f, false),
                MeshBuilder.Trs(Vector3.zero, Vector3.zero, Vector3.one), dial, MeshUvMode.BoxProject);

            // Серединка и десять отверстий — узнаваемый силуэт дискового набора.
            dialBuilder.Add(MeshPrimitives.Prism(0.009f, 0.009f, dialThickness + 0.002f, 12, 0f, false),
                MeshBuilder.Trs(Vector3.zero, Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            for (int h = 0; h < 10; h++)
            {
                float angle = h * 36f;
                Vector3 hole = Quaternion.Euler(0f, angle, 0f) * new Vector3(dialRadius * 0.72f, 0f, 0f);
                dialBuilder.Add(MeshPrimitives.Prism(0.0045f, 0.0045f, dialThickness + 0.002f, 8, 0f, false),
                    MeshBuilder.Trs(hole, Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
            }

            GameObject dialNode = Seal(GeneratedObjectNames.DialWheel, root.transform,
                new Vector3(0f, 0.062f, -0.028f), dialBuilder);
            dialNode.transform.localRotation = Quaternion.Euler(-68f, 0f, 0f);

            // Трубка на шарнире Handset_Pivot: планка 0.17 x 0.024 x 0.024 и две
            // керамические чашки Prism(0.03, 0.03, 0.03) на концах (±0.08, -0.01).
            MeshBuilder handset = new MeshBuilder();
            handset.Add(MeshPrimitives.BeveledBox(new Vector3(0.17f, 0.024f, 0.024f), 0.008f),
                MeshBuilder.Trs(new Vector3(0f, 0.012f, 0f), Vector3.zero, Vector3.one), body,
                MeshUvMode.BoxProject);

            for (int e = 0; e < 2; e++)
            {
                float x = e == 0 ? -0.08f : 0.08f;
                handset.Add(MeshPrimitives.Prism(0.03f, 0.03f, 0.03f, 14, 0.006f, false),
                    MeshBuilder.Trs(new Vector3(x, -0.01f, 0f), new Vector3(90f, 0f, 0f), Vector3.one), body,
                    MeshUvMode.BoxProject);
                handset.Add(MeshPrimitives.Ring(0.014f, 0.03f, 0.032f, 14),
                    MeshBuilder.Trs(new Vector3(x, -0.01f, 0f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                    MeshUvMode.BoxProject);
            }

            Seal(GeneratedObjectNames.HandsetPivot, root.transform, new Vector3(0f, 0.105f, 0.03f), handset);

            return root;
        }

        /// <summary>
        /// Витая трубка и жгуты, сходящиеся с задних стенок телефонов в распредкоробку:
        /// небольшая стальная коробка на столешнице позади ряда телефонов.
        /// </summary>
        private static void BuildPhoneCords(Transform parent, Vector3[] phoneBases)
        {
            Material rubber = Mats.Bakelite(PhoneCordColor);

            // Распредкоробка: в спецификации точка (0, 0.016, 0.20), но там уже
            // стоит ламповая планка (панель занимает локальные z 0.19..0.25, y 0..0.05).
            // Ставлю коробку в (0, 0.014, 0.15) — между задними стенками телефонов
            // (z = 0.09) и лицевой стенкой панели (z = 0.19); габариты 0.10 x 0.028 x 0.05.
            Vector3 junction = new Vector3(0f, 0.014f, 0.15f);

            for (int i = 0; i < phoneBases.Length; i++)
            {
                // Выход жгута из задней нижней части корпуса телефона.
                Vector3 start = phoneBases[i] + new Vector3(0f, 0.012f, 0.07f);
                Vector3[] path = new Vector3[8];

                for (int s = 0; s < path.Length; s++)
                {
                    float t = s / (float)(path.Length - 1);

                    // Провисание по столу: лёгкая дуга между телефоном и коробкой.
                    Vector3 point = Vector3.Lerp(start, junction, t);
                    point.y = Mathf.Lerp(start.y, junction.y, t) - Mathf.Sin(t * Mathf.PI) * 0.01f;
                    point.z += Mathf.Sin(t * Mathf.PI) * 0.03f * (i == 1 ? -1f : 1f);

                    // Ни одна точка жгута не опускается ниже столешницы: иначе пульт
                    // целиком приподнимается при вписывании в габарит.
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
            box.Add(MeshPrimitives.BeveledBox(new Vector3(0.10f, 0.028f, 0.05f), 0.004f),
                MeshBuilder.Trs(junction, Vector3.zero, Vector3.one), Mats.CastIron(), MeshUvMode.BoxProject);
            box.Add(MeshPrimitives.BeveledBox(new Vector3(0.06f, 0.008f, 0.03f), 0.002f),
                MeshBuilder.Trs(junction + new Vector3(0f, 0.018f, 0f), Vector3.zero, Vector3.one),
                Mats.ColdSteel(), MeshUvMode.BoxProject);
            Seal("Phone_Junction_Box", parent, Vector3.zero, box);
        }

        /// <summary>Позади телефонов — плоская ламповая планка Line_Panel: аккуратная
        /// коробка 0.32 x 0.05 x 0.06 с шестью светящимися метками LineLamp_0..5 на
        /// лицевой (игроковой, локальный -Z) стороне. Раньше здесь стояла наклонная
        /// панель во всю ширину стола с шестью лампочками-грибами и шестью тумблерами —
        /// на концепт-арте это простая полоса с диодами.</summary>
        private static void BuildLineLampPanel(Transform parent, Vector3 position)
        {
            Material iron = Mats.CastIron();
            Material amber = Mats.Neon(LampAmberColor);
            Material green = Mats.Neon(LampGreenColor);

            MeshBuilder panel = new MeshBuilder();

            // Лицевая сторона планки — в -Z (к игроку): метки нарочно заходят на
            // 1 мм в корпус, чтобы их задняя грань не z-fight'ила с поверхностью.
            panel.Add(MeshPrimitives.BeveledBox(new Vector3(0.32f, 0.05f, 0.06f), 0.005f),
                MeshBuilder.Trs(new Vector3(0f, 0.025f, 0f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            GameObject root = Seal("Line_Panel", parent, position, panel);

            for (int i = 0; i < GeneratedObjectNames.LineLampCount; i++)
            {
                float x = -0.125f + i * 0.05f;
                Material lampMaterial = i % 2 == 0 ? green : amber;

                MeshBuilder lamp = new MeshBuilder();
                lamp.Add(MeshPrimitives.BeveledBox(new Vector3(0.012f, 0.012f, 0.004f), 0.001f),
                    MeshBuilder.Trs(Vector3.zero, Vector3.zero, Vector3.one), lampMaterial,
                    MeshUvMode.BoxProject);

                Seal(GeneratedObjectNames.LineLamp(i), root.transform, new Vector3(x, 0.025f, -0.031f), lamp);
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
        /// Ядерный пульт: стальная коробка 0.19 x 0.075 x 0.19 в чёрно-жёлтую ленточную
        /// разметку, на верхней грани — стальная пластина 0.11 x 0.006 x 0.11 и
        /// светящаяся красная кнопка «ПУСК» (0.085 x 0.025 x 0.085, #FF1744) в узле
        /// Red_Button_Plunger. Крышка Safety_Cover_Pivot откинута ЗАД (шарнир на
        /// дальней от игрока кромке, локальный +Z), наклон -15° из спецификации.
        /// Поворотный ключ Launch_Key_Pivot и замок — на лицевой стенке (-Z):
        /// прибор сажается на стол без разворота, лицом к игроку.
        /// Тумблеры приказов вынесены на стол отдельно — см. CreateDeskOrderSwitches.
        ///
        /// Геометрия без вертикальных разрывов: корпус (0..0.075), пластина
        /// (0.070..0.076) и кнопка (0.079..0.104) идут вплотную друг к другу.
        /// </summary>
        public static GameObject CreateNuclearLaunchBox(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();
            Material hazard = Mats.Hazard();
            Material glass = Mats.SmokedGlass();
            Material red = Mats.Neon(AlertRedColor);

            const float width = 0.19f;
            const float depth = 0.19f;
            const float bodyHeight = 0.075f;

            MeshBuilder body = new MeshBuilder();

            // Нижний корпус в чёрно-жёлтой диагональной разметке hazard.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(width, bodyHeight, depth), 0.008f),
                MeshBuilder.Trs(new Vector3(0f, bodyHeight * 0.5f, 0f), Vector3.zero, Vector3.one), hazard,
                MeshUvMode.BoxProject);

            // Верхняя стальная пластина 0.11 x 0.006 x 0.11: на ней стоит кнопка «ПУСК».
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.11f, 0.006f, 0.11f), 0.002f),
                MeshBuilder.Trs(new Vector3(0f, 0.073f, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Замок на передней стенке (-Z): прибор без разворота смотрит на игрока.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.055f, 0.05f, 0.018f), 0.005f),
                MeshBuilder.Trs(new Vector3(0f, 0.03f, -depth * 0.5f - 0.009f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            AddRivetRow(body,
                new Vector3(-width * 0.5f + 0.025f, 0.012f, -depth * 0.5f - 0.001f),
                new Vector3(width * 0.5f - 0.025f, 0.012f, -depth * 0.5f - 0.001f),
                5, 0.005f, 0.003f, steel, Vector3.forward);

            GameObject root = Seal(GeneratedObjectNames.NuclearLaunchBox, parent, position, body);

            // Откидная крышка 0.18 x 0.16: серая рамка и дымчатое стекло, шарнир — на
            // задней кромке (-Z-кромка корпуса ближе к игроку, поэтому крышка висит за
            // пультом, в +Z), наклон -15° из спецификации. Вертикальные рамки в Frame
            // строятся в XY, поэтому крышка стоит почти щитом за пультом, не заслоняя
            // кнопку: её верхняя кромка (0.158) заметно выше кнопки (0.104).
            const float coverSizeX = 0.18f;
            const float coverSizeY = 0.16f;
            MeshBuilder cover = new MeshBuilder();
            cover.Add(MeshPrimitives.Frame(new Vector2(coverSizeX, coverSizeY), 0.016f, 0.012f, 0.008f),
                MeshBuilder.Trs(Vector3.zero, Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
            cover.Add(MeshPrimitives.BeveledBox(new Vector3(coverSizeX - 0.026f, coverSizeY - 0.026f, 0.005f),
                0.002f),
                MeshBuilder.Trs(new Vector3(0f, 0f, -0.002f), Vector3.zero, Vector3.one), glass,
                MeshUvMode.BoxProject);

            Seal(GeneratedObjectNames.SafetyCoverPivot, root.transform,
                new Vector3(0f, 0.081f, depth * 0.5f + 0.015f), cover).transform.localRotation =
                Quaternion.Euler(-15f, 0f, 0f);

            // Поворотный ключ: стержень с двумя лопастями, торчит из замка вперёд (-Z).
            MeshBuilder key = new MeshBuilder();
            key.Add(MeshPrimitives.Prism(0.004f, 0.004f, 0.04f, 8, 0f, false),
                MeshBuilder.Trs(new Vector3(0f, 0f, -0.02f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);
            key.Add(MeshPrimitives.BeveledBox(new Vector3(0.016f, 0.004f, 0.016f), 0.001f),
                MeshBuilder.Trs(new Vector3(0f, 0f, -0.038f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);
            key.Add(MeshPrimitives.BeveledBox(new Vector3(0.011f, 0.004f, 0.011f), 0.001f),
                MeshBuilder.Trs(new Vector3(0f, 0f, -0.03f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            Seal(GeneratedObjectNames.LaunchKeyPivot, root.transform,
                new Vector3(0f, 0.03f, -depth * 0.5f - 0.026f), key);

            // Красная кнопка «ПУСК»: посередине стальной пластины, 1-в-1 из
            // спецификации (0.085 x 0.025 x 0.085 с центром y = 0.0915).
            MeshBuilder plunger = new MeshBuilder();
            plunger.Add(MeshPrimitives.BeveledBox(new Vector3(0.085f, 0.025f, 0.085f), 0.004f),
                MeshBuilder.Trs(Vector3.zero, Vector3.zero, Vector3.one), red, MeshUvMode.BoxProject);

            Seal(GeneratedObjectNames.RedButtonPlunger, root.transform,
                new Vector3(0f, 0.0915f, 0f), plunger);

            // Канонический алиас Nuke_Button — тот же меш и материал, чтобы биндер нашёл оба имени.
            GameObject alias = MakeNode(GeneratedObjectNames.NukeButton, root.transform,
                new Vector3(0f, 0.0915f, 0f));
            alias.AddComponent<MeshFilter>().sharedMesh = plunger.Build("NukeButton_Mesh", false);
            alias.AddComponent<MeshRenderer>().sharedMaterials = new[] { red };

            return root;
        }

        /// <summary>
        /// Три тумблера приказов Switch_01..03 на общей стальной пластине 0.24 x 0.012 x 0.09,
        /// стоящей на столешнице рядом с ядерным пультом: качающийся рычаг и низкая бобышка,
        /// шаг между рычагами — 0.07 м. Раньше рядом с каждым рычагом светился битый
        /// шарик-индикатор, которого нет на концепт-арте.
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
                float x = -0.07f + i * 0.07f;

                MeshBuilder toggle = new MeshBuilder();
                toggle.Add(MeshPrimitives.Prism(0.014f, 0.014f, 0.014f, 10, 0.003f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.019f, 0f), Vector3.zero, Vector3.one), steel,
                    MeshUvMode.BoxProject);
                toggle.Add(MeshPrimitives.Prism(0.0045f, 0.0045f, 0.042f, 8, 0.001f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.046f, 0f),
                        new Vector3(i % 2 == 0 ? 30f : -30f, 0f, 0f), Vector3.one), steel, MeshUvMode.BoxProject);

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
