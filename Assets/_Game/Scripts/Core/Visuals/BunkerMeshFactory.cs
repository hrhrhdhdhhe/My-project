// World Crisis | Слой: Core | Ответственный: OpenCode
// Процедурная фабрика моделей локаций «Красная Линия»: приборы личного бункера и предметы
// Круглого зала саммита. Каждый узел Transform соответствует имени из GeneratedObjectNames,
// поэтому сцена собирается целиком в коде и адресуется только поимённо.
//
// Стиль: Industrial Lo-Fi Brutalism / Crisp Retro-PBR. Фасеточные брусы, гранёные цилиндры,
// настоящие булевы вырезы, заклепки, многослойные материалы.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    /// <summary>
    /// Сборщик 3D-моделей локаций. Правила: весь код в Core/Visuals, namespace WorldCrisis,
    /// никаких сериализованных ссылок на графику — узлы адресуются поимённо.
    /// </summary>
    public static partial class BunkerMeshFactory
    {
        #region Общие узлы и утилиты

        /// <summary>Создаёт пустой узел с заданным именем и локальным положением.</summary>
        internal static GameObject MakeNode(string nodeName, Transform parent, Vector3 localPosition)
        {
            GameObject node = new GameObject(nodeName);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPosition;
            return node;
        }

        /// <summary>Создаёт узел с заданным именем, положением и поворотом.</summary>
        internal static GameObject MakeNode(string nodeName, Transform parent, Vector3 localPosition,
            Vector3 localEuler)
        {
            GameObject node = MakeNode(nodeName, parent, localPosition);
            node.transform.localRotation = Quaternion.Euler(localEuler);
            return node;
        }

        /// <summary>
        /// Собирает узел из готового MeshBuilder. Геометрия считается в локальных координатах узла,
        /// начало координат — в точке шарнира (важно для Jaw_Pivot и прочих пивотов).
        /// </summary>
        internal static GameObject Seal(string nodeName, Transform parent, Vector3 localPosition, MeshBuilder builder)
        {
            GameObject node = MakeNode(nodeName, parent, localPosition);
            Mesh mesh = builder.Build(nodeName + "_Mesh", true);

            if (mesh.vertexCount == 0)
            {
                Debug.LogWarning("[WorldCrisis] BunkerMeshFactory: узел " + nodeName + " собран без геометрии.");
                return node;
            }

            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.AddComponent<MeshRenderer>().sharedMaterials = builder.GetMaterials();
            return node;
        }

        /// <summary>Детерминированный генератор мелких пропорций — одинаково на каждом запуске.</summary>
        internal sealed class DetRandom
        {
            private uint _state;

            public DetRandom(int seed)
            {
                _state = seed == 0 ? 0x9E3779B9u : (uint)seed;
            }

            public float Next01()
            {
                _state = _state * 1664525u + 1013904223u;
                return (_state >> 8) / 16777216f;
            }

            public float Range(float min, float max)
            {
                return min + (max - min) * Next01();
            }

            public int RangeInt(int minInclusive, int maxExclusive)
            {
                return maxExclusive <= minInclusive
                    ? minInclusive
                    : minInclusive + (int)(Next01() * (maxExclusive - minInclusive)) % (maxExclusive - minInclusive);
            }
        }

        /// <summary>Квад с UV 0..1, растянутым под ячейку текстурного атласа.</summary>
        internal static Mesh AtlasQuad(float sizeX, float sizeY, Rect uvRect)
        {
            Mesh quad = MeshPrimitives.Plane(sizeX, sizeY);
            Vector2[] uv = quad.uv;

            for (int i = 0; i < uv.Length; i++)
            {
                uv[i] = new Vector2(uvRect.x + uv[i].x * uvRect.width, uvRect.y + uv[i].y * uvRect.height);
            }

            quad.uv = uv;
            return quad;
        }

        /// <summary>Ряды заклепок вдоль ребра — фирменный приём брутализма.</summary>
        internal static void AddRivetRow(MeshBuilder builder, Vector3 from, Vector3 to, int count, float radius,
            float height, Material material, Vector3 upDirection)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                Vector3 point = Vector3.Lerp(from, to, t);
                MeshBuilder.Trs(point, new Vector3(0f, 0f, 0f), Vector3.one);

                builder.Add(MeshPrimitives.RivetDome(radius, height),
                    MeshBuilder.Align(point, upDirection), material, MeshUvMode.BoxProject);
            }
        }

        #endregion

        /// <summary>Добавляет выпуклые MeshCollider-ы ко всем узлам с геометрией.</summary>
        public static void AddColliders(GameObject root, bool convex)
        {
            if (root == null)
            {
                return;
            }

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];

                if (filter == null || filter.sharedMesh == null || filter.GetComponent<MeshCollider>() != null)
                {
                    continue;
                }

                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = convex;
            }
        }

        #region Материалы

        /// <summary>
        /// Ленивый кэш материалов проекта. Каждый материал — один на весь процесс:
        /// текстуры генерируются один раз (RetroPbrTextureGenerator тоже кэширует).
        /// </summary>
        private static class Mats
        {
            private static readonly Dictionary<string, Material> s_Cache = new Dictionary<string, Material>();

            private static Material Get(string key, Func<Material> factory)
            {
                Material material;
                if (s_Cache.TryGetValue(key, out material) && material != null)
                {
                    return material;
                }

                material = factory();
                s_Cache.Add(key, material);
                return material;
            }

            public static Material CastIron()
            {
                return Get("cast_iron",
                    () => RetroPbrTextureGenerator.GetCastIron().CreateMaterial("Mat_CastIron", RetroBlendMode.Opaque));
            }

            public static Material ColdSteel()
            {
                return Get("cold_steel",
                    () => RetroPbrTextureGenerator.GetColdSteel().CreateMaterial("Mat_ColdSteel", RetroBlendMode.Opaque));
            }

            public static Material Concrete()
            {
                return Get("concrete",
                    () => RetroPbrTextureGenerator.GetBunkerConcrete().CreateMaterial("Mat_BunkerConcrete",
                        RetroBlendMode.Opaque));
            }

            public static Material Bakelite(Color color)
            {
                string key = "bakelite_" + ColorUtility.ToHtmlStringRGB(color);
                return Get(key,
                    () => RetroPbrTextureGenerator.GetBakelite(color).CreateMaterial("Mat_Bakelite_" + key,
                        RetroBlendMode.Opaque));
            }

            public static Material Hazard()
            {
                return Get("hazard",
                    () => RetroPbrTextureGenerator.GetHazardStripes().CreateMaterial("Mat_HazardStripes",
                        RetroBlendMode.Opaque));
            }

            public static Material Paper()
            {
                return Get("paper",
                    () => RetroPbrTextureGenerator.GetTreatyPaper().CreateMaterial("Mat_TreatyPaper",
                        RetroBlendMode.Opaque));
            }

            public static Material Scope()
            {
                return Get("scope",
                    () => RetroPbrTextureGenerator.GetOscilloscopeFace().CreateMaterial("Mat_OscilloscopeFace",
                        RetroBlendMode.Opaque));
            }

            public static Material Segment(Color color)
            {
                string key = "segment_" + ColorUtility.ToHtmlStringRGB(color);
                return Get(key,
                    () => RetroPbrTextureGenerator.GetSevenSegmentAtlas(color).CreateMaterial("Mat_SevenSegment_" + key,
                        RetroBlendMode.Opaque));
            }

            public static Material Cloth(Color color)
            {
                string key = "cloth_" + ColorUtility.ToHtmlStringRGB(color);
                return Get(key,
                    () => RetroPbrTextureGenerator.GetCloth(color).CreateMaterial("Mat_Cloth_" + key,
                        RetroBlendMode.Opaque));
            }

            public static Material Gold()
            {
                return Get("gold", () => RetroPbrTextureGenerator.GetGold().CreateMaterial("Mat_Gold", RetroBlendMode.Opaque));
            }

            public static Material Rust()
            {
                return Get("rust",
                    () => RetroPbrTextureGenerator.GetHeavyRust().CreateMaterial("Mat_HeavyRust", RetroBlendMode.Opaque));
            }

            public static Material Frost()
            {
                return Get("frost",
                    () => RetroPbrTextureGenerator.GetFrost().CreateMaterial("Mat_Frost", RetroBlendMode.Opaque));
            }

            public static Material PlasticSkin()
            {
                return Get("plastic_skin",
                    () => RetroPbrTextureGenerator.GetPlasticSkin().CreateMaterial("Mat_PlasticSkin",
                        RetroBlendMode.Opaque));
            }

            public static Material Kevlar()
            {
                return Get("kevlar",
                    () => RetroPbrTextureGenerator.GetKevlar().CreateMaterial("Mat_Kevlar", RetroBlendMode.Opaque));
            }

            public static Material SmokedGlass()
            {
                return Get("smoked_glass",
                    () => RetroPbrTextureGenerator.GetSmokedGlass().CreateMaterial("Mat_SmokedGlass",
                        RetroBlendMode.Transparent));
            }

            public static Material BlackWater()
            {
                return Get("black_water",
                    () => RetroPbrTextureGenerator.GetBlackWater().CreateMaterial("Mat_BlackWater",
                        RetroBlendMode.Opaque));
            }

            public static Material Bronze()
            {
                return Get("bronze",
                    () => RetroPbrTextureGenerator.GetBronze().CreateMaterial("Mat_Bronze", RetroBlendMode.Opaque));
            }

            public static Material Neon(Color color)
            {
                string key = "neon_" + ColorUtility.ToHtmlStringRGB(color);
                return Get(key,
                    () => RetroPbrTextureGenerator.GetNeon(color).CreateMaterial("Mat_Neon_" + key,
                        RetroBlendMode.Additive));
            }

            public static Material Lens(Color color, float smoothness, float metallic)
            {
                string key = "lens_" + ColorUtility.ToHtmlStringRGB(color) + "_" + smoothness + "_" + metallic;
                return Get(key,
                    () => RetroPbrTextureGenerator.GetFlatColor(color).CreateMaterial("Mat_Lens_" + key,
                        RetroBlendMode.Opaque));
            }

            /// <summary>Сбрасывает кэш материалов. Нужно редактору после правки палитры.</summary>
            public static void Clear()
            {
                s_Cache.Clear();
            }
        }

        #endregion
    }
}