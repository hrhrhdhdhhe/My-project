// World Crisis | Слой: Core | Ответственный: OpenCode
// Конструктор геометрии: примитивы и сборка меша с корректной UV-развёрткой и разбивкой по материалам.
// Базис процедурной графики: фабрика бункера и саммита собирает модели только отсюда.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldCrisis
{
    /// <summary>Способ развёртки UV при добавлении готового меша в конструктор.</summary>
    public enum MeshUvMode
    {
        /// <summary>Сохранить UV источника без изменений.</summary>
        Keep = 0,

        /// <summary>Трипланарная проекция по доминирующей оси нормали. Основной режим для тайловых PBR.</summary>
        BoxProject = 1,

        /// <summary>Цилиндрическая проекция вокруг оси Y. Для барабанов, дисков, труб.</summary>
        Cylindrical = 2
    }

    /// <summary>
    /// Аккумулятор геометрии. Кладёт вершины в общий массив и группирует треугольники по материалу,
    /// поэтому один узел Transform может нести несколько материалов через субмеши.
    /// Примитивы добавляются с материалом null (служебная группа 0) и позже переносятся в материалные группы.
    /// </summary>
    public sealed class MeshBuilder
    {
        private const float QuantizeStep = 10000f;

        private sealed class Group
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        private readonly List<Group> _groups = new List<Group>();
        private readonly Dictionary<int, Group> _bySlot = new Dictionary<int, Group>();
        private readonly List<Material> _slotMaterials = new List<Material>();

        private Matrix4x4 _local = Matrix4x4.identity;
        private float _uvScale = 1f;

        public MeshBuilder()
        {
            // Служебная группа 0: геометрия примитивов до переноса в материал.
            _slotMaterials.Add(null);
            _groups.Add(new Group());
            _bySlot.Add(0, _groups[0]);
        }

        /// <summary>Число материалов в собранном меше (служебная группа не считается).</summary>
        public int MaterialCount
        {
            get { return _slotMaterials.Count - 1; }
        }

        /// <summary>Вершин в служебной группе — используется примитивами как счётчик индексов.</summary>
        public int VertexCount
        {
            get { return _groups[0].Positions.Count; }
        }

        /// <summary>Треугольников во всех группах.</summary>
        public int TriangleCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _groups.Count; i++)
                {
                    total += _groups[i].Triangles.Count / 3;
                }

                return total;
            }
        }

        /// <summary>Масштаб тайлинга UV для всех дальнейших добавлений.</summary>
        public MeshBuilder WithUvScale(float uvScale)
        {
            _uvScale = uvScale > 0.0001f ? uvScale : 0.0001f;
            return this;
        }

        /// <summary>Матрица объекта целиком; все AddLocal домножаются на неё.</summary>
        public MeshBuilder SetLocal(Matrix4x4 matrix)
        {
            _local = matrix;
            return this;
        }

        #region Матрицы-хелперы

        /// <summary>Матрица из позиции, эйлеровых углов и масштаба.</summary>
        public static Matrix4x4 Trs(Vector3 position, Vector3 euler, Vector3 scale)
        {
            return Matrix4x4.TRS(position, Quaternion.Euler(euler), scale);
        }

        /// <summary>Матрица из позиции и единичного поворота/масштаба.</summary>
        public static Matrix4x4 Pos(Vector3 position)
        {
            return Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
        }

        /// <summary>Матрица, разворачивающая локальную ось Y вдоль направления direction.</summary>
        public static Matrix4x4 Align(Vector3 position, Vector3 direction)
        {
            Vector3 up = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.up;
            return Matrix4x4.TRS(position, Quaternion.FromToRotation(Vector3.up, up), Vector3.one);
        }

        #endregion

        #region Индексный API для примитивов

        /// <summary>Добавляет вершину в группу материала и возвращает её индекс.</summary>
        public int AddVertex(Material material, Vector3 position, Vector3 normal, Vector2 uv)
        {
            Group group = GetGroup(material);
            int index = group.Positions.Count;
            group.Positions.Add(position);
            group.Normals.Add(normal);
            group.Uvs.Add(uv);
            return index;
        }

        /// <summary>Читает позицию вершины по индексу (для расчёта плоских нормалей).</summary>
        public Vector3 GetPosition(Material material, int index)
        {
            return GetGroup(material).Positions[index];
        }

        /// <summary>
        /// Треугольник по индексам. При smooth используются нормали вершин, иначе нормаль грани
        /// считается по геометрии и вершины дублируются — это даёт чёткие фаски ретро-стиля.
        /// </summary>
        public void AddTriangleByIndex(Material material, int i0, int i1, int i2, bool smooth, bool flip)
        {
            Group group = GetGroup(material);
            Vector3 p0 = group.Positions[i0];
            Vector3 p1 = group.Positions[i1];
            Vector3 p2 = group.Positions[i2];

            if (flip)
            {
                int tmp = i1;
                i1 = i2;
                i2 = tmp;
            }

            Vector3 edge1 = p1 - p0;
            Vector3 edge2 = p2 - p0;

            if (edge1.sqrMagnitude < 0.00000001f && edge2.sqrMagnitude < 0.00000001f)
            {
                return;
            }

            if (smooth)
            {
                group.Triangles.Add(i0);
                group.Triangles.Add(i1);
                group.Triangles.Add(i2);
                return;
            }

            Vector3 normal = Normalize(Vector3.Cross(edge1, edge2));
            if (normal.sqrMagnitude < 0.5f)
            {
                return;
            }

            int baseIndex = group.Positions.Count;
            group.Positions.Add(p0);
            group.Positions.Add(p1);
            group.Positions.Add(p2);
            group.Normals.Add(normal);
            group.Normals.Add(normal);
            group.Normals.Add(normal);
            group.Uvs.Add(group.Uvs[i0]);
            group.Uvs.Add(group.Uvs[i1]);
            group.Uvs.Add(group.Uvs[i2]);

            group.Triangles.Add(baseIndex);
            group.Triangles.Add(baseIndex + 1);
            group.Triangles.Add(baseIndex + 2);
        }

        /// <summary>Четырёхугольник по индексам в порядке i0-i1-i2-i3 (обход против часовой стрелки).</summary>
        public void AddQuadByIndex(Material material, int i0, int i1, int i2, int i3, bool smooth)
        {
            Group group = GetGroup(material);
            Vector3 p0 = group.Positions[i0];
            Vector3 p1 = group.Positions[i1];
            Vector3 p3 = group.Positions[i3];

            Vector3 cross = Vector3.Cross(p1 - p0, p3 - p0);

            if (cross.sqrMagnitude < 0.00000001f)
            {
                // Вырожденный четырёхугольник (вершина конуса, полюс сферы): рёбра i0-i3 нет,
                // но половины квада — полноценные треугольники, их и оставляем.
                AddTriangleByIndex(material, i0, i1, i2, smooth, false);
                AddTriangleByIndex(material, i0, i2, i3, smooth, false);
                return;
            }

            // Порядок может быть вывернут (зависит от профиля примитива) — приводим к правильному.
            Vector3 centerNormal = Normalize(Vector3.Cross(p1 - p0, group.Positions[i2] - p0));
            if (centerNormal.sqrMagnitude > 0.5f && Vector3.Dot(cross, centerNormal) < 0f)
            {
                int tmp = i1;
                i1 = i3;
                i3 = tmp;
            }

            AddTriangleByIndex(material, i0, i1, i2, smooth, false);
            AddTriangleByIndex(material, i0, i2, i3, smooth, false);
        }

        #endregion

        #region Добавление готовых мешей

        /// <summary>Добавляет готовый меш, применяя матрицу и пересчитывая UV выбранным способом.</summary>
        public MeshBuilder Add(Mesh source, Matrix4x4 matrix, Material material, MeshUvMode uvMode)
        {
            // null-материал — это служебная группа 0: примитивы складывают туда геометрию,
            // которую затем переносят в реальные материалы через Add. Пустой меш игнорируем.
            if (source == null)
            {
                return this;
            }

            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            int[] triangles = source.triangles;

            if (positions == null || positions.Length == 0 || triangles == null || triangles.Length < 3)
            {
                return this;
            }

            Group group = GetGroup(material);
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            bool hasNormals = normals != null && normals.Length == positions.Length;

            if (uvMode == MeshUvMode.Keep)
            {
                int baseIndex = group.Positions.Count;

                for (int i = 0; i < positions.Length; i++)
                {
                    group.Positions.Add(matrix.MultiplyPoint3x4(positions[i]));
                    Vector3 n = hasNormals ? normalMatrix.MultiplyVector(normals[i]) : Vector3.up;
                    group.Normals.Add(Normalize(n).sqrMagnitude > 0.5f ? n : Vector3.up);
                    group.Uvs.Add(uvs != null && uvs.Length == positions.Length ? uvs[i] : Vector2.zero);
                }

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = baseIndex + triangles[i];
                    int b = baseIndex + triangles[i + 1];
                    int c = baseIndex + triangles[i + 2];

                    if (a != b && b != c && a != c)
                    {
                        group.Triangles.Add(a);
                        group.Triangles.Add(b);
                        group.Triangles.Add(c);
                    }
                }

                return this;
            }

            Dictionary<long, int> shared = new Dictionary<long, int>(positions.Length * 2);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                Vector3 p0 = matrix.MultiplyPoint3x4(positions[i0]);
                Vector3 p1 = matrix.MultiplyPoint3x4(positions[i1]);
                Vector3 p2 = matrix.MultiplyPoint3x4(positions[i2]);

                if ((p1 - p0).sqrMagnitude < 0.00000001f || (p2 - p0).sqrMagnitude < 0.00000001f)
                {
                    continue;
                }

                Vector3 faceNormal = Normalize(Vector3.Cross(p1 - p0, p2 - p0));
                Vector3 n0 = hasNormals ? normalMatrix.MultiplyVector(normals[i0]) : faceNormal;
                Vector3 n1 = hasNormals ? normalMatrix.MultiplyVector(normals[i1]) : faceNormal;
                Vector3 n2 = hasNormals ? normalMatrix.MultiplyVector(normals[i2]) : faceNormal;

                int a = AddProjected(group, shared, p0, SafeNormal(n0, faceNormal), uvMode);
                int b = AddProjected(group, shared, p1, SafeNormal(n1, faceNormal), uvMode);
                int c = AddProjected(group, shared, p2, SafeNormal(n2, faceNormal), uvMode);

                if (a == b || b == c || a == c)
                {
                    continue;
                }

                group.Triangles.Add(a);
                group.Triangles.Add(b);
                group.Triangles.Add(c);
            }

            return this;
        }

        /// <summary>Треугольник с плоской нормалью — для клиньев, зубьев, осколков.</summary>
        public MeshBuilder AddLocal(Mesh source, Vector3 position, Vector3 euler, Vector3 scale, Material material,
            MeshUvMode uvMode)
        {
            return Add(source, Trs(position, euler, scale) * _local, material, uvMode);
        }

        /// <summary>Четырёхугольник с плоской нормалью; обход приводится к контрчасовой стрелке.</summary>
        public MeshBuilder AddTriangle(Vector3 a, Vector3 b, Vector3 c, Material material, MeshUvMode uvMode)
        {
            Vector3 normal = Normalize(Vector3.Cross(b - a, c - a));
            if (normal.sqrMagnitude < 0.5f)
            {
                return this;
            }

            Group group = GetGroup(material);
            int baseIndex = group.Positions.Count;
            Vector3[] points = { a, b, c };
            Vector2[] uvs = BoxUvs(points, normal);

            for (int i = 0; i < 3; i++)
            {
                group.Positions.Add(points[i]);
                group.Normals.Add(normal);
                group.Uvs.Add(uvs[i]);
            }

            group.Triangles.Add(baseIndex);
            group.Triangles.Add(baseIndex + 1);
            group.Triangles.Add(baseIndex + 2);
            return this;
        }

        /// <summary>Проволока/нить по ломаной: кабели, швы, усики, провода.</summary>
        public MeshBuilder AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Material material,
            MeshUvMode uvMode)
        {
            Vector3 n = Normalize(normal);
            if (n.sqrMagnitude < 0.5f)
            {
                n = Normalize(Vector3.Cross(b - a, c - a));
            }

            if (n.sqrMagnitude < 0.5f)
            {
                return this;
            }

            if (Vector3.Dot(Vector3.Cross(b - a, d - a), n) < 0f)
            {
                Vector3 tmp = b;
                b = d;
                d = tmp;
            }

            Group group = GetGroup(material);
            int baseIndex = group.Positions.Count;
            Vector3[] points = { a, b, c, d };
            Vector2[] uvs = BoxUvs(points, n);

            for (int i = 0; i < 4; i++)
            {
                group.Positions.Add(points[i]);
                group.Normals.Add(n);
                group.Uvs.Add(uvs[i]);
            }

            group.Triangles.Add(baseIndex);
            group.Triangles.Add(baseIndex + 1);
            group.Triangles.Add(baseIndex + 2);
            group.Triangles.Add(baseIndex);
            group.Triangles.Add(baseIndex + 2);
            group.Triangles.Add(baseIndex + 3);
            return this;
        }

        /// <summary>Тангенты считаются только если запрошено — они нужны материалам с картой нормалей.</summary>
        public MeshBuilder AddPolyline(Vector3[] path, float radius, int sides, Material material, MeshUvMode uvMode)
        {
            Mesh source = MeshPrimitives.Tube(path, radius, sides);
            return Add(source, _local, material, uvMode);
        }

        #endregion

        #region Сборка

        /// <summary>
        /// Собирает единый Mesh с субмешами по материалам. Служебная группа 0 всегда пуста,
        /// потому что примитивы переносятся в материалные группы через Add.
        /// </summary>
        public Mesh Build(string meshName, bool recalculateTangents)
        {
            return Assemble(meshName, recalculateTangents, 1);
        }

        /// <summary>
        /// Собирает меш примитива: только служебная группа 0, один субмеш без материалов.
        /// Примитивы ничего не знают о материалах — их геометрия копируется через Add.
        /// </summary>
        public Mesh BuildPrimitive(string meshName, bool recalculateTangents)
        {
            return Assemble(meshName, recalculateTangents, 0);
        }

        private Mesh Assemble(string meshName, bool recalculateTangents, int firstGroup)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;

            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<List<int>> submeshes = new List<List<int>>();

            for (int g = firstGroup; g < _groups.Count; g++)
            {
                Group group = _groups[g];
                int offset = positions.Count;
                positions.AddRange(group.Positions);
                normals.AddRange(group.Normals);
                uvs.AddRange(group.Uvs);

                List<int> indices = new List<int>(group.Triangles.Count);
                for (int i = 0; i < group.Triangles.Count; i++)
                {
                    indices.Add(group.Triangles[i] + offset);
                }

                submeshes.Add(indices);
            }

            if (positions.Count == 0)
            {
                Debug.LogWarning("[WorldCrisis] MeshBuilder.Build: ?????? ????????? (" + meshName + ").");
                return mesh;
            }

            mesh.indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;

            for (int g = 0; g < submeshes.Count; g++)
            {
                mesh.SetTriangles(submeshes[g], g, false);
            }

            if (recalculateTangents)
            {
                TryRecalculateTangents(mesh);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Материалы в порядке субмешей. Порядок совпадает с порядком в Build.</summary>
        public Material[] GetMaterials()
        {
            Material[] result = new Material[MaterialCount];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = _slotMaterials[i + 1];
            }

            return result;
        }

        private static void TryRecalculateTangents(Mesh mesh)
        {
            try
            {
                mesh.RecalculateTangents();
            }
            catch (Exception exception)
            {
                // Вырожденная UV-развёртка — не повод ронять генерацию, меш останется без тангентов.
                Debug.LogWarning("[WorldCrisis] ?? ??????? ????????? ????????: " + exception.Message);
            }
        }

        #endregion

        #region Внутреннее

        private Group GetGroup(Material material)
        {
            int slot = material == null ? 0 : FindSlot(material);
            Group group;
            if (_bySlot.TryGetValue(slot, out group))
            {
                return group;
            }

            group = new Group();
            _bySlot.Add(slot, group);
            _groups.Add(group);
            return group;
        }

        private int FindSlot(Material material)
        {
            for (int i = 1; i < _slotMaterials.Count; i++)
            {
                if (ReferenceEquals(_slotMaterials[i], material))
                {
                    return i;
                }
            }

            _slotMaterials.Add(material);
            return _slotMaterials.Count - 1;
        }

        private int AddProjected(Group group, Dictionary<long, int> shared, Vector3 position, Vector3 normal,
            MeshUvMode uvMode)
        {
            Vector3 abs = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            int axis = abs.y >= abs.x && abs.y >= abs.z ? 1 : (abs.x >= abs.z ? 0 : 2);

            long key = Quantize(position.x) * 73856093L
                       ^ Quantize(position.y) * 19349663L
                       ^ Quantize(position.z) * 83492791L
                       ^ (long)(axis + 1) * 2654435761L;

            int existing;
            if (shared.TryGetValue(key, out existing))
            {
                return existing;
            }

            int index = group.Positions.Count;
            group.Positions.Add(position);
            group.Normals.Add(normal);
            group.Uvs.Add(Project(position, normal, axis, uvMode));
            shared.Add(key, index);
            return index;
        }

        private Vector2 Project(Vector3 position, Vector3 normal, int axis, MeshUvMode uvMode)
        {
            if (uvMode == MeshUvMode.Cylindrical)
            {
                float angle = Mathf.Atan2(position.z, position.x);
                return new Vector2((angle / (Mathf.PI * 2f)) + 0.5f, position.y) * _uvScale;
            }

            float u;
            float v;

            switch (axis)
            {
                case 0:
                    u = -position.z;
                    v = position.y;
                    break;
                case 1:
                    u = position.x;
                    v = -position.z;
                    break;
                default:
                    u = position.x;
                    v = position.y;
                    break;
            }

            return new Vector2(u, v) * _uvScale;
        }

        private Vector2[] BoxUvs(Vector3[] points, Vector3 normal)
        {
            Vector3 abs = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            int axis = abs.y >= abs.x && abs.y >= abs.z ? 1 : (abs.x >= abs.z ? 0 : 2);

            Vector2[] uvs = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                uvs[i] = Project(points[i], normal, axis, MeshUvMode.BoxProject);
            }

            return uvs;
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * QuantizeStep);
        }

        private static Vector3 SafeNormal(Vector3 value, Vector3 fallback)
        {
            Vector3 result = Normalize(value);
            return result.sqrMagnitude > 0.5f ? result : fallback;
        }

        internal static Vector3 Normalize(Vector3 value)
        {
            float length = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z);
            if (length < 0.00001f)
            {
                return Vector3.zero;
            }

            return new Vector3(value.x / length, value.y / length, value.z / length);
        }

        #endregion
    }
}