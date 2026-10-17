// World Crisis | Слой: Core | Ответственный: OpenCode
// Конкретные поверхности Art Bible: чугун, холодная сталь, бетон бункера, карболит,
// разметка опасности, служебная бумага рабочих столов, пиксельное лицо осциллографа,
// семисегментный атлас, ткани, золото, ржавчина, иней, пластик, кевлар, стекло,
// чёрная вода, бронза, неон.

using UnityEngine;

namespace WorldCrisis
{
    public static partial class RetroPbrTextureGenerator
    {
        /// <summary>Детерминированный LCG — одинаковая картинка при каждом запуске.</summary>
        private sealed class Rand
        {
            private uint _state;

            public Rand(int seed)
            {
                _state = seed == 0 ? 0x9E3779B9u : (uint)seed;
            }

            private uint NextUInt()
            {
                _state = _state * 1664525u + 1013904223u;
                return _state;
            }

            public float Next01()
            {
                return (NextUInt() >> 8) / 16777216f;
            }

            public float Range(float min, float max)
            {
                return min + (max - min) * Next01();
            }

            public int RangeInt(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive)
                {
                    return minInclusive;
                }

                return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
            }

            public bool Chance(float probability)
            {
                return Next01() < probability;
            }
        }

        #region Металл

        /// <summary>Чугун #262930: потёртости, ржавые пятна, царапины на гранях.</summary>
        private static RetroPbrSet BuildCastIron(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Rand random = new Rand(0x1C45);
            int period = 8;
            float scale = size / (float)period;
            int rustPeriod = 6;
            float rustScale = size / (float)rustPeriod;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    float coarse = Fbm(u, v, period, 4211, 5, 2f, 0.5f);
                    float fine = Fbm(u * 4f, v * 4f, period * 4, 9137, 3, 2f, 0.55f);

                    float shade = 0.74f + coarse * 0.46f + (fine - 0.5f) * 0.14f;
                    Color color = CastIronBase * shade;

                    float height = 0.46f + (coarse - 0.5f) * 0.5f + (fine - 0.5f) * 0.22f;

                    // Ржавчина: клеточные пятна, подъеденные фрактальным шумом.
                    float cell = Cellular(x / rustScale, y / rustScale, rustPeriod, 3371);
                    float rust = Mathf.Clamp01(1f - (cell - 0.18f) * 3.4f) * Mathf.Clamp01(coarse * 1.5f - 0.18f);
                    rust *= 0.85f;

                    if (rust > 0.01f)
                    {
                        color = Color.Lerp(color, RustBrown * (0.7f + fine * 0.7f), rust);
                        height += rust * 0.18f;
                    }

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(height);
                    canvas.Metallic[index] = Mathf.Lerp(0.88f, 0.12f, rust);
                    canvas.Smoothness[index] = Mathf.Lerp(0.46f + fine * 0.22f, 0.24f, rust);
                }
            }

            AddScratches(canvas, random, size, 46, 0.34f, 0.22f, new Color(0.62f, 0.65f, 0.7f), 0.30f, 0.055f);
            AddWearSpeckles(canvas, random, size, 900, 0.72f, 0.55f);

            return Bake(canvas, "CastIron", 1.1f, true);
        }

        /// <summary>Холодная сталь: брашированные полосы, меньше ржавчины, больше царапин.</summary>
        private static RetroPbrSet BuildColdSteel(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Rand random = new Rand(0x57EE1);
            int period = 8;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    float coarse = Fbm(u, v, period, 7717, 4, 2f, 0.5f);
                    // Браширование: шум, растянутый вдоль X.
                    float brushed = ValueNoise(u * 1.5f, v * 22f, period * 22, 2029);
                    float speck = Fbm(u * 9f, v * 9f, period * 9, 5501, 2, 2f, 0.5f);

                    float shade = 0.8f + coarse * 0.3f + (brushed - 0.5f) * 0.3f + (speck - 0.5f) * 0.08f;
                    Color color = ColdSteelBase * shade;

                    int rustPeriod = 10;
                    float rustScale = size / (float)rustPeriod;
                    float cell = Cellular(x / rustScale, y / rustScale, rustPeriod, 8803);
                    float rust = Mathf.Clamp01(1f - (cell - 0.1f) * 5f) * Mathf.Clamp01(coarse * 1.6f - 0.62f);

                    if (rust > 0.01f)
                    {
                        color = Color.Lerp(color, RustBrown * 0.85f, rust);
                    }

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (brushed - 0.5f) * 0.35f + (coarse - 0.5f) * 0.2f);
                    canvas.Metallic[index] = Mathf.Lerp(0.96f, 0.2f, rust);
                    canvas.Smoothness[index] = Mathf.Lerp(0.52f + brushed * 0.34f, 0.3f, rust);
                }
            }

            AddScratches(canvas, random, size, 120, 0.55f, 0.14f, new Color(0.78f, 0.82f, 0.88f), 0.34f, 0.03f);
            AddWearSpeckles(canvas, random, size, 1400, 0.82f, 0.4f);

            return Bake(canvas, "ColdSteel", 1.1f, true);
        }

        /// <summary>
        /// Бетон бункера и зала саммита: тональный диапазон строго по формуле
        /// Lerp((0.07,0.07,0.08),(0.12,0.11,0.10), noise) плюс тёмные вертикальные
        /// подтёки #121315..#1C1A18 от стыков опалубки. Шум тайлится бесшавно,
        /// масштаб 1.6 метра на плитку.
        /// </summary>
        private static RetroPbrSet BuildBunkerConcrete(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            int period = 8;
            float scale = size / (float)period;
            int finePeriod = period * 12;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    float noise = Fbm(u, v, period, 1237, 5, 2f, 0.52f);
                    float fine = ValueNoise(u * 12f, v * 12f, finePeriod, 6151);

                    Color color = Color.Lerp(ConcreteDarkBase, ConcreteLightBase,
                        Mathf.Clamp01(noise * 0.82f + (fine - 0.5f) * 0.18f));

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (noise - 0.5f) * 0.3f + (fine - 0.5f) * 0.12f);
                    canvas.Metallic[index] = 0f;
                    canvas.Smoothness[index] = Mathf.Clamp01(0.22f + (fine - 0.5f) * 0.08f);
                }
            }

            // Вертикальные подтёки: узкие длинные полосы живут своей высотой и
            // слегка блуждают по X; тон часть между #121315 и #1C1A18.
            Rand streaks = new Rand(0xD819);
            int streakCount = size / 10;

            for (int i = 0; i < streakCount; i++)
            {
                float startX = streaks.Range(0f, size);
                float startY = streaks.Range(0f, size);
                float length = streaks.Range(size * 0.2f, size * 0.7f);
                float width = streaks.Range(0.8f, 2.2f);
                Color tint = Color.Lerp(ConcreteStreakDark, ConcreteStreakLight, Hash(i, 7, 5171));

                for (int step = 0; step <= length; step++)
                {
                    float fade = 1f - step / length;
                    float drift = (ValueNoise(startX / 11f, step / 37f, 64, 8123) - 0.5f) * size * 0.014f;

                    for (int w = -(int)width; w <= (int)width; w++)
                    {
                        int index = canvas.Index(
                            Mathf.RoundToInt(startX + drift) + w,
                            Mathf.RoundToInt(startY) + step);
                        canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], tint, 0.4f * fade);
                    }
                }
            }

            return Bake(canvas, "BunkerConcrete", 1.6f, true);
        }

        #endregion

        #region Пластик и разметка

        /// <summary>Карболит: глубокий цвет, тонкие царапины, следы пальцев.</summary>
        private static RetroPbrSet BuildBakelite(Color baseColor, int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Rand random = new Rand(0xBA4E);
            int period = 6;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    float marble = Fbm(u, v, period, 4519, 4, 2.1f, 0.5f);
                    float micro = Fbm(u * 12f, v * 12f, period * 12, 8821, 2, 2f, 0.5f);

                    float shade = 0.82f + (marble - 0.5f) * 0.32f + (micro - 0.5f) * 0.1f;
                    int index = y * size + x;

                    // Альбедо здесь серое: настоящий цвет карболита несёт Tint и попадает
                    // в материал через _BaseColor. Иначе цвет тонул бы в текстуре, и красный,
                    // жёлтый и чёрный телефоны невозможно было бы отличить друг от друга.
                    canvas.Albedo[index] = new Color(shade, shade, shade, 1f);
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (marble - 0.5f) * 0.3f + (micro - 0.5f) * 0.12f);
                    canvas.Metallic[index] = 0.04f;
                    canvas.Smoothness[index] = 0.66f + (marble - 0.5f) * 0.24f;
                }
            }

            AddScratches(canvas, random, size, 34, 0.3f, 0.3f, new Color(1f, 1f, 1f), 0.24f, 0.045f);

            for (int i = 0; i < 4; i++)
            {
                SmudgeCircle(canvas,
                    new Vector2(random.Range(0f, size), random.Range(0f, size)),
                    size * random.Range(0.05f, 0.11f), 0.16f, new Color(0.18f, 0.18f, 0.18f, 1f));
            }

            RetroPbrSet set = Bake(canvas, "Bakelite", 0.4f, true);
            set.Tint = baseColor;
            return set;
        }

        /// <summary>
        /// Разметка опасности для корпуса ядерного пульта: чёткие диагонали 45°
        /// строго по формуле — целочисленное деление (x + y) / 20 задаёт полосы
        /// шириной 20 пикселей, жёлтый (0.82,0.60,0.08), тёмный (0.08,0.08,0.09),
        /// без потёртостей и прочих модификаций.
        /// </summary>
        private static RetroPbrSet BuildHazardStripes(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Color yellow = new Color(0.82f, 0.60f, 0.08f, 1f);
            Color dark = new Color(0.08f, 0.08f, 0.09f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isYellow = ((x + y) / 20) % 2 == 0;
                    int index = y * size + x;
                    canvas.Albedo[index] = isYellow ? yellow : dark;
                    canvas.HeightMap[index] = 0.5f;
                    canvas.Metallic[index] = 0.08f;
                    canvas.Smoothness[index] = 0.45f;
                }
            }

            return Bake(canvas, "HazardStripes", 0.55f, true);
        }

        #endregion

        #region Документы и экраны

        /// <summary>
        /// Служебная бумага рабочих столов: грязно-бежевый лист #9A9280 с тёмными
        /// строками машинописи #282622. Без заголовков и печатей — распечатка
        /// санитарного отчёта, а не парадный договор. Один и тот же лист оживит
        /// шредер, принтер, клипборд и папку.
        /// </summary>
        private static RetroPbrSet BuildDeskDocumentPaper(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Color paper = new Color(0.604f, 0.573f, 0.502f, 1f); // #9A9280
            Color ink = new Color(0.157f, 0.149f, 0.133f, 1f); // #282622
            int period = 8;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    float fibre = Fbm(u * 26f, v * 26f, period * 26, 1783, 2, 2f, 0.5f);
                    float blotch = Fbm(u, v, period, 5077, 4, 2f, 0.55f);

                    float shade = 0.94f + (blotch - 0.5f) * 0.12f + (fibre - 0.5f) * 0.06f;
                    int index = y * size + x;
                    canvas.Albedo[index] = paper * shade;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (fibre - 0.5f) * 0.2f + (blotch - 0.5f) * 0.15f);
                    canvas.Metallic[index] = 0f;
                    canvas.Smoothness[index] = 0.12f;
                }
            }

            // Строки машинописи: тёмные «слова» с разрывами, у части строк —
            // выровненный конец, как у ленты принтера.
            Rand lines = new Rand(0x7E47);
            int margin = Mathf.Max(6, size / 7);
            int lineHeight = Mathf.Max(4, size / 26);
            int thickness = Mathf.Max(1, lineHeight / 5);
            int cursorY = Mathf.RoundToInt(size * 0.86f);

            while (cursorY > size * 0.1f)
            {
                if (lines.Chance(0.14f))
                {
                    cursorY -= lineHeight;
                    continue;
                }

                bool flushRight = lines.Chance(0.2f);
                int penX = margin + (flushRight ? 0 : lines.RangeInt(0, margin / 2));
                int rightEdge = size - margin - (flushRight ? lines.RangeInt(0, margin / 2) : 0);

                while (penX < rightEdge)
                {
                    int wordWidth = lines.RangeInt(4, 11) * thickness * 2;
                    int gap = thickness + lines.RangeInt(0, 2) * thickness;

                    for (int w = 0; w < wordWidth && penX + w < rightEdge; w++)
                    {
                        for (int t = 0; t < thickness; t++)
                        {
                            int index = canvas.Index(penX + w, cursorY + t);
                            canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], ink, 0.82f);
                            canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] - 0.12f);
                        }
                    }

                    penX += wordWidth + gap;
                }

                cursorY -= lineHeight;
            }

            return Bake(canvas, "DeskDocumentPaper", 0.32f, true);
        }

        /// <summary>Пиксельное зелёное лицо-осциллограф #00E676 для экрана Нео-Кремния.</summary>
        private static RetroPbrSet BuildOscilloscopeFace(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Color faceColor = OscilloscopeGreen;
            Color glass = new Color(0.015f, 0.06f, 0.035f, 1f);
            int pixel = Mathf.Max(2, size / 96);

            canvas.Fill(glass);
            canvas.FillHeight(0.5f);
            canvas.FillSmoothness(0.85f);
            canvas.FillMetallic(0f);

            // Маска пикселей: экран собран из отдельных светящихся точек.
            for (int y = 0; y < size; y += pixel)
            {
                for (int x = 0; x < size; x += pixel)
                {
                    for (int j = 0; j < pixel; j++)
                    {
                        for (int i = 0; i < pixel; i++)
                        {
                            int index = canvas.Index(x + i, y + j);
                            float falloff = Mathf.Clamp01(1f
                                - Mathf.Sqrt(i * i + j * j) / Mathf.Max(1f, pixel * 0.85f));
                            canvas.Albedo[index] = Color.Lerp(glass, faceColor * 0.35f, falloff * 0.25f);
                            canvas.HeightMap[index] = 0.5f + falloff * 0.12f;
                        }
                    }
                }
            }

            DrawPixelFace(canvas, size, pixel, faceColor);
            DrawWaveform(canvas, size, pixel, faceColor);

            // Помехи и виньетка.
            Rand random = new Rand(0x05C1);

            for (int i = 0; i < size * 2; i++)
            {
                int x = random.RangeInt(0, size);
                int y = random.RangeInt(0, size);
                canvas.AddEmission(x, y, faceColor * random.Range(0.05f, 0.22f));
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)size - 0.5f) * 2f;
                    float ny = (y / (float)size - 0.5f) * 2f;
                    float vignette = Mathf.Clamp01(1f - (nx * nx + ny * ny) * 0.55f);
                    int index = y * size + x;

                    canvas.Albedo[index] = Color.Lerp(glass * 0.4f, canvas.Albedo[index], vignette);
                    canvas.Emission[index] *= vignette;
                }
            }

            RetroPbrSet set = Bake(canvas, "OscilloscopeFace", 0.22f, false);
            set.EmissionIntensity = 2.4f;
            return set;
        }

        /// <summary>Пиксельное лицо: глаза, зрачки, брови, рот-«скрежет».</summary>
        private static void DrawPixelFace(PbrCanvas canvas, int size, int pixel, Color color)
        {
            int eyeWidth = Mathf.Max(2, size / 12) / pixel * pixel;
            int eyeHeight = Mathf.Max(2, size / 16) / pixel * pixel;
            int eyeLeft = Mathf.RoundToInt(size * 0.26f) / pixel * pixel;
            int eyeRight = Mathf.RoundToInt(size * 0.74f - eyeWidth) / pixel * pixel;
            int eyeY = Mathf.RoundToInt(size * 0.62f) / pixel * pixel;

            DrawGlowRect(canvas, eyeLeft, eyeY, eyeWidth, eyeHeight, pixel, color, 1f);

            // Зрачки: вертикальные щели, как у кинескопа.
            int pupilWidth = Mathf.Max(1, eyeWidth / 3 / pixel * pixel);
            int pupilHeight = Mathf.Max(2, (int)(eyeHeight * 1.4f) / pixel * pixel);
            DrawGlowRect(canvas, eyeLeft + (eyeWidth - pupilWidth) / 2, eyeY - (pupilHeight - eyeHeight) / 2,
                pupilWidth, pupilHeight, pixel, color * 0.25f, 1f);
            DrawGlowRect(canvas, eyeRight + (eyeWidth - pupilWidth) / 2, eyeY - (pupilHeight - eyeHeight) / 2,
                pupilWidth, pupilHeight, pixel, color * 0.25f, 1f);

            // Брови — злые, с наклоном к центру.
            int browThickness = Mathf.Max(1, size / 90 / pixel * pixel);
            DrawGlowLine(canvas,
                new Vector2(eyeLeft - pixel, eyeY + eyeHeight + pixel * 2f),
                new Vector2(eyeLeft + eyeWidth + pixel, eyeY + eyeHeight + pixel * 4f),
                browThickness, pixel, color, 0.95f);
            DrawGlowLine(canvas,
                new Vector2(eyeRight - pixel, eyeY + eyeHeight + pixel * 4f),
                new Vector2(eyeRight + eyeWidth + pixel, eyeY + eyeHeight + pixel * 2f),
                browThickness, pixel, color, 0.95f);

            // Рот — прямоугольник с «зубцами» внутри, как у потрескавшегося экрана.
            int mouthWidth = Mathf.Max(2, size / 5) / pixel * pixel;
            int mouthHeight = Mathf.Max(2, size / 22) / pixel * pixel;
            int mouthX = (size - mouthWidth) / 2 / pixel * pixel;
            int mouthY = Mathf.RoundToInt(size * 0.3f) / pixel * pixel;

            for (int x = 0; x < mouthWidth; x += pixel)
            {
                int toothHeight = Mathf.Max(pixel, (int)(mouthHeight * (0.35f + 0.65f
                    * Mathf.Abs(Mathf.Sin((x / (float)mouthWidth) * Mathf.PI * 3f)))));
                DrawGlowRect(canvas, mouthX + x, mouthY, pixel, toothHeight, pixel, color, 1f);
            }

            // Нос — тонкая вертикальная черта.
            int noseX = (size / 2 / pixel) * pixel;
            DrawGlowRect(canvas, noseX, eyeY - eyeHeight - pixel * 3, pixel, pixel * 2, pixel, color, 0.55f);
        }

        /// <summary>Линия осциллографа: пилообразная волна по всей ширине экрана.</summary>
        private static void DrawWaveform(PbrCanvas canvas, int size, int pixel, Color color)
        {
            int amplitude = Mathf.RoundToInt(size * 0.1f);
            int thickness = Mathf.Max(1, pixel);

            for (int x = 0; x < size; x += pixel)
            {
                float t = x / (float)size;
                float wave = Mathf.Sin(t * Mathf.PI * 4f) * 0.7f
                             + Mathf.Sin(t * Mathf.PI * 11f) * 0.3f;
                int y = Mathf.RoundToInt(size * 0.47f + wave * amplitude);

                for (int w = -thickness; w <= thickness; w++)
                {
                    int index = canvas.Index(x, y + w);
                    float falloff = 1f - Mathf.Abs(w) / (thickness + 1f);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], color, 0.55f + falloff * 0.45f);
                    canvas.AddEmission(x, y + w, color * (0.5f + falloff * 0.9f));
                    canvas.HeightMap[index] = 0.7f;
                }
            }
        }

        private static void DrawGlowRect(PbrCanvas canvas, int x, int y, int width, int height, int pixel, Color color,
            float strength)
        {
            int glow = Mathf.Max(1, pixel * 2);

            for (int j = -glow; j < height + glow; j++)
            {
                for (int i = -glow; i < width + glow; i++)
                {
                    bool inside = i >= 0 && i < width && j >= 0 && j < height;
                    float distance = inside ? 0f : Mathf.Sqrt(
                        Mathf.Max(0, i < 0 ? -i : (i >= width ? i - width + 1 : 0)) * 1f
                        + Mathf.Max(0, j < 0 ? -j : (j >= height ? j - height + 1 : 0)) * 1f);
                    float falloff = Mathf.Clamp01(1f - distance / (glow + pixel));

                    if (falloff <= 0.01f)
                    {
                        continue;
                    }

                    int index = canvas.Index(x + i, y + j);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], color * 0.5f, falloff * strength * 0.6f);
                    canvas.AddEmission(x + i, y + j, color * falloff * strength);
                    canvas.HeightMap[index] = Mathf.Clamp01(0.55f + falloff * 0.3f);
                }
            }
        }

        private static void DrawGlowLine(PbrCanvas canvas, Vector2 from, Vector2 to, int thickness, int pixel,
            Color color, float strength)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(from, to)));

            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
                int baseX = Mathf.RoundToInt(point.x) / pixel * pixel;
                int baseY = Mathf.RoundToInt(point.y) / pixel * pixel;
                DrawGlowRect(canvas, baseX, baseY, pixel, pixel, pixel, color, strength);
            }
        }

        /// <summary>Семисегментный атлас 4x4: 0-9, пусто, минус, пусто, двоеточие, пусто, пусто.</summary>
        private static RetroPbrSet BuildSevenSegmentAtlas(Color color, int cellSize)
        {
            int size = cellSize * 4;
            PbrCanvas canvas = new PbrCanvas(size, size);
            canvas.Fill(new Color(0.02f, 0.02f, 0.025f, 1f));
            canvas.FillHeight(0.35f);
            canvas.FillSmoothness(0.4f);
            canvas.FillMetallic(0.1f);

            for (int cell = 0; cell < SevenSegmentCellCount; cell++)
            {
                int column = cell % 4;
                int row = cell / 4;
                RectInt rect = new RectInt(column * cellSize, row * cellSize, cellSize, cellSize);

                // Разделительная рамка между ячейками, чтобы UV-сэмплинг не замыливал соседей.
                for (int x = rect.x; x < rect.x + rect.width; x++)
                {
                    canvas.SetHeight(x, rect.y, 0.1f);
                    canvas.SetHeight(x, rect.y + rect.height - 1, 0.1f);
                }

                for (int y = rect.y; y < rect.y + rect.height; y++)
                {
                    canvas.SetHeight(rect.x, y, 0.1f);
                    canvas.SetHeight(rect.x + rect.width - 1, y, 0.1f);
                }

                DrawSevenSegment(canvas, rect, cell, color);
            }

            RetroPbrSet set = Bake(canvas, "SevenSegment", 1f, true);
            set.EmissionIntensity = 3.2f;
            return set;
        }

        #endregion

        #region Материалы персонажей и предметов

        /// <summary>Суконная ткань: переплетение нитей, ворс, лёгкая выгорелость.</summary>
        private static RetroPbrSet BuildCloth(Color color, int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            int period = 8;
            float scale = size / (float)period;
            int weavePeriod = 24;
            float weaveScale = size / (float)weavePeriod;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
            {
                    float warp = Mathf.Sin(x / weaveScale * Mathf.PI);
                    float weft = Mathf.Sin(y / weaveScale * Mathf.PI);
                    float weave = (warp * weft) * 0.5f + 0.5f;

                    float u = x / scale;
                    float v = y / scale;
                    float fuzz = Fbm(u * 8f, v * 8f, period * 8, 7717, 3, 2f, 0.5f);
                    float fade = Fbm(u, v, period, 3391, 3, 2f, 0.5f);

                    float shade = 0.72f + weave * 0.34f + (fuzz - 0.5f) * 0.24f + (fade - 0.5f) * 0.2f;
                    int index = y * size + x;
                    canvas.Albedo[index] = color * shade;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.42f + weave * 0.36f + (fuzz - 0.5f) * 0.28f);
                    canvas.Metallic[index] = 0f;
                    canvas.Smoothness[index] = 0.2f + weave * 0.16f;
                }
            }

            return Bake(canvas, "Cloth", 0.28f, true);
        }

        /// <summary>Полированное золото с царапинами и зелёной патиной в углублениях.</summary>
        private static RetroPbrSet BuildGold(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Rand random = new Rand(0x901D);
            Color baseGold = Hex(0xD4A93C);
            Color patina = Hex(0x3E5A43);
            int period = 8;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    float polish = Fbm(u * 2f, v * 2f, period * 2, 6151, 3, 2f, 0.5f);
                    float fine = ValueNoise(u * 20f, v * 20f, period * 20, 2287);
                    float shade = 0.86f + (polish - 0.5f) * 0.3f + (fine - 0.5f) * 0.1f;

                    Color color = baseGold * shade;
                    float patinaAmount = Mathf.Clamp01((polish - 0.62f) * 3.4f);
                    color = Color.Lerp(color, patina, patinaAmount * 0.55f);

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.55f + (polish - 0.5f) * 0.3f + (fine - 0.5f) * 0.1f);
                    canvas.Metallic[index] = Mathf.Lerp(1f, 0.35f, patinaAmount);
                    canvas.Smoothness[index] = Mathf.Lerp(0.82f, 0.34f, patinaAmount);
                }
            }

            AddScratches(canvas, random, size, 60, 0.4f, 0.1f, Hex(0xF0D488), 0.3f, 0.028f);
            return Bake(canvas, "Gold", 0.3f, true);
        }

        /// <summary>Тяжёлая ржавчина: слои корки, выкрашенный металл, шелуха.</summary>
        private static RetroPbrSet BuildHeavyRust(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            int rustPeriod = 5;
            float rustScale = size / (float)rustPeriod;
            int period = 8;
            float scale = size / (float)period;
            Color darkMetal = Hex(0x3A2B1E);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cell = Cellular(x / rustScale, y / rustScale, rustPeriod, 1471);
                    float layers = Fbm(x / scale, y / scale, period, 5519, 5, 2f, 0.55f);
                    float flake = Fbm(x / (scale * 0.3f), y / (scale * 0.3f), period * 3, 8221, 3, 2f, 0.5f);

                    float rust = Mathf.Clamp01(1f - cell * 1.6f) * Mathf.Clamp01(layers * 1.35f + 0.12f);
                    Color rustColor = RustBrown * (0.55f + flake * 0.95f);
                    Color color = Color.Lerp(darkMetal, rustColor, rust);

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.42f + rust * 0.4f + (flake - 0.5f) * 0.24f);
                    canvas.Metallic[index] = Mathf.Lerp(0.75f, 0.05f, rust);
                    canvas.Smoothness[index] = Mathf.Lerp(0.4f, 0.2f, rust);
                }
            }

            return Bake(canvas, "HeavyRust", 0.45f, true);
        }

        /// <summary>Иней: кристаллические грани, холодные тени, блёстки.</summary>
        private static RetroPbrSet BuildFrost(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            int icePeriod = 6;
            float iceScale = size / (float)icePeriod;
            Color iceLight = Hex(0xE6F2FA);
            Color iceDeep = Hex(0x7FA8C4);
            int period = 8;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cell = Cellular(x / iceScale, y / iceScale, icePeriod, 4099);
                    float ridges = Mathf.Clamp01(1f - cell * 2.6f);
                    float grain = Fbm(x / scale, y / scale, period, 6779, 4, 2f, 0.5f);

                    Color color = Color.Lerp(iceDeep, iceLight, Mathf.Clamp01(ridges * 1.2f + grain * 0.4f));
                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.35f + ridges * 0.6f + (grain - 0.5f) * 0.2f);
                    canvas.Metallic[index] = 0f;
                    canvas.Smoothness[index] = 0.55f + ridges * 0.35f;
                }
            }

            return Bake(canvas, "Frost", 0.24f, true);
        }

        /// <summary>Глянцевый пластик манекена: «апельсиновая корка», мелкие царапины.</summary>
        private static RetroPbrSet BuildPlasticSkin(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Rand random = new Rand(0x91A5);
            Color baseSkin = Hex(0xD9A377);
            int period = 8;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float peel = Fbm(x / (scale * 0.45f), y / (scale * 0.45f), period * 3, 2213, 3, 2f, 0.5f);
                    float broad = Fbm(x / scale, y / scale, period, 8467, 3, 2f, 0.5f);
                    float shade = 0.9f + (peel - 0.5f) * 0.12f + (broad - 0.5f) * 0.12f;

                    int index = y * size + x;
                    canvas.Albedo[index] = baseSkin * shade;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (peel - 0.5f) * 0.5f);
                    canvas.Metallic[index] = 0f;
                    canvas.Smoothness[index] = 0.6f + (peel - 0.5f) * 0.2f;
                }
            }

            AddScratches(canvas, random, size, 26, 0.35f, 0.25f, Color.Lerp(baseSkin, Color.white, 0.4f), 0.22f, 0.04f);
            return Bake(canvas, "PlasticSkin", 0.25f, true);
        }

        /// <summary>Кевларовое волокно: диагональный саржевый переплетение.</summary>
        private static RetroPbrSet BuildKevlar(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Color baseColor = Hex(0x2A2C26);
            int weavePeriod = 32;
            float weaveScale = size / (float)weavePeriod;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float warp = Mathf.Sin((x / weaveScale + y / weaveScale) * Mathf.PI);
                    float weft = Mathf.Sin((x / weaveScale - y / weaveScale) * Mathf.PI);
                    float weave = warp * weft;
                    float shade = 0.78f + weave * 0.3f;

                    int index = y * size + x;
                    canvas.Albedo[index] = baseColor * shade;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + weave * 0.4f);
                    canvas.Metallic[index] = 0.05f;
                    canvas.Smoothness[index] = 0.32f + weave * 0.12f;
                }
            }

            return Bake(canvas, "Kevlar", 0.2f, true);
        }

        /// <summary>Мутное оргстекло: царапины, пыль, слабые блики.</summary>
        private static RetroPbrSet BuildSmokedGlass(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Rand random = new Rand(0x9C55);
            Color glass = new Color(0.1f, 0.11f, 0.13f, 1f);
            int period = 8;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float haze = Fbm(x / scale, y / scale, period, 4523, 4, 2f, 0.5f);
                    int index = y * size + x;
                    canvas.Albedo[index] = glass * (0.7f + haze * 0.7f);
                    canvas.HeightMap[index] = 0.5f + (haze - 0.5f) * 0.2f;
                    canvas.Metallic[index] = 0f;
                    canvas.Smoothness[index] = 0.9f + (haze - 0.5f) * 0.1f;
                }
            }

            AddScratches(canvas, random, size, 40, 0.45f, 0.2f, new Color(0.4f, 0.43f, 0.48f), 0.3f, 0.03f);

            for (int i = 0; i < size / 8; i++)
            {
                int x = random.RangeInt(0, size);
                int y = random.RangeInt(0, size);
                int index = canvas.Index(x, y);
                canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], new Color(0.5f, 0.5f, 0.5f), 0.4f);
            }

            return Bake(canvas, "SmokedGlass", 0.4f, true);
        }

        /// <summary>Чёрная вода: рябь, зеркальные блики, ничего не видно в глубине.</summary>
        private static RetroPbrSet BuildBlackWater(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            int period = 8;
            float scale = size / (float)period;
            Color deep = Hex(0x04050A);
            Color sheen = Hex(0x1A2230);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ripple = Fbm(x / (scale * 0.5f), y / (scale * 0.5f), period * 2, 6691, 4, 2.1f, 0.5f);
                    float crest = Mathf.Clamp01(Mathf.Abs(ripple - 0.5f) * 3.2f);

                    int index = y * size + x;
                    canvas.Albedo[index] = Color.Lerp(deep, sheen, crest);
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (ripple - 0.5f) * 0.7f);
                    canvas.Metallic[index] = 0.1f;
                    canvas.Smoothness[index] = 0.96f;
                }
            }

            return Bake(canvas, "BlackWater", 0.6f, true);
        }

        /// <summary>Бронза с зелёной патиной для голографических архипелагов.</summary>
        private static RetroPbrSet BuildBronze(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            int period = 8;
            float scale = size / (float)period;
            Color bronze = Hex(0x8C6A3F);
            Color patina = Hex(0x3F7A63);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coarse = Fbm(x / scale, y / scale, period, 3851, 4, 2f, 0.55f);
                    float fine = Fbm(x / (scale * 0.3f), y / (scale * 0.3f), period * 3, 9973, 2, 2f, 0.5f);
                    float patinaAmount = Mathf.Clamp01((coarse * 0.7f + fine * 0.3f - 0.52f) * 3.2f);

                    int index = y * size + x;
                    canvas.Albedo[index] = Color.Lerp(bronze * (0.82f + fine * 0.4f), patina, patinaAmount * 0.7f);
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (fine - 0.5f) * 0.35f + patinaAmount * 0.15f);
                    canvas.Metallic[index] = Mathf.Lerp(0.95f, 0.2f, patinaAmount);
                    canvas.Smoothness[index] = Mathf.Lerp(0.55f, 0.3f, patinaAmount);
                }
            }

            return Bake(canvas, "Bronze", 0.25f, true);
        }

        /// <summary>Однотонная неоновая поверхность с мягким градиентом к краям.</summary>
        private static RetroPbrSet BuildNeon(Color color, int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float edge = Mathf.Clamp01(Mathf.Min(u, 1f - u) * Mathf.Min(v, 1f - v) * 6f);
                    float intensity = 0.65f + edge * 0.35f;

                    int index = y * size + x;
                    canvas.Albedo[index] = color * 0.25f;
                    canvas.Emission[index] = color * intensity;
                    canvas.HeightMap[index] = 0.5f;
                    canvas.Smoothness[index] = 0.6f;
                    canvas.Metallic[index] = 0f;
                }
            }

            RetroPbrSet set = Bake(canvas, "Neon", 1f, false);
            set.EmissionIntensity = 2.8f;
            return set;
        }

        /// <summary>Плоская заливка: стекло линз, мелкие пластиковые детали.</summary>
        private static RetroPbrSet BuildFlatColor(Color color, int size, float smoothness, float metallic)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            canvas.Fill(color);
            canvas.FillHeight(0.5f);
            canvas.FillSmoothness(smoothness);
            canvas.FillMetallic(metallic);
            return Bake(canvas, "Flat", 1f, false);
        }

        /// <summary>
        /// Карта грязи/царапин, которую шейдеры CrispRetroLit и WireframeHologram читают
        /// как глобальную текстуру. Упаковка каналов:
        ///   R — маска потёртостей (гасит блик и затемняет альбедо),
        ///   G — мелкий шум (мерцание и «шум развёртки» голограмм),
        ///   B — крупные пятна (масляные разводы и масштабные пятна ржавчины).
        /// Картинка тайлится бесшовно: все октавы шума берутся с сохранением периода.
        /// </summary>
        private static RetroPbrSet BuildRetroGrunge(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            int period = 8;
            float scale = size / (float)period;
            int blotchPeriod = 4;
            float blotchScale = size / (float)blotchPeriod;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    // Потёртости: крупная клеточная сетка, подъеденная фрактальным шумом.
                    float cell = Cellular(u, v, period, 7717);
                    float grungeCoarse = Fbm(u, v, period, 2029, 5, 2f, 0.5f);
                    float wear = Mathf.Clamp01(1f - (cell - 0.2f) * 2.6f) * Mathf.Clamp01(grungeCoarse * 1.6f - 0.25f);

                    // Мелкий шум для мерцания и дробления эмиссии.
                    float grain = Fbm(u * 6f, v * 6f, period * 6, 5519, 3, 2f, 0.6f);

                    // Крупные масляные разводы: самые большие пятна на всей карте.
                    float blotchCell = Cellular(x / blotchScale, y / blotchScale, blotchPeriod, 9311);
                    float blotch = Mathf.Clamp01(1f - (blotchCell - 0.12f) * 2.2f);

                    int index = y * size + x;

                    // Упаковка в цвет: шейдеры читают каналы по отдельности.
                    canvas.Albedo[index] = new Color(wear, grain, blotch, 1f);
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (wear - 0.5f) * 0.35f);
                    canvas.Emission[index] = Color.black;
                    canvas.Smoothness[index] = 0.5f;
                    canvas.Metallic[index] = 0f;
                }
            }

            // Царапины проступают только в R-канале (маска потёртостей): общий AddScratches
            // красит все каналы сразу, а здесь G и B должны остаться нетронутыми.
            AddWearStrokes(canvas, new Rand(0x6A17), size, 30);

            return Bake(canvas, "RetroGrunge", 1f, false);
        }

        /// <summary>
        /// Короткие царапины, снижающие маску грязи в R-канале: поцарапанный металл
        /// в этих полосах чище и блестит сильнее — именно так он выглядит на концпт-артах.
        /// </summary>
        private static void AddWearStrokes(PbrCanvas canvas, Rand random, int size, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 start = new Vector2(random.Range(0f, size), random.Range(0f, size));
                float angle = random.Range(0f, Mathf.PI * 2f);
                float length = random.Range(size * 0.03f, size * 0.18f);
                Vector2 end = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                float width = random.Range(0.5f, 1.6f);

                Vector2 delta = end - start;
                float len = delta.magnitude;

                if (len < 0.5f)
                {
                    continue;
                }

                Vector2 direction = delta / len;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                int steps = Mathf.CeilToInt(len) + 1;

                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector2 point = start + delta * t;
                    float taper = Mathf.Sin(t * Mathf.PI);

                    for (int w = -(int)width; w <= (int)width; w++)
                    {
                        float offset = w;
                        int x = Mathf.RoundToInt(point.x + perpendicular.x * offset);
                        int y = Mathf.RoundToInt(point.y + perpendicular.y * offset);
                        float falloff = 1f - Mathf.Abs(offset) / (width + 0.5f);
                        float strength = 0.75f * taper * falloff;

                        if (strength <= 0.001f)
                        {
                            continue;
                        }

                        int index = canvas.Index(x, y);
                        Color pixel = canvas.Albedo[index];
                        pixel.r = Mathf.Lerp(pixel.r, 0f, strength);
                        canvas.Albedo[index] = pixel;
                    }
                }
            }
        }

        #endregion

        #region Бункер: новые поверхности

        /// <summary>
        /// Столешница пульта и Круглого стола саммита: тусклая оружейная сталь строго
        /// по формуле. n1 — крупный шум (период 20 клеток, ~25 px особенности),
        /// n2 — мелкая структура (период 92), fine — микрозерно (период 333):
        ///   steel = Lerp((0.11,0.12,0.13),(0.18,0.20,0.21), n1*0.7 + fine*0.3);
        ///   ржавчина (0.28,0.15,0.08) тянется через SmoothStep(0.58,0.72, n1*0.6 + n2*0.4).
        /// Никаких царапин и камуфляжных пятен: весь рисунок задаёт формула.
        /// </summary>
        private static RetroPbrSet BuildBunkerConsoleTop(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Color rust = new Color(0.28f, 0.15f, 0.08f, 1f);
            Color steelDark = new Color(0.11f, 0.12f, 0.13f, 1f);
            Color steelLight = new Color(0.18f, 0.20f, 0.21f, 1f);

            float n1Scale = size / 20f;
            float n2Scale = size / 92f;
            float fineScale = size / 333f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n1 = ValueNoise(x / n1Scale, y / n1Scale, 20, 8123);
                    float n2 = ValueNoise(x / n2Scale, y / n2Scale, 92, 4471);
                    float fine = ValueNoise(x / fineScale, y / fineScale, 333, 5519);

                    Color steel = Color.Lerp(steelDark, steelLight, n1 * 0.7f + fine * 0.3f);

                    float rustAmount = Mathf.SmoothStep(0.58f, 0.72f, n1 * 0.6f + n2 * 0.4f);
                    Color color = Color.Lerp(steel, rust, rustAmount);

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (n1 - 0.5f) * 0.25f + (fine - 0.5f) * 0.1f);
                    canvas.Smoothness[index] = Mathf.Lerp(0.55f, 0.2f, rustAmount);
                    canvas.Metallic[index] = Mathf.Lerp(0.3f, 0.12f, rustAmount);
                }
            }

            return Bake(canvas, "DeskSteelRust", 1f, false);
        }

        /// <summary>
        /// Корпус ЭЛТ-монитора: светло-серый потёртый металл #6E736B с сеткой трещин
        /// на лаке. Трещины темнее и глубже — по ним снимается гладкость.
        /// </summary>
        private static RetroPbrSet BuildCrackedConsoleShell(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Rand random = new Rand(0x9A31);
            int period = 8;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    float grain = Fbm(u * 3f, v * 3f, period * 3, 7717, 4, 2f, 0.5f);
                    float blotch = Fbm(u, v, period, 3313, 3, 2f, 0.55f);

                    // Пятна лака, сошедшего до серого металла.
                    float wear = Mathf.Clamp01(1f - (blotch - 0.42f) * 3.4f);

                    Color color = Color.Lerp(CrackedShellBase, ConcreteLightBase, grain * 0.35f);
                    color = Color.Lerp(color, Color.Lerp(CrackedShellBase, InkBlack, 0.55f), wear * 0.6f);

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (grain - 0.5f) * 0.2f - wear * 0.1f);
                    canvas.Smoothness[index] = Mathf.Lerp(0.42f, 0.24f, wear);
                    canvas.Metallic[index] = Mathf.Lerp(0.2f, 0.15f, wear);
                }
            }

            // Сетка трещин: короткие ломаные штрихи, сходящиеся в узлах.
            int cracks = 26;
            Color crackInk = Color.Lerp(InkBlack, ConcreteLightBase, 0.3f);

            for (int i = 0; i < cracks; i++)
            {
                Vector2 point = new Vector2(random.Range(0f, size), random.Range(0f, size));
                float heading = random.Range(0f, Mathf.PI * 2f);
                int segments = random.RangeInt(4, 9);
                Vector2[] path = new Vector2[segments];

                for (int s = 0; s < segments; s++)
                {
                    path[s] = point;
                    point += new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * random.Range(0.06f, 0.16f) * size;
                    heading += random.Range(-0.7f, 0.7f);
                }

                for (int s = 0; s < segments - 1; s++)
                {
                    StrokeLine(canvas, path[s], path[s + 1], 0.7f, 0.35f, crackInk, 0.7f);
                }
            }

            AddWearSpeckles(canvas, random, size, 420, 0.6f, 0.35f);

            return Bake(canvas, "CrackedConsoleShell", 1f, false);
        }

        /// <summary>
        /// Экран терминала «CITY STATUS» 512x384, FilterMode.Point — раскладка строго
        /// по макету. Фон #031208, яркий фосфор #00E676, тусклый #008844.
        /// Двойная рамка с отступами 10 и 14 px; вертикальный разделитель при x = 185.
        /// Слева (x 20..175): вверху y 200..360 шесть пунктирных строк толщиной 6 px,
        /// внизу y 24..180 рамка таблицы с восемью индикаторными строками.
        /// Справа (x 195..492): вверху y 335..364 рамка заголовка «CITY STATUS» (шрифт 5x7),
        /// в середине y 95..325 радар x 205..482 — сетка 24 px, три концентрических круга
        /// центра (343,210), контуры кварталов, четыре яркие метки городов 8x8;
        /// внизу y 24..85 рамка лога из трёх строк. Эмиссия повторяет рисунок.
        /// </summary>
        private static RetroPbrSet BuildCityStatusScreen(int size)
        {
            // Текстура прямоугольная: размер задаётся по ширине, высота — 3/4 ширины.
            int width = Mathf.Max(128, size);
            int height = width * 3 / 4;

            PbrCanvas canvas = new PbrCanvas(width, height);
            Color glow = OscilloscopeGreen;
            Color mid = ScreenMid;

            canvas.Fill(ScreenBase);
            canvas.FillHeight(0.5f);
            canvas.FillSmoothness(0.42f);
            canvas.FillMetallic(0f);

            // Вся вёрстка считается в координатах макета 512x384; 1 unit = 1 пиксель макета.
            float unit = width / 512f;

            // --- Двойная рамка: контуры с отступами 10 и 14 px ---
            StrokeRect(canvas, 10f * unit, 10f * unit, width - 10f * unit, height - 10f * unit, unit, glow, 0.9f);
            StrokeRect(canvas, 14f * unit, 14f * unit, width - 14f * unit, height - 14f * unit, unit, mid, 0.7f);

            // --- Вертикальный разделитель между колонками ---
            float dividerX = 185f * unit;
            DrawGlowLine(canvas, new Vector2(dividerX, 14f * unit), new Vector2(dividerX, height - 14f * unit),
                1, 1, mid, 0.8f);

            // --- Левая колонка x 20..175 ---
            float leftX = 20f * unit;
            float leftXEnd = 175f * unit;

            // Сепаратор между верхней и нижней зонами.
            DrawGlowLine(canvas, new Vector2(leftX, 190f * unit), new Vector2(leftXEnd, 190f * unit),
                1, 1, mid, 0.75f);

            // Шесть пунктирных строк толщиной 6 px в зоне y 200..360.
            int dashThickness = Mathf.Max(1, Mathf.RoundToInt(6f * unit));

            for (int row = 0; row < 6; row++)
            {
                float y = (200f + row * 32f) * unit;

                for (float x = leftX; x < leftXEnd - 1f; x += 16f * unit)
                {
                    float xEnd = Mathf.Min(x + 10f * unit, leftXEnd);
                    DrawGlowLine(canvas, new Vector2(x, y), new Vector2(xEnd, y), dashThickness, 1, glow, 0.7f);
                }
            }

            // Рамка таблицы y 24..180 и восемь индикаторных строк.
            StrokeRect(canvas, leftX, 24f * unit, leftXEnd, 180f * unit, unit, mid, 0.7f);
            float trackX0 = 28f * unit;
            float trackX1 = leftXEnd - 8f * unit;
            int barThickness = Mathf.Max(1, Mathf.RoundToInt(9f * unit));

            for (int row = 0; row < 8; row++)
            {
                float level = 0.25f + ((row * 37) % 61) / 61f * 0.7f;
                float barY = (40f + row * 17f) * unit;
                float fillX1 = trackX0 + (trackX1 - trackX0) * level;

                // Тусклый трек строки, поверх — яркий закрашенный сегмент.
                DrawGlowLine(canvas, new Vector2(trackX0, barY), new Vector2(trackX1, barY), 1, 1, mid * 0.6f, 0.45f);
                DrawGlowLine(canvas, new Vector2(trackX0, barY), new Vector2(fillX1, barY), barThickness, 1, glow,
                    0.95f);
            }

            // --- Правая колонка x 195..492 ---
            float rightX = 195f * unit;
            float rightXEnd = 492f * unit;

            // Рамка заголовка y 335..364 и заголовок шрифтом 5x7.
            StrokeRect(canvas, rightX, 335f * unit, rightXEnd, 364f * unit, unit, glow, 0.9f);
            int titleScale = Mathf.Max(1, Mathf.RoundToInt(3f * unit));
            int titleWidth = MeasureText("CITY STATUS", titleScale);
            DrawText(canvas,
                new Vector2((rightX + rightXEnd - titleWidth) * 0.5f, 339f * unit),
                "CITY STATUS", titleScale, glow, 1f);

            // --- Радар x 205..482, y 95..325 ---
            float mapX0 = 205f * unit;
            float mapX1 = 482f * unit;
            float mapY0 = 95f * unit;
            float mapY1 = 325f * unit;

            // Сетка шагом 24 px.
            float gridStep = 24f * unit;

            for (float gx = mapX0 + gridStep; gx < mapX1 - 1f; gx += gridStep)
            {
                DrawGlowLine(canvas, new Vector2(gx, mapY0), new Vector2(gx, mapY1), 1, 1, mid, 0.3f);
            }

            for (float gy = mapY0 + gridStep; gy < mapY1 - 1f; gy += gridStep)
            {
                DrawGlowLine(canvas, new Vector2(mapX0, gy), new Vector2(mapX1, gy), 1, 1, mid, 0.3f);
            }

            // Три концентрических круга радара с центром (343,210).
            float radarCx = 343f * unit;
            float radarCy = 210f * unit;

            for (int ring = 0; ring < 3; ring++)
            {
                float radius = (110f - ring * 35f) * unit;
                int steps = 40 + ring * 12;

                for (int i = 0; i < steps; i++)
                {
                    float a0 = i / (float)steps * Mathf.PI * 2f;
                    float a1 = (i + 1) / (float)steps * Mathf.PI * 2f;

                    DrawGlowLine(canvas,
                        new Vector2(radarCx + Mathf.Cos(a0) * radius, radarCy + Mathf.Sin(a0) * radius),
                        new Vector2(radarCx + Mathf.Cos(a1) * radius, radarCy + Mathf.Sin(a1) * radius),
                        1, 1, mid, 0.55f);
                }
            }

            // Контуры кварталов города — только рамки, без заливки.
            Rand blocks = new Rand(0x5A71);

            for (int i = 0; i < 20; i++)
            {
                float angle = blocks.Range(0f, Mathf.PI * 2f);
                float distance = blocks.Range(0.12f, 0.88f) * (110f * unit);
                int blockX = Mathf.RoundToInt(radarCx + Mathf.Cos(angle) * distance);
                int blockY = Mathf.RoundToInt(radarCy + Mathf.Sin(angle) * distance);
                int blockW = blocks.RangeInt(8, 26) * Mathf.Max(1, Mathf.RoundToInt(unit));
                int blockH = blocks.RangeInt(6, 18) * Mathf.Max(1, Mathf.RoundToInt(unit));

                StrokeRect(canvas, blockX, blockY, blockX + blockW, blockY + blockH, unit, mid, 0.5f);
            }

            // Четыре яркие метки городов — блоки 8x8 px.
            Rand dots = new Rand(0xC17D);

            for (int i = 0; i < 4; i++)
            {
                float angle = i / 4f * Mathf.PI * 2f + 0.6f;
                float distance = dots.Range(0.4f, 0.85f) * (110f * unit);
                int dotX = Mathf.RoundToInt(radarCx + Mathf.Cos(angle) * distance);
                int dotY = Mathf.RoundToInt(radarCy + Mathf.Sin(angle) * distance);
                int dotSize = Mathf.Max(4, Mathf.RoundToInt(8f * unit));

                DrawGlowRect(canvas, dotX - dotSize / 2, dotY - dotSize / 2, dotSize, dotSize, 1, glow, 1f);
            }

            // --- Рамка лога y 24..85 из трёх строк ---
            StrokeRect(canvas, rightX, 24f * unit, rightXEnd, 85f * unit, unit, glow, 0.9f);
            int logScale = Mathf.Max(1, Mathf.RoundToInt(2f * unit));
            string[] logRows =
            {
                "> LINK ESTABLISHED",
                "> NO ANOMALIES",
                "> AWAITING ORDER"
            };

            for (int i = 0; i < logRows.Length; i++)
            {
                DrawText(canvas, new Vector2((rightX + 10f) * unit, (30f + i * 16f) * unit),
                    logRows[i], logScale, glow, 0.9f);
            }

            // Развёртка: горизонтальные тёмные полосы, как у реального ЭЛТ.
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    canvas.Albedo[index] = canvas.Albedo[index] * 0.82f;
                    canvas.Emission[index] = canvas.Emission[index] * 0.82f;
                }
            }

            // Эмиссия повторяет рисунок, иначе экран останется тёмным.
            for (int i = 0; i < canvas.Albedo.Length; i++)
            {
                canvas.Emission[i] = Color.Lerp(canvas.Emission[i], canvas.Albedo[i], 0.75f);
            }

            RetroPbrSet set = Bake(canvas, "CityStatusScreen", 1f, false);
            set.EmissionIntensity = 2.6f;

            // Почти чёрный тон экрана: весь цвет приходит из эмиссии. Светлая лампа
            // над столом иначе превращала CITY STATUS в белый квадрат.
            set.Tint = ScreenSurfaceColor;

            // Точечная фильтрация: сетка радара и пиксельные буквы должны остаться
            // резкими, с bilinear-фильтром экран превращался в кашу.
            set.Albedo.filterMode = FilterMode.Point;

            if (set.Emission != null)
            {
                set.Emission.filterMode = FilterMode.Point;
            }

            return set;
        }

        /// <summary>Прямоугольная рамка из четырёх светящихся линий.</summary>
        private static void StrokeRect(PbrCanvas canvas, float x0, float y0, float x1, float y1, float unit,
            Color color, float strength)
        {
            int thickness = Mathf.Max(1, Mathf.RoundToInt(unit));
            DrawGlowLine(canvas, new Vector2(x0, y0), new Vector2(x1, y0), thickness, 1, color, strength);
            DrawGlowLine(canvas, new Vector2(x0, y1), new Vector2(x1, y1), thickness, 1, color, strength);
            DrawGlowLine(canvas, new Vector2(x0, y0), new Vector2(x0, y1), thickness, 1, color, strength);
            DrawGlowLine(canvas, new Vector2(x1, y0), new Vector2(x1, y1), thickness, 1, color, strength);
        }

        #endregion

        #region Общие приёмы

        /// <summary>
        /// Разбрасывает царапины: короткие штрихи с лёгким высветлением цвета.
        /// widthScale/ tintStrength подбираются под материал: на бетоне царапин почти нет,
        /// на стали их десятки.
        /// </summary>
        private static void AddScratches(PbrCanvas canvas, Rand random, int size, int count, float lengthScale,
            float tintStrength, Color tint, float depth, float widthScale)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 start = new Vector2(random.Range(0f, size), random.Range(0f, size));
                float angle = random.Range(0f, Mathf.PI * 2f);
                float length = random.Range(size * 0.02f, size * 0.16f) * lengthScale + size * 0.01f;
                Vector2 end = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                float width = random.Range(0.4f, 1.3f) + widthScale * size * 0.004f;

                StrokeLine(canvas, start, end, width, depth, tint, tintStrength);
            }
        }

        /// <summary>Светлые потёртости на самых выпуклых местах — следы ладони и кромок.</summary>
        private static void AddWearSpeckles(PbrCanvas canvas, Rand random, int size, int count, float sizeScale,
            float tintStrength)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 center = new Vector2(random.Range(0f, size), random.Range(0f, size));
                float radius = random.Range(0.6f, 2.4f) + size * 0.003f * sizeScale;

                for (int y = Mathf.RoundToInt(center.y - radius); y <= center.y + radius; y++)
                {
                    for (int x = Mathf.RoundToInt(center.x - radius); x <= center.x + radius; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);

                        if (distance > radius)
                        {
                            continue;
                        }

                        float falloff = 1f - distance / radius;
                        int index = canvas.Index(x, y);
                        canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index],
                            Color.Lerp(canvas.Albedo[index], Color.white, 0.6f), falloff * tintStrength);
                        canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] - falloff * 0.08f);
                    }
                }
            }
        }

        #endregion
    }
}
