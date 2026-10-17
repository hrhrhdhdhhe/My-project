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

        /// <summary>Радиус столешницы (Art Bible: стол 6 м).</summary>
        public const float TableRadius = 3f;

        /// <summary>Высота каркаса стола.</summary>
        public const float TableFrameHeight = 0.78f;

        /// <summary>Толщина столешницы.</summary>
        public const float TableTopThickness = 0.16f;

        /// <summary>
        /// Высота верхней поверхности стола. Настольные наборы кладутся строго на неё:
        /// раньше умножение вектора на радиус поднимало их вдвое выше столешницы.
        /// </summary>
        public const float TableSurfaceY = TableFrameHeight + TableTopThickness;

        /// <summary>Радиус настольных наборов — внутри столешницы.</summary>
        public const float DeskRingRadius = 2.35f;

        /// <summary>
        /// Радиус рабочих мест диктаторов. Обязан быть больше TableRadius, иначе риги
        /// стоят внутри стола.
        /// </summary>
        public const float SeatRingRadius = 3.45f;

        /// <summary>Мировая координата центра зала: разнесён с бункером, чтобы не пересекались.</summary>
        public static readonly Vector3 Center = new Vector3(0f, 0f, 40f);

        /// <summary>Угол рабочего места. Слот 0 смотрит в сторону входа в зал.</summary>
        public static float GetSeatAngle(int index, int seatCount)
        {
            int count = seatCount > 0 ? seatCount : 1;
            return Mathf.PI * 2f * index / count + Mathf.PI * 0.5f;
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