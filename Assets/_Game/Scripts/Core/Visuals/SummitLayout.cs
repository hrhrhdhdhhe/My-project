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
        public const float TableSurfaceY = 0.78f;

        /// <summary>Высота каркаса стола.</summary>
        public const float TableFrameHeight = 0.72f;

        /// <summary>Толщина столешницы.</summary>
        public const float TableTopThickness = 0.06f;

        /// <summary>
        /// Внешний радиус стола (1-в-1 по концепт-арту). Кольцо диктаторов стоит
        /// на 1.75 м, поэтому 1.45 — единственный радиус, при котором никто не
        /// оказывается внутри столешницы.
        /// </summary>
        public const float TableRadius = 1.45f;

        /// <summary>Радиус центрального колодца голограммы.</summary>
        public const float WellRadius = 0.54f;

        /// <summary>
        /// Высота, на которой стоит центр голографического глобуса/колодца.
        /// Чуть ниже столешницы, поэтому полусфера целиком помещается в углубление.
        /// </summary>
        public const float HologramWellY = 0.80f;

        /// <summary>Радиус светящегося голографического глобуса в колодце.</summary>
        public const float HologramGlobeRadius = 0.50f;

        /// <summary>Высота полусферы голографического глобуса.</summary>
        public const float HologramDomeHeight = 0.44f;

        /// <summary>Радиус настольных наборов — внутри столешницы.</summary>
        public const float DeskRingRadius = 1.12f;

        /// <summary>Радиус рабочего места ИИ-диктатора: 1.75 м от центра стола.</summary>
        public const float SeatRingRadius = 1.75f;

        /// <summary>Радиус рабочего места игрока: чуть дальше, чем места диктаторов.</summary>
        public const float PlayerSeatRadius = 1.95f;

        /// <summary>
        /// Углы рабочих мест пяти ИИ-диктаторов в градусах (0° — восток, 90° — север).
        /// Три главных стоят прямо в кадре напротив игрока (135°, 90°, 45°), двое
        /// крайних (168° и 12°) — по краям кадра. Место игрока — −90°, то есть юг.
        /// </summary>
        public static readonly float[] SeatAnglesDegrees = { 135f, 90f, 45f, 168f, 12f };

        /// <summary>Угол места игрока: -90°, то есть южная сторона стола.</summary>
        public const float PlayerSeatAngleDegrees = -90f;

        /// <summary>
        /// ГЛАЗ КАМЕРЫ ИГРОКА ЗА СТОЛОМ. Жёстко (0, 1.40, 38.05): любое смещение по X
        /// уводило камеру в ржавую стену шахты на краю зала. Никаких «мест» и
        /// «углов» в формуле быть не должно — только число, которое проверяет стенд.
        /// </summary>
        public static readonly Vector3 PlayerEyePosition = new Vector3(0f, 1.40f, 38.05f);

        /// <summary>Поворот камеры в зале саммита: жёстко 12° вниз по X, без рыскания.</summary>
        public static readonly Vector3 PlayerEulerDegrees = new Vector3(12f, 0f, 0f);

        /// <summary>
        /// Мёртвая зона перед камерой игрока: ни один полигон в этом радиусе строиться
        /// не должен, иначе кабина лифта, стойка поручня или стена зала оказываются
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
            public static readonly Vector3 EyePosition = new Vector3(0f, 1.32f, -0.52f);

            /// <summary>Поворот камеры бункера: 18° вниз — на столешницу с приборами.</summary>
            public static readonly Vector3 EulerDegrees = new Vector3(18f, 0f, 0f);

            /// <summary>Поворот камеры бункера, собранный из углов.</summary>
            public static Quaternion EyeRotation
            {
                get { return Quaternion.Euler(EulerDegrees); }
            }

            /// <summary>Наклон взгляда вниз, градусы.</summary>
            public const float PitchDegrees = 18f;

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

            /// <summary>Дальняя стена кабины: придвинута к столу, комната читается как каюта.</summary>
            public const float BackWallZ = 1.68f;

            /// <summary>Глубина ниши стальной двери в задней стене.</summary>
            public const float DoorNicheZ = 1.66f;

            /// <summary>Полуширина ниши двери.</summary>
            public const float DoorHalfWidth = 0.46f;

            /// <summary>Высота ниши двери.</summary>
            public const float DoorHeight = 1.55f;

            /// <summary>Точка подвеса плафона лампы над столом.</summary>
            public static readonly Vector3 CeilingLampPosition = new Vector3(0f, 1.52f, 0.55f);

            /// <summary>Точка крепления гирлянд кабелей над плафоном.</summary>
            public static readonly Vector3 CableAnchorPosition = new Vector3(0f, 1.62f, 0.60f);

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
                new Vector3(0.95f, 0.3f, 0.5f),
                new Vector3(0.26f, 0.03f, 0.32f),
                new Vector3(0.26f, 0.32f, 0.26f),
                new Vector3(0.54f, 0.46f, 0.46f),
                new Vector3(0.32f, 0.16f, 0.26f)
            };

            public static readonly Vector3[] DeskPropAnchor =
            {
                new Vector3(-0.66f, 0.76f, 0.52f),
                new Vector3(-0.50f, 0.76f, 0.26f),
                new Vector3(0.18f, 0.76f, 0.50f),
                new Vector3(0.64f, 0.76f, 0.48f),
                new Vector3(1.02f, 0.76f, 0.42f)
            };

            public static readonly float[] DeskPropYaw = { 166f, 162f, 180f, 200f, 205f };

            /// <summary>Ни одна точка прибора не должна быть ближе этого расстояния до глаза.</summary>
            public const float MinPropDistanceToEye = 0.42f;

            /// <summary>Верх приборов не должен подниматься выше этой отметки.</summary>
            public const float MaxPropTopY = 1.25f;
        }

        /// <summary>Угол рабочего места по индексу: слот 0 — место игрока (юг), дальше пять
        /// мест ИИ-диктаторов по углам концепт-арта.</summary>
        public static float GetSeatAngle(int index, int seatCount)
        {
            int count = seatCount > 0 ? seatCount : 1;

            if (index <= 0)
            {
                return PlayerSeatAngleDegrees * Mathf.Deg2Rad;
            }

            // Места диктаторов идут по списку углов и повторяются по кругу, если держав
            // окажется больше, чем углов в макете.
            int slot = (index - 1) % SeatAnglesDegrees.Length;
            return SeatAnglesDegrees[slot] * Mathf.Deg2Rad;
        }

        /// <summary>Положение настольного набора: лежит на столешнице.</summary>
        public static Vector3 GetDeskPosition(int index, int seatCount)
        {
            float angle = GetSeatAngle(index, seatCount);
            return new Vector3(Mathf.Cos(angle) * DeskRingRadius, TableSurfaceY,
                Mathf.Sin(angle) * DeskRingRadius);
        }

        /// <summary>Положение рабочего места: игрок сидит дальше диктаторов.</summary>
        public static Vector3 GetSeatPosition(int index, int seatCount)
        {
            float angle = GetSeatAngle(index, seatCount);
            float radius = index <= 0 ? PlayerSeatRadius : SeatRingRadius;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
    }
}
