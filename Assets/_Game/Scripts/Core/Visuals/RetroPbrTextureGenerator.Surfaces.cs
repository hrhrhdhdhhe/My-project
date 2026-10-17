// World Crisis | Слой: Core | Ответственный: OpenCode
// Конкретные поверхности Art Bible: чугун, холодная сталь, бетон бункера, карболит,
// разметка опасности, бумага договора, пиксельное лицо осциллографа, семисегментный атлас,
// ткани, золото, ржавчина, иней, пластик, кевлар, стекло, чёрная вода, бронза, неон.

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

        /// <summary>Тёмный бетон бункера #0D0E11: опалубка, швы, подтёки, включения.</summary>
        private static RetroPbrSet BuildBunkerConcrete(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            int plankCount = 8;
            float plankHeight = size / (float)plankCount;
            int period = 8;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    float coarse = Fbm(u, v, period, 1237, 5, 2f, 0.52f);
                    float aggregate = ValueNoise(u * 16f, v * 16f, period * 16, 6151);
                    float grain = Fbm(u * 30f, v * 30f, period * 30, 3391, 2, 2f, 0.5f);

                    int plank = Mathf.FloorToInt(y / plankHeight);
                    float shade = 0.7f + coarse * 0.7f + (aggregate - 0.5f) * 0.22f + (grain - 0.5f) * 0.12f;
                    shade *= 0.9f + Hash(plank, 3, 77) * 0.2f;

                    // Бетон ведёт себя в диапазоне #383634..#4A4640: тень от опалубки уходит в
                    // тёмный бруталистский тон, а выветренная доска — в светлый. Разброс
                    // узкий намеренно: при широком разбросе бетон превращался в красно-оливковые
                    // пятна, которые после постеризации до 32 уровней давали шум.
                    Color color = Color.Lerp(ConcreteBase, ConcreteLightBase, coarse * 0.45f) * shade;

                    // Шов опалубки между досками — тёмная узкая канавка.
                    float localY = y - plank * plankHeight;
                    float seamDistance = Mathf.Min(localY, plankHeight - localY);
                    float seam = Mathf.Clamp01(1f - seamDistance / Mathf.Max(1.5f, plankHeight * 0.035f));

                    if (seam > 0f)
                    {
                        color = Color.Lerp(color, ConcreteBase * 0.5f, seam);
                    }

                    // Древесная фактура досок опалубки, растянутая вдоль доски.
                    float wood = ValueNoise(u * 0.6f, v * 14f, period * 14, 4499 + plank * 31);
                    color = Color.Lerp(color, color * 1.35f, (wood - 0.5f) * 0.5f + 0.25f);

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.55f + (coarse - 0.5f) * 0.35f - seam * 0.6f
                        + (aggregate - 0.5f) * 0.16f + (grain - 0.5f) * 0.2f);
                    canvas.Metallic[index] = 0f;
                    canvas.Smoothness[index] = 0.18f + (aggregate - 0.5f) * 0.12f;
                }
            }

            // Штыри опалубки — сетка отверстий.
            float holeSpacing = size / 4f;

            for (int gy = 0; gy < 4; gy++)
            {
                for (int gx = 0; gx < 4; gx++)
                {
                    Vector2 center = new Vector2(gx * holeSpacing + holeSpacing * 0.5f,
                        gy * holeSpacing + holeSpacing * 0.5f);
                    int radius = Mathf.Max(3, Mathf.RoundToInt(size * 0.012f));

                    for (int y = Mathf.RoundToInt(center.y) - radius; y <= center.y + radius; y++)
                    {
                        for (int x = Mathf.RoundToInt(center.x) - radius; x <= center.x + radius; x++)
                        {
                            float distance = Vector2.Distance(new Vector2(x, y), center);
                            float falloff = Mathf.Clamp01(1f - distance / radius);
                            int index = canvas.Index(x, y);
                            canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], ConcreteBase * 0.2f, falloff);
                            canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] - falloff * 0.7f);
                            canvas.Smoothness[index] = Mathf.Lerp(canvas.Smoothness[index], 0.5f, falloff);
                        }
                    }
                }
            }

            // Подтёки и разводы от швов вниз по стене.
            Rand drips = new Rand(0xD819);
            int dripsCount = size / 24;

            for (int i = 0; i < dripsCount; i++)
            {
                float startX = drips.Range(0f, size);
                int startPlank = drips.RangeInt(0, plankCount);
                float length = drips.Range(size * 0.06f, size * 0.3f);
                float width = drips.Range(1.5f, size * 0.012f);
                Color tint = ConcreteBase * 0.45f;

                for (int step = 0; step < length; step++)
                {
                    float fade = 1f - step / length;
                    float offset = (ValueNoise(startX / 12f, step / 26f, 64, 8123) - 0.5f) * size * 0.02f;

                    for (int w = 0; w <= Mathf.Max(1, (int)width); w++)
                    {
                        int index = canvas.Index(
                            Mathf.RoundToInt(startX + offset) + w,
                            Mathf.RoundToInt(startPlank * plankHeight) + step);
                        canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], tint, 0.22f * fade);
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
                    canvas.Albedo[index] = baseColor * shade;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (marble - 0.5f) * 0.3f + (micro - 0.5f) * 0.12f);
                    canvas.Metallic[index] = 0.04f;
                    canvas.Smoothness[index] = 0.66f + (marble - 0.5f) * 0.24f;
                }
            }

            AddScratches(canvas, random, size, 34, 0.3f, 0.3f, Color.Lerp(baseColor, Color.white, 0.35f), 0.24f, 0.045f);

            for (int i = 0; i < 4; i++)
            {
                SmudgeCircle(canvas,
                    new Vector2(random.Range(0f, size), random.Range(0f, size)),
                    size * random.Range(0.05f, 0.11f), 0.16f, Color.Lerp(baseColor, Color.black, 0.35f));
            }

            return Bake(canvas, "Bakelite", 0.4f, true);
        }

        /// <summary>Жёлто-чёрная диагональная разметка опасности с обломами и грязью.</summary>
        private static RetroPbrSet BuildHazardStripes(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            int bandCount = 8;
            float bandWidth = size / (float)bandCount;
            int period = 8;
            float scale = size / (float)period;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Диагональ 45°: сумма координат, чтобы текстура бесшовно тайлилась.
                    float diagonal = (x + y) / bandWidth;
                    int band = Mathf.Abs(Mathf.FloorToInt(diagonal)) % 2;
                    float edgeNoise = Fbm(x / scale, y / scale, period, 9931, 4, 2f, 0.5f);

                    bool isYellow = band == 0;

                    // Обломы: края полосы «съедены» шумом.
                    float localBand = diagonal - Mathf.FloorToInt(diagonal);
                    float chip = Mathf.Clamp01(Mathf.Abs(localBand - 0.5f) * 2f - 0.62f)
                                 * (edgeNoise - 0.35f) * 2.6f;

                    Color baseTone = isYellow ? HazardYellow : HazardBlack;
                    float shade = isYellow ? 0.86f + edgeNoise * 0.28f : 0.78f + edgeNoise * 0.44f;
                    Color color = baseTone * shade;

                    if (chip > 0.02f)
                    {
                        // Сколы открывают серый металл корпуса.
                        color = Color.Lerp(color, new Color(0.29f, 0.3f, 0.33f), Mathf.Clamp01(chip));
                    }

                    float grime = Fbm(x / (scale * 0.4f), y / (scale * 0.4f), period * 3, 6277, 3, 2f, 0.55f);
                    color = Color.Lerp(color, color * 0.55f, Mathf.Clamp01(grime - 0.45f) * 1.4f);

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.55f + (edgeNoise - 0.5f) * 0.2f - chip * 0.45f);
                    canvas.Metallic[index] = Mathf.Lerp(0.08f, 0.8f, Mathf.Clamp01(chip));
                    canvas.Smoothness[index] = Mathf.Lerp(0.52f, 0.4f, grime);
                }
            }

            return Bake(canvas, "HazardStripes", 0.55f, true);
        }

        #endregion

        #region Документы и экраны

        /// <summary>Бумага мирного договора: TREATY, подписи, круглая сургучная печать #FF1744.</summary>
        private static RetroPbrSet BuildTreatyPaper(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
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

                    float shade = 0.9f + (blotch - 0.5f) * 0.16f + (fibre - 0.5f) * 0.08f;
                    int index = y * size + x;
                    canvas.Albedo[index] = PaperBase * shade;
                    canvas.HeightMap[index] = Mathf.Clamp01(0.5f + (fibre - 0.5f) * 0.45f + (blotch - 0.5f) * 0.2f);
                    canvas.Metallic[index] = 0f;
                    canvas.Smoothness[index] = 0.12f;
                }
            }

            // Пожелтевшие «лисички» по краям и в середине листа.
            Rand random = new Rand(0x7EA1);

            for (int i = 0; i < size / 18; i++)
            {
                Stain(canvas,
                    new Vector2(random.Range(0f, size), random.Range(0f, size)),
                    size * random.Range(0.02f, 0.07f), random.Range(0.1f, 0.26f),
                    new Color(0.62f, 0.52f, 0.34f), 0.6f);
            }

            int margin = Mathf.RoundToInt(size * 0.11f);

            // Заголовок TREATY — крупно, по центру.
            int headingScale = Mathf.Max(4, size / 46);
            int headingWidth = MeasureText("TREATY", headingScale);
            DrawText(canvas, new Vector2((size - headingWidth) * 0.5f, size * 0.78f), "TREATY", headingScale,
                InkBlack, 0.94f);

            // Двойная линия под заголовком.
            int ruleY = Mathf.RoundToInt(size * 0.74f);

            for (int y = ruleY; y < ruleY + Mathf.Max(2, size / 190); y++)
            {
                for (int x = margin; x < size - margin; x++)
                {
                    int index = canvas.Index(x, y);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], InkBlack, 0.85f);
                    canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] - 0.3f);
                }
            }

            for (int y = ruleY + Mathf.Max(3, size / 95); y < ruleY + Mathf.Max(5, size / 75); y++)
            {
                for (int x = margin; x < size - margin; x++)
                {
                    int index = canvas.Index(x, y);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], InkBlack, 0.6f);
                    canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] - 0.18f);
                }
            }

            // Подзаголовок.
            int subScale = Mathf.Max(2, size / 150);
            string subtitle = "PEACE ACCORD - FINAL DRAFT";
            DrawText(canvas, new Vector2((size - MeasureText(subtitle, subScale)) * 0.5f, size * 0.7f), subtitle,
                subScale, InkBlack, 0.8f);

            // Основной текст — строки мелким шрифтом.
            string[] words =
            {
                "ARTICLE", "TREATY", "SECTION", "THE", "PARTIES", "AGREE", "TO", "CEASE", "ALL", "HOSTILE",
                "ACTIONS", "AND", "SHARE", "GRAIN", "IN", "EQUAL", "SHARES", "FOR", "EVER", "SIGNED"
            };

            Rand textRandom = new Rand(0x7E47);
            int bodyScale = Mathf.Max(2, size / 170);
            int lineHeight = Mathf.RoundToInt(bodyScale * 11f);
            float cursor = size * 0.61f;

            while (cursor > size * 0.24f)
            {
                int wordsInLine = textRandom.RangeInt(6, 11);
                int lineWidth = 0;

                for (int w = 0; w < wordsInLine; w++)
                {
                    lineWidth += MeasureText(words[textRandom.RangeInt(0, words.Length)], bodyScale)
                                 + bodyScale * 2;
                }

                float startX = Mathf.Max(margin * 0.6f, (size - lineWidth) * 0.5f);
                int penX = Mathf.RoundToInt(startX);

                for (int w = 0; w < wordsInLine; w++)
                {
                    if (penX + bodyScale * 26 > size - margin * 0.5f)
                    {
                        break;
                    }

                    penX += DrawText(canvas, new Vector2(penX, cursor), words[textRandom.RangeInt(0, words.Length)],
                        bodyScale, InkBlack, 0.72f) + bodyScale * 2;
                }

                cursor -= lineHeight;
            }

            // Блок подписей: линии и «рукописные» росчерки.
            int signatureY = Mathf.RoundToInt(size * 0.16f);
            int columnWidth = Mathf.RoundToInt(size * 0.32f);

            for (int column = 0; column < 2; column++)
            {
                int lineStart = margin + column * (columnWidth + size / 12);

                for (int x = lineStart; x < lineStart + columnWidth; x++)
                {
                    int index = canvas.Index(x, signatureY);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], InkBlack, 0.8f);
                    canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] - 0.25f);
                }

                Rand hand = new Rand(0x51C0 + column * 977);

                for (int stroke = 0; stroke < 6; stroke++)
                {
                    Vector2 from = new Vector2(lineStart + hand.Range(0f, columnWidth * 0.5f),
                        signatureY + hand.Range(6f, size * 0.05f));
                    Vector2 to = new Vector3(from.x + hand.Range(columnWidth * 0.3f, columnWidth * 0.8f),
                        from.y + hand.Range(-size * 0.012f, size * 0.012f), 0f);
                    StrokeLine(canvas, from, to, hand.Range(0.6f, 1.6f), 0.16f, InkBlack, 0.8f);
                }

                DrawText(canvas, new Vector2(lineStart, signatureY - Mathf.Max(2, size / 105)),
                    column == 0 ? "SIGNATORY" : "WITNESS", Mathf.Max(2, size / 190), InkBlack, 0.62f);
            }

            // Круглая сургучная печать #FF1744 с рельефной звездой.
            Vector2 sealCenter = new Vector2(size * 0.76f, size * 0.26f);
            float sealRadius = size * 0.115f;

            for (int y = Mathf.RoundToInt(sealCenter.y - sealRadius * 1.2f);
                 y <= sealCenter.y + sealRadius * 1.2f; y++)
            {
                for (int x = Mathf.RoundToInt(sealCenter.x - sealRadius * 1.2f);
                     x <= sealCenter.x + sealRadius * 1.2f; x++)
                {
                    Vector2 offset = new Vector2(x - sealCenter.x, y - sealCenter.y);
                    float distance = offset.magnitude;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float wobble = sealRadius * (1f + (ValueNoise(
                        Mathf.Cos(angle) * 3f + 2f, Mathf.Sin(angle) * 3f + 2f, 8, 9311) - 0.5f) * 0.12f);

                    if (distance > wobble)
                    {
                        continue;
                    }

                    float falloff = Mathf.Clamp01(1f - distance / wobble);
                    Color wax = WaxSealRed * (0.72f + Fbm(x / 14f, y / 14f, 64, 6521, 3, 2f, 0.5f) * 0.6f);

                    // Вдавленный ободок по краю печати.
                    if (falloff < 0.16f)
                    {
                        wax *= 0.6f;
                    }

                    int index = canvas.Index(x, y);
                    canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], wax, 0.96f);
                    canvas.HeightMap[index] = Mathf.Clamp01(0.6f + falloff * 0.45f - (falloff < 0.16f ? 0.3f : 0f));
                    canvas.Smoothness[index] = 0.34f;
                }
            }

            // Рельефная звезда в центре печати.
            int starPoints = 10;
            float starOuter = sealRadius * 0.62f;
            float starInner = sealRadius * 0.26f;

            for (int i = 0; i < starPoints; i++)
            {
                float angleA = Mathf.PI * 2f * i / starPoints;
                float angleB = Mathf.PI * 2f * (i + 0.5f) / starPoints;
                Vector2 from = sealCenter + new Vector2(Mathf.Cos(angleA), Mathf.Sin(angleA))
                               * (i % 2 == 0 ? starOuter : starInner);
                Vector2 to = sealCenter + new Vector2(Mathf.Cos(angleB), Mathf.Sin(angleB))
                             * (i % 2 == 0 ? starInner : starOuter);

                for (int step = 0; step <= 18; step++)
                {
                    Vector2 point = Vector2.Lerp(from, to, step / 18f);

                    for (int w = -1; w <= 1; w++)
                    {
                        int index = canvas.Index(Mathf.RoundToInt(point.x) + w, Mathf.RoundToInt(point.y) + w);
                        canvas.Albedo[index] = Color.Lerp(canvas.Albedo[index], WaxSealRed * 0.45f, 0.8f);
                        canvas.HeightMap[index] = Mathf.Clamp01(canvas.HeightMap[index] + 0.3f);
                    }
                }
            }

            return Bake(canvas, "TreatyPaper", 0.32f, true);
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
        /// Столешница пульта и Круглого стола. Основа — зеленовато-болотная краска,
        /// поверх неё рыже-коричневая ржавчина и светлые царапины до чистого металла.
        /// Ржавчина и царапины светлее базы: в тёплом свете лампы именно они читаются.
        /// </summary>
        private static RetroPbrSet BuildBunkerConsoleTop(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Rand random = new Rand(0xC047);
            int period = 6;
            float scale = size / (float)period;
            int rustPeriod = 4;
            float rustScale = size / (float)rustPeriod;

            Color bareSteel = new Color(0.352f, 0.376f, 0.396f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / scale;
                    float v = y / scale;

                    float coarse = Fbm(u, v, period, 8123, 4, 2f, 0.5f);
                    float fine = Fbm(u * 5f, v * 5f, period * 5, 4471, 3, 2f, 0.55f);

                    Color color = ConsoleTopBase * (0.9f + coarse * 0.24f);
                    color = Color.Lerp(color, ConcreteLightBase, fine * 0.14f);

                    float height = 0.5f + (coarse - 0.5f) * 0.3f;

                    // Ржавчина: клеточные пятна, подъеденные шумом, с тёплым подтоном.
                    float cell = Cellular(x / rustScale, y / rustScale, rustPeriod, 6151);
                    float rust = Mathf.Clamp01(1f - (cell - 0.3f) * 3.1f) * Mathf.Clamp01(coarse * 1.5f - 0.42f);

                    // Ржавчины намеренно немного, и она подмешивается не до конца:
                    // при полном покрытии столешница читалась как камуфляж, а не как
                    // вытертая сталь под слоем краски.
                    if (rust > 0.01f)
                    {
                        color = Color.Lerp(color, RustBrown * (0.85f + fine * 0.4f), rust * 0.62f);
                        height -= rust * 0.14f;
                    }

                    int index = y * size + x;
                    canvas.Albedo[index] = color;
                    canvas.HeightMap[index] = Mathf.Clamp01(height);

                    // Краска забирает гладкость, ржавчина тем более; чистый металл блестит.
                    canvas.Smoothness[index] = Mathf.Lerp(0.44f + fine * 0.1f, 0.22f, rust);
                    canvas.Metallic[index] = Mathf.Lerp(0.24f, 0.14f, rust);
                }
            }

            // Светлые царапины до металла — именно они дают «тёплое потёртое стальное» чтение.
            AddScratches(canvas, random, size, 58, 0.55f, 0.75f, bareSteel, 0.16f, 0.07f);
            AddWearSpeckles(canvas, random, size, 520, 0.5f, 0.6f);

            return Bake(canvas, "BunkerConsoleTop", 1f, false);
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
        /// Экран терминала «CITY STATUS». Тёмный фон, яркая зелёная сетка радара,
        /// четыре контура городов и строки текста. Всё это попадает в карту эмиссии,
        /// поэтому фон уходит в почти чёрный, а линии светятся #00E676.
        /// </summary>
        private static RetroPbrSet BuildCityStatusScreen(int size)
        {
            PbrCanvas canvas = new PbrCanvas(size, size);
            Color deep = new Color(0.008f, 0.035f, 0.02f);
            Color glow = OscilloscopeGreen;

            canvas.Fill(deep);
            canvas.FillHeight(0.5f);
            canvas.FillSmoothness(0.42f);
            canvas.FillMetallic(0f);

            float centerX = size * 0.5f;
            float centerY = size * 0.5f;
            float radius = size * 0.42f;

            // Концентрические кольца и четыре луча — классическая сетка радара.
            for (int ring = 1; ring <= 4; ring++)
            {
                int steps = 64 + ring * 16;

                for (int i = 0; i < steps; i++)
                {
                    float a0 = i / (float)steps * Mathf.PI * 2f;
                    float a1 = (i + 1) / (float)steps * Mathf.PI * 2f;
                    float r = radius * ring / 4f;

                    StrokeLine(canvas,
                        new Vector2(centerX + Mathf.Cos(a0) * r, centerY + Mathf.Sin(a0) * r),
                        new Vector2(centerX + Mathf.Cos(a1) * r, centerY + Mathf.Sin(a1) * r),
                        0.6f, 0f, glow * 0.55f, 0.85f);
                }
            }

            for (int spoke = 0; spoke < 4; spoke++)
            {
                float angle = spoke * Mathf.PI * 0.5f;
                StrokeLine(canvas,
                    new Vector2(centerX, centerY),
                    new Vector2(centerX + Mathf.Cos(angle) * radius, centerY + Mathf.Sin(angle) * radius),
                    0.6f, 0f, glow * 0.5f, 0.8f);
            }

            // Контуры четырёх городов разного размера, ближе к центру.
            Rand random = new Rand(0x5717);

            for (int i = 0; i < 4; i++)
            {
                float angle = i / 4f * Mathf.PI * 2f + 0.4f;
                float distance = radius * random.Range(0.22f, 0.62f);
                float cityRadius = radius * random.Range(0.1f, 0.2f);
                Vector2 city = new Vector2(centerX + Mathf.Cos(angle) * distance, centerY + Mathf.Sin(angle) * distance);

                int points = 7;
                Vector2[] outline = new Vector2[points];

                for (int p = 0; p < points; p++)
                {
                    float a = p / (float)points * Mathf.PI * 2f;
                    float r = cityRadius * random.Range(0.7f, 1.3f);
                    outline[p] = city + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                }

                for (int p = 0; p < points; p++)
                {
                    StrokeLine(canvas, outline[p], outline[(p + 1) % points], 0.9f, 0f, glow, 0.95f);
                }
            }

            // Заголовок и строки состояния.
            int titleScale = Mathf.Max(1, Mathf.RoundToInt(size / 128f));
            DrawText(canvas, new Vector2(size * 0.12f, size * 0.86f), "CITY STATUS", titleScale, glow, 0.95f);

            int rowScale = Mathf.Max(1, titleScale - 1);
            string[] rows = { "NORTH OK", "SOUTH WARN", "EAST OK", "WEST LOST" };

            for (int i = 0; i < rows.Length; i++)
            {
                float y = size * (0.16f - i * 0.085f);
                DrawText(canvas, new Vector2(size * 0.12f, y), rows[i], rowScale, glow * 0.85f, 0.8f);
            }

            // Развёртка: горизонтальные тёмные полосы, как у реального ЭЛТ.
            for (int y = 0; y < size; y += 4)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    canvas.Albedo[index] = canvas.Albedo[index] * 0.82f;
                }
            }

            // Эмиссия повторяет рисунок, иначе экран останется тёмным.
            for (int i = 0; i < canvas.Albedo.Length; i++)
            {
                canvas.Emission[i] = canvas.Albedo[i];
            }

            RetroPbrSet set = Bake(canvas, "CityStatusScreen", 1f, false);
            set.EmissionIntensity = 3.2f;
            return set;
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