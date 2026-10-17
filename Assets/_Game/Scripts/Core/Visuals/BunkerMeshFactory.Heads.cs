// World Crisis | Слой: Core | Ответственный: OpenCode
// Шесть уникальных голов-масок диктаторов и их нижние челюсти Jaw_Pivot.
// Ветвление только по DictatorHeadStyle (тип из данных), не по CountryId:
// новая держава с уже существующей головой — правка только данных.
// Геометрия строится в локальных координатах узла: начало — в шарнире шеи (Head_Upper)
// и в шарнире челюсти (Jaw_Pivot), поэтому анимация не разрывает меш.

using UnityEngine;

namespace WorldCrisis
{
    public static partial class BunkerMeshFactory
    {
        /// <summary>Точка шарнира челюсти: чуть позади подбородка и ниже оси головы.</summary>
        private static readonly Vector3 JawPivotOffset = new Vector3(0f, -0.055f, -0.025f);

        /// <summary>Строит узел Head_Upper: верхняя часть головы-маски под текущий стиль.</summary>
        private static GameObject BuildHead(Transform parent, DictatorVisualLook look, Material skin, Material metal,
            Material glow)
        {
            MeshBuilder head = new MeshBuilder();

            switch (look.HeadStyle)
            {
                case DictatorHeadStyle.ScreenTv:
                    BuildTvHead(head, metal, glow);
                    break;

                case DictatorHeadStyle.GrotesqueGeneral:
                    BuildGrotesqueGeneralHead(head, look, skin, metal, glow);
                    break;

                case DictatorHeadStyle.FrostRespirator:
                    BuildFrostRespiratorHead(head, look, metal, glow);
                    break;

                case DictatorHeadStyle.GoldenMask:
                    BuildGoldenMaskHead(head, look, metal, glow);
                    break;

                case DictatorHeadStyle.RustedGasMask:
                    BuildRustedGasMaskHead(head, look, metal, glow);
                    break;

                case DictatorHeadStyle.ResortDoll:
                    BuildResortDollHead(head, look, skin, metal, glow);
                    break;

                default:
                    Debug.LogWarning("[WorldCrisis] Неизвестный стиль головы, взят базовый силуэт: " + look.HeadStyle);
                    BuildBasicHead(head, look, skin);
                    break;
            }

            GameObject node = Seal(GeneratedObjectNames.HeadUpper, parent, new Vector3(0f, HeadPivotY, 0f), head);
            node.transform.localScale = new Vector3(look.HeadScale, look.HeadScale, look.HeadScale);
            return node;
        }

        /// <summary>
        /// Строит узел Jaw_Pivot — нижнюю челюсть. Геометрия относительно шарнира,
        /// поэтому поворот узла по X на 0..25° открывает и закрывает рот без разрывов.
        /// </summary>
        private static void BuildJaw(Transform parent, DictatorVisualLook look, Material skin, Material metal,
            Material glow)
        {
            MeshBuilder jaw = new MeshBuilder();

            switch (look.HeadStyle)
            {
                case DictatorHeadStyle.ScreenTv:
                    // У ЭЛТ-головы челюсти нет: узел остаётся опорным шарниром, а амплитуду
                    // волны на экране качает Core через масштаб этого узла.
                    jaw.Add(MeshPrimitives.BeveledBox(new Vector3(0.05f, 0.02f, 0.05f), 0.006f),
                        Matrix4x4.identity, metal, MeshUvMode.BoxProject);
                    break;

                case DictatorHeadStyle.GrotesqueGeneral:
                    BuildGeneralJaw(jaw, look, skin);
                    break;

                case DictatorHeadStyle.FrostRespirator:
                    BuildFrostJaw(jaw, metal);
                    break;

                case DictatorHeadStyle.GoldenMask:
                    BuildGoldenBeardJaw(jaw, metal);
                    break;

                case DictatorHeadStyle.RustedGasMask:
                    BuildMechanicalJaw(jaw, metal, glow);
                    break;

                case DictatorHeadStyle.ResortDoll:
                    BuildDollJaw(jaw, look, skin);
                    break;

                default:
                    jaw.Add(MeshPrimitives.BeveledBox(new Vector3(0.09f, 0.045f, 0.09f), 0.012f),
                        Matrix4x4.identity, skin, MeshUvMode.BoxProject);
                    break;
            }

            Seal(GeneratedObjectNames.JawPivot, parent, JawPivotOffset, jaw);
        }

        #region Базовая голова (запасной вариант)

        private static void BuildBasicHead(MeshBuilder head, DictatorVisualLook look, Material skin)
        {
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.17f, 0.16f, 0.19f), 0.03f),
                MeshBuilder.Trs(new Vector3(0f, 0.08f, 0f), Vector3.zero, Vector3.one), skin, MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.035f, 0.05f, 0.045f), 0.008f),
                MeshBuilder.Trs(new Vector3(0f, 0.05f, 0.1f), Vector3.zero, Vector3.one), skin, MeshUvMode.BoxProject);
        }

        #endregion

        #region 1. Нео-Кремний — ЭЛТ-монитор вместо головы

        /// <summary>
        /// Старый бежево-серый телевизор 1980-х: утопленный кинескоп со светящимся зелёным
        /// пиксельным лицом и линией осциллографа, боковые тумблеры настройки,
        /// V-образные антенны-рожки и пучок кабелей на шее.
        /// </summary>
        private static void BuildTvHead(MeshBuilder head, Material metal, Material glow)
        {
            Material shell = Mats.Lens(new Color(0.72f, 0.68f, 0.57f), 0.48f, 0.08f);
            Material glass = Mats.Lens(new Color(0.015f, 0.035f, 0.025f, 1f), 0.96f, 0f);

            // Корпус телевизора.
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.42f, 0.36f, 0.38f), 0.035f),
                MeshBuilder.Trs(new Vector3(0f, 0.12f, -0.01f), Vector3.zero, Vector3.one), shell, MeshUvMode.BoxProject);

            // Рамка вокруг кинескопа: четыре бруска вместо вырезанного окна.
            head.Add(MeshPrimitives.Frame(new Vector2(0.44f, 0.38f), 0.07f, 0.06f, 0.012f),
                MeshBuilder.Trs(new Vector3(0f, 0.13f, 0.17f), Vector3.zero, Vector3.one), shell,
                MeshUvMode.BoxProject);

            // Утопленный кинескоп и пиксельное лицо на экране.
            head.Add(MeshPrimitives.Dome(0.21f, 0.08f, 18, 7, true),
                MeshBuilder.Trs(new Vector3(-0.03f, 0.13f, 0.06f), new Vector3(-90f, 0f, 0f), Vector3.one), glass,
                MeshUvMode.BoxProject);
            head.Add(AtlasQuad(0.3f, 0.26f, new Rect(0f, 0f, 1f, 1f)),
                MeshBuilder.Trs(new Vector3(-0.03f, 0.13f, 0.146f), Vector3.zero, Vector3.one), Mats.Scope(),
                MeshUvMode.Keep);

            // Зелёный ореол вокруг экрана.
            head.Add(MeshPrimitives.Torus(0.17f, 0.007f, 22, 5),
                MeshBuilder.Trs(new Vector3(-0.03f, 0.13f, 0.175f), new Vector3(90f, 0f, 0f),
                    new Vector3(1f, 1.28f, 1f)), glow, MeshUvMode.BoxProject);

            // Боковые тумблеры настройки.
            for (int i = 0; i < 3; i++)
            {
                float y = 0.06f + i * 0.07f;
                head.Add(MeshPrimitives.Prism(0.015f, 0.015f, 0.014f, 8, 0.003f, false),
                    MeshBuilder.Trs(new Vector3(0.16f, y, 0.19f), new Vector3(90f, 0f, 0f), Vector3.one), metal,
                    MeshUvMode.BoxProject);
                head.Add(MeshPrimitives.Prism(0.005f, 0.005f, 0.028f, 6, 0f, false),
                    MeshBuilder.Trs(new Vector3(0.16f, y + 0.015f, 0.198f),
                        new Vector3(i == 0 ? 22f : -22f, 0f, 0f), Vector3.one), metal, MeshUvMode.BoxProject);
            }

            // Решётка динамика под экраном.
            for (int i = 0; i < 6; i++)
            {
                head.Add(MeshPrimitives.BeveledBox(new Vector3(0.24f, 0.008f, 0.012f), 0.002f),
                    MeshBuilder.Trs(new Vector3(-0.03f, -0.035f - i * 0.016f, 0.19f), Vector3.zero, Vector3.one),
                    metal, MeshUvMode.BoxProject);
            }

            // V-образные антенны-рожки.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                head.Add(MeshPrimitives.Prism(0.006f, 0.004f, 0.28f, 6, 0f, false),
                    MeshBuilder.Trs(new Vector3(side * 0.09f, 0.42f, -0.06f),
                        new Vector3(0f, 0f, side * -24f), Vector3.one), metal, MeshUvMode.BoxProject);
                head.Add(MeshPrimitives.Sphere(0.013f, 8, 6),
                    MeshBuilder.Trs(new Vector3(side * 0.17f, 0.55f, -0.06f), Vector3.zero, Vector3.one), metal,
                    MeshUvMode.BoxProject);
            }

            // Пучок кабелей, свисающих на шею.
            for (int i = 0; i < 5; i++)
            {
                float offset = (i - 2) * 0.024f;
                Vector3[] path =
                {
                    new Vector3(offset, -0.05f, -0.12f),
                    new Vector3(offset * 1.7f, -0.14f, -0.19f),
                    new Vector3(offset * 2.3f, -0.24f, -0.16f),
                    new Vector3(offset * 2.5f, -0.34f, -0.07f)
                };

                head.Add(MeshPrimitives.Tube(path, 0.006f, 5), Matrix4x4.identity, metal, MeshUvMode.BoxProject);
            }

            // Ножка подставки телевизора.
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.24f, 0.03f, 0.16f), 0.008f),
                MeshBuilder.Trs(new Vector3(0f, -0.05f, -0.02f), Vector3.zero, Vector3.one), metal, MeshUvMode.BoxProject);
        }

        #endregion

        #region 2. Гранд-Коста — гротескный генерал в высокой фуражке

        /// <summary>
        /// Массивная карикатурная голова: квадратная челюсть, густые чёрные усы,
        /// зеркальные очка-авиаторы, невероятно высокая фуражка с золотым орлом и кокардой.
        /// </summary>
        private static void BuildGrotesqueGeneralHead(MeshBuilder head, DictatorVisualLook look, Material skin,
            Material metal, Material glow)
        {
            Material moustache = Mats.Cloth(new Color(0.05f, 0.04f, 0.03f));
            Material capFelt = Mats.Cloth(new Color(0.16f, 0.2f, 0.12f));
            Material gold = Mats.Gold();

            // Череп: широкий гранёный брус.
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.23f, 0.2f, 0.22f), 0.035f),
                MeshBuilder.Trs(new Vector3(0f, 0.1f, 0f), Vector3.zero, Vector3.one), skin, MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.21f, 0.07f, 0.2f), 0.025f),
                MeshBuilder.Trs(new Vector3(0f, 0.21f, -0.01f), Vector3.zero, Vector3.one), skin, MeshUvMode.BoxProject);

            // Нос-кувалда.
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.055f, 0.06f, 0.05f), 0.012f),
                MeshBuilder.Trs(new Vector3(0f, 0.08f, 0.11f), Vector3.zero, Vector3.one), skin, MeshUvMode.BoxProject);

            // Зеркальные очки-авиаторы: обод, две линзы, дужка.
            head.Add(MeshPrimitives.Torus(0.145f, 0.008f, 18, 5),
                MeshBuilder.Trs(new Vector3(0f, 0.135f, 0.02f), new Vector3(74f, 0f, 0f), new Vector3(1f, 0.75f, 1f)),
                gold, MeshUvMode.BoxProject);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                head.Add(MeshPrimitives.Sphere(0.042f, 12, 8),
                    MeshBuilder.Trs(new Vector3(side * 0.068f, 0.135f, 0.095f), Vector3.zero,
                        new Vector3(1f, 0.62f, 0.35f)), Mats.Lens(new Color(0.72f, 0.8f, 0.86f, 1f), 0.98f, 1f),
                    MeshUvMode.BoxProject);
                head.Add(MeshPrimitives.Torus(0.045f, 0.007f, 12, 5),
                    MeshBuilder.Trs(new Vector3(side * 0.068f, 0.135f, 0.096f), Vector3.zero,
                        new Vector3(1f, 0.62f, 1f)), gold, MeshUvMode.BoxProject);
                head.Add(MeshPrimitives.Prism(0.006f, 0.006f, 0.09f, 5, 0f, false),
                    MeshBuilder.Align(new Vector3(side * 0.115f, 0.135f, 0.02f), new Vector3(side, 0f, -0.5f)),
                    gold, MeshUvMode.BoxProject);
            }

            // Густые усы: несколько сплюснутых эллипсоидов поперёк лица.
            for (int i = 0; i < 5; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float t = i / 4f;
                head.Add(MeshPrimitives.Sphere(0.028f, 8, 6),
                    MeshBuilder.Trs(new Vector3(side * (0.02f + t * 0.09f), 0.075f - t * 0.012f, 0.105f),
                        Vector3.zero, new Vector3(1.5f, 0.55f, 0.7f)), moustache, MeshUvMode.BoxProject);
            }

            // Высокая военная фуражка: тулья, кокарда, золотой орел.
            head.Add(MeshPrimitives.Prism(0.13f, 0.11f, 0.3f, 14, 0.012f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.38f, 0f), Vector3.zero, Vector3.one), capFelt, MeshUvMode.BoxProject);

            // Кокарда — широкая круглая нашивка.
            head.Add(MeshPrimitives.Prism(0.135f, 0.135f, 0.02f, 16, 0.004f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.25f, 0f), Vector3.zero, Vector3.one), gold, MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.Prism(0.1f, 0.1f, 0.026f, 16, 0.004f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.251f, 0f), Vector3.zero, Vector3.one),
                Mats.Cloth(new Color(0.5f, 0.12f, 0.14f)), MeshUvMode.BoxProject);

            // Золотой орел на кокарде: распластанные крылья и туловище.
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.02f, 0.05f, 0.012f), 0.003f),
                MeshBuilder.Trs(new Vector3(0f, 0.252f, 0.088f), Vector3.zero, Vector3.one), gold, MeshUvMode.BoxProject);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                head.Add(MeshPrimitives.BeveledBox(new Vector3(0.09f, 0.022f, 0.01f), 0.003f),
                    MeshBuilder.Trs(new Vector3(side * 0.05f, 0.254f, 0.086f), new Vector3(0f, side * -14f, side * 8f),
                        Vector3.one), gold, MeshUvMode.BoxProject);
            }

            // Козырёк фуражки.
            head.Add(MeshPrimitives.Wedge(new Vector3(0.22f, 0.02f, 0.11f)),
                MeshBuilder.Trs(new Vector3(0f, 0.245f, 0.1f), new Vector3(-12f, 0f, 0f), Vector3.one), capFelt,
                MeshUvMode.BoxProject);

            // Дымящая сигара в углу рта.
            Vector3 cigarTip = new Vector3(0.085f, 0.05f, 0.1f);
            head.Add(MeshPrimitives.Prism(0.009f, 0.008f, 0.1f, 8, 0f, false),
                MeshBuilder.Align(cigarTip, new Vector3(0.7f, 0.15f, 0.7f)),
                Mats.Cloth(new Color(0.45f, 0.28f, 0.12f)), MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.Sphere(0.009f, 6, 5),
                MeshBuilder.Trs(cigarTip + new Vector3(0.05f, 0.008f, 0.05f), Vector3.zero, Vector3.one), glow,
                MeshUvMode.BoxProject);

            // Столбики дыма от кончика сигары.
            Vector3 smokeStart = cigarTip + new Vector3(0.06f, 0.01f, 0.06f);
            Vector3[] smokePath = new Vector3[6];
            for (int i = 0; i < smokePath.Length; i++)
            {
                float t = i / (float)(smokePath.Length - 1);
                smokePath[i] = smokeStart + new Vector3(t * 0.06f, t * 0.16f, t * 0.05f)
                                          + new Vector3(Mathf.Sin(t * 7f) * 0.012f, 0f, Mathf.Cos(t * 6f) * 0.012f);
            }

            head.Add(MeshPrimitives.Tube(smokePath, 0.012f, 6), Matrix4x4.identity,
                Mats.Lens(new Color(0.6f, 0.6f, 0.62f, 0.35f), 0.2f, 0f), MeshUvMode.BoxProject);
        }

        /// <summary>Массивная квадратная челюсть генерала.</summary>
        private static void BuildGeneralJaw(MeshBuilder jaw, DictatorVisualLook look, Material skin)
        {
            jaw.Add(MeshPrimitives.BeveledBox(new Vector3(0.2f, 0.09f, 0.18f), 0.025f),
                MeshBuilder.Trs(new Vector3(0f, -0.03f, 0.015f), Vector3.zero, Vector3.one), skin, MeshUvMode.BoxProject);

            // Зубы вдоль верхней кромки — «скрежет» генерала.
            for (int i = 0; i < 6; i++)
            {
                jaw.Add(MeshPrimitives.BeveledBox(new Vector3(0.022f, 0.022f, 0.016f), 0.003f),
                    MeshBuilder.Trs(new Vector3(-0.075f + i * 0.03f, 0.015f, 0.05f), Vector3.zero, Vector3.one),
                    Mats.Lens(new Color(0.9f, 0.88f, 0.78f), 0.6f, 0f), MeshUvMode.BoxProject);
            }
        }

        #endregion

        #region 3. Вечная Мерзлота — меховая ушанка и чугунный респиратор

        /// <summary>
        /// Клёпаная чугунная маска-респиратор с двумя круглыми светящимися янтарными линзами,
        /// массивная меховая шапка-ушанка с железной звездой.
        /// </summary>
        private static void BuildFrostRespiratorHead(MeshBuilder head, DictatorVisualLook look, Material metal,
            Material glow)
        {
            Material iron = Mats.CastIron();
            Material fur = Mats.Cloth(new Color(0.34f, 0.3f, 0.26f));
            Material ice = Mats.Frost();

            // Чугунная маска: рамка-корпус вокруг проёма для лица плюс затылочный блок.
            head.Add(MeshPrimitives.Frame(new Vector2(0.2f, 0.19f), 0.05f, 0.19f, 0.025f),
                MeshBuilder.Trs(new Vector3(0f, 0.1f, 0f), Vector3.zero, Vector3.one), iron, MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.2f, 0.19f, 0.08f), 0.025f),
                MeshBuilder.Trs(new Vector3(0f, 0.1f, -0.055f), Vector3.zero, Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Шапка-ушанка: сфера + меховая опушка и уши.
            head.Add(MeshPrimitives.Sphere(0.115f, 14, 10),
                MeshBuilder.Trs(new Vector3(0f, 0.26f, -0.01f), Vector3.zero, new Vector3(1f, 0.85f, 1f)), fur,
                MeshUvMode.BoxProject);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                head.Add(MeshPrimitives.Sphere(0.055f, 10, 8),
                    MeshBuilder.Trs(new Vector3(side * 0.115f, 0.22f, -0.02f), Vector3.zero, Vector3.one), fur,
                    MeshUvMode.BoxProject);
                head.Add(MeshPrimitives.Sphere(0.038f, 8, 6),
                    MeshBuilder.Trs(new Vector3(side * 0.115f, 0.13f, -0.02f), Vector3.zero, Vector3.one), ice,
                    MeshUvMode.BoxProject);
            }

            // Железная звезда на лбу.
            for (int i = 0; i < 5; i++)
            {
                float angle = 90f + i * 72f;
                Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                head.Add(MeshPrimitives.BeveledBox(new Vector3(0.018f, 0.06f, 0.014f), 0.003f),
                    MeshBuilder.Trs(new Vector3(0f, 0.21f, 0.085f) + direction * 0.032f,
                        new Vector3(0f, 0f, angle), Vector3.one), metal, MeshUvMode.BoxProject);
            }

            // Круглые янтарные линзы в гнёздах.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 lensPosition = new Vector3(side * 0.058f, 0.12f, 0.082f);

                head.Add(MeshPrimitives.Prism(0.042f, 0.038f, 0.03f, 14, 0.005f, false),
                    MeshBuilder.Align(lensPosition, Vector3.forward), metal, MeshUvMode.BoxProject);
                head.Add(MeshPrimitives.Dome(0.036f, 0.016f, 14, 5, true),
                    MeshBuilder.Align(lensPosition + Vector3.forward * 0.016f, Vector3.forward), glow,
                    MeshUvMode.BoxProject);

                // Заклёпки по ободу линзы.
                for (int r = 0; r < 6; r++)
                {
                    float angle = r * 60f * Mathf.Deg2Rad;
                    Vector3 offset = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg) * new Vector3(0.047f, 0f, 0f);
                    head.Add(MeshPrimitives.RivetDome(0.006f, 0.005f),
                        MeshBuilder.Align(lensPosition + offset + Vector3.forward * 0.012f, Vector3.forward), metal,
                        MeshUvMode.BoxProject);
                }
            }

            // Холодный фильтр-грибо́к под маской.
            head.Add(MeshPrimitives.Prism(0.045f, 0.03f, 0.035f, 10, 0.006f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.035f, 0.07f), new Vector3(90f, 0f, 0f), Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Наледь на маске.
            for (int i = 0; i < 7; i++)
            {
                float angle = i * 51f * Mathf.Deg2Rad;
                Vector3 point = new Vector3(Mathf.Cos(angle) * 0.1f, 0.16f + Mathf.Sin(angle) * 0.05f,
                    Mathf.Sin(angle) * 0.06f);
                head.Add(MeshPrimitives.Sphere(0.012f, 6, 5),
                    MeshBuilder.Trs(point, Vector3.zero, new Vector3(1f, 1.6f, 1f)), ice, MeshUvMode.BoxProject);
            }
        }

        /// <summary>Тяжёлая шарнирная железная челюсть-ковш.</summary>
        private static void BuildFrostJaw(MeshBuilder jaw, Material metal)
        {
            Material iron = Mats.CastIron();

            // Ковш: усечённый конус, раскрытый вниз.
            jaw.Add(MeshPrimitives.Prism(0.045f, 0.085f, 0.07f, 10, 0.008f, false),
                MeshBuilder.Trs(new Vector3(0f, -0.035f, 0.02f), new Vector3(180f, 0f, 0f), Vector3.one), iron,
                MeshUvMode.BoxProject);

            // Рёбра жёсткости и шарнирные пальцы.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                jaw.Add(MeshPrimitives.Prism(0.012f, 0.012f, 0.03f, 8, 0.003f, false),
                    MeshBuilder.Align(new Vector3(side * 0.075f, 0f, -0.02f), Vector3.right), metal, MeshUvMode.BoxProject);
            }

            jaw.Add(MeshPrimitives.Prism(0.014f, 0.014f, 0.16f, 8, 0.004f, false),
                MeshBuilder.Align(new Vector3(0f, 0.005f, -0.02f), Vector3.right), metal, MeshUvMode.BoxProject);
        }

        #endregion

        #region 4. Золотой Халифат Дюн — чеканная золотая маска

        /// <summary>
        /// Лицо закрыто чеканной маской из полированного золота с пустыми тёмными прорезями глаз,
        /// на маске держится белый платок-гутра золотым обручем в виде нефтяной трубы.
        /// </summary>
        private static void BuildGoldenMaskHead(MeshBuilder head, DictatorVisualLook look, Material metal,
            Material glow)
        {
            Material gold = Mats.Gold();
            Material cloth = Mats.Cloth(new Color(0.88f, 0.86f, 0.8f));
            Material dark = Mats.Lens(new Color(0.03f, 0.025f, 0.02f, 1f), 0.8f, 0f);

            // Чеканная маска: сплюснутая полусфера с плоским лицом.
            head.Add(MeshPrimitives.Sphere(0.115f, 16, 12),
                MeshBuilder.Trs(new Vector3(0f, 0.1f, -0.005f), Vector3.zero, new Vector3(1.05f, 1.25f, 1.1f)),
                gold, MeshUvMode.BoxProject);

            // Лицевая пластина маски: рамка вокруг прорезей глаз плюс затылок.
            head.Add(MeshPrimitives.Frame(new Vector2(0.19f, 0.19f), 0.055f, 0.04f, 0.012f),
                MeshBuilder.Trs(new Vector3(0f, 0.1f, 0.095f), Vector3.zero, Vector3.one), gold,
                MeshUvMode.BoxProject);

            // Прорези глаз — глубокие тёмные прорези, а не глаза.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                head.Add(MeshPrimitives.BeveledBox(new Vector3(0.045f, 0.016f, 0.012f), 0.003f),
                    MeshBuilder.Trs(new Vector3(side * 0.048f, 0.13f, 0.108f), new Vector3(0f, 0f, side * -10f),
                        Vector3.one), dark, MeshUvMode.BoxProject);
            }

            // Нос-грань маски.
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.022f, 0.06f, 0.03f), 0.006f),
                MeshBuilder.Trs(new Vector3(0f, 0.09f, 0.115f), Vector3.zero, Vector3.one), gold, MeshUvMode.BoxProject);

            // Чеканные завитки по краю маски.
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float row = i < 2 ? 0.17f : 0.04f;
                head.Add(MeshPrimitives.Torus(0.022f, 0.005f, 12, 5),
                    MeshBuilder.Trs(new Vector3(side * 0.075f, row, 0.108f), new Vector3(0f, 0f, 0f), Vector3.one),
                    gold, MeshUvMode.BoxProject);
            }

            // Платок-гутра: белая ткань, собранная наверх.
            head.Add(MeshPrimitives.Sphere(0.12f, 14, 10),
                MeshBuilder.Trs(new Vector3(0f, 0.29f, -0.015f), Vector3.zero, new Vector3(1.05f, 0.9f, 1.05f)),
                cloth, MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.Prism(0.05f, 0.035f, 0.09f, 12, 0.01f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.4f, 0.01f), new Vector3(-14f, 0f, 0f), Vector3.one), cloth,
                MeshUvMode.BoxProject);

            // Золотой обруч в форме нефтяной трубы с фланцами.
            head.Add(MeshPrimitives.Prism(0.135f, 0.135f, 0.022f, 18, 0.004f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.27f, -0.015f), Vector3.zero, Vector3.one), gold, MeshUvMode.BoxProject);

            for (int i = 0; i < 2; i++)
            {
                head.Add(MeshPrimitives.Prism(0.14f, 0.14f, 0.012f, 18, 0.003f, false),
                    MeshBuilder.Trs(new Vector3(0f, 0.245f + i * 0.05f, -0.015f), Vector3.zero, Vector3.one), gold,
                    MeshUvMode.BoxProject);
            }

            // Заклёпки по обручу.
            for (int i = 0; i < 8; i++)
            {
                float angle = Mathf.PI * 2f * i / 8f;
                head.Add(MeshPrimitives.RivetDome(0.008f, 0.006f),
                    MeshBuilder.Align(new Vector3(Mathf.Cos(angle) * 0.138f, 0.27f,
                        Mathf.Sin(angle) * 0.138f - 0.015f), new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))),
                    gold, MeshUvMode.BoxProject);
            }
        }

        /// <summary>Отдельная геометрическая золотая челюсть-борода.</summary>
        private static void BuildGoldenBeardJaw(MeshBuilder jaw, Material metal)
        {
            Material gold = Mats.Gold();

            // Борода: ступенчатый конус с гранёными ступенями.
            jaw.Add(MeshPrimitives.Prism(0.075f, 0.085f, 0.05f, 12, 0.008f, false),
                MeshBuilder.Trs(new Vector3(0f, -0.02f, 0.005f), Vector3.zero, Vector3.one), gold, MeshUvMode.BoxProject);
            jaw.Add(MeshPrimitives.Prism(0.085f, 0.05f, 0.06f, 10, 0.006f, false),
                MeshBuilder.Trs(new Vector3(0f, -0.075f, 0.0f), Vector3.zero, Vector3.one), gold, MeshUvMode.BoxProject);

            // Лучки бороды — расходящиеся клинья.
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f - 0.5f;
                jaw.Add(MeshPrimitives.Prism(0.016f, 0.006f, 0.1f, 6, 0f, false),
                    MeshBuilder.Trs(new Vector3(t * 0.09f, -0.06f, 0.01f),
                        new Vector3(0f, 0f, t * 26f), Vector3.one), gold, MeshUvMode.BoxProject);
            }
        }

        #endregion

        #region 5. Союз Ржавых Шестеренок — труба и противогаз

        /// <summary>
        /// Высокий ржавый металлический цилиндр-труба вместо головы (из верхушки идёт чёрный дым),
        /// латунно-чугунный противогаз с манометром во лбу и асимметричными окулярами.
        /// </summary>
        private static void BuildRustedGasMaskHead(MeshBuilder head, DictatorVisualLook look, Material metal,
            Material glow)
        {
            Material rust = Mats.Rust();
            Material brass = Mats.Lens(new Color(0.62f, 0.46f, 0.22f), 0.7f, 0.9f);
            Material smoke = Mats.Lens(new Color(0.06f, 0.05f, 0.05f, 0.5f), 0.2f, 0f);

            // Труба-голова: высокий цилиндр с гофрой и ржавчиной.
            head.Add(MeshPrimitives.Prism(0.115f, 0.1f, 0.52f, 14, 0.014f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.42f, -0.02f), Vector3.zero, Vector3.one), rust, MeshUvMode.BoxProject);

            for (int i = 0; i < 4; i++)
            {
                head.Add(MeshPrimitives.Torus(0.108f - i * 0.004f, 0.014f, 14, 6),
                    MeshBuilder.Trs(new Vector3(0f, 0.24f + i * 0.12f, -0.02f), Vector3.zero, Vector3.one), metal,
                    MeshUvMode.BoxProject);
            }

            // Выхлопная заслонка на верхушке трубы.
            head.Add(MeshPrimitives.Prism(0.085f, 0.085f, 0.02f, 14, 0.004f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.69f, -0.02f), Vector3.zero, Vector3.one), metal, MeshUvMode.BoxProject);

            // Чёрный дым из трубы: расширяющийся конус дыма.
            Vector3[] smokePath = new Vector3[7];
            for (int i = 0; i < smokePath.Length; i++)
            {
                float t = i / (float)(smokePath.Length - 1);
                smokePath[i] = new Vector3(
                    Mathf.Sin(t * 5.5f) * 0.035f,
                    0.7f + t * 0.34f,
                    -0.02f + Mathf.Cos(t * 4.5f) * 0.03f);
            }

            head.Add(MeshPrimitives.Tube(smokePath, 0.055f, 8), Matrix4x4.identity, smoke, MeshUvMode.BoxProject);

            // Противогаз на нижней части трубы: рамка-корпус с проёмом для лица.
            head.Add(MeshPrimitives.Frame(new Vector2(0.21f, 0.19f), 0.055f, 0.18f, 0.025f),
                MeshBuilder.Trs(new Vector3(0f, 0.11f, 0.01f), Vector3.zero, Vector3.one), rust,
                MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.21f, 0.19f, 0.07f), 0.025f),
                MeshBuilder.Trs(new Vector3(0f, 0.11f, -0.055f), Vector3.zero, Vector3.one), rust,
                MeshUvMode.BoxProject);

            // Гофрированный фильтр.
            head.Add(MeshPrimitives.Prism(0.05f, 0.055f, 0.09f, 12, 0.008f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.02f, 0.085f), new Vector3(84f, 0f, 0f), Vector3.one), metal,
                MeshUvMode.BoxProject);

            for (int i = 0; i < 5; i++)
            {
                head.Add(MeshPrimitives.Torus(0.054f, 0.007f, 12, 5),
                    MeshBuilder.Trs(new Vector3(0f, 0.05f - i * 0.018f, 0.08f + i * 0.004f),
                        new Vector3(84f, 0f, 0f), Vector3.one), metal, MeshUvMode.BoxProject);
            }

            // Манометр во лбу: латунный круг с бегунком и стеклом.
            Vector3 gaugePosition = new Vector3(0f, 0.2f, 0.075f);
            head.Add(MeshPrimitives.Prism(0.038f, 0.038f, 0.018f, 14, 0.004f, false),
                MeshBuilder.Align(gaugePosition, Vector3.forward), brass, MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.Dome(0.032f, 0.01f, 14, 5, true),
                MeshBuilder.Align(gaugePosition + Vector3.forward * 0.01f, Vector3.forward),
                Mats.Lens(new Color(0.85f, 0.85f, 0.8f, 1f), 0.95f, 0f), MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.004f, 0.026f, 0.004f), 0.001f),
                MeshBuilder.Trs(gaugePosition + new Vector3(0.006f, 0.008f, 0.013f), new Vector3(0f, 0f, 32f),
                    Vector3.one), glow, MeshUvMode.BoxProject);

            // Асимметричные окуляры: левый круглый, правый прямоугольный.
            head.Add(MeshPrimitives.Prism(0.042f, 0.038f, 0.028f, 14, 0.005f, false),
                MeshBuilder.Align(new Vector3(-0.06f, 0.13f, 0.075f), Vector3.forward), brass, MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.Dome(0.036f, 0.014f, 14, 5, true),
                MeshBuilder.Align(new Vector3(-0.06f, 0.13f, 0.09f), Vector3.forward), glow, MeshUvMode.BoxProject);

            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.09f, 0.05f, 0.03f), 0.008f),
                MeshBuilder.Trs(new Vector3(0.065f, 0.13f, 0.085f), Vector3.zero, Vector3.one), brass,
                MeshUvMode.BoxProject);
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.07f, 0.032f, 0.012f), 0.003f),
                MeshBuilder.Trs(new Vector3(0.065f, 0.13f, 0.1f), Vector3.zero, Vector3.one), glow, MeshUvMode.BoxProject);

            // Заклёпки по маске.
            AddRivetRow(head, new Vector3(-0.085f, 0.06f, 0.055f), new Vector3(0.085f, 0.06f, 0.055f), 5, 0.007f,
                0.005f, metal, Vector3.forward);
        }

        /// <summary>Тяжёлая механическая челюсть барона — литая деталь с зубами и шарниром.</summary>
        private static void BuildMechanicalJaw(MeshBuilder jaw, Material metal, Material glow)
        {
            Material rust = Mats.Rust();

            jaw.Add(MeshPrimitives.BeveledBox(new Vector3(0.17f, 0.06f, 0.15f), 0.018f),
                MeshBuilder.Trs(new Vector3(0f, -0.025f, 0.015f), Vector3.zero, Vector3.one), rust, MeshUvMode.BoxProject);

            // Механические зубы — небольшие бойки вдоль кромки.
            for (int i = 0; i < 7; i++)
            {
                jaw.Add(MeshPrimitives.BeveledBox(new Vector3(0.018f, 0.024f, 0.014f), 0.002f),
                    MeshBuilder.Trs(new Vector3(-0.06f + i * 0.02f, 0.015f, 0.055f), Vector3.zero, Vector3.one), metal,
                    MeshUvMode.BoxProject);
            }

            // Шарнирные пальцы и тяга.
            jaw.Add(MeshPrimitives.Prism(0.013f, 0.013f, 0.15f, 8, 0.004f, false),
                MeshBuilder.Align(new Vector3(0f, 0f, -0.015f), Vector3.right), metal, MeshUvMode.BoxProject);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                jaw.Add(MeshPrimitives.Prism(0.02f, 0.02f, 0.025f, 8, 0.004f, false),
                    MeshBuilder.Align(new Vector3(side * 0.085f, 0f, -0.015f), Vector3.right), metal,
                    MeshUvMode.BoxProject);
                jaw.Add(MeshPrimitives.BeveledBox(new Vector3(0.01f, 0.04f, 0.01f), 0.002f),
                    MeshBuilder.Trs(new Vector3(side * 0.04f, -0.03f, -0.03f), new Vector3(0f, 0f, side * 12f),
                        Vector3.one), glow, MeshUvMode.BoxProject);
            }
        }

        #endregion

        #region 6. Атолл Свободных Курортов — пластиковый манекен курорта

        /// <summary>
        /// Глянцевая загорелая пластиковая маска манекена с широкой улыбкой,
        /// неоновые пляжные очки-жалюзи и цветочный венок на шее.
        /// </summary>
        private static void BuildResortDollHead(MeshBuilder head, DictatorVisualLook look, Material skin,
            Material metal, Material glow)
        {
            Material plastic = Mats.PlasticSkin();
            Material neon = Mats.Neon(new Color(0.25f, 0.95f, 0.85f));
            Material teethMaterial = Mats.Lens(new Color(0.95f, 0.95f, 0.92f), 0.7f, 0f);
            Material flowerA = Mats.Cloth(new Color(0.95f, 0.25f, 0.4f));
            Material flowerB = Mats.Cloth(new Color(0.98f, 0.8f, 0.2f));

            // Глянцевая голова манекена: вытянутый эллипсоид.
            head.Add(MeshPrimitives.Sphere(0.11f, 16, 12),
                MeshBuilder.Trs(new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(1f, 1.3f, 1.08f)), plastic,
                MeshUvMode.BoxProject);

            // Затылок-«парик» из гладкого пластика.
            head.Add(MeshPrimitives.Sphere(0.108f, 14, 10),
                MeshBuilder.Trs(new Vector3(0f, 0.11f, -0.03f), Vector3.zero, new Vector3(1f, 1.28f, 1f)),
                Mats.PlasticSkin(), MeshUvMode.BoxProject);

            // Нос.
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.026f, 0.05f, 0.03f), 0.008f),
                MeshBuilder.Trs(new Vector3(0f, 0.095f, 0.1f), Vector3.zero, Vector3.one), plastic, MeshUvMode.BoxProject);

            // Широкая белозубая улыбка: дуга зубов + тёмная полость рта.
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.13f, 0.028f, 0.02f), 0.006f),
                MeshBuilder.Trs(new Vector3(0f, 0.045f, 0.095f), Vector3.zero, Vector3.one),
                Mats.Lens(new Color(0.25f, 0.08f, 0.1f, 1f), 0.6f, 0f), MeshUvMode.BoxProject);

            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f - 0.5f;
                head.Add(MeshPrimitives.BeveledBox(new Vector3(0.015f, 0.022f, 0.012f), 0.003f),
                    MeshBuilder.Trs(new Vector3(t * 0.12f, 0.052f, 0.102f), new Vector3(0f, 0f, t * 8f), Vector3.one),
                    teethMaterial, MeshUvMode.BoxProject);
            }

            // Неоновые пляжные очки-жалюзи: рамка-солнцезащитный козырёк.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                head.Add(MeshPrimitives.BeveledBox(new Vector3(0.085f, 0.05f, 0.012f), 0.004f),
                    MeshBuilder.Trs(new Vector3(side * 0.052f, 0.125f, 0.085f),
                        new Vector3(0f, side * -10f, side * 6f), Vector3.one), neon, MeshUvMode.BoxProject);
                head.Add(MeshPrimitives.BeveledBox(new Vector3(0.08f, 0.045f, 0.008f), 0.003f),
                    MeshBuilder.Trs(new Vector3(side * 0.052f, 0.125f, 0.076f),
                        new Vector3(0f, side * -10f, side * 6f), Vector3.one),
                    Mats.Lens(new Color(0.1f, 0.12f, 0.16f, 0.6f), 0.9f, 0f), MeshUvMode.BoxProject);
            }

            // Мост между очками.
            head.Add(MeshPrimitives.BeveledBox(new Vector3(0.03f, 0.01f, 0.01f), 0.002f),
                MeshBuilder.Trs(new Vector3(0f, 0.132f, 0.098f), Vector3.zero, Vector3.one), neon, MeshUvMode.BoxProject);

            // Цветочный венок на шее.
            for (int i = 0; i < 9; i++)
            {
                float angle = Mathf.PI * 2f * i / 9f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * 0.075f, -0.02f, Mathf.Sin(angle) * 0.065f);
                Material petal = i % 2 == 0 ? flowerA : flowerB;

                head.Add(MeshPrimitives.Sphere(0.022f, 6, 5),
                    MeshBuilder.Trs(point, Vector3.zero, new Vector3(1.4f, 0.6f, 1f)), petal, MeshUvMode.BoxProject);
            }
        }

        /// <summary>Шарнирная кукольная челюсть манекена.</summary>
        private static void BuildDollJaw(MeshBuilder jaw, DictatorVisualLook look, Material skin)
        {
            Material plastic = Mats.PlasticSkin();
            Material teethMaterial = Mats.Lens(new Color(0.95f, 0.95f, 0.92f), 0.7f, 0f);

            // Подбородок манекена.
            jaw.Add(MeshPrimitives.Sphere(0.085f, 12, 9),
                MeshBuilder.Trs(new Vector3(0f, -0.045f, 0.02f), Vector3.zero, new Vector3(1f, 0.9f, 1.1f)), plastic,
                MeshUvMode.BoxProject);

            // Нижний ряд зубов — «кукольная» улыбка остаётся ровной.
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f - 0.5f;
                jaw.Add(MeshPrimitives.BeveledBox(new Vector3(0.015f, 0.018f, 0.012f), 0.003f),
                    MeshBuilder.Trs(new Vector3(t * 0.11f, 0.008f, 0.085f), Vector3.zero, Vector3.one), teethMaterial,
                    MeshUvMode.BoxProject);
            }

            // Видимый шарнир челюсти — «манекенность» персонажа.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                jaw.Add(MeshPrimitives.Prism(0.016f, 0.016f, 0.02f, 8, 0.004f, false),
                    MeshBuilder.Align(new Vector3(side * 0.08f, 0f, 0.01f), Vector3.right), plastic,
                    MeshUvMode.BoxProject);
            }
        }

        #endregion
    }
}