// World Crisis | Слой: Core | Ответственный: OpenCode
// Состояние одной державы в матче: бюджет, 4 города, склад ракет, секретная доктрина,
// активные пакты о ненападении, отметка предателя и очередь приказов текущего раунда.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    /// <summary>Типы приказов, вводимых в скрытой фазе терминала.</summary>
    public enum OrderType
    {
        DevelopCity = 0,
        BuyAegisShield = 1,
        AssembleMissile = 2,
        DonateEcology = 3,
        LaunchMissile = 4
    }

    /// <summary>Тип телефонного звонка в фазе переговоров (влияет только на события и репутацию ИИ).</summary>
    public enum CallKind
    {
        Bluster = 0,
        PactOffer = 1,
        Bribe = 2,
        Threat = 3,
        Flattery = 4,
        Warning = 5
    }

    /// <summary>Один приказ в очереди текущего раунда. Сериализуется в сеть как есть.</summary>
    [Serializable]
    public class PlayerOrder
    {
        /// <summary>Идентификатор приказа внутри раунда — по нему приказ отменяется.</summary>
        public int OrderId;

        public OrderType Type;

        /// <summary>Индекс своего города 0..3 (для DevelopCity и BuyAegisShield).</summary>
        public int CityIndex = -1;

        /// <summary>Цель пуска ракеты: страна.</summary>
        public CountryId TargetCountry;

        /// <summary>Цель пуска ракеты: индекс города 0..3.</summary>
        public int TargetCityIndex = -1;

        public PlayerOrder()
        {
        }

        public PlayerOrder(OrderType type)
        {
            Type = type;
        }

        public static PlayerOrder Develop(int cityIndex)
        {
            PlayerOrder order = new PlayerOrder(OrderType.DevelopCity);
            order.CityIndex = cityIndex;
            return order;
        }

        public static PlayerOrder Shield(int cityIndex)
        {
            PlayerOrder order = new PlayerOrder(OrderType.BuyAegisShield);
            order.CityIndex = cityIndex;
            return order;
        }

        public static PlayerOrder Assemble()
        {
            return new PlayerOrder(OrderType.AssembleMissile);
        }

        public static PlayerOrder DonateEcology()
        {
            return new PlayerOrder(OrderType.DonateEcology);
        }

        public static PlayerOrder Launch(CountryId target, int targetCityIndex)
        {
            PlayerOrder order = new PlayerOrder(OrderType.LaunchMissile);
            order.TargetCountry = target;
            order.TargetCityIndex = targetCityIndex;
            return order;
        }

        /// <summary>Копия приказа (для безопасной передачи по сети и в UI).</summary>
        public PlayerOrder Clone()
        {
            PlayerOrder copy = new PlayerOrder();
            copy.OrderId = OrderId;
            copy.Type = Type;
            copy.CityIndex = CityIndex;
            copy.TargetCountry = TargetCountry;
            copy.TargetCityIndex = TargetCityIndex;
            return copy;
        }

        /// <summary>Короткое русское описание приказа для очереди в терминале и журнала.</summary>
        public string Describe(System.Func<CountryId, string> countryName)
        {
            switch (Type)
            {
                case OrderType.DevelopCity:
                    return "Развить свой город " + (CityIndex + 1);

                case OrderType.BuyAegisShield:
                    return "Щит ПВО над своим городом " + (CityIndex + 1);

                case OrderType.AssembleMissile:
                    return "Собрать ракету на склад";

                case OrderType.DonateEcology:
                    return "Вклад в мировую экологию";

                case OrderType.LaunchMissile:
                {
                    string target = countryName != null ? countryName(TargetCountry) : TargetCountry.ToString();
                    return "Пуск по " + target + ", город " + (TargetCityIndex + 1);
                }

                default:
                    return "Неизвестный приказ";
            }
        }
    }

    /// <summary>Предложение подписать пакт, высказанное по телефону в фазе переговоров.</summary>
    [Serializable]
    public class PactOffer
    {
        public CountryId From;
        public CountryId To;
        public int BribeAmount;
        public bool Accepted;

        public bool IsResolved
        {
            get { return Accepted; }
        }
    }

    /// <summary>Результат вскрытия вкладов в экологию в начале фазы «Расчёт».</summary>
    [Serializable]
    public class EcologyContributionRecord
    {
        public CountryId Country;
        public bool Donated;
        public float EcologyAfter;
        public bool BrokePromise;
    }

    /// <summary>Состояние одной державы в матче. Компонент сцены своё состояние не хранит.</summary>
    [Serializable]
    public class PlayerState
    {
        public CountryId Country;
        public CountryDefinition Definition;
        public string DisplayName;
        public bool IsAI;
        public bool IsDefeated;

        /// <summary>Бюджет державы в долларах.</summary>
        public int Money;

        /// <summary>Города державы: 4 по <see cref="EconomyConstants.StartingCitiesPerCountry"/>.</summary>
        public CityState[] Cities = new CityState[0];

        /// <summary>Ядерные боеголовки на складе (собранные, но не выпущенные).</summary>
        public int MissileStock;

        public SecretDoctrineType DoctrineType;

        /// <summary>Подписанные пакты о ненападении. Предательство не стирает запись.</summary>
        public HashSet<CountryId> ActivePacts = new HashSet<CountryId>();

        /// <summary>Нарушил ли держава хоть один пакт (метка предателя для UI и репутации ИИ).</summary>
        public bool IsTraitorsMark;

        /// <summary>Очередь приказов текущего раунда: исполняется по порядку в фазе «Расчёт».</summary>
        public List<PlayerOrder> TurnOrderQueue = new List<PlayerOrder>();

        /// <summary>Незакрытые предложения пактов, полученные по телефону.</summary>
        public List<PactOffer> PendingPactOffers = new List<PactOffer>();

        /// <summary>Обещал ли вклад в экологию в этом раунде (обещание делается в переговорах или на саммите).</summary>
        public bool PledgedEcologyContribution;

        /// <summary>Фактически внёс ли вклад в экологию в этом раунде.</summary>
        public bool DonatedEcologyThisRound;

        /// <summary>Санкции наложены и подлежат применению к доходу следующего раунда.</summary>
        public bool SanctionedNextRound;

        /// <summary>Выдавал ли Халифат кредит из воздуха в этом раунде.</summary>
        public bool AirCreditIssuedThisRound;

        public CountryId AirCreditPartner;

        /// <summary>Бесплатные прослушки (пассивка Нео-Кремния).</summary>
        public int FreeWiretapsLeft;

        /// <summary>Доктрины, которые игрок узнал прослушкой.</summary>
        public HashSet<CountryId> WiretapKnown = new HashSet<CountryId>();

        /// <summary>Державы, финансовое состояние которых раскрыто разведкой.</summary>
        public HashSet<CountryId> ReconKnown = new HashSet<CountryId>();

        public int CitiesLostCount;
        public int MissilesLaunchedTotal;
        public int BetrayalsCommitted;
        public int ConsecutiveRoundWins;
        public bool DoctrineFulfilled;

        public PlayerState()
        {
        }

        /// <summary>Полная подготовка державы к матчу.</summary>
        public void Setup(CountryDefinition definition, int cityCount, SecretDoctrineType doctrineType, bool isAI)
        {
            Country = definition.Id;
            Definition = definition;
            DisplayName = definition.DisplayName;
            IsAI = isAI;
            DoctrineType = doctrineType;
            ResetForMatch(cityCount);
        }

        /// <summary>Сброс переменных матча при старте новой партии.</summary>
        public void ResetForMatch(int cityCount)
        {
            Money = Definition != null ? Definition.StartingMoney : EconomyConstants.StartingMoney;
            IsDefeated = false;
            MissileStock = 0;
            ActivePacts.Clear();
            IsTraitorsMark = false;
            TurnOrderQueue.Clear();
            PendingPactOffers.Clear();
            PledgedEcologyContribution = false;
            DonatedEcologyThisRound = false;
            SanctionedNextRound = false;
            AirCreditIssuedThisRound = false;
            AirCreditPartner = CountryId.NeoSilicon;
            FreeWiretapsLeft = Definition != null ? Definition.GetFreeActionCount() : 0;
            WiretapKnown.Clear();
            ReconKnown.Clear();
            CitiesLostCount = 0;
            MissilesLaunchedTotal = 0;
            BetrayalsCommitted = 0;
            ConsecutiveRoundWins = 0;
            DoctrineFulfilled = false;

            Cities = new CityState[Mathf.Clamp(cityCount, 1, EconomyConstants.MaxCitiesPerCountry)];
            for (int i = 0; i < Cities.Length; i++)
            {
                CityState city = new CityState();
                city.Setup(Country, i);
                Cities[i] = city;
            }

            // Пассивка «Толстая крышка»: часть стартовых щитов уже развёрнута.
            int startingShields = Definition != null ? Definition.StartingAegisShields : 0;
            for (int i = 0; i < startingShields && i < Cities.Length; i++)
            {
                Cities[i].HasAAShield = true;
            }
        }

        public CityState GetCity(int index)
        {
            if (index < 0 || index >= Cities.Length)
            {
                return null;
            }

            return Cities[index];
        }

        public int AliveCityCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Cities.Length; i++)
                {
                    if (Cities[i] != null && Cities[i].IsAlive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Сколько щитов ПВО сейчас держит держава (города с щитом считаются).</summary>
        public int ShieldedCityCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Cities.Length; i++)
                {
                    CityState city = Cities[i];
                    if (city != null && city.IsAlive && city.HasAAShield)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsAliveCountry
        {
            get { return !IsDefeated && AliveCityCount > 0; }
        }

        /// <summary>Суммарный доход за следующий раунд без учёта санкций.</summary>
        public int GetRoundIncome()
        {
            if (IsDefeated)
            {
                return 0;
            }

            int income = 0;
            for (int i = 0; i < Cities.Length; i++)
            {
                CityState city = Cities[i];
                if (city != null && city.IsAlive)
                {
                    income += city.GetRoundIncome(Definition);
                }
            }

            return income;
        }

        public bool HasPact(CountryId other)
        {
            return ActivePacts.Contains(other);
        }

        public void AddPact(CountryId other)
        {
            if (other != Country)
            {
                ActivePacts.Add(other);
            }
        }

        public void RemovePact(CountryId other)
        {
            ActivePacts.Remove(other);
        }

        /// <summary>Номер первого живого города без щита — цель для «умных» ИИ.</summary>
        public int GetFirstUnshieldedAliveCityIndex()
        {
            for (int i = 0; i < Cities.Length; i++)
            {
                CityState city = Cities[i];
                if (city != null && city.IsAlive && !city.HasAAShield)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}