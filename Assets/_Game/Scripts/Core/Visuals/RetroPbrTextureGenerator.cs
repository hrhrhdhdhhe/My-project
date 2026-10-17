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

        /// <summary>Тайлинг по умолчанию: сколько раз текстура укладывается на 1 метр.</summary>
        public float MetersPerTile = 1f;

        /// <summary>
        /// Создаёт URP-материал из комплекта. Шейдер ищется по стандартным именам URP,
        /// новые шейдеры проект не пишет (AGENTS.md §3).
        /// </summary>
        public Material CreateMaterial(string materialName, RetroBlendMode blendMode)
        {
            Material material = new Material(ResolveLitShader());
            material.name = materialName;

            if (Albedo != null)
            {
                material.SetTexture("_BaseMap", Albedo);
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
    /// Цвета соответствуют Art Bible: чугун #262930, бетон #0D0E11, сургуч #FF1744,
    /// осциллограф #00E676, янтарь #FFB000.
    /// </summary>
    public static partial class RetroPbrTextureGenerator
    {
        // --- Палитра Art Bible ---
        private static readonly Color CastIronBase = Hex(0x262930);
        private static readonly Color ColdSteelBase = Hex(0x2C313A);
        private static readonly Color ConcreteBase = Hex(0x0D0E11);
        private static readonly Color HazardYellow = Hex(0xFFB000);
        private static readonly Color HazardBlack = Hex(0x0D0E11);
        private static readonly Color RustBrown = Hex(0x6B3A1E);
        private static readonly Color WaxSealRed = Hex(0xFF1744);
        private static readonly Color OscilloscopeGreen = Hex(0x00E676);
        private static readonly Color SegmentAmber = Hex(0xFFB000);
        private static readonly Color PaperBase = Hex(0xD9D2BE);
        private static readonly Color InkBlack = Hex(0x1A1712);

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

        /// <summary>Тёмный бетон бункера #0D0E11: опалубка, швы, подтёки, включения.</summary>
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

        /// <summary>Жёлто-чёрная диагональная разметка опасности для ядерного пульта.</summary>
        public static RetroPbrSet GetHazardStripes(int size = 512)
        {
            return Cached("hazard_stripes", delegate { return BuildHazardStripes(size); });
        }

        /// <summary>Бумага мирного договора: текст TREATY, подписи, круглая сургучная печать #FF1744.</summary>
        public static RetroPbrSet GetTreatyPaper(int size = 512)
        {
            return Cached("treaty_paper", delegate { return BuildTreatyPaper(size); });
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