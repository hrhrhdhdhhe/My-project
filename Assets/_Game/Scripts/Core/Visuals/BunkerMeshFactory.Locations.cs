// World Crisis | Слой: Core | Ответственный: OpenCode
// Сборка локаций целиком: личный бункер (№1) и Круглый зал саммита (№2).
// Расстановка задаётся здесь, состояние города приходит данными — фабрика только читает.

using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    public static partial class BunkerMeshFactory
    {
        /// <summary>Радиус, на котором сидят диктаторы вокруг стола переговоров (метры).</summary>
        public const float BunkerTableRadius = 1.35f;

        /// <summary>Радиус стола саммита.</summary>
        public const float SummitTableRadius = SummitLayout.TableRadius;

        /// <summary>Высота поверхности стола саммита.</summary>
        public const float SummitTableSurfaceY = SummitLayout.TableSurfaceY;

        /// <summary>Радиус настольных наборов на столе (метры, от центра).</summary>
        public const float SummitDeskRingRadius = SummitLayout.DeskRingRadius;

        /// <summary>Радиус рабочих мест диктаторов на грузовых платформах.</summary>
        public const float SummitSeatRingRadius = SummitLayout.SeatRingRadius;

        /// <summary>Угол рабочего места по индексу: 0 — напротив входа в зал.</summary>
        public static float GetSummitSeatAngle(int index)
        {
            return SummitLayout.GetSeatAngle(index, GameDefinitions.AllCountryIds.Length);
        }

        /// <summary>Положение настольного набора (лежит на столе).</summary>
        public static Vector3 GetSummitDeskPosition(int index)
        {
            return SummitLayout.GetDeskPosition(index, GameDefinitions.AllCountryIds.Length);
        }

        /// <summary>Положение рабочего места диктатора (стоит на грузовой платформе).</summary>
        public static Vector3 GetSummitSeatPosition(int index)
        {
            return SummitLayout.GetSeatPosition(index, GameDefinitions.AllCountryIds.Length);
        }

        /// <summary>
        /// Локация №1 «Личный бункер»: пульт связи, терминал ввода приказов с принтером,
        /// ядерный пульт и настенное табло статуса государств.
        /// </summary>
        public static GameObject CreateLocationOneBunker(Transform parent, IReadOnlyList<CityState> cities)
        {
            GameObject root = MakeNode("Location_1_Bunker", parent, Vector3.zero);

            // Пульт связи у дальней стены. Лицевая сторона (диски, трубки, табличка) смотрит
            // в +Z, то есть к игроку: поворот на 180 разворачивал пульт лицом к стене.
            GameObject switchboard = CreateSwitchboardUnit(root.transform);
            switchboard.transform.localPosition = new Vector3(0f, 0f, -1.5f);
            switchboard.transform.localRotation = Quaternion.identity;

            // Терминал ввода приказов слева, принтер справа от него.
            GameObject terminal = CreateOrdersCrtTerminal(root.transform, new Vector3(-1.05f, 0f, 0.55f));
            terminal.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);

            GameObject printer = CreateDotMatrixPrinter(root.transform, new Vector3(-0.35f, 0f, 0.62f));
            printer.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);

            // Ядерный пульт на отдельной тумбе справа.
            MeshBuilder pedestal = new MeshBuilder();
            pedestal.Add(MeshPrimitives.BeveledBox(new Vector3(0.9f, 0.68f, 0.62f), 0.02f),
                MeshBuilder.Trs(new Vector3(0f, 0.34f, 0f), Vector3.zero, Vector3.one), Mats.Concrete(),
                MeshUvMode.BoxProject);
            Seal("Launch_Pedestal", root.transform, new Vector3(1.15f, 0f, 0.5f), pedestal);

            GameObject launchBox = CreateNuclearLaunchBox(root.transform, new Vector3(1.15f, 0.68f, 0.5f));
            launchBox.transform.localRotation = Quaternion.Euler(0f, -24f, 0f);

            // Настенное табло «СТАТУС ГОСУДАРСТВА» над терминалом. Секции-диорамы и табличка
            // лежат на +Z-стороне узла, поэтому разворот на 180 был лишним.
            GameObject board = CreateStateStatusBoard(root.transform, new Vector3(0f, 1.5f, -2.1f), cities);
            board.transform.localRotation = Quaternion.identity;

            return root;
        }

        /// <summary>
        /// Локация №2 «Круглый зал саммита»: шестиметровый стол с архипелагами держав,
        /// подвесной неоновый куб часов и настольный набор предметов.
        /// </summary>
        public static GameObject CreateLocationTwoSummit(Transform parent)
        {
            GameObject root = MakeNode("Location_2_Summit", parent, Vector3.zero);

            GameObject table = CreateSummitRoundTable(root.transform, Vector3.zero);

            // Часы висят над столом на высоте 3.4 м.
            CreateDoomsdayHexClock(root.transform, new Vector3(0f, 3.4f, 0f));

            // Настольный набор у каждого места: микрофон, шредер, договор, стакан, папка, магнитофон.
            // Позиция берётся из общей раскладки GetSummitDeskPosition, иначе наборы
            // разъезжаются с рабочими местами диктаторов.
            for (int i = 0; i < GameDefinitions.AllCountryIds.Length; i++)
            {
                float angle = GetSummitSeatAngle(i);
                CreateSummitPersonalSet(root.transform, GetSummitDeskPosition(i), angle * Mathf.Rad2Deg + 180f);
            }

            // Шесть диктаторов по краям стола, на грузовых платформах.
            CreateSummitDictators(root.transform);

            return table.transform.parent != null ? table : root;
        }

        /// <summary>
        /// Расставляет риги всех шести держав по кругу. Каждый диктатор смотрит в центр стола,
        /// предплечья лежат на подлокотнике, голова может доворачиваться к собеседнику.
        /// </summary>
        public static GameObject CreateSummitDictators(Transform parent)
        {
            GameObject root = MakeNode("Summit_Dictators", parent, Vector3.zero);

            for (int i = 0; i < GameDefinitions.AllCountryIds.Length; i++)
            {
                CountryId id = GameDefinitions.AllCountryIds[i];
                float angle = GetSummitSeatAngle(i);

                GameObject rig = CreateDictatorRig(id, root.transform);

                // Позиция берётся из общей раскладки, иначе диктатор встаёт не на своё место.
                rig.transform.localPosition = GetSummitSeatPosition(i);

                // Разворот лицом к центру: персонаж стоит на +Z, поэтому доворачиваем на угол минус.
                rig.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 180f, 0f);
            }

            return root;
        }

        /// <summary>Шесть ригов диктаторов вокруг переговорного стола в бункере.</summary>
        public static GameObject CreateBunkerDictators(Transform parent)
        {
            GameObject root = MakeNode("Bunker_Dictators", parent, Vector3.zero);

            for (int i = 0; i < GameDefinitions.AllCountryIds.Length; i++)
            {
                CountryId id = GameDefinitions.AllCountryIds[i];
                float angle = Mathf.PI * 2f * i / GameDefinitions.AllCountryIds.Length + Mathf.PI / 6f;
                Vector3 position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * BunkerTableRadius;

                GameObject rig = CreateDictatorRig(id, root.transform);
                rig.transform.localPosition = position;
                rig.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 180f, 0f);
            }

            return root;
        }

        /// <summary>
        /// Полный набор моделей проекта: обе локации и шесть диктаторов.
        /// Удобная точка входа для редакторного меню и для проверки перед сборкой.
        /// </summary>
        public static GameObject CreateFullScene(Transform parent, IReadOnlyList<CityState> cities)
        {
            GameObject root = MakeNode("WorldCrisis_Visuals", parent, Vector3.zero);

            CreateLocationOneBunker(root.transform, cities);

            GameObject summit = MakeNode("Summit", root.transform, new Vector3(24f, 0f, 0f));
            CreateLocationTwoSummit(summit.transform);

            return root;
        }
    }
}