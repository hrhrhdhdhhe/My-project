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
        private static readonly Color PhoneRedColor = new Color(0.75f, 0.12f, 0.12f);
        private static readonly Color PhoneYellowColor = new Color(0.85f, 0.62f, 0.12f);
        private static readonly Color PhoneBlackColor = new Color(0.14f, 0.15f, 0.17f);

        /// <summary>Молочно-белый диск набора и шкала прибора — как на телефонах 70-х.</summary>
        private static readonly Color PhoneDialWhite = new Color(0.86f, 0.85f, 0.80f);

        private static readonly Color BodyGreyColor = new Color(0.24f, 0.25f, 0.27f);
        private static readonly Color BeigeColor = new Color(0.62f, 0.58f, 0.48f);
        private static readonly Color AlertRedColor = new Color(1f, 0.09f, 0.27f);
        private static readonly Color LampAmberColor = new Color(1f, 0.69f, 0f);
        private static readonly Color LampGreenColor = new Color(0f, 0.9f, 0.46f);
        private static readonly Color CrtGreenColor = new Color(0f, 0.902f, 0.463f);
        private static readonly Color ShieldDomeColor = new Color(0f, 0.902f, 0.463f);

        /// <summary>
        /// Пульт связи бункера: настольный прибор без ножек и тумбы — плоская тёмная
        /// стальная подставка (20 мм), на ней слева направо три тяжёлых карболитовых
        /// телефона (красный, жёлтый, чёрный с магнитофонными катушками), а ПОЗАДИ
        /// телефонов — невысокая наклонная панель с шестью лампами LineLamp_0..5
        /// и шестью тумблерами.
        ///
        /// Каждый телефон — отдельный узел со своим MeshRenderer и своим цветным материалом:
        /// общий серый материал на все три превращал красный, жёлтый и чёрный в белый.
        /// Локальная лицевая сторона прибора смотрит в +Z (диски набора), игрок после
        /// разворота на 180° оказывается в -Z — отсюда FacingYaw.
        /// </summary>
        public static GameObject CreateSwitchboardUnit(Transform parent)
        {
            Material steel = Mats.ColdSteel();
            Material iron = Mats.CastIron();

            const float width = 1.9f;
            const float depth = 0.62f;

            // Подставка: 20 мм тёмной стали, прибор лежит прямо на столешнице.
            // Цоколя, ножек и задней стенки нет намеренно — «подставка под стол»
            // выглядела ногами наперевес и занимала кадр вместо приборов.
            MeshBuilder basePlate = new MeshBuilder();
            basePlate.Add(MeshPrimitives.BeveledBox(new Vector3(width, 0.02f, depth), 0.006f),
                MeshBuilder.Trs(new Vector3(0f, 0.01f, 0f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Заклёпки по периметру подставки — прибор не выглядит картонкой.
            AddRivetRow(basePlate, new Vector3(-width * 0.5f + 0.09f, 0.021f, depth * 0.5f - 0.07f),
                new Vector3(width * 0.5f - 0.09f, 0.021f, depth * 0.5f - 0.07f), 7, 0.011f, 0.007f,
                steel, Vector3.up);
            AddRivetRow(basePlate, new Vector3(-width * 0.5f + 0.09f, 0.021f, -depth * 0.5f + 0.07f),
                new Vector3(width * 0.5f - 0.09f, 0.021f, -depth * 0.5f + 0.07f), 7, 0.011f, 0.007f,
                steel, Vector3.up);

            GameObject root = Seal(GeneratedObjectNames.SwitchboardUnit, parent, Vector3.zero, basePlate);

            // --- Три телефона слева направо: красный, жёлтый, чёрный ---
            BuildBakelitePhone(root.transform, GeneratedObjectNames.PhoneRed, PhoneRedColor,
                new Vector3(-0.62f, 0.02f, 0.09f), false);
            BuildBakelitePhone(root.transform, GeneratedObjectNames.PhoneYellow, PhoneYellowColor,
                new Vector3(0f, 0.02f, 0.09f), false);
            BuildBakelitePhone(root.transform, GeneratedObjectNames.PhoneBlack, PhoneBlackColor,
                new Vector3(0.62f, 0.02f, 0.09f), true);

            // --- Позади телефонов: наклонная панель с лампами и тумблерами ---
            BuildLineLampPanel(root.transform, new Vector3(0f, 0.02f, -0.2f));

            return root;
        }

        /// <summary>
        /// Карболитовый дисковый телефон: корпус своего цвета + МОЛОЧНО-БЕЛЫЙ диск набора
        /// Dial_Wheel спереди на наклонной панели (по нему авторазворот прибора понимает,
        /// где у телефона лицо), трубка Handset_Pivot сверху. У чёрного варианта вместо
        /// трубки — окно с двумя магнитофонными катушками Reel_L / Reel_R.
        /// </summary>
        private static void BuildBakelitePhone(Transform parent, string nodeName, Color bodyColor, Vector3 position,
            bool withReels)
        {
            Material body = Mats.Bakelite(bodyColor);
            Material dial = Mats.Bakelite(PhoneDialWhite);
            Material steel = Mats.ColdSteel();
            MeshBuilder shell = new MeshBuilder();

            // Корпус-трапеция: задняя часть ниже передней — так у телефонов 70-х.
            shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.38f, 0.13f, 0.3f), 0.016f),
                MeshBuilder.Trs(new Vector3(0f, 0.065f, 0f), Vector3.zero, Vector3.one), body, MeshUvMode.BoxProject);

            // Наклонная лицевая панель: на ней стоит белый диск набора.
            shell.Add(MeshPrimitives.BeveledBox(new Vector3(0.38f, 0.07f, 0.2f), 0.014f),
                MeshBuilder.Trs(new Vector3(0f, 0.15f, 0.07f), new Vector3(-22f, 0f, 0f), Vector3.one), body,
                MeshUvMode.BoxProject);

            if (withReels)
            {
                // Окно катушек собрано рамкой — ролики видно сквозь проём.
                shell.Add(MeshPrimitives.Frame(new Vector2(0.34f, 0.24f), 0.04f, 0.035f, 0.006f),
                    MeshBuilder.Trs(new Vector3(0f, 0.195f, -0.04f), Vector3.zero, Vector3.one), body,
                    MeshUvMode.BoxProject);
            }

            GameObject root = Seal(nodeName, parent, position, shell);

            if (withReels)
            {
                // Катушки магнитофона Reel_L / Reel_R — вращаемые узлы.
                for (int i = 0; i < 2; i++)
                {
                    Material reelMaterial = steel;
                    MeshBuilder reel = new MeshBuilder();
                    reel.Add(MeshPrimitives.Prism(0.052f, 0.052f, 0.022f, 14, 0.005f, false),
                        MeshBuilder.Trs(Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one), reelMaterial,
                        MeshUvMode.BoxProject);
                    reel.Add(MeshPrimitives.Prism(0.016f, 0.016f, 0.03f, 10, 0.004f, false),
                        MeshBuilder.Trs(Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one), dial,
                        MeshUvMode.BoxProject);

                    // Спицы — чтобы при вращении было видно движение.
                    for (int s = 0; s < 3; s++)
                    {
                        float angle = s * 120f;
                        reel.Add(MeshPrimitives.BeveledBox(new Vector3(0.07f, 0.012f, 0.008f), 0.003f),
                            MeshBuilder.Trs(Vector3.zero, new Vector3(90f, angle, 0f), Vector3.one), reelMaterial,
                            MeshUvMode.BoxProject);
                    }

                    Seal(i == 0 ? GeneratedObjectNames.ReelLeft : GeneratedObjectNames.ReelRight, root.transform,
                        new Vector3(i == 0 ? -0.065f : 0.065f, 0.195f, -0.04f), reel);
                }
            }

            // Вращающийся белый диск набора — узел Dial_Wheel на лицевой панели.
            MeshBuilder dialWheel = new MeshBuilder();
            dialWheel.Add(MeshPrimitives.Prism(0.105f, 0.105f, 0.03f, 20, 0.006f, false),
                MeshBuilder.Trs(Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one), dial, MeshUvMode.BoxProject);
            dialWheel.Add(MeshPrimitives.Ring(0.028f, 0.05f, 0.034f, 16),
                MeshBuilder.Trs(Vector3.zero, new Vector3(90f, 0f, 0f), Vector3.one), steel, MeshUvMode.BoxProject);

            // Десять отверстий по кругу диска.
            for (int h = 0; h < 10; h++)
            {
                float angle = h * 36f;
                Vector3 hole = Quaternion.Euler(0f, 0f, angle) * new Vector3(0.078f, 0f, 0f);
                dialWheel.Add(MeshPrimitives.BeveledBox(new Vector3(0.022f, 0.034f, 0.022f), 0.004f),
                    MeshBuilder.Trs(hole, new Vector3(90f, angle, 0f), Vector3.one), steel, MeshUvMode.BoxProject);
            }

            Seal(GeneratedObjectNames.DialWheel, root.transform, new Vector3(0f, 0.158f, 0.16f), dialWheel);

            if (withReels)
            {
                return;
            }

            // Трубка на шарнире Handset_Pivot.
            MeshBuilder handset = new MeshBuilder();
            handset.Add(MeshPrimitives.BeveledBox(new Vector3(0.31f, 0.045f, 0.062f), 0.012f),
                MeshBuilder.Trs(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0f, 6f), Vector3.one), body,
                MeshUvMode.BoxProject);

            for (int e = 0; e < 2; e++)
            {
                float x = e == 0 ? -0.135f : 0.135f;
                handset.Add(MeshPrimitives.Prism(0.043f, 0.043f, 0.04f, 14, 0.008f, false),
                    MeshBuilder.Trs(new Vector3(x, 0.0f, 0f), new Vector3(90f, 0f, 0f), Vector3.one), body,
                    MeshUvMode.BoxProject);
                handset.Add(MeshPrimitives.Ring(0.02f, 0.043f, 0.044f, 14),
                    MeshBuilder.Trs(new Vector3(x, 0.0f, 0f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                    MeshUvMode.BoxProject);
            }

            Seal(GeneratedObjectNames.HandsetPivot, root.transform, new Vector3(0f, 0.2f, -0.055f), handset);

            // Витая трубка: спираль между трубкой и корпусом.
            MeshBuilder cord = new MeshBuilder();
            MeshPrimitives.Helix(0.035f, 0.006f, 5f, 0.16f, 12, 6);
            cord.Add(MeshPrimitives.Helix(0.034f, 0.0055f, 5f, 0.15f, 12, 6),
                MeshBuilder.Trs(new Vector3(-0.18f, 0.02f, -0.05f), new Vector3(0f, 0f, 88f), Vector3.one),
                steel, MeshUvMode.BoxProject);
            Seal("Handset_Cord", root.transform, Vector3.zero, cord);
        }

        /// <summary>Позади телефонов — невысокая наклонная панель: 6 сигнальных ламп
        /// LineLamp_0..5 и 6 тумблеров. Стоит на низком цоколе и повёрнута к игроку,
        /// поэтому лампы читаются, а не теряются за корпусами телефонов.</summary>
        private static void BuildLineLampPanel(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();
            Material iron = Mats.CastIron();
            Material amber = Mats.Neon(LampAmberColor);
            Material green = Mats.Neon(LampGreenColor);

            MeshBuilder panel = new MeshBuilder();
            panel.Add(MeshPrimitives.BeveledBox(new Vector3(1.7f, 0.035f, 0.2f), 0.008f),
                MeshBuilder.Trs(Vector3.zero, new Vector3(-18f, 0f, 0f), Vector3.one), iron, MeshUvMode.BoxProject);

            // Низкий цоколь: панель возвышается над подставкой, но не закрывает телефоны.
            panel.Add(MeshPrimitives.BeveledBox(new Vector3(1.7f, 0.11f, 0.05f), 0.008f),
                MeshBuilder.Trs(new Vector3(0f, -0.06f, -0.05f), new Vector3(-18f, 0f, 0f), Vector3.one), iron,
                MeshUvMode.BoxProject);

            GameObject root = Seal("Line_Panel", parent, position, panel);

            for (int i = 0; i < GeneratedObjectNames.LineLampCount; i++)
            {
                float x = -0.7f + i * 0.28f;
                Material lampMaterial = i % 3 == 0 ? green : amber;

                MeshBuilder lamp = new MeshBuilder();
                lamp.Add(MeshPrimitives.Prism(0.022f, 0.022f, 0.03f, 10, 0.005f, false),
                    MeshBuilder.Trs(Vector3.zero, new Vector3(-18f, 0f, 0f), Vector3.one), steel, MeshUvMode.BoxProject);
                lamp.Add(MeshPrimitives.Dome(0.021f, 0.026f, 10, 4, true),
                    MeshBuilder.Trs(new Vector3(0f, 0.026f, -0.008f), new Vector3(-18f, 0f, 0f), Vector3.one),
                    lampMaterial, MeshUvMode.BoxProject);

                Seal(GeneratedObjectNames.LineLamp(i), root.transform, new Vector3(x, 0.045f, 0.055f), lamp);

                // Тумблер под лампой.
                MeshBuilder toggle = new MeshBuilder();
                toggle.Add(MeshPrimitives.Prism(0.014f, 0.014f, 0.016f, 8, 0.003f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.014f, 0f), new Vector3(-18f, 0f, 0f), Vector3.one), steel,
                    MeshUvMode.BoxProject);

                float tilt = i % 2 == 0 ? 26f : -26f;
                toggle.Add(MeshPrimitives.Prism(0.005f, 0.005f, 0.05f, 6, 0.002f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.042f, 0.0f), new Vector3(-18f + tilt, 0f, 0f), Vector3.one), steel,
                    MeshUvMode.BoxProject);
                toggle.Add(MeshPrimitives.Sphere(0.011f, 8, 6),
                    MeshBuilder.Trs(new Vector3(0f, 0.062f, -0.014f), Vector3.zero, Vector3.one), steel,
                    MeshUvMode.BoxProject);

                Seal("LineToggle_" + i, root.transform, new Vector3(x, 0.01f, -0.03f), toggle);
            }
        }

        /// <summary>
        /// Советский ЭЛТ-терминал ввода приказов: выпуклый корпус, толстое стекло экрана
        /// Screen_Surface, механические кнопки по рамке и 7-сегментные индикаторы бюджета и ракет.
        /// </summary>
        public static GameObject CreateOrdersCrtTerminal(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();

            // Корпус средне-серый (#4A4F49): на фоне тёмного бетона монитор читается,
            // но и не превращается в белое пятно.
            Material iron = Mats.CrackedShell();
            Material glass = Mats.Lens(new Color(0.03f, 0.05f, 0.04f, 1f), 0.96f, 0f);
            Material screen = Mats.CityStatusScreen();

            MeshBuilder body = new MeshBuilder();

            // Корпус: единый массивный советский терминал. Спереди — толстая
            // фаскованная рамка, сзади — сужающийся кожух кинескопа с круглым
            // задником. Локальная лицевая сторона (+Z) смотрит на игрока.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.62f, 0.52f, 0.3f), 0.03f),
                MeshBuilder.Trs(new Vector3(0f, 0.26f, -0.02f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);

            // Рамка экрана: четыре бруска вокруг проёма, толстая и фаскованная.
            body.Add(MeshPrimitives.Frame(new Vector2(0.6f, 0.46f), 0.09f, 0.07f, 0.014f),
                MeshBuilder.Trs(new Vector3(0f, 0.29f, 0.14f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Кожух кинескора СЗАДИ: сужается к трубке и круглому заднику.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.5f, 0.42f, 0.18f), 0.025f),
                MeshBuilder.Trs(new Vector3(0f, 0.25f, -0.24f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.Prism(0.14f, 0.14f, 0.06f, 16, 0.01f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.25f, -0.36f), new Vector3(90f, 0f, 0f), Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Сплошная верхняя крышка. Раньше здесь стояла решётка вентиляционных
            // перекладин, отдельные балки висели в воздухе над монитором.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.62f, 0.04f, 0.32f), 0.01f),
                MeshBuilder.Trs(new Vector3(0f, 0.52f, -0.02f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Заклёпки по рамке.
            AddRivetRow(body, new Vector3(-0.26f, 0.07f, 0.181f), new Vector3(0.26f, 0.07f, 0.181f), 7, 0.008f,
                0.006f, steel, Vector3.forward);

            GameObject root = Seal(GeneratedObjectNames.OrdersCrtTerminal, parent, position, body);

            // Выпуклая поверхность экрана: приплюснутый купол вместо плоскости.
            // Купол повёрнут на 90° и растёт по +Z от z = 0.10, то есть его вершина
            // оказывается на z = 0.19.
            MeshBuilder screenBuilder = new MeshBuilder();
            screenBuilder.Add(MeshPrimitives.Dome(0.3f, 0.09f, 20, 8, true),
                MeshBuilder.Trs(new Vector3(0f, 0.31f, 0.1f), new Vector3(90f, 0f, 0f), Vector3.one), glass,
                MeshUvMode.BoxProject);

            // Зелёное свечение экрана «CITY STATUS». Плоскость выносится ПЕРЕД вершиной
            // купола (0.19), иначе непрозрачное стекло накрывает её и игрок видит
            // чёрный овал вместо радара. Именно Plane, а не AtlasQuad: CityStatusScreen —
            // самостоятельная текстура, а не ячейка атласа, Keep-проекция берёт UV 0..1.
            screenBuilder.Add(MeshPrimitives.Plane(0.36f, 0.26f),
                MeshBuilder.Trs(new Vector3(0f, 0.31f, 0.196f), Vector3.zero, Vector3.one), screen,
                MeshUvMode.Keep);
            Seal(GeneratedObjectNames.ScreenSurface, root.transform, Vector3.zero, screenBuilder);

            // Индикаторы: три цифры бюджета и три цифры склада ракет.
            // Это по-прежнему ячейки семисегментного атласа, поэтому им нужен свой материал.
            Material digitsMaterial = Mats.Segment(CrtGreenColor);
            MeshBuilder digits = new MeshBuilder();
            float startX = -0.19f;
            for (int i = 0; i < 3; i++)
            {
                digits.Add(AtlasQuad(0.05f, 0.08f, RetroPbrTextureGenerator.GetSevenSegmentCell(i == 0 ? 1 : 0)),
                    MeshBuilder.Trs(new Vector3(startX + i * 0.06f, 0.1f, 0.182f), Vector3.zero, Vector3.one),
                    digitsMaterial,
                    MeshUvMode.Keep);
            }

            for (int i = 0; i < 3; i++)
            {
                digits.Add(AtlasQuad(0.05f, 0.08f, RetroPbrTextureGenerator.GetSevenSegmentCell(i == 2 ? 9 : 0)),
                    MeshBuilder.Trs(new Vector3(0.05f + i * 0.06f, 0.1f, 0.182f), Vector3.zero, Vector3.one),
                    digitsMaterial,
                    MeshUvMode.Keep);
            }

            Seal("Terminal_Digits", root.transform, Vector3.zero, digits);

            // Механические кнопки по бокам рамки.
            MeshBuilder keys = new MeshBuilder();

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -0.265f : 0.265f;

                for (int k = 0; k < 3; k++)
                {
                    keys.Add(MeshPrimitives.BeveledBox(new Vector3(0.03f, 0.05f, 0.02f), 0.005f),
                        MeshBuilder.Trs(new Vector3(x, 0.16f + k * 0.1f, 0.17f), Vector3.zero, Vector3.one), steel,
                        MeshUvMode.BoxProject);
                }
            }

            Seal("Terminal_Keys", root.transform, Vector3.zero, keys);

            // Ножки.
            MeshBuilder feet = new MeshBuilder();

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.24f;
                float z = (i < 2 ? -1f : 1f) * 0.2f;
                feet.Add(MeshPrimitives.Prism(0.026f, 0.026f, 0.05f, 8, 0.006f, false),
                    MeshBuilder.Trs(new Vector3(x, 0.025f, z), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
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
        /// Ядерный пульт: стальная коробка в чёрно-жёлтую разметку, откидная крышка из оргстекла
        /// Safety_Cover_Pivot, замок с поворотным ключом Launch_Key_Pivot, большая красная кнопка
        /// «ПУСК» Red_Button_Plunger и три тумблера приказов справа.
/// <summary>
        /// Ядерный пульт: компактная стальная коробка в чёрно-жёлтую разметку, откидная
        /// крышка из оргстекла Safety_Cover_Pivot, поворотный ключ Launch_Key_Pivot,
        /// светящаяся красная кнопка «ПУСК» Red_Button_Plunger и три тумблера приказов.
        ///
        /// Геометрия намеренно компактная и БЕЗ разрывов по вертикали: корпус,
        /// рамка, кнопка и крышка идут вплотную друг к другу (0.00 → 0.12 → 0.13 → 0.165).
        /// Раньше корпус кончался на y = 0.42, а кнопка висела на y = 0.51 — между ними
        /// зияла пустота, и при масштабировании прибора пульт вырастал во весь экран.
        /// Габарит задаётся снизу вверх без зазоров, а общий размер прибора дополнительно
        /// принудительно вписывается в бокс вызовом BunkerWorldBuilder.FitPropToBox.
        /// </summary>
        public static GameObject CreateNuclearLaunchBox(Transform parent, Vector3 position)
        {
            Material steel = Mats.ColdSteel();
            Material hazard = Mats.Hazard();
            Material glass = Mats.SmokedGlass();
            Material red = Mats.Neon(AlertRedColor);

            const float width = 0.26f;
            const float depth = 0.26f;
            const float baseHeight = 0.10f;
            const float frameTop = 0.11f;
            const float buttonRadius = 0.055f;
            const float buttonHeight = 0.035f;

            MeshBuilder body = new MeshBuilder();

            // Нижний корпус 0.26 x 0.10 x 0.26 в чёрно-жёлтой диагональной разметке.
            // Раньше он был белым (обычный чугун), и пульт читался как коробка из-под молока.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(width, baseHeight, depth), 0.012f),
                MeshBuilder.Trs(new Vector3(0f, baseHeight * 0.5f, 0f), Vector3.zero, Vector3.one), hazard,
                MeshUvMode.BoxProject);

            // Верхняя стальная рамка КОЛЬЦОМ: кнопка «ПУСК» должна стоять посередине верхней
// грани корпуса (y = 0.10), а не на сплошной пластине.
            body.Add(MeshPrimitives.Ring(0.075f, width * 0.52f, 0.01f, 24),
                MeshBuilder.Trs(new Vector3(0f, baseHeight + 0.005f, 0f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Передняя (лицевая, +Z) планка под тумблеры приказов.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(width * 0.86f, 0.06f, 0.02f), 0.005f),
                MeshBuilder.Trs(new Vector3(0f, 0.035f, depth * 0.5f + 0.01f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Замок и поворотный ключ на передней стенке.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.06f, 0.06f, 0.02f), 0.005f),
                MeshBuilder.Trs(new Vector3(0f, 0.035f, depth * 0.5f + 0.01f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            AddRivetRow(body,
                new Vector3(-width * 0.5f + 0.03f, 0.012f, depth * 0.5f + 0.001f),
                new Vector3(width * 0.5f - 0.03f, 0.012f, depth * 0.5f + 0.001f),
                5, 0.005f, 0.003f, steel, Vector3.forward);

            GameObject root = Seal(GeneratedObjectNames.NuclearLaunchBox, parent, position, body);

            // Откидная крышка из оргстекла. Шарнир — на ЗАДНЕЙ кромке (локальный -Z),
            // то есть дальше от игрока, и открывается вверх-назад на 70°. Раньше шарнир
            // стоял спереди: крышка оказывалась между камерой и кнопкой и закрывала её.
            MeshBuilder cover = new MeshBuilder();
            cover.Add(MeshPrimitives.BeveledBox(new Vector3(width * 0.9f, 0.01f, depth * 0.86f), 0.003f),
                MeshBuilder.Trs(new Vector3(0f, 0f, depth * 0.43f), Vector3.zero, Vector3.one), glass,
                MeshUvMode.BoxProject);
            cover.Add(MeshPrimitives.BeveledBox(new Vector3(width * 0.9f, 0.012f, 0.012f), 0.003f),
                MeshBuilder.Trs(new Vector3(0f, 0f, depth * 0.86f), Vector3.zero, Vector3.one), steel,
                MeshUvMode.BoxProject);

            // Петли на задней грани пульта.
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.08f : 0.08f;
                cover.Add(MeshPrimitives.Prism(0.008f, 0.008f, 0.03f, 8, 0.002f, false),
                    MeshBuilder.Align(new Vector3(x, 0f, -0.01f), Vector3.left), steel, MeshUvMode.BoxProject);
            }

            Seal(GeneratedObjectNames.SafetyCoverPivot, root.transform,
                new Vector3(0f, frameTop, -depth * 0.46f), cover).transform.localRotation =
                Quaternion.Euler(-70f, 0f, 0f);

            // Поворотный ключ: стержень с двумя лопастями, торчит из замка.
            MeshBuilder key = new MeshBuilder();
            key.Add(MeshPrimitives.Prism(0.004f, 0.004f, 0.045f, 8, 0f, false),
                MeshBuilder.Trs(new Vector3(0f, 0f, 0.022f), new Vector3(90f, 0f, 0f), Vector3.one), steel,
                MeshUvMode.BoxProject);
            key.Add(MeshPrimitives.BeveledBox(new Vector3(0.016f, 0.004f, 0.016f), 0.001f),
                MeshBuilder.Trs(new Vector3(0f, 0f, 0.042f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
            key.Add(MeshPrimitives.BeveledBox(new Vector3(0.011f, 0.004f, 0.011f), 0.001f),
                MeshBuilder.Trs(new Vector3(0f, 0f, 0.034f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);

            Seal(GeneratedObjectNames.LaunchKeyPivot, root.transform,
                new Vector3(0f, 0.035f, depth * 0.5f + 0.03f), key);

            // Красная кнопка «ПУСК»: посередине верхней грани корпуса, ровно над центром.
            MeshBuilder plunger = new MeshBuilder();
            plunger.Add(MeshPrimitives.Prism(buttonRadius + 0.012f, buttonRadius + 0.012f, 0.012f, 16, 0.003f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.006f, 0f), Vector3.zero, Vector3.one), steel, MeshUvMode.BoxProject);
            plunger.Add(MeshPrimitives.Prism(buttonRadius, buttonRadius, buttonHeight, 16, 0.004f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.012f + buttonHeight * 0.5f, 0f), Vector3.zero, Vector3.one), red,
                MeshUvMode.BoxProject);
            plunger.Add(MeshPrimitives.Dome(buttonRadius, 0.012f, 16, 5, true),
                MeshBuilder.Trs(new Vector3(0f, 0.012f + buttonHeight, 0f), Vector3.zero, Vector3.one), red,
                MeshUvMode.BoxProject);

            Seal(GeneratedObjectNames.RedButtonPlunger, root.transform,
                new Vector3(0f, baseHeight, 0f), plunger);

            // Канонический алиас Nuke_Button — тот же меш и материал, чтобы биндер нашёл оба имени.
            GameObject alias = MakeNode(GeneratedObjectNames.NukeButton, root.transform,
                new Vector3(0f, baseHeight, 0f));
            alias.AddComponent<MeshFilter>().sharedMesh = plunger.Build("NukeButton_Mesh", false);
            alias.AddComponent<MeshRenderer>().sharedMaterials = new[] { steel, red };

            // Три металлических тумблера приказов СПЕРЕДИ, перед корпусом — ближе к камере,
            // чем кнопка и шарнир крышки.
            for (int i = 0; i < GeneratedObjectNames.OrderSwitchCount; i++)
            {
                float x = -0.07f + i * 0.07f;

                MeshBuilder toggle = new MeshBuilder();
                toggle.Add(MeshPrimitives.Prism(0.012f, 0.012f, 0.012f, 10, 0.002f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.006f, 0f), Vector3.zero, Vector3.one), steel,
                    MeshUvMode.BoxProject);
                toggle.Add(MeshPrimitives.Prism(0.004f, 0.004f, 0.038f, 8, 0.001f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.031f, 0f), new Vector3(i % 2 == 0 ? 32f : -32f, 0f, 0f),
                        Vector3.one), steel, MeshUvMode.BoxProject);
                toggle.Add(MeshPrimitives.Sphere(0.009f, 8, 6),
                    MeshBuilder.Trs(new Vector3(0f, 0.052f, 0.011f), Vector3.zero, Vector3.one),
                    Mats.Neon(i == 0 ? AlertRedColor : LampAmberColor), MeshUvMode.BoxProject);

                Seal(GeneratedObjectNames.OrderSwitch(i), root.transform,
                    new Vector3(x, 0.012f, depth * 0.5f + 0.02f), toggle);
            }

            return root;
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
