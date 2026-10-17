// World Crisis | Слой: Core | Ответственный: OpenCode
// Процедурный генератор ретро-PBR текстур (Industrial Lo-Fi Brutalism / Crisp Retro-PBR).
// Всё рисуется в памяти кодом: шум, потёртости, ржавчина, царапины, опалубка, разметка,
// бумага договора, пиксельное лицо осциллографа, семисегментный атлас цифр.
// На выходе — комплект Albedo / Normal / MetallicSmoothness / Emission и готовый URP-материал.
// Текстуры кэшируются в статике; повторный запрос возвращает тот же объект.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    /// <summary>Режим прозрачности материала, сгенерированного генератором.</summary>
    public enum RetroBlendMode
    {
        Opaque = 0,
        Transparent = 1,
        Additive = 2
    }

    /// <summary>Полный PBR-комплект одной поверхности.</summary>
    public sealed class RetroPbrSet
    {
        public Texture2D Albedo;
        public Texture2D Normal;
        public Texture2D MetallicSmoothness;
        public Texture2D Emission;

        /// <summary>Сила свечения, умножаемая на карту эмиссии в материале.</summary>
        public float EmissionIntensity = 1f;

        /// <summary>
        /// Тон поверхности, который несёт цвет. Для однотонных материалов (карболит
        /// телефонов, ткань мундиров) альбедо-текстура серая, а настоящий цвет лежит
        /// здесь и попадает в материал через <c>_BaseColor</c>. Так цвет читается и
        /// глазами, и в проверках — не надо усреднять пиксели текстуры.
        /// </summary>
        public Color Tint = Color.white;

        /// <summary>Тайлинг по умолчанию: сколько раз текстура укладывается на 1 метр.</summary>
        public float MetersPerTile = 1f;

        /// <summary>
        /// Безопасные пределы для непрозрачных поверхностей закрытого помещения.
        /// В кабине нет ни кубических карт, ни отражений окружения, поэтому высокий
        /// Metallic съедает диффузную составляющую и объект становится чёрным.
        /// </summary>
        public const float BunkerMaxMetallic = 0.30f;

        /// <summary>Минимальная металличность: ниже нуля она бессмысленна.</summary>
        public const float BunkerMinMetallic = 0.15f;

        /// <summary>Верхняя граница гладкости непрозрачных поверхностей бункера.</summary>
        public const float BunkerMaxSmoothness = 0.48f;

        /// <summary>Нижняя граница гладкости: совсем матовые поверхности читаются как грязь.</summary>
        public const float BunkerMinSmoothness = 0.30f;

        /// <summary>
        /// Зажимает карту metallic/smoothness под бетонную кабину. Применяется ко всем
        /// непрозрачным материалам; эмиссивные и прозрачные не трогаются — там
        /// металличность не влияет на читаемость свечения.
        /// </summary>
        private static void ClampMetallicSmoothness(RetroPbrSet set, RetroBlendMode blendMode)
        {
            if (set == null || set.MetallicSmoothness == null)
            {
                return;
            }

            if (blendMode != RetroBlendMode.Opaque)
            {
                return;
            }

            Color[] pixels = set.MetallicSmoothness.GetPixels();

            for (int i = 0; i < pixels.Length; i++)
            {
                // URP-Lit читает металличность из R, гладкость — из A.
                float metallic = Mathf.Clamp(pixels[i].r, BunkerMinMetallic, BunkerMaxMetallic);
                float smoothness = Mathf.Clamp(pixels[i].a, BunkerMinSmoothness, BunkerMaxSmoothness);

                pixels[i] = new Color(metallic, pixels[i].g, pixels[i].b, smoothness);
            }

            set.MetallicSmoothness.SetPixels(pixels);
            set.MetallicSmoothness.Apply();
        }

        /// <summary>
        /// Создаёт URP-материал из комплекта. Шейдер берётся из
        /// <see cref="RetroPbrTextureGenerator.PreferredLitShaderName"/> с откатом на URP/Lit.
        /// </summary>
        public Material CreateMaterial(string materialName, RetroBlendMode blendMode)
        {
            ClampMetallicSmoothness(this, blendMode);

            Material material = new Material(ResolveLitShader());
            material.name = materialName;

            // Тон поверхности: серое альбедо + цветной множитель. Без него карболитовые
            // телефоны выходят белыми — цвет нужно проверять не по текстуре, а по материалу.
            material.SetColor("_BaseColor", Tint);

            if (Albedo != null)
            {
                // Альбедо кладётся в ОБОИ слота: фрагмент CrispRetroLit читает их произведение.
                material.SetTexture("_BaseMap", Albedo);
                material.SetTexture("_MainTex", Albedo);
            }

            if (Normal != null)
            {
                material.SetTexture("_BumpMap", Normal);
                material.SetTexture("_NormalMap", Normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            if (MetallicSmoothness != null)
            {
                material.SetTexture("_MetallicGlossMap", MetallicSmoothness);
            }

            material.SetFloat("_Smoothness", ReadAverageSmoothness());

            if (Emission != null && EmissionIntensity > 0.001f)
            {
                material.SetTexture("_EmissionMap", Emission);
                material.SetColor("_EmissionColor", new Color(1f, 1f, 1f, 1f) * EmissionIntensity);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                // Фрагмент CrispRetroLit ВСЕГДА читает карту эмиссии × _EmissionColor.
                // У материала без свечения обязана быть чёрная связка, иначе объект светится.
                material.SetTexture("_EmissionMap", Texture2D.blackTexture);
                material.SetColor("_EmissionColor", new Color(0f, 0f, 0f, 1f));
            }

            ApplyBlendMode(material, blendMode);
            return material;
        }

        private float ReadAverageSmoothness()
        {
            // В URP-Lit карта metallic/smoothness читает гладкость из альфы, но базовое значение
            // тоже должно быть осмысленным — берём среднее по альфе карты.
            if (MetallicSmoothness == null)
            {
                return 0.5f;
            }

            Color[] pixels = MetallicSmoothness.GetPixels();
            float sum = 0f;

            for (int i = 0; i < pixels.Length; i++)
            {
                sum += pixels[i].a;
            }

            return pixels.Length > 0 ? sum / pixels.Length : 0.5f;
        }

        private static void ApplyBlendMode(Material material, RetroBlendMode blendMode)
        {
            switch (blendMode)
            {
                case RetroBlendMode.Transparent:
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_ZWrite", 0f);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    break;

                case RetroBlendMode.Additive:
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 1f);
                    material.SetFloat("_ZWrite", 0f);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    break;

                default:
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_ZWrite", 1f);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    break;
            }
        }

        private static Shader ResolveLitShader()
        {
            Shader shader = null;

            if (!string.IsNullOrEmpty(RetroPbrTextureGenerator.PreferredLitShaderName))
            {
                shader = Shader.Find(RetroPbrTextureGenerator.PreferredLitShaderName);
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogWarning("[WorldCrisis] Не найден URP/Lit шейдер, материалы будут без освещения.");
                return null;
            }

            return shader;
        }
    }

    /// <summary>
    /// Генератор всех текстур проекта. Ни одного внешнего файла: только шум и рисование по пикселям.
    /// Цвета соответствуют Art Bible: бетон #0D0E11..#1E1A19, чугун #262930,
    /// осциллограф #00E676, янтарь #FFB000.
    /// </summary>
    public static partial class RetroPbrTextureGenerator
    {
        // --- Палитра Art Bible ---
        // Всё тёмное и конкретное: бункер — подземная кабина 4x5 м, а не спортзал.
        // Бетон уходит в #0D0E11..#1E1A19, чугун в #262930, столешница — тусклая
        // оружейная сталь #121315..#1C1A21 с рыжей ржавчиной.
        private static readonly Color CastIronBase = Hex(0x262930);
        private static readonly Color ColdSteelBase = Hex(0x3B4148);

        /// <summary> Нижняя тональность бетона бункера: Lerp((0.07,0.07,0.08),(0.12,0.11,0.10), noise). </summary>
        private static readonly Color ConcreteDarkBase = new Color(0.07f, 0.07f, 0.08f);
        private static readonly Color ConcreteLightBase = new Color(0.12f, 0.11f, 0.10f);

        /// <summary>Тёмная вертикальная влага бето́на: #121315..#1C1A18.</summary>
        private static readonly Color ConcreteStreakDark = Hex(0x121315);
        private static readonly Color ConcreteStreakLight = Hex(0x1C1A18);

        /// <summary>Корпус ЭЛТ-монитора: потёртый серо-оливковый металл.</summary>
        private static readonly Color CrackedShellBase = Hex(0x4A504A);

        /// <summary>Полотно стальной двери в задней стене бункера #121417.</summary>
        private static readonly Color DoorNiche = Hex(0x121417);
        private static readonly Color RustBrown = Hex(0x4E3B2C);
        private static readonly Color InkBlack = Hex(0x1A1712);

        /// <summary>Фосфор зелёного экрана/осциллографа #00E676.</summary>
        private static readonly Color OscilloscopeGreen = Hex(0x00E676);

        /// <summary>Фон экрана «CITY STATUS»: #031208 — глубокий тёмно-зелёный цвет кинескопа.</summary>
        private static readonly Color ScreenBase = Hex(0x031208);

        /// <summary>Полутон графики на экране — тусклый фосфор #008844.</summary>
        private static readonly Color ScreenMid = Hex(0x008844);

        /// <summary>Число ячеек в атласе семисегментных индикаторов (4x4).</summary>
        public const int SevenSegmentCellCount = 16;

        /// <summary>Разрешение одной ячейки атласа цифр в пикселях.</summary>
        public const int SevenSegmentCellSize = 128;

        private static readonly Dictionary<string, RetroPbrSet> s_Sets = new Dictionary<string, RetroPbrSet>();

        /// <summary>Сбрасывает кэш (нужно в редакторе после правки палитры).</summary>
        public static void ClearCache()
        {
            s_Sets.Clear();
        }

        /// <summary>
        /// Шейдер поверхностей проекта. По умолчанию — наш CrispRetroLit (тот же набор свойств,
        /// что у URP/Lit, поэтому SRP Batcher не ломается). Если шейдер не скомпилировался,
        /// ResolveLitShader молча откатывается на URP/Lit.
        /// </summary>
        public static string PreferredLitShaderName = "WorldCrisis/Crisp Retro Lit";

        #region Публичные генераторы

        /// <summary>Чугун #262930: потёртости, ржавые пятна, царапины на гранях.</summary>
        public static RetroPbrSet GetCastIron(int size = 512)
        {
            return Cached("cast_iron", delegate { return BuildCastIron(size); });
        }

        /// <summary>Холодная сталь: меньше ржавчины, больше брашированных царапин.</summary>
        public static RetroPbrSet GetColdSteel(int size = 512)
        {
            return Cached("cold_steel", delegate { return BuildColdSteel(size); });
        }

        /// <summary>Бетон бункера и зала саммита: (0.07,0.07,0.08)..(0.12,0.11,0.10) + вертикальные подтёки.</summary>
        public static RetroPbrSet GetBunkerConcrete(int size = 512)
        {
            return Cached("bunker_concrete", delegate { return BuildBunkerConcrete(size); });
        }

        /// <summary>Карболит заданного цвета: телефонные диски и корпуса (красный/жёлтый/чёрный).</summary>
        public static RetroPbrSet GetBakelite(Color baseColor, int size = 256)
        {
            string key = "bakelite_" + ColorUtility.ToHtmlStringRGB(baseColor) + "_" + size;
            return Cached(key, delegate { return BuildBakelite(baseColor, size); });
        }

        /// <summary>
        /// Диагональная разметка опасности для ядерного пульта: полосы ровно 20 px
        /// по целочисленной диагонали, жёлтый (0.82,0.60,0.08), тёмный (0.08,0.08,0.09).
        /// </summary>
        public static RetroPbrSet GetHazardStripes(int size = 256)
        {
            return Cached("hazard_stripes", delegate { return BuildHazardStripes(size); });
        }

        /// <summary>
        /// Служебная бумага рабочих столов: грязно-бежевый лист #9A9280 с тёмными
        /// строками машинописи #282622. Один лист для принтера, шредера, клипборда и папки.
        /// </summary>
        public static RetroPbrSet GetDeskDocumentPaper(int size = 512)
        {
            return Cached("desk_document", delegate { return BuildDeskDocumentPaper(size); });
        }

        /// <summary>Пиксельное зелёное лицо-осциллограф #00E676 для экрана Нео-Кремния.</summary>
        public static RetroPbrSet GetOscilloscopeFace(int size = 256)
        {
            return Cached("oscilloscope", delegate { return BuildOscilloscopeFace(size); });
        }

        /// <summary>Семисегментный атлас цифр заданного цвета (янтарь/зелёный/красный).</summary>
        public static RetroPbrSet GetSevenSegmentAtlas(Color color, int cellSize = RetroPbrTextureGenerator.SevenSegmentCellSize)
        {
            string key = "seg_" + ColorUtility.ToHtmlStringRGB(color) + "_" + cellSize;
            return Cached(key, delegate { return BuildSevenSegmentAtlas(color, cellSize); });
        }

        /// <summary>Суконная ткань мундира/костюма.</summary>
        public static RetroPbrSet GetCloth(Color color, int size = 256)
        {
            string key = "cloth_" + ColorUtility.ToHtmlStringRGB(color) + "_" + size;
            return Cached(key, delegate { return BuildCloth(color, size); });
        }

        /// <summary>Полированное золото с царапинами и патиной.</summary>
        public static RetroPbrSet GetGold(int size = 256)
        {
            return Cached("gold", delegate { return BuildGold(size); });
        }

        /// <summary>Тяжёлая ржавчина для трубы индустриального барона.</summary>
        public static RetroPbrSet GetHeavyRust(int size = 256)
        {
            return Cached("heavy_rust", delegate { return BuildHeavyRust(size); });
        }

        /// <summary>Иней и наледь: кристаллические грани.</summary>
        public static RetroPbrSet GetFrost(int size = 256)
        {
            return Cached("frost", delegate { return BuildFrost(size); });
        }

        /// <summary>Глянцевый пластик манекена.</summary>
        public static RetroPbrSet GetPlasticSkin(int size = 256)
        {
            return Cached("plastic_skin", delegate { return BuildPlasticSkin(size); });
        }

        /// <summary>Кевларовое волокно бронежилета.</summary>
        public static RetroPbrSet GetKevlar(int size = 256)
        {
            return Cached("kevlar", delegate { return BuildKevlar(size); });
        }

        /// <summary>Мутное оргстекло крышки пульта.</summary>
        public static RetroPbrSet GetSmokedGlass(int size = 256)
        {
            return Cached("smoked_glass", delegate { return BuildSmokedGlass(size); });
        }

        /// <summary>Чёрная вода в колодце стола саммита.</summary>
        public static RetroPbrSet GetBlackWater(int size = 256)
        {
            return Cached("black_water", delegate { return BuildBlackWater(size); });
        }

        /// <summary>Бронза с зелёной патиной для голографических архипелагов.</summary>
        public static RetroPbrSet GetBronze(int size = 256)
        {
            return Cached("bronze", delegate { return BuildBronze(size); });
        }

        /// <summary>Однотонная неоновая поверхность (подсветка, индикаторы).</summary>
        public static RetroPbrSet GetNeon(Color color, int size = 128)
        {
            string key = "neon_" + ColorUtility.ToHtmlStringRGB(color) + "_" + size;
            return Cached(key, delegate { return BuildNeon(color, size); });
        }

        /// <summary>Плоская однотонная заливка без шума — для мелких деталей и стекла линз.</summary>
        public static RetroPbrSet GetFlatColor(Color color, int size = 128, float smoothness = 0.5f, float metallic = 0f)
        {
            string key = "flat_" + ColorUtility.ToHtmlStringRGB(color) + "_" + size + "_" + smoothness + "_" + metallic;
            return Cached(key, delegate { return BuildFlatColor(color, size, smoothness, metallic); });
        }

        /// <summary>
        /// Карта грязи и царапин для ретро-шейдеров: R — маска грязи и потёртостей,
        /// G — мелкий шум (мерцание голограмм), B — крупные пятна и разводы.
        /// Одна карта на всю сцену, поэтому внешний вид задаётся глобально, а не в каждом материале.
        /// </summary>
        public static RetroPbrSet GetRetroGrunge(int size = 512)
        {
            string key = "retro_grunge_" + size;
            return Cached(key, delegate { return BuildRetroGrunge(size); });
        }

        /// <summary>
        /// Столешница пульта бункера и Круглого стола: тусклая оружейная сталь
        /// (0.11..0.21 тона) с рыжей ржавчиной (0.28,0.15,0.08) по формуле
        /// SmoothStep(0.58,0.72) от крупного шума. Никаких царапин и камуфляжа.
        /// </summary>
        public static RetroPbrSet GetBunkerConsoleTop(int size = 512)
        {
            return Cached("console_top", delegate { return BuildBunkerConsoleTop(size); });
        }

        /// <summary>
        /// Корпус ЭЛТ-монитора: светло-серый потёртый металл с сеткой трещин
        /// на лаковом покрытии (#6E736B).
        /// </summary>
        public static RetroPbrSet GetCrackedConsoleShell(int size = 256)
        {
            return Cached("cracked_shell", delegate { return BuildCrackedConsoleShell(size); });
        }

        /// <summary>
        /// Экран терминала «CITY STATUS»: фон #031208, яркий фосфор #00E676,
        /// тусклый #008844; рамки, пунктирные строки, таблица с восемью
        /// индикаторами, радар с тремя кругами и лог из трёх строк.
        /// </summary>
        public static RetroPbrSet GetCityStatusScreen(int size = 512)
        {
            return Cached("city_status", delegate { return BuildCityStatusScreen(size); });
        }

        /// <summary>
        /// Почти чёрный цвет экрана терминала. Ставится в _BaseColor материала
        /// Screen_Surface: у экрана почти нет альбедо, весь цвет приходит из эмиссии,
        /// иначе внешняя лампа превращала CITY STATUS в белый квадрат.
        /// </summary>
        public static readonly Color ScreenSurfaceColor = new Color(0.01f, 0.03f, 0.01f, 1f);

        /// <summary>Цвет ниши стальной двери в задней стене бункера.</summary>
        public static Color DoorNicheColor
        {
            get { return DoorNiche; }
        }

        /// <summary>UV-прямоугольник ячейки атласа цифр в нормализованных координатах.</summary>
        public static Rect GetSevenSegmentCell(int cellIndex)
        {
            int column = Mathf.Clamp(cellIndex, 0, SevenSegmentCellCount - 1) % 4;
            int row = Mathf.Clamp(cellIndex, 0, SevenSegmentCellCount - 1) / 4;
            const float cell = 0.25f;

            return new Rect(column * cell, row * cell, cell, cell);
        }

        /// <summary>
        /// Экспорт сгенерированных текстур в папку графического пайплайна. Только в редакторе,
        /// вызывается вручную из меню — рантайм остаётся полностью процедурным.
        /// </summary>
#if UNITY_EDITOR
        public static string ExportFolder
        {
            get { return "Assets/GeneratedTextures"; }
        }
#endif

        #endregion

        #region Кэш и утилиты

        private static RetroPbrSet Cached(string key, Func<RetroPbrSet> factory)
        {
            RetroPbrSet set;
            if (s_Sets.TryGetValue(key, out set) && set != null)
            {
                return set;
            }

            set = factory();
            s_Sets.Add(key, set);
            return set;
        }

        private static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }

        #endregion
    }
}
