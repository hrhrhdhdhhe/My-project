// World Crisis | Слой: Core/Visuals | Ответственный: OpenCode
// Контракт планировки Круглого зала саммита: радиусы, высоты и углы рабочих мест.
//
// Модуль намеренно не зависит ни от GameObject, ни от Transform, ни от фабрики
// моделей: только Vector3 и Mathf. Поэтому его можно проверять headless-стендом,
// а обе локации и режиссёр освещения читают одни и те же числа — разъехаться
// рабочие места и настольные наборы уже не могут.

using UnityEngine;

namespace WorldCrisis
{
    /// <summary>
    /// Единственный источник правды по геометрии зала саммита.
    /// Число рабочих мест совпадает с <c>GeneratedObjectNames.CountryCount</c>:
    /// при появлении седьмой державы правится здесь и там.
    /// </summary>
    public static class SummitLayout
    {
        /// <summary>Число рабочих мест вокруг стола.</summary>
        public const int SeatCount = 6;

        /// <summary>
        /// Мировая координата центра зала: разнесён с бункером, чтобы не пересекались.
        /// ОБЪЯВЛЕН ДО любых static readonly, которые от него зависят: статические
        /// поля инициализируются строго в порядке объявления, иначе производные
        /// координаты посчитаются от нуля.
        /// </summary>
        public static readonly Vector3 Center = new Vector3(0f, 0f, 40f);

        /// <summary>Высота столешницы Круглого стола.</summary>
        public const float TableSurfaceY = TableFrameHeight + TableTopThickness;

        /// <summary>Высота каркаса стола.</summary>
        public const float TableFrameHeight = 0.80f;

        /// <summary>Толщина столешницы.</summary>
        public const float TableTopThickness = 0.06f;

        /// <summary>
        /// Внешний радиус стола. Уменьшен с 3.0 до 1.65 м (диаметр 3.30): при старом
        /// радиусе места диктаторов уходили на 7 м от игрока и тонули в темноте.
        /// </summary>
        public const float TableRadius = 1.65f;

        /// <summary>Радиус центрального колодца голограммы.</summary>
        public const float WellRadius = 0.58f;

        /// <summary>
        /// Высота, на которой стоит центр голографического глобуса/колодца:
        /// (0.0f, 0.82f, 40.0f) в мировых координатах. Чуть ниже столешницы, поэтому
        /// полусфера целиком помещается в углубление и не режет взгляд за стол.
        /// </summary>
        public const float HologramWellY = 0.82f;

        /// <summary>Радиус светящегося голографического глобуса в колодце.</summary>
        public const float HologramGlobeRadius = 0.58f;

        /// <summary>Высота полукупола голографической карты мира над колодцем.</summary>
        public const float HologramDomeHeight = 0.52f;

        /// <summary>Радиус настольных наборов — внутри столешницы.</summary>
        public const float DeskRingRadius = 1.15f;

        /// <summary>
        /// Радиус рабочих мест диктаторов. Обязан быть больше TableRadius, иначе риги
        /// стоят внутри стола. И одновременно больше радиуса грузовой платформы,
        /// иначе диктатор висит в воздухе над ней.
        /// </summary>
        public const float SeatRingRadius = 1.92f;

        /// <summary>Угол места игрока: -90°, то есть южная сторона стола.</summary>
        public const float PlayerSeatAngleDegrees = -90f;

        /// <summary>
        /// ГЛАЗ КАМЕРЫ ИГРОКА ЗА СТОЛОМ. Жёстко (0, 1.42, 37.95): любое смещение по X
        /// уводило камеру в ржавую стену шахты на краю зала, и игрок смотрел в угол.
        /// Никаких «мест», «углов» и «радиусов» в формуле быть не должно — только
        /// число, которое проверяет headless-стенд.
        /// </summary>
        public static readonly Vector3 PlayerEyePosition = new Vector3(0f, 1.42f, 37.95f);

        /// <summary>Поворот камеры в зале саммита: жёстко 12° вниз по X, без рыскания.</summary>
        public static readonly Vector3 PlayerEulerDegrees = new Vector3(12f, 0f, 0f);

        /// <summary>
        /// Мёртвая зона перед камерой игрока: ни один полигон в этом радиусе строиться
        /// не должен, иначе кабина лифта, стойка поручня или стена шахты оказываются
        /// прямо перед глазами. Проверяется и сборщиком, и headless-стендом.
        /// </summary>
        public const float PlayerClearRadius = 1.2f;

        /// <summary>true, если мировая точка попадает в мёртвую зону перед камерой.</summary>
        public static bool IsInPlayerClearance(Vector3 worldPoint)
        {
            return (worldPoint - PlayerEyePosition).sqrMagnitude < PlayerClearRadius * PlayerClearRadius;
        }

        /// <summary>Поворот камеры за столом, собранный из углов.</summary>
        public static Quaternion PlayerEyeRotation
        {
            get { return Quaternion.Euler(PlayerEulerDegrees); }
        }

        /// <summary>Наклон взгляда вниз на колодец с картой мира, градусы.</summary>
        public const float PlayerPitchDegrees = 12f;

        /// <summary>Поле зрения камеры саммита.</summary>
        public const float PlayerFieldOfView = 58f;

        /// <summary>Габарит рига диктатора после вписывания в бокс.</summary>
        public static readonly Vector3 DictatorBoxSize = new Vector3(0.72f, 1.25f, 0.55f);

        /// <summary>Высота, на которой стоят локти сидящих диктаторов (низ бокса).</summary>
        public const float DictatorBoxBaseY = 0.35f;

        /// <summary>Диктаторы должны быть видны крупно: от 1.6 до 4.0 м от камеры.</summary>
        public const float DictatorMinDistance = 1.6f;

        public const float DictatorMaxDistance = 4.0f;

        /// <summary>Верх голов диктаторов в кадре.</summary>
        public const float DictatorHeadMinY = 1.35f;

        public const float DictatorHeadMaxY = 1.68f;

        /// <summary>
        /// Планировка личного бункера. Вынесена сюда рядом с залом саммита, потому что
        /// обе раскладки проверяются одним и тем же headless-стендом: ошибки вида
        /// «прибор висит перед лицом» или «камера оказалась за креслом» — это в первую
        /// очередь ошибки чисел, а не геометрии.
        /// </summary>
        public static class Bunker
        {
            /// <summary>Глаз игрока: сидит в кресле вплотную к столу.</summary>
            public static readonly Vector3 EyePosition = new Vector3(0.00f, 1.32f, -0.55f);

            /// <summary>Наклон взгляда вниз, градусы.</summary>
            public const float PitchDegrees = 20f;

            /// <summary>Поле зрения.</summary>
            public const float FieldOfView = 58f;

            /// <summary>Ближняя плоскость отсечения.</summary>
            public const float NearClip = 0.08f;

            /// <summary>Кресло игрока — целиком за камерой.</summary>
            public const float ChairZ = -0.85f;

            /// <summary>Высота столешницы, на неё нижней гранью встают все приборы.</summary>
            public const float DeskTopY = 0.76f;

            /// <summary>Плита столешницы: 0.06 м толщиной, от 0.70 до 0.76.</summary>
            public const float DeskSlabThickness = 0.06f;

            public const float DeskWidth = 2.60f;

            public const float DeskDepth = 1.10f;

            /// <summary>Центр стола по Z: передний край 0.00, задний 1.10.</summary>
            public const float DeskCenterZ = 0.55f;

            /// <summary>Дальняя стена кабины.</summary>
            public const float BackWallZ = 2.30f;

            /// <summary>
            /// Разворот оборудования лицом к игроку. Локальная лицевая сторона приборов
            /// смотрит в +Z, игрок находится в -Z, поэтому всё разворачивается на 180°.
            /// </summary>
            public const float FacingYaw = 180f;

            /// <summary>
            /// Габариты приборов на столе: имя, предельный размер X/Y/Z в метрах и точка
            /// посадки на столешнице (нижняя грань прибора). Эти же числа использует
            /// BunkerWorldBuilder.FitPropToBox и headless-стенд, поэтому заявленные
            /// размеры проверяемы и не могут разойтись с реальной геометрией.
            /// </summary>
            public static readonly string[] DeskPropNames =
            {
                "Switchboard_Unit",
                "Clipboard",
                "Nuclear_Launch_Box",
                "Orders_CRT_Terminal",
                "DotMatrix_Printer"
            };

            public static readonly Vector3[] DeskPropMaxSize =
            {
                new Vector3(0.78f, 0.24f, 0.38f),
                new Vector3(0.26f, 0.03f, 0.32f),
                new Vector3(0.28f, 0.24f, 0.28f),
                new Vector3(0.54f, 0.46f, 0.42f),
                new Vector3(0.32f, 0.16f, 0.26f)
            };

            public static readonly Vector3[] DeskPropAnchor =
            {
                new Vector3(-0.62f, 0.76f, 0.52f),
                new Vector3(-0.92f, 0.76f, 0.24f),
                new Vector3(0.08f, 0.76f, 0.48f),
                new Vector3(0.66f, 0.76f, 0.56f),
                new Vector3(1.06f, 0.76f, 0.44f)
            };

            public static readonly float[] DeskPropYaw = { 170f, 162f, 180f, 202f, 205f };

            /// <summary>Ни одна точка прибора не должна быть ближе этого расстояния до глаза.</summary>
            public const float MinPropDistanceToEye = 0.42f;

            /// <summary>Верх приборов не должен подниматься выше этой отметки.</summary>
            public const float MaxPropTopY = 1.25f;
        }

        /// <summary>Угол рабочего места. Слот 0 смотрит в сторону входа в зал.</summary>
        public static float GetSeatAngle(int index, int seatCount)
        {
            int count = seatCount > 0 ? seatCount : 1;

            // Слот 0 — место игрока на южной стороне стола (angle = -90°), дальше по кругу.
            return PlayerSeatAngleDegrees * Mathf.Deg2Rad + Mathf.PI * 2f * index / count;
        }

        /// <summary>Положение настольного набора: лежит на столешнице.</summary>
        public static Vector3 GetDeskPosition(int index, int seatCount)
        {
            float angle = GetSeatAngle(index, seatCount);
            return new Vector3(Mathf.Cos(angle) * DeskRingRadius, TableSurfaceY,
                Mathf.Sin(angle) * DeskRingRadius);
        }

        /// <summary>Положение рабочего места диктатора: стоит на грузовой платформе.</summary>
        public static Vector3 GetSeatPosition(int index, int seatCount)
        {
            float angle = GetSeatAngle(index, seatCount);
            return new Vector3(Mathf.Cos(angle) * SeatRingRadius, 0f, Mathf.Sin(angle) * SeatRingRadius);
        }
    }
}
