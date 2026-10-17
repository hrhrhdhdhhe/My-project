// World Crisis | Слой: Core | Ответственный: OpenCode
// Триангуляция простых многоугольников клиппированием ушей. Нужна для плоских торцевых граней
// невыпуклых контуров: лист договора, эмблемы, нашивки, силуэты голов.

using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    /// <summary>
    /// Классический алгоритм «уши» для простого многоугольника без дыр.
    /// Возвращает индексы треугольников в порядке обхода контура против часовой стрелки.
    /// </summary>
    public static class PolygonTriangulator
    {
        private const float Epsilon = 0.000001f;

        /// <summary>
        /// Триангулирует контур. Если контур вырожденный, возвращает пустой массив —
        /// вызывающий код просто не получит торцевую грань и не сломается.
        /// </summary>
        public static int[] EarClip(Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
            {
                return new int[0];
            }

            List<int> indices = new List<int>(polygon.Length);
            float signedArea = SignedArea(polygon);
            bool counterClockwise = signedArea > 0f;

            for (int i = 0; i < polygon.Length; i++)
            {
                indices.Add(counterClockwise ? i : polygon.Length - 1 - i);
            }

            List<int> triangles = new List<int>(polygon.Length);
            int guard = indices.Count * indices.Count + 16;

            while (indices.Count > 3 && guard-- > 0)
            {
                bool earFound = false;

                for (int i = 0; i < indices.Count; i++)
                {
                    int previous = indices[(i - 1 + indices.Count) % indices.Count];
                    int current = indices[i];
                    int next = indices[(i + 1) % indices.Count];

                    if (!IsEar(polygon, indices, previous, current, next, counterClockwise))
                    {
                        continue;
                    }

                    triangles.Add(previous);
                    triangles.Add(current);
                    triangles.Add(next);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    // Самопересекающийся или вырожденный контур: достраиваем веером,
                    // лучше лишний треугольник, чем ничего.
                    for (int i = 1; i < indices.Count - 1; i++)
                    {
                        triangles.Add(indices[0]);
                        triangles.Add(indices[i]);
                        triangles.Add(indices[i + 1]);
                    }

                    return triangles.ToArray();
                }
            }

            if (indices.Count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }

            return triangles.ToArray();
        }

        /// <summary>Площадь со знаком: положительная — обход против часовой стрелки.</summary>
        public static float SignedArea(Vector2[] polygon)
        {
            float sum = 0f;

            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                sum += (b.x - a.x) * (b.y + a.y);
            }

            return -sum * 0.5f;
        }

        private static bool IsEar(Vector2[] polygon, List<int> indices, int previous, int current, int next,
            bool counterClockwise)
        {
            Vector2 a = polygon[previous];
            Vector2 b = polygon[current];
            Vector2 c = polygon[next];

            // Выпуклость: внутренний угол должен смотреть наружу контура.
            float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
            if (counterClockwise ? cross <= Epsilon : cross >= -Epsilon)
            {
                return false;
            }

            for (int i = 0; i < indices.Count; i++)
            {
                int candidate = indices[i];
                if (candidate == previous || candidate == current || candidate == next)
                {
                    continue;
                }

                if (IsInside(polygon[candidate], a, b, c))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsInside(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(point, a, b);
            float d2 = Cross(point, b, c);
            float d3 = Cross(point, c, a);

            bool hasNegative = d1 < Epsilon || d2 < Epsilon || d3 < Epsilon;
            bool hasPositive = d1 > Epsilon || d2 > Epsilon || d3 > Epsilon;
            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }
    }
}