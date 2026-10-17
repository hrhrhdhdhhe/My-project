// World Crisis | Слой: Core | Ответственный: OpenCode
// Реализация процедурной отрисовки PBR-текстур: холст, тайлящийся шум, царапины, потёртости,
// растровый шрифт 5x7, семисегментный атлас и все конкретные поверхности Art Bible.

using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    public static partial class RetroPbrTextureGenerator
    {
        /// <summary>
        /// Холст поверхности: цвет, карта высот, гладкость, металличность, свечение.
        /// Всё в 0..1, перевод в Texture2D делает Bake.
        /// </summary>
        private sealed class PbrCanvas
        {
            public readonly int Width;
            public readonly int PixelHeight;
            public readonly Color[] Albedo;
            public readonly Color[] Emission;
            public readonly float[] HeightMap;
            public readonly float[] Smoothness;
            public readonly float[] Metallic;

            public PbrCanvas(int width, int height)
            {
                Width = Mathf.Max(4, width);
                PixelHeight = Mathf.Max(4, height);
                int count = Width * PixelHeight;

                Albedo = new Color[count];
                Emission = new Color[count];
                HeightMap = new float[count];
                Smoothness = new float[count];
                Metallic = new float[count];

                for (int i = 0; i < count; i++)
                {
                    Emission[i] = Color.black;
                }
            }

            public int Index(int x, int y)
            {
                int wrappedX = x < 0 ? x + Width : (x >= Width ? x - Width : x);
                int wrappedY = y < 0 ? y + PixelHeight : (y >= PixelHeight ? y - PixelHeight : y);
                return wrappedY * Width + wrappedX;
            }

            public void Set(int x, int y, Color color)
            {
                Albedo[Index(x, y)] = color;
            }

            public Color Get(int x, int y)
            {
                return Albedo[Index(x, y)];
            }

            public void AddHeight(int x, int y, float value)
            {
                int index = Index(x, y);
                HeightMap[index] = Mathf.Clamp01(HeightMap[index] + value);
            }

            public void SetHeight(int x, int y, float value)
            {
                HeightMap[Index(x, y)] = Mathf.Clamp01(value);
            }

            public void AddEmission(int x, int y, Color color)
            {
                int index = Index(x, y);
                Emission[index] += color;
            }

            public void SetSmoothness(int x, int y, float value)
            {
                Smoothness[Index(x, y)] = Mathf.Clamp01(value);
            }

            public void SetMetallic(int x, int y, float value)
            {
                Metallic[Index(x, y)] = Mathf.Clamp01(value);
            }

            /// <summary>Заполняет весь холст сплошным цветом.</summary>
            public void Fill(Color color)
            {
                for (int i = 0; i < Albedo.Length; i++)
                {
                    Albedo[i] = color;
                }
            }

            public void FillHeight(float value)
            {
                for (int i = 0; i < HeightMap.Length; i++)
                {
                    HeightMap[i] = value;
                }
            }

            public void FillSmoothness(float value)
            {
                for (int i = 0; i < Smoothness.Length; i++)
                {
                    Smoothness[i] = value;
                }
            }

            public void FillMetallic(float value)
            {
                for (int i = 0; i < Metallic.Length; i++)
                {
                    Metallic[i] = value;
                }
            }
        }

        #region Холст -> текстуры

        /// <summary>Запекает холст в PBR-комплект: albedo, normal (Sobel), metallic/smoothness, emission.</summary>
        private static RetroPbrSet Bake(PbrCanvas canvas, string name, float metersPerTile, bool buildNormal)
        {
            RetroPbrSet set = new RetroPbrSet();
            set.MetersPerTile = metersPerTile;

            Texture2D albedo = NewTexture(canvas.Width, canvas.PixelHeight, false);
            albedo.SetPixels(canvas.Albedo);
            albedo.Apply(false);
            albedo.name = name + "_Albedo";
            set.Albedo = albedo;

            Texture2D metallic = NewTexture(canvas.Width, canvas.PixelHeight, true);
            Color[] packed = new Color[canvas.Albedo.Length];

            for (int i = 0; i < packed.Length; i++)
            {
                // URP-Lit (Metallic workflow): R — металличность, A — гладкость.
                packed[i] = new Color(canvas.Metallic[i], 0f, 0f, canvas.Smoothness[i]);
            }

            metallic.SetPixels(packed);
            metallic.Apply(false);
            metallic.name = name + "_MetallicSmoothness";
            set.MetallicSmoothness = metallic;

            if (buildNormal)
            {
                Texture2D normal = NewTexture(canvas.Width, canvas.PixelHeight, true);
                normal.SetPixels(HeightToNormal(canvas));
                normal.Apply(false);
                normal.name = name + "_Normal";
                set.Normal = normal;
            }

            bool hasEmission = false;
            for (int i = 0; i < canvas.Emission.Length; i++)
            {
                if (canvas.Emission[i].maxColorComponent > 0.002f)
                {
                    hasEmission = true;
                    break;
                }
            }

            if (hasEmission)
            {
                Texture2D emission = NewTexture(canvas.Width, canvas.PixelHeight, false);

                for (int i = 0; i < canvas.Emission.Length; i++)
                {
                    emission.SetPixel(i % canvas.Width, i / canvas.Width, canvas.Emission[i]);
                }

                emission.Apply(false);
                emission.name = name + "_Emission";
                set.Emission = emission;
            }

            return set;
        }

        private static Texture2D NewTexture(int width, int height, bool linear)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true, linear);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;
            return texture;
        }

        /// <summary>Карта нормалей из карты высот: центральные разности + нормализация.</summary>
        private static Color[] HeightToNormal(PbrCanvas canvas)
        {
            int width = canvas.Width;
            int height = canvas.PixelHeight;
            Color[] result = new Color[width * height];
            float strength = 3.2f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float left = canvas.HeightMap[canvas.Index(x - 1, y)];
                    float right = canvas.HeightMap[canvas.Index(x + 1, y)];
                    float down = canvas.HeightMap[canvas.Index(x, y - 1)];
                    float up = canvas.HeightMap[canvas.Index(x, y + 1)];

                    Vector3 normal = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                    result[y * width + x] = new Color(
                        Mathf.Clamp01(normal.x * 0.5f + 0.5f),
                        Mathf.Clamp01(normal.y * 0.5f + 0.5f),
                        Mathf.Clamp01(normal.z * 0.5f + 0.5f),
                        1f);
                }
            }

            return result;
        }

        #endregion

        #region Шум

        /// <summary>Детерминированный хеш решётки: одинаково на всех платформах и запусках.</summary>
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = seed;
                h = h * 73856093 ^ x * 19349663 ^ y * 83492791;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
            }
        }

        private static int Mod(int value, int period)
        {
            int result = value % period;
            return result < 0 ? result + period : result;
        }

        /// <summary>
        /// Тайлящийся value-шум: решётка повторяется каждые period клеток,
        /// поэтому текстура бесшовна при тайлинге.
        /// </summary>
        private static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            int ix0 = Mod(x0, period);
            int iy0 = Mod(y0, period);
            int ix1 = Mod(x0 + 1, period);
            int iy1 = Mod(y0 + 1, period);

            float u = fx * fx * (3f - 2f * fx);
            float v = fy * fy * (3f - 2f * fy);

            float a = Hash(ix0, iy0, seed);
            float b = Hash(ix1, iy0, seed);
            float c = Hash(ix0, iy1, seed);
            float d = Hash(ix1, iy1, seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Фрактальный шум с сохранением тайлинга на каждом октаве.</summary>
        private static float Fbm(float x, float y, int period, int seed, int octaves, float lacunarity, float gain)
        {
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;
            int currentPeriod = period;

            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x, y, currentPeriod, seed + i * 131) * amplitude;
                total += amplitude;
                amplitude *= gain;
                x *= lacunarity;
                y *= lacunarity;
                currentPeriod *= Mathf.Max(2, Mathf.RoundToInt(lacunarity));
            }

            return total > 0.0001f ? sum / total : 0f;
        }

        /// <summary>
        /// Тайлящийся клеточный шум (Worley): расстояние до ближайшей точки сетки.
        /// Используется для ржавых пятен и ледяных граней.
        /// </summary>
        private static float Cellular(float x, float y, int period, int seed)
        {
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            float best = 8f;

            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox;
                    int gy = cy + oy;
                    int wrappedX = Mod(gx, period);
                    int wrappedY = Mod(gy, period);

                    float px = gx + Hash(wrappedX, wrappedY, seed);
                    float py = gy + Hash(wrappedX, wrappedY, seed + 7717);
                    float dx = px - x;
                    float dy = py - y;
                    float distance = dx * dx + dy * dy;

                    if (distance < best)
                    {
                        best = distance;
                    }
                }
            }

            return Mathf.Sqrt(best);
        }

        #endregion

        #region Кисти

        /// <summary>Тонкая царапина: короткий штрих с затуханием по краям.</summary>
        private static void StrokeLine(PbrCanvas canvas, Vector2 start, Vector2 end, float width, float depth,
            Color tint, float tintStrength)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;

            if (length < 0.5f)
            {
                return;
            }

            Vector2 direction = delta / length;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            int steps = Mathf.CeilToInt(length) + 1;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 point = start + delta * t;
                float taper = Mathf.Sin(t * Mathf.PI);

                for (int w = -(int)width; w <= (int)width; w++)
                {
                    float offset = w;
                    int x = Mathf.RoundToInt(point.x + perpendicular.x * offset);
                    int y = Mathf.RoundToInt(point.y + perpendicular.y * offset);
                    float falloff = 1f - Mathf.Abs(offset) / (width + 0.5f);

                    int index = canvas.Index(x, y);
                    canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] - depth * taper * falloff);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], tint, tintStrength * taper * falloff);
                }
            }
        }

        /// <summary>Круглая потёртость — след пальца или вытертое пятно.</summary>
        private static void SmudgeCircle(PbrCanvas canvas, Vector2 center, float radius, float strength, Color tint)
        {
            int minX = Mathf.FloorToInt(center.x - radius);
            int maxX = Mathf.CeilToInt(center.x + radius);
            int minY = Mathf.FloorToInt(center.y - radius);
            int maxY = Mathf.CeilToInt(center.y + radius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);

                    if (distance > radius)
                    {
                        continue;
                    }

                    float falloff = 1f - distance / radius;
                    int index = canvas.Index(x, y);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], tint, strength * falloff * falloff);
                    canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] + strength * 0.12f * falloff);
                }
            }
        }

        /// <summary>Мягкое пятно без высоты — ржавчина, подтёк, затенение.</summary>
        private static void Stain(PbrCanvas canvas, Vector2 center, float radius, float strength, Color tint, float wobble)
        {
            int minX = Mathf.FloorToInt(center.x - radius);
            int maxX = Mathf.CeilToInt(center.x + radius);
            int minY = Mathf.FloorToInt(center.y - radius);
            int maxY = Mathf.CeilToInt(center.y + radius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 offset = new Vector2(x - center.x, y - center.y);
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float distance = offset.magnitude;
                    float edge = radius * (1f + wobble * (ValueNoise(
                        Mathf.Cos(angle) * 2f + 5f, Mathf.Sin(angle) * 2f + 5f, 8, 9111) - 0.5f) * 0.6f);

                    if (distance > edge)
                    {
                        continue;
                    }

                    float falloff = 1f - distance / Mathf.Max(0.001f, edge);
                    int index = canvas.Index(x, y);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], tint, strength * falloff * falloff);
                }
            }
        }

        #endregion

        #region Растровый шрифт 5x7

        private const string FontChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -./:%#+*(),";

        // 7 строк на символ, 5 бит на строку, старший бит — левая колонка.
        private static readonly int[] FontGlyphs =
        {
            0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11, // A
            0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E, // B
            0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E, // C
            0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E, // D
            0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F, // E
            0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10, // F
            0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F, // G
            0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11, // H
            0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E, // I
            0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C, // J
            0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11, // K
            0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F, // L
            0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11, // M
            0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11, // N
            0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E, // O
            0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10, // P
            0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D, // Q
            0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11, // R
            0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E, // S
            0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, // T
            0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E, // U
            0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04, // V
            0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11, // W
            0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11, // X
            0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04, // Y
            0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F, // Z
            0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E, // 0
            0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E, // 1
            0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F, // 2
            0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E, // 3
            0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02, // 4
            0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E, // 5
            0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E, // 6
            0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08, // 7
            0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E, // 8
            0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C, // 9
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // пробел
            0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00, // -
            0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C, // .
            0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10, // /
            0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00, // :
            0x19, 0x1A, 0x02, 0x04, 0x08, 0x0B, 0x13, // %
            0x0A, 0x0A, 0x1F, 0x0A, 0x1F, 0x0A, 0x0A, // #
            0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00, // +
            0x00, 0x15, 0x0E, 0x1F, 0x0E, 0x15, 0x00, // *
            0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02, // (
            0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08, // )
            0x00, 0x00, 0x00, 0x00, 0x0C, 0x04, 0x08  // ,
        };

        private static int FontGlyphIndex(char character)
        {
            char upper = char.ToUpperInvariant(character);

            for (int i = 0; i < FontChars.Length; i++)
            {
                if (FontChars[i] == upper)
                {
                    return i;
                }
            }

            return FontChars.Length - 5; // неизвестный символ рисуем как прочерк
        }

        /// <summary>
        /// Рисует строку растровым шрифтом. scale — размер пикселя одного «бита» шрифта.
        /// Возвращает ширину нарисованной строки в пикселях.
        /// </summary>
        private static int DrawText(PbrCanvas canvas, Vector2 origin, string text, int scale, Color color,
            float inkStrength)
        {
            int penX = Mathf.RoundToInt(origin.x);
            int top = Mathf.RoundToInt(origin.y);

            for (int i = 0; i < text.Length; i++)
            {
                int glyph = FontGlyphIndex(text[i]);
                int advance = 6 * scale;

                if (text[i] != ' ')
                {
                    for (int row = 0; row < 7; row++)
                    {
                        int bits = FontGlyphs[glyph * 7 + row];

                        for (int column = 0; column < 5; column++)
                        {
                            if ((bits & (1 << (4 - column))) == 0)
                            {
                                continue;
                            }

                            for (int sy = 0; sy < scale; sy++)
                            {
                                for (int sx = 0; sx < scale; sx++)
                                {
                                    int x = penX + column * scale + sx;
                                    int y = top + row * scale + sy;
                                    int index = canvas.Index(x, y);
                                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], color, inkStrength);
                                    canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] - 0.12f * inkStrength);
                                }
                            }
                        }
                    }
                }

                penX += advance;
            }

            return penX - Mathf.RoundToInt(origin.x);
        }

        private static int MeasureText(string text, int scale)
        {
            return text.Length * 6 * scale;
        }

        #endregion

        #region Семисегментный индикатор

        // Битовая маска сегментов: A-B-C-D-E-F-G.
        private static readonly int[] SegmentMasks =
        {
            0x3F, // 0
            0x06, // 1
            0x5B, // 2
            0x4F, // 3
            0x66, // 4
            0x6D, // 5
            0x7D, // 6
            0x07, // 7
            0x7F, // 8
            0x6F, // 9
            0x00, // 10: пусто
            0x40, // 11: минус (средний сегмент)
            0x00, // 12: пусто
            0x00, // 13: двоеточие рисуется отдельно
            0x00, // 14: пусто
            0x00  // 15: пусто
        };

        /// <summary>
        /// Рисует один символ семисегментного индикатора в ячейку атласа.
        /// Погашенные сегменты остаются видимыми как тёмные — характерный LED-вид.
        /// </summary>
        private static void DrawSevenSegment(PbrCanvas canvas, RectInt cell, int digit, Color color)
        {
            int mask = digit >= 0 && digit < SegmentMasks.Length ? SegmentMasks[digit] : 0;
            int width = cell.width;
            int height = cell.height;

            int thickness = Mathf.Max(2, width / 14);
            int gap = Mathf.Max(1, thickness / 2);
            int margin = Mathf.RoundToInt(width * 0.14f);

            int left = cell.x + margin;
            int right = cell.x + width - margin;
            int top = cell.y + margin;
            int bottom = cell.y + height - margin;
            int midY = (top + bottom) / 2;
            int halfThickness = Mathf.Max(1, thickness / 2);

            DrawSegment(canvas, left + halfThickness, top + halfThickness, right - halfThickness,
                top + halfThickness, thickness, (mask & 0x01) != 0, color);
            DrawSegment(canvas, right - halfThickness, top + halfThickness, right - halfThickness,
                midY - halfThickness, thickness, (mask & 0x02) != 0, color);
            DrawSegment(canvas, right - halfThickness, midY + halfThickness, right - halfThickness,
                bottom - halfThickness, thickness, (mask & 0x04) != 0, color);
            DrawSegment(canvas, left + halfThickness, bottom - halfThickness, right - halfThickness,
                bottom - halfThickness, thickness, (mask & 0x08) != 0, color);
            DrawSegment(canvas, left + halfThickness, midY + halfThickness, left + halfThickness,
                bottom - halfThickness, thickness, (mask & 0x10) != 0, color);
            DrawSegment(canvas, left + halfThickness, top + halfThickness, left + halfThickness,
                midY - halfThickness, thickness, (mask & 0x20) != 0, color);
            DrawSegment(canvas, left + halfThickness, midY, right - halfThickness, midY, thickness,
                (mask & 0x40) != 0, color);

            if (digit == 13)
            {
                // Двоеточие для «100%»/«-12%».
                int dotSize = Mathf.Max(2, width / 12);
                FillBlock(canvas, (cell.x + width / 2) - dotSize / 2, midY - (int)(height * 0.22f), dotSize,
                    dotSize, true, color);
                FillBlock(canvas, (cell.x + width / 2) - dotSize / 2, midY + (int)(height * 0.18f), dotSize,
                    dotSize, true, color);
            }
        }

        private static void DrawSegment(PbrCanvas canvas, int x0, int y0, int x1, int y1, int thickness, bool lit,
            Color color)
        {
            int minX = Mathf.Min(x0, x1);
            int maxX = Mathf.Max(x0, x1);
            int minY = Mathf.Min(y0, y1);
            int maxY = Mathf.Max(y0, y1);

            Color body = lit ? color * 0.55f : new Color(color.r * 0.09f, color.g * 0.09f, color.b * 0.09f, 1f);
            Color glow = lit ? color : new Color(color.r * 0.03f, color.g * 0.03f, color.b * 0.03f, 1f);

            for (int y = minY; y <= maxY + thickness; y++)
            {
                for (int x = minX; x <= maxX + thickness; x++)
                {
                    int index = canvas.Index(x, y);

                    // Мягкое свечение вокруг сегмента: слегка расширяем ядро и гасим ореол.
                    float dx = Mathf.Max(0, Mathf.Max(minX - x, x - (maxX + thickness)));
                    float dy = Mathf.Max(0, Mathf.Max(minY - y, y - (maxY + thickness)));
                    float halo = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(1f, thickness));

                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], body, halo);
                    canvas.AddEmission(x, y, glow * halo * (lit ? 1f : 0.15f));
                    canvas.SetHeight(x, y, lit ? 0.85f : 0.35f);
                }
            }
        }

        private static void FillBlock(PbrCanvas canvas, int x, int y, int width, int height, bool emissive, Color color)
        {
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    int index = canvas.Index(x + i, y + j);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], color * 0.6f, 0.85f);

                    if (emissive)
                    {
                        canvas.AddEmission(x + i, y + j, color);
                    }
                }
            }
        }

        #endregion
    }
}