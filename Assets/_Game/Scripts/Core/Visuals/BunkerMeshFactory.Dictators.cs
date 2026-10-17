// World Crisis | Слой: Core | Ответственный: OpenCode
// Модульные персонажи — 6 диктаторов. Единая базовая иерархия (Torso_Shared → Head_Upper → Jaw_Pivot),
// уникальная голова-маска по DictatorHeadStyle, мундир по DictatorUniformKind.
// Внешность приходит из данных (CountryDefinition.Visual), поэтому фабрика не ветвится по CountryId.

using UnityEngine;

namespace WorldCrisis
{
    public static partial class BunkerMeshFactory
    {
        // Анатомия рига в метрах (рост сидящего диктатора ≈ 1.4 м).
        private const float TorsoHeight = 0.62f;
        private const float ShoulderY = 0.55f;
        private const float HeadPivotY = 0.66f;
        private const float ShoulderHalfWidth = 0.29f;

        /// <summary>
        /// Полный риг диктатора для державы. Иерархия:
        /// DictatorRoot_&lt;CountryId&gt; → Torso_Shared → Head_Upper → Jaw_Pivot.
        /// Head_Upper поворачивается к собеседнику, Jaw_Pivot качается по оси X от 0° до 25° (голос/осциллограф).
        /// </summary>
        public static GameObject CreateDictatorRig(CountryId id, Transform parent)
        {
            CountryDefinition definition = GameDefinitions.GetConfig().GetOrCreateCountry(id);
            DictatorVisualLook look = definition.Visual;

            GameObject root = MakeNode(GeneratedObjectNames.DictatorRoot(id), parent, Vector3.zero);

            Material cloth = Mats.Cloth(look.ClothColor);
            Material shade = Mats.Cloth(look.ClothShadeColor);
            Material trim = Mats.Cloth(look.TrimColor);
            Material metal = Mats.Lens(look.MetalColor, 0.78f, 0.95f);
            Material skin = Mats.Lens(look.SkinColor, 0.42f, 0f);
            Material glow = Mats.Neon(look.GlowColor);

            MeshBuilder armrest = new MeshBuilder();
            BuildArmrest(armrest, metal, shade);
            Seal(GeneratedObjectNames.ArmrestBase, root.transform, Vector3.zero, armrest);

            GameObject torsoNode = BuildTorso(root.transform, look, cloth, shade, trim, metal, skin, glow);
            GameObject headNode = BuildHead(torsoNode.transform, look, skin, metal, glow);
            BuildJaw(headNode.transform, look, skin, metal, glow);

            return root;
        }

        /// <summary>Подлокотник/край стола, на который кладутся предплечья диктатора.</summary>
        private static void BuildArmrest(MeshBuilder builder, Material metal, Material shade)
        {
            builder.Add(MeshPrimitives.BeveledBox(new Vector3(0.78f, 0.05f, 0.34f), 0.012f),
                MeshBuilder.Trs(new Vector3(0f, 0.2f, 0.2f), Vector3.zero, Vector3.one), metal, MeshUvMode.BoxProject);

            // Две опоры под подлокотником.
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.32f : 0.32f;
                builder.Add(MeshPrimitives.BeveledBox(new Vector3(0.05f, 0.2f, 0.28f), 0.01f),
                    MeshBuilder.Trs(new Vector3(x, 0.1f, 0.2f), Vector3.zero, Vector3.one), shade, MeshUvMode.BoxProject);
            }

            // Мягкая накладка на подлокотник.
            builder.Add(MeshPrimitives.BeveledBox(new Vector3(0.7f, 0.02f, 0.26f), 0.008f),
                MeshBuilder.Trs(new Vector3(0f, 0.232f, 0.2f), Vector3.zero, Vector3.one), shade, MeshUvMode.BoxProject);
        }

        /// <summary>
        /// Широкоплечий силуэт в мундире: грудь, талия, плечи, руки и предплечья на подлокотнике,
        /// плюс детали кроя (погоны, ордена, воротник, пуговицы) по типу формы.
        /// Возвращает узел Torso_Shared.
        /// </summary>
        private static GameObject BuildTorso(Transform parent, DictatorVisualLook look, Material cloth, Material shade,
            Material trim, Material metal, Material skin, Material glow)
        {
            MeshBuilder body = new MeshBuilder();
            float bulk = look.BulkScale;

            // Грудь и талия: два фасеточных бруса с разной шириной дают силуэт «песочных часов».
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.5f * bulk, 0.3f, 0.3f * bulk), 0.035f),
                MeshBuilder.Trs(new Vector3(0f, TorsoHeight - 0.12f, 0f), Vector3.zero, Vector3.one), cloth,
                MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.44f * bulk, 0.22f, 0.28f * bulk), 0.03f),
                MeshBuilder.Trs(new Vector3(0f, 0.2f, 0f), Vector3.zero, Vector3.one), cloth, MeshUvMode.BoxProject);

            // Плечи — приплюснутые сферы, ключевой элемент широкоплечего силуэта.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 position = new Vector3(side * ShoulderHalfWidth * bulk, ShoulderY, 0f);

                body.Add(MeshPrimitives.Sphere(0.11f * bulk, 12, 8),
                    MeshBuilder.Trs(position, Vector3.zero, new Vector3(1f, 1.15f, 1f)), cloth, MeshUvMode.BoxProject);

                // Верхняя часть руки: от плеча к локтю.
                Vector3 elbow = new Vector3(side * (ShoulderHalfWidth + 0.03f) * bulk, 0.36f, 0.03f);
                body.Add(MeshPrimitives.Prism(0.065f * bulk, 0.055f * bulk,
                        Vector3.Distance(position, elbow), 10, 0.012f, false),
                    MeshBuilder.Align((position + elbow) * 0.5f, elbow - position), cloth, MeshUvMode.BoxProject);

                // Предплечье лежит на подлокотнике.
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.1f, 0.075f, 0.3f), 0.02f),
                    MeshBuilder.Trs(new Vector3(side * 0.31f * bulk, 0.255f, 0.13f),
                        new Vector3(0f, side * 7f, 0f), Vector3.one), cloth, MeshUvMode.BoxProject);

                // Кисть.
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.085f, 0.055f, 0.1f), 0.018f),
                    MeshBuilder.Trs(new Vector3(side * 0.32f * bulk, 0.255f, 0.31f),
                        new Vector3(0f, side * 7f, 0f), Vector3.one), skin, MeshUvMode.BoxProject);
            }

            // Шея.
            body.Add(MeshPrimitives.Prism(0.055f, 0.05f, 0.1f, 10, 0.008f, false),
                MeshBuilder.Trs(new Vector3(0f, TorsoHeight + 0.03f, 0f), Vector3.zero, Vector3.one), skin,
                MeshUvMode.BoxProject);

            switch (look.UniformKind)
            {
                case DictatorUniformKind.CeremonialUniform:
                    BuildCeremonialUniform(body, look, trim, metal, glow);
                    break;

                case DictatorUniformKind.HeavyParka:
                    BuildHeavyParka(body, look, trim, metal, glow);
                    break;

                case DictatorUniformKind.BlackGoldRobe:
                    BuildBlackGoldRobe(body, trim, metal, glow);
                    break;

                case DictatorUniformKind.RustCloak:
                    BuildRustCloak(body, look, trim, metal, glow);
                    break;

                case DictatorUniformKind.HawaiianVest:
                    BuildHawaiianVest(body, trim, metal, glow);
                    break;

                default:
                    BuildTurtleneckSuit(body, trim, metal, glow);
                    break;
            }

            // Пуговицы вдоль борта — общий элемент всех мундиров.
            for (int i = 0; i < 4; i++)
            {
                body.Add(MeshPrimitives.Rivet(0.011f, 0.008f, 8),
                    MeshBuilder.Trs(new Vector3(0.06f, 0.34f - i * 0.08f, 0.155f * look.BulkScale),
                        new Vector3(90f, 0f, 0f), Vector3.one), metal, MeshUvMode.BoxProject);
            }

            return Seal(GeneratedObjectNames.TorsoShared, parent, Vector3.zero, body);
        }

        /// <summary>Пиджак поверх чёрной водолазки: воротник, лацканы, нагрудный карман.</summary>
        private static void BuildTurtleneckSuit(MeshBuilder body, Material trim, Material metal, Material glow)
        {
            Material black = Mats.Cloth(new Color(0.05f, 0.05f, 0.06f));

            // Водолазка: плотный цилиндр вокруг шеи.
            body.Add(MeshPrimitives.Prism(0.075f, 0.07f, 0.1f, 12, 0.006f, true),
                MeshBuilder.Trs(new Vector3(0f, TorsoHeight + 0.09f, 0f), Vector3.zero, Vector3.one), black,
                MeshUvMode.BoxProject);

            // Лацканы — два наклонённых бруса.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.1f, 0.2f, 0.02f), 0.006f),
                    MeshBuilder.Trs(new Vector3(side * 0.08f, TorsoHeight - 0.1f, 0.152f),
                        new Vector3(0f, 0f, side * 18f), Vector3.one), trim, MeshUvMode.BoxProject);
            }

            // Нагрудный карман и значок.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.11f, 0.09f, 0.015f), 0.004f),
                MeshBuilder.Trs(new Vector3(-0.15f, TorsoHeight - 0.17f, 0.152f), Vector3.zero, Vector3.one),
                trim, MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.Rivet(0.016f, 0.006f, 8),
                MeshBuilder.Trs(new Vector3(-0.15f, TorsoHeight - 0.13f, 0.162f), new Vector3(90f, 0f, 0f),
                    Vector3.one), glow, MeshUvMode.BoxProject);
        }

        /// <summary>Парадный мундир: погоны с бахромой, ряд орденов, золотой кант.</summary>
        private static void BuildCeremonialUniform(MeshBuilder body, DictatorVisualLook look, Material trim,
            Material metal, Material glow)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;

                // Погон: плоская плита с золотым кантом.
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.16f, 0.03f, 0.12f), 0.008f),
                    MeshBuilder.Trs(new Vector3(side * 0.2f, TorsoHeight + 0.03f, 0f), Vector3.zero, Vector3.one),
                    trim, MeshUvMode.BoxProject);
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.17f, 0.008f, 0.13f), 0.003f),
                    MeshBuilder.Trs(new Vector3(side * 0.2f, TorsoHeight + 0.046f, 0f), Vector3.zero, Vector3.one),
                    metal, MeshUvMode.BoxProject);

                // Бахрома из тонких цилиндров.
                for (int f = 0; f < 5; f++)
                {
                    body.Add(MeshPrimitives.Prism(0.005f, 0.005f, 0.05f, 5, 0f, false),
                        MeshBuilder.Trs(new Vector3(side * (0.14f + f * 0.03f), TorsoHeight + 0.065f, 0.05f),
                            new Vector3(0f, 0f, side * -12f), Vector3.one), metal, MeshUvMode.BoxProject);
                }
            }

            // Ордена: три «звезды» и две планки.
            for (int i = 0; i < 3; i++)
            {
                float x = 0.1f + i * 0.055f;
                body.Add(MeshPrimitives.Prism(0.024f, 0.006f, 0.01f, 5, 0f, false),
                    MeshBuilder.Trs(new Vector3(x, TorsoHeight - 0.11f, 0.158f), new Vector3(90f, 0f, 0f),
                        Vector3.one), metal, MeshUvMode.BoxProject);
            }

            for (int i = 0; i < 2; i++)
            {
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.1f, 0.026f, 0.008f), 0.002f),
                    MeshBuilder.Trs(new Vector3(-0.12f, TorsoHeight - 0.11f - i * 0.04f, 0.158f), Vector3.zero,
                        Vector3.one), metal, MeshUvMode.BoxProject);
            }

            // Золотой кант по краю воротника и пуговицы-гвоздики.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.2f, 0.016f, 0.016f), 0.004f),
                MeshBuilder.Trs(new Vector3(0f, TorsoHeight + 0.005f, 0.145f), Vector3.zero, Vector3.one), metal,
                MeshUvMode.BoxProject);
        }

        /// <summary>Тяжёлая парка: воротник-стойка, накладные карманы, наросты инея и сосульки.</summary>
        private static void BuildHeavyParka(MeshBuilder body, DictatorVisualLook look, Material trim,
            Material metal, Material glow)
        {
            Material ice = Mats.Frost();
            DetRandom random = new DetRandom(0xF2057);

            // Высокий воротник-стойка.
            body.Add(MeshPrimitives.Prism(0.115f, 0.125f, 0.14f, 12, 0.02f, true),
                MeshBuilder.Trs(new Vector3(0f, TorsoHeight + 0.06f, -0.01f), Vector3.zero, Vector3.one), trim,
                MeshUvMode.BoxProject);

            // Наложные карманы.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.13f, 0.09f, 0.05f), 0.01f),
                    MeshBuilder.Trs(new Vector3(side * 0.14f, 0.3f, 0.16f), Vector3.zero, Vector3.one), trim,
                    MeshUvMode.BoxProject);
            }

            // Иней и сосульки на плечах — детали, которые делают образ узнаваемым.
            for (int i = 0; i < 9; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float t = random.Range(0f, 1f);
                Vector3 point = new Vector3(side * (0.1f + t * 0.16f) * look.BulkScale,
                    TorsoHeight + 0.06f + random.Range(-0.02f, 0.02f), random.Range(-0.08f, 0.09f));

                body.Add(MeshPrimitives.Sphere(random.Range(0.012f, 0.03f), 6, 4),
                    MeshBuilder.Trs(point, Vector3.zero, new Vector3(1f, 0.6f, 1f)), ice, MeshUvMode.BoxProject);
            }

            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float length = random.Range(0.03f, 0.1f);
                Vector3 point = new Vector3(side * random.Range(0.12f, 0.24f) * look.BulkScale,
                    TorsoHeight + 0.03f, random.Range(0.06f, 0.14f));

                body.Add(MeshPrimitives.Prism(0.008f, 0f, length, 5, 0f, false),
                    MeshBuilder.Trs(point + Vector3.down * length * 0.5f, Vector3.zero, Vector3.one), ice,
                    MeshUvMode.BoxProject);
            }

            // Ряды заклёпок на воротнике.
            AddRivetRow(body, new Vector3(-0.09f, TorsoHeight + 0.1f, 0.09f),
                new Vector3(0.09f, TorsoHeight + 0.1f, 0.09f), 5, 0.008f, 0.005f, metal, Vector3.up);
        }

        /// <summary>Чёрно-золотое одеяние: тяжёлый подол, золотое шитьё, наплечные накладки.</summary>
        private static void BuildBlackGoldRobe(MeshBuilder body, Material trim, Material metal, Material glow)
        {
            // Расходящийся подол — усечённый конус вместо бруса.
            body.Add(MeshPrimitives.Prism(0.3f, 0.46f, 0.24f, 16, 0.02f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.12f, 0f), Vector3.zero, Vector3.one), trim, MeshUvMode.BoxProject);

            // Золотое шитьё двумя кольцевыми кантами.
            for (int i = 0; i < 2; i++)
            {
                float radius = 0.34f + i * 0.08f;
                body.Add(MeshPrimitives.Ring(radius - 0.01f, radius + 0.01f, 0.012f, 20),
                    MeshBuilder.Trs(new Vector3(0f, 0.06f + i * 0.1f, 0f), Vector3.zero, Vector3.one), metal,
                    MeshUvMode.BoxProject);
            }

            // Наплечные накладки с золотым кантом.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.15f, 0.04f, 0.15f), 0.012f),
                    MeshBuilder.Trs(new Vector3(side * 0.21f, TorsoHeight + 0.02f, 0f), Vector3.zero, Vector3.one),
                    trim, MeshUvMode.BoxProject);
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.155f, 0.01f, 0.155f), 0.003f),
                    MeshBuilder.Trs(new Vector3(side * 0.21f, TorsoHeight + 0.042f, 0f), Vector3.zero, Vector3.one),
                    metal, MeshUvMode.BoxProject);
            }

            // Золотая цепь на груди.
            for (int i = 0; i < 14; i++)
            {
                float angle = i / 13f * Mathf.PI;
                body.Add(MeshPrimitives.Sphere(0.009f, 6, 5),
                    MeshBuilder.Trs(new Vector3(-0.06f + Mathf.Cos(angle) * 0.09f, TorsoHeight - 0.05f
                        - Mathf.Sin(angle) * 0.09f, 0.16f), Vector3.zero, Vector3.one), metal, MeshUvMode.BoxProject);
            }
        }

        /// <summary>Потёртый плащ с высоким воротником и ржавыми заклёпками.</summary>
        private static void BuildRustCloak(MeshBuilder body, DictatorVisualLook look, Material trim,
            Material metal, Material glow)
        {
            Material rust = Mats.Rust();
            DetRandom random = new DetRandom(0x2E05);

            // Плащ: расширяющийся конус сзади.
            body.Add(MeshPrimitives.Prism(0.34f, 0.5f, 0.62f, 14, 0.03f, false),
                MeshBuilder.Trs(new Vector3(0f, 0.3f, -0.04f), new Vector3(-6f, 0f, 0f), Vector3.one), trim,
                MeshUvMode.BoxProject);

            // Высокий воротник, откинутый назад.
            body.Add(MeshPrimitives.Prism(0.14f, 0.16f, 0.2f, 12, 0.02f, true),
                MeshBuilder.Trs(new Vector3(0f, TorsoHeight + 0.1f, -0.06f), new Vector3(-18f, 0f, 0f), Vector3.one),
                trim, MeshUvMode.BoxProject);

            // Ржавые заклёпки вдоль края плаща.
            for (int i = 0; i < 9; i++)
            {
                float angle = Mathf.PI * (0.1f + i / 8f * 0.8f);
                Vector3 point = new Vector3(Mathf.Cos(angle) * 0.34f, 0.1f, Mathf.Sin(angle) * 0.44f - 0.04f);
                body.Add(MeshPrimitives.RivetDome(0.012f, 0.01f),
                    MeshBuilder.Align(point + new Vector3(0f, 0.3f, 0f), new Vector3(Mathf.Cos(angle), 0.4f, Mathf.Sin(angle))),
                    rust, MeshUvMode.BoxProject);
            }

            // Обрывки ткани по краю.
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.PI * (0.15f + i / 5f * 0.7f);
                body.Add(MeshPrimitives.BeveledBox(new Vector3(random.Range(0.04f, 0.09f), 0.02f, 0.05f), 0.004f),
                    MeshBuilder.Trs(new Vector3(Mathf.Cos(angle) * 0.42f, random.Range(0.02f, 0.1f),
                        Mathf.Sin(angle) * 0.5f - 0.04f), new Vector3(0f, angle * Mathf.Rad2Deg, 0f), Vector3.one),
                    rust, MeshUvMode.BoxProject);
            }
        }

        /// <summary>Гавайская рубашка под кевларовым бронежилетом, венок на шее.</summary>
        private static void BuildHawaiianVest(MeshBuilder body, Material trim, Material metal, Material glow)
        {
            Material kevlar = Mats.Kevlar();
            Material shirt = Mats.Cloth(new Color(0.78f, 0.2f, 0.24f));

            // Кевларовый бронежилет: две плиты с вырезом спереди.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.24f, 0.4f, 0.26f), 0.02f),
                    MeshBuilder.Trs(new Vector3(side * 0.13f, TorsoHeight - 0.12f, -0.01f), Vector3.zero, Vector3.one),
                    kevlar, MeshUvMode.BoxProject);
            }

            // Рубашка видна в вырезе и на плечах.
            body.Add(MeshPrimitives.BeveledBox(new Vector3(0.18f, 0.38f, 0.26f), 0.02f),
                MeshBuilder.Trs(new Vector3(0f, TorsoHeight - 0.1f, 0f), Vector3.zero, Vector3.one), shirt,
                MeshUvMode.BoxProject);
            body.Add(MeshPrimitives.Prism(0.11f, 0.115f, 0.06f, 12, 0.01f, true),
                MeshBuilder.Trs(new Vector3(0f, TorsoHeight + 0.02f, 0f), Vector3.zero, Vector3.one), shirt,
                MeshUvMode.BoxProject);

            // Плечевые ремни жилета.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                body.Add(MeshPrimitives.BeveledBox(new Vector3(0.06f, 0.22f, 0.28f), 0.012f),
                    MeshBuilder.Trs(new Vector3(side * 0.15f, TorsoHeight + 0.04f, 0f), Vector3.zero, Vector3.one),
                    kevlar, MeshUvMode.BoxProject);
            }

            // Цветочный венок: кольцо из лепестков вокруг шеи.
            for (int i = 0; i < 11; i++)
            {
                float angle = Mathf.PI * 2f * i / 11f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * 0.08f, TorsoHeight + 0.03f,
                    Mathf.Sin(angle) * 0.07f + 0.01f);
                Material petalMaterial = i % 3 == 0 ? trim : shirt;

                body.Add(MeshPrimitives.Sphere(0.022f, 6, 5),
                    MeshBuilder.Trs(point, Vector3.zero, new Vector3(1.3f, 0.7f, 1f)),
                    petalMaterial, MeshUvMode.BoxProject);
            }

            // Пляжные значки на груди.
            for (int i = 0; i < 2; i++)
            {
                body.Add(MeshPrimitives.Rivet(0.013f, 0.006f, 8),
                    MeshBuilder.Trs(new Vector3(0.06f + i * 0.05f, TorsoHeight - 0.1f, 0.145f),
                        new Vector3(90f, 0f, 0f), Vector3.one), glow, MeshUvMode.BoxProject);
            }
        }
    }
}