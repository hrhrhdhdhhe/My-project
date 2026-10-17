// World Crisis | Слой: Core | Ответственный: OpenCode
// Состояние одного города державы: разрушен ли, уровень развития, накрыт ли щитом ПВО.

using System;

namespace WorldCrisis
{
    /// <summary>
    /// Изменяемое состояние города. Живёт внутри <see cref="PlayerState"/>.
    /// Для сетевой синхронизации используется отдельный DTO <c>CitySnapshot</c>.
    /// </summary>
    [Serializable]
    public class CityState
    {
        /// <summary>Индекс стойки города 0..3 (у каждой державы ровно 4 города).</summary>
        public int Index;

        public CountryId Owner;
        public bool IsDestroyed;

        /// <summary>Сколько раз вкладывались в развитие города. Каждый уровень добавляет доход.</summary>
        public int DevelopmentLevel;

        /// <summary>Щит ПВО над городом. Сгорает при одном отражённом пуске.</summary>
        public bool HasAAShield;

        public bool IsAlive
        {
            get { return !IsDestroyed; }
        }

        public void Setup(CountryId owner, int index)
        {
            Owner = owner;
            Index = index;
            IsDestroyed = false;
            DevelopmentLevel = 0;
            HasAAShield = false;
        }

        /// <summary>
        /// Доход города за раунд. База — <see cref="EconomyConstants.CityBaseIncome"/>,
        /// каждое развитие добавляет <see cref="EconomyConstants.CityDevelopmentIncomeBonus"/>,
        /// затем применяется модификатор державы (<see cref="CountryDefinition.ResolveCityIncome"/>).
        /// </summary>
        public int GetRoundIncome(CountryDefinition definition)
        {
            int baseValue = EconomyConstants.CityBaseIncome
                            + DevelopmentLevel * EconomyConstants.CityDevelopmentIncomeBonus;

            if (definition == null)
            {
                return baseValue;
            }

            return definition.ResolveCityIncome(baseValue);
        }

        /// <summary>Короткая подпись города для журнала и HUD.</summary>
        public string GetShortLabel(string ownerShortName)
        {
            return ownerShortName + "·" + (Index + 1);
        }

        public string GetDisplayName(string ownerShortName)
        {
            return ownerShortName + ", город " + (Index + 1);
        }
    }
}