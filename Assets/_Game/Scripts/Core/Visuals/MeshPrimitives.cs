// World Crisis | Слой: Core | Ответственный: OpenCode
// Библиотека процедурных примитивов в стиле Industrial Lo-Fi Brutalism: фасеточные брусы,
// тела вращения, сферы и оболочки, трубы по пути, спирали, выдавленные полигоны.
// Примитивы не знают о материалах — их геометрия переносится в материалные группы вызывающим кодом.

using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    /// <summary>
    /// Фабрика базовых форм. Ничего не знает о бункере и саммите — только геометрия.
    /// Стиль задан по умолчанию: фаски на брусах, низкая полигональность у цилиндров,
    /// гладкие нормали только у округлых форм (купола, кабели, линзы).
    /// </summary>
    public static class MeshPrimitives
    {
        internal const float Epsilon = 0.00001f;

        #region Брусы с фаской

        /// <summary>Прямоугольный брус с фаской по всем рёбрам.</summary>
        public static Mesh BeveledBox(Vector3 size, float bevel)
        {
            return BeveledBox(size, new Vector3(bevel, bevel, bevel));
        }

        /// <summary>Брус с независимой фаской по каждой оси (удобно для тонких пластин).</summary>
        public static Mesh BeveledBox(Vector3 size, Vector3 bevel)
        {
            Vector3 half = size * 0.5f;

            // Фаска нулевой ширины означает «без фаски»: иначе появляются вырожденные
            // грани толщиной в Epsilon, которые взрывают дерево BSP при булевых вырезах.
            bool hasBevel = bevel.x > Epsilon || bevel.y > Epsilon || bevel.z > Epsilon;

            if (!hasBevel)
            {
                return PlainBox(half);
            }

            Vector3 b = new Vector3(
                Mathf.Clamp(bevel.x, Epsilon, half.x * 0.98f),
                Mathf.Clamp(bevel.y, Epsilon, half.y * 0.98f),
                Mathf.Clamp(bevel.z, Epsilon, half.z * 0.98f));

            Vector3 inner = half - b;
            MeshBuilder geometry = new MeshBuilder();

            // --- 6 основных граней. t1 x t2 = внешняя нормаль грани. ---
            for (int axis = 0; axis < 3; axis++)
            {
                int t1 = (axis + 1) % 3;
                int t2 = (axis + 2) % 3;

                for (int s = 0; s < 2; s++)
                {
                    float sign = s == 0 ? 1f : -1f;
                    Vector3 normal = Axis(axis) * sign;
                    Vector3 center = Axis(axis) * (half[axis] * sign);

                    Vector3 p0 = center - Axis(t1) * inner[t1] - Axis(t2) * inner[t2];
                    Vector3 p1 = center + Axis(t1) * inner[t1] - Axis(t2) * inner[t2];
                    Vector3 p2 = center + Axis(t1) * inner[t1] + Axis(t2) * inner[t2];
                    Vector3 p3 = center - Axis(t1) * inner[t1] + Axis(t2) * inner[t2];

                    geometry.AddQuad(p0, p1, p2, p3, normal, null, MeshUvMode.BoxProject);
                }
            }

            // --- 12 фасочных рёбер ---
            for (int a = 0; a < 3; a++)
            {
                for (int c = a + 1; c < 3; c++)
                {
                    int run = 3 - a - c;
                    float runHalf = inner[run];

                    for (int sa = 0; sa < 2; sa++)
                    {
                        for (int sc = 0; sc < 2; sc++)
                        {
                            float signA = sa == 0 ? 1f : -1f;
                            float signC = sc == 0 ? 1f : -1f;
                            Vector3 normal = Normalize(Axis(a) * signA + Axis(c) * signC);

                            Vector3 alongRunNeg = -Axis(run) * runHalf;
                            Vector3 alongRunPos = Axis(run) * runHalf;
                            Vector3 onA = Axis(a) * (half[a] * signA) + Axis(c) * ((half[c] - b[c]) * signC);
                            Vector3 onC = Axis(c) * (half[c] * signC) + Axis(a) * ((half[a] - b[a]) * signA);

                            geometry.AddQuad(
                                onA + alongRunNeg, onA + alongRunPos, onC + alongRunPos, onC + alongRunNeg,
                                normal, null, MeshUvMode.BoxProject);
                        }
                    }
                }
            }

            // --- 8 угловых треугольников ---
            for (int sx = 0; sx < 2; sx++)
            {
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sz = 0; sz < 2; sz++)
                    {
                        float fx = sx == 0 ? 1f : -1f;
                        float fy = sy == 0 ? 1f : -1f;
                        float fz = sz == 0 ? 1f : -1f;

                        Vector3 p0 = new Vector3(half.x * fx, inner.y * fy, inner.z * fz);
                        Vector3 p1 = new Vector3(inner.x * fx, half.y * fy, inner.z * fz);
                        Vector3 p2 = new Vector3(inner.x * fx, inner.y * fy, half.z * fz);

                        geometry.AddTriangle(p0, p1, p2, null, MeshUvMode.BoxProject);
                    }
                }
            }

            return geometry.BuildPrimitive("BeveledBox", true);
        }

        /// <summary>Плоский брус без фаски: ровно 12 треугольников. База для булевых инструментов.</summary>
        private static Mesh PlainBox(Vector3 half)
        {
            MeshBuilder geometry = new MeshBuilder();

            for (int axis = 0; axis < 3; axis++)
            {
                int t1 = (axis + 1) % 3;
                int t2 = (axis + 2) % 3;

                for (int s = 0; s < 2; s++)
                {
                    float sign = s == 0 ? 1f : -1f;
                    Vector3 normal = Axis(axis) * sign;
                    Vector3 center = Axis(axis) * (half[axis] * sign);

                    geometry.AddQuad(
                        center - Axis(t1) * half[t1] - Axis(t2) * half[t2],
                        center + Axis(t1) * half[t1] - Axis(t2) * half[t2],
                        center + Axis(t1) * half[t1] + Axis(t2) * half[t2],
                        center - Axis(t1) * half[t1] + Axis(t2) * half[t2],
                        normal, null, MeshUvMode.BoxProject);
                }
            }

            return geometry.BuildPrimitive("PlainBox", true);
        }

        /// <summary>Тонкая металлическая пластина в плоскости XZ — корпуса приборов, крышки, щиты.</summary>
        public static Mesh Plate(Vector2 size, float thickness, float bevel)
        {
            return BeveledBox(new Vector3(size.x, thickness, size.y), bevel);
        }

        /// <summary>Клиновидный брус: скаты, покатости линз, козырьки, покатые крышки.</summary>
        public static Mesh Wedge(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            MeshBuilder geometry = new MeshBuilder();

            // Профиль в плоскости XY (треугольник), выдавленный вдоль Z.
            Vector3[] p =
            {
                new Vector3(-h.x, -h.y, h.z),
                new Vector3(h.x, -h.y, h.z),
                new Vector3(-h.x, h.y, h.z),
                new Vector3(-h.x, -h.y, -h.z),
                new Vector3(h.x, -h.y, -h.z),
                new Vector3(-h.x, h.y, -h.z)
            };

            // Передний и задний торцы (треугольники), смотрящие наружу.
            geometry.AddTriangle(p[0], p[1], p[2], null, MeshUvMode.BoxProject);
            geometry.AddTriangle(p[3], p[5], p[4], null, MeshUvMode.BoxProject);

            // Три наклонные грани + нижняя.
            geometry.AddQuad(p[0], p[3], p[4], p[1], Vector3.down, null, MeshUvMode.BoxProject);
            geometry.AddQuad(p[1], p[4], p[5], p[2], Normalize(new Vector3(h.x, h.y, h.z)) * -1f, null, MeshUvMode.BoxProject);
            geometry.AddQuad(p[2], p[5], p[3], p[0], -Vector3.right, null, MeshUvMode.BoxProject);

            return geometry.BuildPrimitive("Wedge", true);
        }

        #endregion

        #region Тела вращения

        /// <summary>
        /// Тело вращения по профилю (радиус, высота). Основа цилиндров, конусов, куполов, стаканов
        /// и линз. Профиль идёт снизу вверх, радиус 0 даёт вершину. Если профиль замкнут
        /// (первая и последняя точки совпадают), стык замыкается и торцы не ставятся.
        /// </summary>
        public static Mesh Lathe(Vector2[] profile, int sides, bool smooth, float uvScale)
        {
            sides = Mathf.Clamp(sides, 3, 64);
            MeshBuilder geometry = new MeshBuilder().WithUvScale(uvScale);

            if (profile == null || profile.Length < 2)
            {
                Debug.LogWarning("[WorldCrisis] MeshPrimitives.Lathe: ??????? ?????? ???? ?????.");
                return geometry.BuildPrimitive("Lathe", false);
            }

            bool closed = Vector2.Distance(profile[0], profile[profile.Length - 1]) < 0.0001f;
            int ringCount = closed ? profile.Length - 1 : profile.Length;

            if (ringCount < 2)
            {
                Debug.LogWarning("[WorldCrisis] MeshPrimitives.Lathe: ??????????? ???????.");
                return geometry.BuildPrimitive("Lathe", false);
            }

            Vector2[] profileNormals = ProfileNormals(profile, ringCount);
            int[][] ringIndices = new int[ringCount][];

            for (int i = 0; i < ringCount; i++)
            {
                float radius = profile[i].x;
                float height = profile[i].y;
                Vector2 profileNormal = profileNormals[i];
                int[] indices = new int[sides + 1];
                float circumference = Mathf.PI * 2f * Mathf.Max(radius, 0.0001f);
                bool isPoint = radius <= Epsilon;

                for (int j = 0; j <= sides; j++)
                {
                    float angle = (Mathf.PI * 2f * j) / sides;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    Vector3 position = new Vector3(cos * radius, height, sin * radius);

                    Vector3 normal;
                    if (smooth)
                    {
                        // Вырожденное кольцо (вершина конуса или полюс сферы) получает полный
                        // набор нормалей: координаты совпадают, но освещение на острие корректно.
                        normal = new Vector3(cos * profileNormal.x, profileNormal.y, sin * profileNormal.x);
                    }
                    else if (isPoint)
                    {
                        normal = new Vector3(0f, profileNormal.y >= 0f ? 1f : -1f, 0f);
                    }
                    else
                    {
                        normal = new Vector3(cos, 0f, sin);
                    }

                    indices[j] = geometry.AddVertex(null, position, SafeNormalize(normal),
                        isPoint
                            ? new Vector2(0.5f, height * uvScale)
                            : new Vector2(j / (float)sides * circumference * uvScale, height * uvScale));
                }

                ringIndices[i] = indices;
            }

            int segments = closed ? ringCount : ringCount - 1;

            for (int i = 0; i < segments; i++)
            {
                int[] lower = ringIndices[i];
                int[] upper = ringIndices[(i + 1) % ringCount];

                for (int j = 0; j < sides; j++)
                {
                    // Порядок lower -> upper -> upper+1 -> lower+1 даёт внешнюю нормаль.
                    geometry.AddQuadByIndex(null, lower[j], upper[j], upper[j + 1], lower[j + 1], smooth);
                }
            }

            if (!closed)
            {
                if (profile[0].x > Epsilon)
                {
                    AddCap(geometry, profile[0], -1f, sides, smooth);
                }

                if (profile[ringCount - 1].x > Epsilon)
                {
                    AddCap(geometry, profile[ringCount - 1], 1f, sides, smooth);
                }
            }

            return geometry.BuildPrimitive("Lathe", true);
        }

        /// <summary>
        /// Многогранный цилиндр/конус/усечённый конус с фаской по торцам.
        /// 8..16 сторон дают характерную гранёность ретро-стиля.
        /// </summary>
        public static Mesh Prism(float radiusBottom, float radiusTop, float height, int sides, float bevel, bool smooth)
        {
            sides = Mathf.Clamp(sides, 3, 64);
            height = Mathf.Max(0.0005f, height);

            float yBottom = -height * 0.5f;
            float yTop = height * 0.5f;
            List<Vector2> profile = new List<Vector2>();

            // Нулевой радиус даёт настоящий конус, а не «двойной конус»: точка профиля
            // схлопывается в ноль ровно на своём торце, и дно у конуса не появляется.
            if (radiusBottom > Epsilon)
            {
                float bottomBevel = bevel > Epsilon
                    ? Mathf.Clamp(bevel, Epsilon, Mathf.Min(radiusBottom, height) * 0.49f)
                    : 0f;

                profile.Add(new Vector2(radiusBottom - bottomBevel, yBottom));

                if (bottomBevel > 0f)
                {
                    profile.Add(new Vector2(radiusBottom, yBottom + bottomBevel));
                }
            }
            else
            {
                profile.Add(new Vector2(0f, yBottom));
            }

            if (radiusTop > Epsilon)
            {
                float topBevel = bevel > Epsilon
                    ? Mathf.Clamp(bevel, Epsilon, Mathf.Min(radiusTop, height) * 0.49f)
                    : 0f;

                profile.Add(new Vector2(radiusTop, yTop - topBevel));

                if (topBevel > 0f)
                {
                    profile.Add(new Vector2(radiusTop - topBevel, yTop));
                }
            }
            else
            {
                profile.Add(new Vector2(0f, yTop));
            }

            return Lathe(profile.ToArray(), sides, smooth, 1f);
        }

        /// <summary>Купол/полусфера высотой height. Для сферических линз и колпачков.</summary>
        public static Mesh Dome(float radius, float height, int segments, int rings, bool smooth)
        {
            return Lathe(DomeProfile(radius, height, Mathf.Clamp(rings, 3, 48)), Mathf.Clamp(segments, 6, 48),
                smooth, 1f);
        }

        /// <summary>
        /// Полусферическая оболочка заданной толщины (проволочный купол ПВО, обтекатели, шлемы).
        /// Внешняя и внутренняя поверхности плюс кольцевой торец по краю — цельный замкнутый объём.
        /// </summary>
        public static Mesh DomeShell(float radius, float thickness, int segments, int rings)
        {
            rings = Mathf.Clamp(rings, 3, 32);
            float inner = Mathf.Max(0.001f, radius - thickness);

            Vector2[] outer = DomeProfile(radius, radius, rings);
            Vector2[] innerArc = DomeProfile(inner, inner, rings);

            // Профиль замкнут на себя: внешняя дуга снизу вверх, внутренняя — обратно вниз,
            // затем явная точка (R, 0), которая Lathe воспримет как замыкание контура.
            // Именно она замыкает нижний кольцевой торец оболочки.
            Vector2[] profile = new Vector2[outer.Length + innerArc.Length + 1];
            int index = 0;

            for (int i = 0; i < outer.Length; i++)
            {
                profile[index++] = outer[i];
            }

            for (int i = innerArc.Length - 1; i >= 0; i--)
            {
                profile[index++] = innerArc[i];
            }

            profile[profile.Length - 1] = outer[0];
            return Lathe(profile, Mathf.Clamp(segments, 8, 48), true, 1f);
        }

        /// <summary>Тор: микрофонное кольцо, антенны, обручи, шайбы.</summary>
        public static Mesh Torus(float majorRadius, float minorRadius, int majorSegments, int minorSegments)
        {
            Vector2[] profile = new Vector2[Mathf.Clamp(minorSegments, 4, 32) + 1];
            int count = profile.Length;

            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f * i) / (count - 1);
                profile[i] = new Vector2(majorRadius + Mathf.Cos(angle) * minorRadius, Mathf.Sin(angle) * minorRadius);
            }

            return Lathe(profile, Mathf.Clamp(majorSegments, 6, 64), true, 1f);
        }

        /// <summary>Сфера. Через масштаб матрицы превращается в эллипсоид (линзы, щёки, колпаки).</summary>
        public static Mesh Sphere(float radius, int segments, int rings)
        {
            segments = Mathf.Clamp(segments, 6, 48);
            rings = Mathf.Clamp(rings, 4, 40);
            MeshBuilder geometry = new MeshBuilder();

            int stride = segments + 1;
            int[][] indices = new int[rings + 1][];

            for (int ring = 0; ring <= rings; ring++)
            {
                float phi = Mathf.PI * ring / rings;
                indices[ring] = new int[stride];

                for (int segment = 0; segment <= segments; segment++)
                {
                    float theta = Mathf.PI * 2f * segment / segments;
                    Vector3 normal = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));

                    indices[ring][segment] = geometry.AddVertex(null, normal * radius, normal,
                        new Vector2(segment / (float)segments * Mathf.PI * 2f * radius, ring / (float)rings * Mathf.PI * radius));
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = indices[ring][segment];
                    int b = indices[ring][segment + 1];
                    int c = indices[ring + 1][segment];
                    int d = indices[ring + 1][segment + 1];

                    // На полюсах первая половина вырождается — не добавляем вырожденные треугольники.
                    if (ring != 0)
                    {
                        geometry.AddTriangleByIndex(null, a, b, c, true, false);
                    }

                    if (ring != rings - 1)
                    {
                        geometry.AddTriangleByIndex(null, b, d, c, true, false);
                    }
                }
            }

            return geometry.BuildPrimitive("Sphere", true);
        }

        /// <summary>Кольцо-воронка с плоским тором: громкоговорители, шайбы, козырьки линз.</summary>
        public static Mesh Ring(float innerRadius, float outerRadius, float thickness, int segments)
        {
            float half = Mathf.Max(0.0005f, thickness * 0.5f);
            Vector2[] profile =
            {
                new Vector2(innerRadius, -half),
                new Vector2(outerRadius, -half),
                new Vector2(outerRadius, half),
                new Vector2(innerRadius, half),
                new Vector2(innerRadius, -half)
            };

            return Lathe(profile, Mathf.Clamp(segments, 6, 48), false, 1f);
        }

        /// <summary>Скруглённый цилиндр: заклепка, болт, шайба, кнопка.</summary>
        public static Mesh Rivet(float radius, float height, int sides)
        {
            return Prism(radius, radius * 0.78f, height, Mathf.Clamp(sides, 6, 16), height * 0.25f, true);
        }

        /// <summary>Заклёпка-купол: классический крепёж брутализма.</summary>
        public static Mesh RivetDome(float radius, float height)
        {
            Vector2[] profile =
            {
                new Vector2(radius, 0f),
                new Vector2(radius * 0.92f, height * 0.35f),
                new Vector2(radius * 0.6f, height * 0.8f),
                new Vector2(0f, height)
            };

            return Lathe(profile, 10, true, 1f);
        }

        #endregion

        #region Трубы, спирали, полигоны

        /// <summary>
        /// Прямоугольная рамка из четырёх фасочных брусков: внешний размер, толщина перекладины
        /// и глубина в плоскости Z. Это прямая замена булеву вырезу окна — рамка честнее
        /// выглядит и не требует CSG.
        /// </summary>
        public static Mesh Frame(Vector2 outer, float bar, float depth, float bevel)
        {
            MeshBuilder geometry = new MeshBuilder();
            float halfX = outer.x * 0.5f;
            float halfY = outer.y * 0.5f;
            float innerX = Mathf.Max(0.0005f, halfX - bar);
            float innerY = Mathf.Max(0.0005f, halfY - bar);

            // Горизонтальные перекладины.
            for (int side = 0; side < 2; side++)
            {
                float y = side == 0 ? halfY - bar * 0.5f : -halfY + bar * 0.5f;
                geometry.Add(BeveledBox(new Vector3(outer.x, bar, depth), bevel),
                    MeshBuilder.Trs(new Vector3(0f, y, 0f), Vector3.zero, Vector3.one), null, MeshUvMode.BoxProject);
            }

            // Вертикальные стойки.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? halfX - bar * 0.5f : -halfX + bar * 0.5f;
                float height = Mathf.Max(0.0005f, innerY * 2f - bar);
                geometry.Add(BeveledBox(new Vector3(bar, height, depth), bevel),
                    MeshBuilder.Trs(new Vector3(x, 0f, 0f), Vector3.zero, Vector3.one), null, MeshUvMode.BoxProject);
            }

            return geometry.BuildPrimitive("Frame", true);
        }

        /// <summary>
        /// Плоская столешница с круглым углублением-колодцем и внутренней перегородкой.
        /// Профиль тела вращения сразу задаёт и кромку, и стенку, и дно колодца.
        /// </summary>
        public static Mesh TableWithWell(float outerRadius, float rimThickness, float wellRadius, float wellDepth,
            float thickness, int segments)
        {
            segments = Mathf.Clamp(segments, 12, 64);
            rimThickness = Mathf.Clamp(rimThickness, 0.0005f, Mathf.Max(0.0005f, (outerRadius - wellRadius) * 0.5f));

            // Профиль идёт по границе материала столешницы: нижняя плоскость наружу,
            // внешняя кромка вверх, верхняя плоскость внутрь к колодцу, стенка вниз,
            // дно колодца к оси и замыкание по оси. Обход такой, что материал всё время
            // остаётся слева — значит, нормали автоматически смотрят наружу.
            Vector2[] profile =
            {
                new Vector2(0f, -thickness),
                new Vector2(outerRadius, -thickness),
                new Vector2(outerRadius, -rimThickness),
                new Vector2(outerRadius - rimThickness, 0f),
                new Vector2(wellRadius, 0f),
                new Vector2(wellRadius, -wellDepth),
                new Vector2(0f, -wellDepth),
                new Vector2(0f, -thickness)
            };

            return Lathe(profile, segments, true, 1f);
        }

        /// <summary>
        /// Массивный шестигранный блок — «куб» часов. Это гранёная шестигранная призма
        /// с сильной фаской по торцам, растянутая по ширине и высоте: получается
        /// тяжёлый шестигранный ящик без единой булевой операции.
        /// </summary>
        public static Mesh HexBlock(Vector3 size, float bevel)
        {
            float radius = Mathf.Max(size.x, size.z) * 0.5f;
            Mesh hexagon = Prism(radius, radius, size.y, 6, bevel, true);

            // Растягиваем круглое сечение в прямоугольное и поворачиваем плоской стороной к зрителю.
            Vector3 scale = new Vector3(size.x / (radius * 2f), 1f, size.z / (radius * 2f));
            MeshBuilder geometry = new MeshBuilder();
            geometry.Add(hexagon, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 0f), scale), null,
                MeshUvMode.BoxProject);
            return geometry.BuildPrimitive("HexBlock", true);
        }

        /// <summary>
        /// Плоский квад в плоскости XY с UV ровно 0..1 и нормалью в +Z.
        /// Единственный примитив без тайлинга — им натягиваются атласы
        /// (семисегментные цифры, печати, пиксельные лица) через MeshUvMode.Keep.
        /// </summary>
        public static Mesh Plane(float sizeX, float sizeY)
        {
            float hx = sizeX * 0.5f;
            float hy = sizeY * 0.5f;
            MeshBuilder geometry = new MeshBuilder();

            Vector3 a = new Vector3(-hx, -hy, 0f);
            Vector3 b = new Vector3(hx, -hy, 0f);
            Vector3 c = new Vector3(hx, hy, 0f);
            Vector3 d = new Vector3(-hx, hy, 0f);

            AddUvQuad(geometry, a, b, c, d, Vector3.forward);
            AddUvQuad(geometry, d, c, b, a, Vector3.back);

            return geometry.BuildPrimitive("Plane", false);
        }

        private static void AddUvQuad(MeshBuilder builder, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int i0 = builder.AddVertex(null, a, normal, new Vector2(0f, 0f));
            int i1 = builder.AddVertex(null, b, normal, new Vector2(1f, 0f));
            int i2 = builder.AddVertex(null, c, normal, new Vector2(1f, 1f));
            int i3 = builder.AddVertex(null, d, normal, new Vector2(0f, 1f));

            builder.AddQuadByIndex(null, i0, i1, i2, i3, true);
        }

        /// <summary>Труба постоянного сечения по ломаной: кабели, гусиная шея, шланги, усики.</summary>
        public static Mesh Tube(Vector3[] path, float radius, int sides)
        {
            sides = Mathf.Clamp(sides, 3, 24);

            if (path == null || path.Length < 2)
            {
                Debug.LogWarning("[WorldCrisis] MeshPrimitives.Tube: ????? ??????? ??? ????? ????.");
                return Sphere(Mathf.Max(radius, 0.01f), 8, 6);
            }

            MeshBuilder geometry = new MeshBuilder();
            Vector3[] tangents = new Vector3[path.Length];
            float totalLength = 0f;

            for (int i = 0; i < path.Length; i++)
            {
                Vector3 tangent;
                if (i == 0)
                {
                    tangent = path[1] - path[0];
                }
                else if (i == path.Length - 1)
                {
                    tangent = path[i] - path[i - 1];
                }
                else
                {
                    tangent = path[i + 1] - path[i - 1];
                }

                tangents[i] = SafeNormalize(tangent);

                if (i > 0)
                {
                    totalLength += Vector3.Distance(path[i], path[i - 1]);
                }
            }

            Vector3 reference = SafeNormalize(Vector3.Cross(
                Mathf.Abs(tangents[0].y) > 0.9f ? Vector3.right : Vector3.up, tangents[0]));
            float travelled = 0f;
            int stride = sides + 1;

            for (int i = 0; i < path.Length; i++)
            {
                if (i > 0)
                {
                    Vector3 cross = Vector3.Cross(tangents[i - 1], tangents[i]);

                    if (cross.sqrMagnitude > Epsilon)
                    {
                        float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(tangents[i - 1], tangents[i]), -1f, 1f));
                        reference = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, cross.normalized) * reference;
                    }

                    reference = SafeNormalize(reference - tangents[i] * Vector3.Dot(reference, tangents[i]));
                    travelled += Vector3.Distance(path[i], path[i - 1]);
                }

                Vector3 binormal = SafeNormalize(Vector3.Cross(tangents[i], reference));
                reference = SafeNormalize(Vector3.Cross(binormal, tangents[i]));
                float v = totalLength > Epsilon ? travelled / totalLength : 0f;

                for (int j = 0; j <= sides; j++)
                {
                    float angle = Mathf.PI * 2f * j / sides;
                    Vector3 radial = reference * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);

                    geometry.AddVertex(null, path[i] + radial * radius, radial,
                        new Vector2(angle * radius, v * totalLength));
                }
            }

            for (int i = 0; i < path.Length - 1; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * stride + j;
                    int b = a + 1;
                    int c = (i + 1) * stride + j;
                    int d = c + 1;

                    geometry.AddTriangleByIndex(null, a, c, b, true, false);
                    geometry.AddTriangleByIndex(null, b, c, d, true, false);
                }
            }

            return geometry.BuildPrimitive("Tube", true);
        }

        /// <summary>Спираль: витой провод телефонной трубки, змеевик, хладагент.</summary>
        public static Mesh Helix(float coilRadius, float wireRadius, float turns, float height, int samplesPerTurn, int sides)
        {
            samplesPerTurn = Mathf.Clamp(samplesPerTurn, 4, 32);
            int total = Mathf.Max(6, Mathf.RoundToInt(turns * samplesPerTurn));
            Vector3[] path = new Vector3[total + 1];

            for (int i = 0; i <= total; i++)
            {
                float t = i / (float)total;
                float angle = t * turns * Mathf.PI * 2f;
                path[i] = new Vector3(Mathf.Cos(angle) * coilRadius, t * height, Mathf.Sin(angle) * coilRadius);
            }

            return Tube(path, wireRadius, sides);
        }

        /// <summary>Дуга в плоскости XY — усы генерала, ручки, скобы, дуги антенн.</summary>
        public static Mesh Arc(float radius, float arcDegrees, float thickness, int segments)
        {
            segments = Mathf.Clamp(segments, 3, 48);
            int samples = segments + 1;
            Vector3[] path = new Vector3[samples];
            float step = arcDegrees / segments * Mathf.Deg2Rad;

            for (int i = 0; i < samples; i++)
            {
                float angle = step * i;
                path[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }

            return Tube(path, thickness, 8);
        }

        /// <summary>
        /// Выдавленный контур с плоскими торцами (лист договора, эмблемы, нашивки).
        /// Торцы триангулируются клиппированием ушей.
        /// </summary>
        public static Mesh ExtrudePolygon(Vector2[] outline, float depth, float uvScale)
        {
            MeshBuilder geometry = new MeshBuilder().WithUvScale(uvScale);

            if (outline == null || outline.Length < 3)
            {
                Debug.LogWarning("[WorldCrisis] MeshPrimitives.ExtrudePolygon: ????? ??????? ??? ??????? ???????.");
                return geometry.BuildPrimitive("ExtrudePolygon", false);
            }

            int count = outline.Length;
            float half = depth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % count];
                Vector3 sideNormal = new Vector3(b.y - a.y, -(b.x - a.x), 0f);
                sideNormal = sideNormal.sqrMagnitude > Epsilon ? sideNormal.normalized : Vector3.up;

                geometry.AddQuad(
                    new Vector3(a.x, a.y, -half),
                    new Vector3(b.x, b.y, -half),
                    new Vector3(b.x, b.y, half),
                    new Vector3(a.x, a.y, half),
                    sideNormal, null, MeshUvMode.BoxProject);
            }

            int[] cap = PolygonTriangulator.EarClip(outline);

            for (int i = 0; i + 2 < cap.Length; i += 3)
            {
                Vector2 f0 = outline[cap[i]];
                Vector2 f1 = outline[cap[i + 1]];
                Vector2 f2 = outline[cap[i + 2]];

                geometry.AddTriangle(
                    new Vector3(f0.x, f0.y, half), new Vector3(f1.x, f1.y, half), new Vector3(f2.x, f2.y, half),
                    null, MeshUvMode.BoxProject);
                geometry.AddTriangle(
                    new Vector3(f2.x, f2.y, -half), new Vector3(f1.x, f1.y, -half), new Vector3(f0.x, f0.y, -half),
                    null, MeshUvMode.BoxProject);
            }

            return geometry.BuildPrimitive("ExtrudePolygon", true);
        }

        #endregion

        #region Вспомогательное

        private static Vector2[] DomeProfile(float radius, float height, int rings)
        {
            Vector2[] profile = new Vector2[rings + 1];

            for (int i = 0; i <= rings; i++)
            {
                float angle = Mathf.PI * 0.5f * i / rings;
                profile[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * height);
            }

            profile[rings].x = 0f;
            return profile;
        }

        private static Vector2[] ProfileNormals(Vector2[] profile, int ringCount)
        {
            Vector2[] normals = new Vector2[ringCount];

            for (int i = 0; i < ringCount; i++)
            {
                Vector2 before = profile[Mathf.Max(0, i - 1)];
                Vector2 after = profile[Mathf.Min(ringCount - 1, i + 1)];
                Vector2 tangent = after - before;
                Vector2 normal = new Vector2(tangent.y, -tangent.x);

                normals[i] = normal.sqrMagnitude > Epsilon ? normal.normalized : Vector2.up;
            }

            return normals;
        }

        private static void AddCap(MeshBuilder builder, Vector2 ring, float direction, int sides, bool smooth)
        {
            Vector3 normal = Vector3.up * direction;
            int centerIndex = builder.AddVertex(null, new Vector3(0f, ring.y, 0f), normal, new Vector2(0.5f, 0.5f));
            int start = builder.VertexCount;

            for (int j = 0; j <= sides; j++)
            {
                float angle = Mathf.PI * 2f * j / sides;
                builder.AddVertex(null,
                    new Vector3(Mathf.Cos(angle) * ring.x, ring.y, Mathf.Sin(angle) * ring.x),
                    normal,
                    new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            }

            for (int j = 0; j < sides; j++)
            {
                // Обход кольца идёт по возрастанию угла, поэтому порядок зависит от стороны крышки.
                builder.AddTriangleByIndex(null, centerIndex, start + j + (direction > 0f ? 1 : 0),
                    start + j + (direction > 0f ? 0 : 1), smooth, false);
            }
        }

        private static Vector3 Axis(int index)
        {
            if (index == 0)
            {
                return Vector3.right;
            }

            return index == 1 ? Vector3.up : Vector3.forward;
        }

        internal static Vector3 Normalize(Vector3 value)
        {
            return MeshBuilder.Normalize(value);
        }

        private static Vector3 SafeNormalize(Vector3 value)
        {
            Vector3 result = MeshBuilder.Normalize(value);
            return result.sqrMagnitude > 0.5f ? result : Vector3.up;
        }

        #endregion
    }
}