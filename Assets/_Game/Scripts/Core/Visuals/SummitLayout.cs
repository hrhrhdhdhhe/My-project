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
        public const float WellRadius = 0.55f;

        /// <summary>
        /// Высота, на которой стоит центр голографического глобуса/колодца.
        /// Чуть ниже столешницы, поэтому полусфера целиком помещается в углубление.
        /// </summary>
        public const float HologramWellY = 0.80f;

        /// <summary>Радиус светящегося голографического глобуса в колодце.</summary>
        public const float HologramGlobeRadius = 0.48f;

        /// <summary>Высота полусферы голографического глобуса.</summary>
        public const float HologramDomeHeight = 0.44f;

        /// <summary>Радиус настольных наборов ИИ-диктаторов — внутри столешницы.</summary>
        public const float DeskRingRadius = 1.12f;

        /// <summary>
        /// Локальные позиции шести рабочих мест (относительно Center) — раскладка
        /// концепт-арта: игрок на юге за столом, пять диктаторов вдоль стола.
        /// Три главных напротив игрока, двое крайних по бокам. Калькируется с
        /// BunkerWorldBuilder.BuildSeatChairs и CreateSummitDictators.
        /// </summary>
        public static readonly Vector3[] SeatPositions =
        {
            new Vector3(0f, 0f, -1.95f),
            new Vector3(-1.32f, 0f, 0.85f),
            new Vector3(0f, 0f, 1.68f),
            new Vector3(1.32f, 0f, 0.85f),
            new Vector3(-1.68f, 0f, -0.35f),
            new Vector3(1.68f, 0f, -0.35f)
        };

        /// <summary>
        /// Развороты мест лицом к столу (yaw, градусы). Игрок без разворота — камера
        /// смотрит через его спину; №1/№3 развёрнуты на точку (0, 1, -0.5),
        /// №2 — прямо в камеру (0, 1, -1.5), №4/№5 — на центр столешницы (0, 1, 0).
        /// </summary>
        public static readonly float[] SeatYawDegrees = { 0f, 135.6f, 180f, -135.6f, 78.2f, -78.2f };

        /// <summary>
        /// ГЛАЗ КАМЕРЫ ИГРОКА ЗА СТОЛОМ. Жёстко (0, 1.38, 38.05): любое смещение по X
        /// уводило камеру в ржавую стену шахты на краю зала. Никаких «мест» и
        /// «углов» в формуле быть не должно — только число, которое проверяет стенд.
        /// </summary>
        public static readonly Vector3 PlayerEyePosition = new Vector3(0f, 1.38f, 38.05f);

        /// <summary>Поворот камеры в зале саммита: жёстко 13.3° вниз по X, без рыскания.</summary>
        public static readonly Vector3 PlayerEulerDegrees = new Vector3(13.3f, 0f, 0f);

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
        public const float PlayerPitchDegrees = 13.3f;

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

            /// <summary>Глубина ниши стальной двери в задней стене (плоскость полотна 1.65).</summary>
            public const float DoorNicheZ = 1.65f;

            /// <summary>Полуширина ниши двери.</summary>
            public const float DoorHalfWidth = 0.45f;

            /// <summary>Высота ниши двери.</summary>
            public const float DoorHeight = 1.50f;

            /// <summary>Точка подвеса плафона лампы над столом (1-в-1 из спецификации).</summary>
            public static readonly Vector3 CeilingLampPosition = new Vector3(0f, 1.48f, 0.52f);

            /// <summary>Точка крепления гирлянд кабелей над плафоном.</summary>
            public static readonly Vector3 CableAnchorPosition = new Vector3(0f, 1.62f, 0.60f);

            /// <summary>
            /// Разворот оборудования лицом к игроку. Локальная лицевая сторона приборов
            /// (диски телефонов, замок и кнопка пуска) смотрит в -Z, игрок сидит в -Z,
            /// поэтому новые приборы сажаются на столе без разворота. Значение 180
            /// осталось для терминала, принтера и планшета, у которых лицо построено в +Z.
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
                new Vector3(0.75f, 0.15f, 0.38f),
                new Vector3(0.26f, 0.03f, 0.32f),
                new Vector3(0.19f, 0.11f, 0.19f),
                new Vector3(0.54f, 0.46f, 0.46f),
                new Vector3(0.32f, 0.16f, 0.26f)
            };

            public static readonly Vector3[] DeskPropAnchor =
            {
                new Vector3(-0.32f, 0.76f, 0.50f),
                new Vector3(-0.50f, 0.76f, 0.26f),
                new Vector3(0.22f, 0.76f, 0.48f),
                new Vector3(0.64f, 0.76f, 0.50f),
                new Vector3(1.02f, 0.76f, 0.42f)
            };

            public static readonly float[] DeskPropYaw = { 0f, 162f, 0f, 200f, 205f };

            /// <summary>Ни одна точка прибора не должна быть ближе этого расстояния до глаза.</summary>
            public const float MinPropDistanceToEye = 0.42f;

            /// <summary>Верх приборов не должен подниматься выше этой отметки.</summary>
            public const float MaxPropTopY = 1.25f;
        }

        /// <summary>Слот рабочего места по индексу: 0 — место игрока (юг), дальше пять
        /// мест ИИ-диктаторов по раскладке концепт-арта. Индексы повторяются по кругу.</summary>
        public static int SeatSlot(int index, int seatCount)
        {
            int count = seatCount > 0 ? seatCount : 1;
            int slot = ((index % count) + count) % count;
            return Mathf.Min(slot, SeatPositions.Length - 1);
        }

        /// <summary>Положение рабочего места (локально относительно Center).</summary>
        public static Vector3 GetSeatPosition(int index, int seatCount)
        {
            return SeatPositions[SeatSlot(index, seatCount)];
        }

        /// <summary>Разворот рабочего места лицом к столу, градусы.</summary>
        public static float GetSeatYawDegrees(int index, int seatCount)
        {
            return SeatYawDegrees[SeatSlot(index, seatCount)];
        }

        /// <summary>Положение настольного набора: у игрока — перед камерой, у диктаторов —
        /// по направлению места на радиусе DeskRingRadius.</summary>
        public static Vector3 GetDeskPosition(int index, int seatCount)
        {
            if (index <= 0)
            {
                return new Vector3(0f, TableSurfaceY, -1.30f);
            }

            Vector3 seat = GetSeatPosition(index, seatCount);
            Vector2 dir = new Vector2(seat.x, seat.z).normalized;
            return new Vector3(dir.x * DeskRingRadius, TableSurfaceY, dir.y * DeskRingRadius);
        }
    }
}
