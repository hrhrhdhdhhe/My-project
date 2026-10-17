// World Crisis | Слой: Core | Ответственный: OpenCode
// Авторитетный контроллер матча и стейт-машина фаз: бункер-дипломатия → саммит ООН →
// ввод приказов → расчёт. Держит всё состояние партии, принимает команды игроков и ботов,
// рассылает события для UI/Voice/Interaction и обслуживает сеть (хост/клиент/локальный стенд).

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    public enum JournalTone
    {
        System = 0,
        Info = 1,
        Good = 2,
        Bad = 3,
        Secret = 4
    }

    /// <summary>Запись журнала матча — из неё строится «Хроника Лжи» в UI и озвучка.</summary>
    [Serializable]
    public class MatchLogEntry
    {
        public int Round;
        public GamePhase Phase;
        public string Text;
        public JournalTone Tone;
    }

    /// <summary>Строка итоговой таблицы партии.</summary>
    [Serializable]
    public class MatchStanding
    {
        public CountryId Country;
        public int Money;
        public int AliveCities;
        public int ShieldedCities;
        public int Launches;
        public SecretDoctrineType Doctrine;
        public bool DoctrineFulfilled;
        public int Score;
    }

    /// <summary>Поданный голос за санкции против конкретной державы.</summary>
    [Serializable]
    public class SanctionVote
    {
        public CountryId Voter;
        public CountryId Target;
    }

    [DisallowMultipleComponent]
    public class MatchManager : MonoBehaviour
    {
        /// <summary>Пара «кто исполнил — какой приказ» для пошагового расчёта.</summary>
        private sealed class ExecutionItem
        {
            public CountryId Actor;
            public PlayerOrder Order;
        }

        private const string LogPrefix = "[WorldCrisis] ";
        private const float SnapshotInterval = 0.5f;
        private const int SnapshotJournalTail = 14;

        public static MatchManager Instance { get; private set; }

        // --- События матча -------------------------------------------------
        public event Action<GamePhase> OnPhaseChanged;
        public event Action<GamePhase> OnPhaseEnding;
        public event Action<int> OnRoundStarted;
        public event Action<CountryId, PlayerOrder, bool> OnOrderResolved;
        public event Action<CountryId, PlayerOrder> OnOrderQueued;
        public event Action<CountryId, int> OnOrderCancelled;
        public event Action<CountryId, CountryId, int, int> OnMoneyTransferred;
        public event Action<CountryId, CountryId> OnAirCreditIssued;
        public event Action<CountryId, CountryId> OnAirCreditClawback;
        public event Action<CountryId, CountryId> OnReconPerformed;
        public event Action<CountryId, CountryId> OnWiretapPerformed;
        public event Action<CountryId, CountryId, CallKind, string> OnPhoneCall;
        public event Action<CountryId> OnEcologyPledgeMade;
        public event Action<IReadOnlyList<EcologyContributionRecord>> OnEcologyContributionsRevealed;
        public event Action<CountryId> OnEcologyPromiseBroken;
        public event Action<CountryId, CountryId> OnSanctionVoteCast;
        public event Action<CountryId, int> OnSanctionImposed;
        public event Action<CountryId, CountryId> OnPactOffered;
        public event Action<CountryId, CountryId> OnPactSigned;
        public event Action<CountryId, CountryId> OnTreatyBetrayed;
        public event Action<CountryId, CountryId, int> OnMissileLaunched;
        public event Action<CountryId, CountryId, int, bool> OnCityHitOrShielded;
        public event Action<bool, int> OnEcologyCollapse;
        public event Action<CountryId> OnCountryEliminated;
        public event Action<CountryId, SecretDoctrineType> OnDoctrineFulfilled;
        public event Action<CountryId?> OnMatchFinished;

        // --- Состояние ------------------------------------------------------
        public GameConfig Config { get; private set; }
        public GamePhase Phase { get; private set; }
        public int Round { get; private set; }
        public float PhaseTimeLeft { get; private set; }
        public float WorldEcology { get; private set; }
        public CountryId? Winner { get; private set; }
        public string WinReason { get; private set; } = string.Empty;
        public int GlobalCitiesDestroyed { get; private set; }
        public CountryId LocalPlayer { get; private set; }

        /// <summary>Ускорение таймеров фаз для отладочного HUD: 1 — боевой режим.</summary>
        public float TimeScale { get; set; } = 1f;
        public NetRole NetRole { get; private set; }
        public bool IsAuthoritative { get { return NetRole != NetRole.Client; } }
        public bool MatchFinished { get { return Phase == GamePhase.GameOver; } }
        public IReadOnlyList<PlayerState> AllPlayers { get { return _players; } }
        public IReadOnlyList<MatchLogEntry> Journal { get { return _journal; } }
        public IReadOnlyList<MatchStanding> Standings { get { return _standings; } }
        public IReadOnlyList<EcologyContributionRecord> LastContributions { get { return _lastContributions; } }

        private readonly PlayerState[] _players = new PlayerState[GameDefinitions.AllCountryIds.Length];
        private readonly List<MatchLogEntry> _journal = new List<MatchLogEntry>();
        private readonly List<MatchStanding> _standings = new List<MatchStanding>();
        private readonly List<EcologyContributionRecord> _lastContributions = new List<EcologyContributionRecord>();
        private readonly List<SanctionVote> _sanctionVotes = new List<SanctionVote>();
        private readonly HashSet<CountryId> _voters = new HashSet<CountryId>();
        private readonly HashSet<CountryId> _readyPlayers = new HashSet<CountryId>();
        private readonly HashSet<CountryId> _attackedThisRound = new HashSet<CountryId>();
        private readonly Dictionary<string, CountryId> _peerSlots = new Dictionary<string, CountryId>();

        private INetworkTransport _transport;
        private float _snapshotTimer;
        private bool _reckoningFastForward;
        private bool _phaseTransitioning;
        private bool _applyingRemoteCommand;
        private int _nextOrderId;
        private CountryId? _lastRoundWinner;

        #region Жизненный цикл

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Config = GameDefinitions.GetConfig();
            NetRole = NetRole.Solo;

            // Локальный стенд: команды идут через loopback-транспорт, тот же путь, что и по сети.
            _transport = new LocalLoopbackTransport();

            EnterLobby();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            StopNetworking();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (_transport != null)
            {
                _transport.Poll();
                PumpIncoming();
            }

            if (!IsAuthoritative || Phase == GamePhase.Lobby || Phase == GamePhase.GameOver)
            {
                return;
            }

            if (Phase == GamePhase.Reckoning)
            {
                // Фаза «Расчёт» идёт корутиной и не должна прерываться таймером.
                return;
            }

            PhaseTimeLeft -= deltaTime * Mathf.Max(0.01f, TimeScale);
            if (PhaseTimeLeft <= 0f)
            {
                PhaseTimeLeft = 0f;
                ForceAdvancePhase();
            }

            if (NetRole == NetRole.Host)
            {
                _snapshotTimer += deltaTime;
                if (_snapshotTimer >= SnapshotInterval)
                {
                    _snapshotTimer = 0f;
                    FlushSnapshot();
                }
            }
        }

        #endregion

        #region Лобби и старт матча

        /// <summary>Возвращает матч в лобби: все слоты — боты, кроме выбранного локального игрока.</summary>
        public void EnterLobby()
        {
            Config = Config != null ? Config : GameDefinitions.GetConfig();
            Phase = GamePhase.Lobby;
            Round = 0;
            PhaseTimeLeft = 0f;
            Winner = null;
            WinReason = string.Empty;
            GlobalCitiesDestroyed = 0;
            WorldEcology = Config.Ecology != null ? Config.Ecology.StartingValue : EconomyConstants.StartingWorldEcology;
            _journal.Clear();
            _standings.Clear();
            _lastContributions.Clear();
            _sanctionVotes.Clear();
            _voters.Clear();
            _readyPlayers.Clear();
            _attackedThisRound.Clear();
            _nextOrderId = 0;
            _lastRoundWinner = null;
            _reckoningFastForward = false;

            int cityCount = Mathf.Clamp(
                Config.StartingCitiesPerCountry,
                1,
                EconomyConstants.MaxCitiesPerCountry);

            for (int i = 0; i < GameDefinitions.AllCountryIds.Length; i++)
            {
                CountryId id = GameDefinitions.AllCountryIds[i];
                CountryDefinition definition = Config.GetOrCreateCountry(id);
                PlayerState state = _players[i] ?? new PlayerState();
                state.Setup(definition, cityCount, SecretDoctrineType.None, id != LocalPlayer);
                _players[i] = state;
            }

            AddJournal("Лобби: 6 держав, раундов — " + Config.TotalRounds + ".", JournalTone.System);
            RaisePhaseChanged();
        }

        public void SetLocalPlayer(CountryId id)
        {
            if (Phase != GamePhase.Lobby)
            {
                Debug.LogWarning(LogPrefix + "Сменить державу можно только в лобби.");
                return;
            }

            if (GetPlayer(id) == null)
            {
                return;
            }

            CountryId previous = LocalPlayer;
            LocalPlayer = id;

            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null)
                {
                    _players[i].IsAI = _players[i].Country != id;
                }
            }

            if (_players[(int)previous] != null && previous != id)
            {
                _players[(int)previous].IsAI = true;
            }
        }

        /// <summary>Локальный стенд: один человек и нужное количество ИИ-диктаторов.</summary>
        public void StartSoloMatch(CountryId humanCountry, int botCount)
        {
            if (botCount < 0 || botCount >= _players.Length)
            {
                Debug.LogWarning(LogPrefix + "Некорректное количество ботов: " + botCount);
                return;
            }

            EnterLobby();
            SetLocalPlayer(humanCountry);

            int assigned = 0;
            for (int i = 0; i < _players.Length && assigned < botCount; i++)
            {
                CountryId id = _players[i].Country;
                if (id == humanCountry)
                {
                    continue;
                }

                _players[i].IsAI = true;
                assigned++;
            }

            StartMatch();
        }

        /// <summary>Начало партии: раздача секретных доктрин и переход в первый раунд.</summary>
        public bool StartMatch()
        {
            if (!IsAuthoritative)
            {
                SendCommand(NetOp.StartMatch, LocalPlayer);
                return true;
            }

            if (Phase != GamePhase.Lobby)
            {
                Debug.LogWarning(LogPrefix + "Матч уже идёт, старт игнорирован.");
                return false;
            }

            SecretDoctrineType[] doctrines = DealDoctrines();
            int cityCount = Mathf.Clamp(Config.StartingCitiesPerCountry, 1, EconomyConstants.MaxCitiesPerCountry);

            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                state.Setup(state.Definition, cityCount, doctrines[i], state.IsAI);
            }

            _journal.Clear();
            _standings.Clear();
            GlobalCitiesDestroyed = 0;
            WorldEcology = Config.Ecology != null ? Config.Ecology.StartingValue : EconomyConstants.StartingWorldEcology;
            Winner = null;
            WinReason = string.Empty;
            _nextOrderId = 0;

            Round = 1;
            AddJournal("Партия началась. Держав: " + _players.Length + ", раундов: " + Config.TotalRounds + ".", JournalTone.System);
            AdvanceTo(GamePhase.BunkerDiplomacy);
            FlushSnapshot();
            return true;
        }

        /// <summary>Перемешивает доктрины и выдаёт каждой державе одну (первым шести).</summary>
        private SecretDoctrineType[] DealDoctrines()
        {
            List<SecretDoctrineType> pool = new List<SecretDoctrineType>(GameDefinitions.AllDoctrineTypes);
            System.Random random = new System.Random(Environment.TickCount);

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                SecretDoctrineType tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            SecretDoctrineType[] result = new SecretDoctrineType[_players.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = i < pool.Count ? pool[i] : SecretDoctrineType.None;
            }

            return result;
        }

        #endregion

        #region Стейт-машина фаз

        public float GetPhaseDuration(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.BunkerDiplomacy:
                    return EconomyConstants.BunkerDiplomacySeconds;
                case GamePhase.SummitUN:
                    return EconomyConstants.SummitUNSeconds;
                case GamePhase.OrderInput:
                    return EconomyConstants.OrderInputSeconds;
                case GamePhase.Reckoning:
                    return EconomyConstants.ReckoningSeconds;
                default:
                    return 0f;
            }
        }

        public static bool IsTimedPhase(GamePhase phase)
        {
            return phase == GamePhase.BunkerDiplomacy
                   || phase == GamePhase.SummitUN
                   || phase == GamePhase.OrderInput
                   || phase == GamePhase.Reckoning;
        }

        /// <summary>Готовость державы досрочно завершить фазу. Все живые готовые → переход.</summary>
        public bool EndPhaseEarly(CountryId actor)
        {
            if (!IsAuthoritative)
            {
                SendCommand(NetOp.EndPhaseEarly, actor);
                return true;
            }

            if (!IsTimedPhase(Phase) || Phase == GamePhase.Reckoning)
            {
                return false;
            }

            PlayerState state = GetPlayer(actor);
            if (state == null || state.IsDefeated)
            {
                return false;
            }

            _readyPlayers.Add(actor);
            if (AreAllAlivePlayersReady())
            {
                ForceAdvancePhase();
            }

            return true;
        }

        /// <summary>Горячая клавиша/кнопка отладки: немедленно завершить текущую фазу.</summary>
        public void ForceAdvancePhase()
        {
            if (!IsAuthoritative)
            {
                SendCommand(NetOp.ForceAdvance, LocalPlayer);
                return;
            }

            if (!IsTimedPhase(Phase) || _phaseTransitioning)
            {
                return;
            }

            if (Phase == GamePhase.Reckoning)
            {
                // Расчёт не прерываем, а только ускоряем: покадровый yield без пауз.
                _reckoningFastForward = true;
                return;
            }

            switch (Phase)
            {
                case GamePhase.BunkerDiplomacy:
                    AdvanceTo(GamePhase.SummitUN);
                    break;
                case GamePhase.SummitUN:
                    FinalizeSanctionVote();
                    AdvanceTo(GamePhase.OrderInput);
                    break;
                case GamePhase.OrderInput:
                    AdvanceTo(GamePhase.Reckoning);
                    break;
            }
        }

        private bool AreAllAlivePlayersReady()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state.IsDefeated)
                {
                    continue;
                }

                if (!_readyPlayers.Contains(state.Country))
                {
                    return false;
                }
            }

            return true;
        }

        private void AdvanceTo(GamePhase next)
        {
            if (_phaseTransitioning)
            {
                // Защита от каскада: флаш отложенных действий ботов не должен проскочить через фазу.
                return;
            }

            _phaseTransitioning = true;

            try
            {
                GamePhase previous = Phase;
                OnPhaseEnding?.Invoke(previous);

                Phase = next;
                PhaseTimeLeft = GetPhaseDuration(next);
                HandlePhaseEntered(next);
                RaisePhaseChanged();
                FlushSnapshot();
            }
            finally
            {
                _phaseTransitioning = false;
            }
        }

        private void RaisePhaseChanged()
        {
            OnPhaseChanged?.Invoke(Phase);
        }

        private void HandlePhaseEntered(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.BunkerDiplomacy:
                    BeginBunkerDiplomacy();
                    break;
                case GamePhase.SummitUN:
                    BeginSummit();
                    break;
                case GamePhase.OrderInput:
                    _readyPlayers.Clear();
                    break;
                case GamePhase.Reckoning:
                    BeginReckoning();
                    break;
            }
        }

        private void BeginBunkerDiplomacy()
        {
            _readyPlayers.Clear();
            _sanctionVotes.Clear();
            _voters.Clear();
            _attackedThisRound.Clear();

            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null)
                {
                    continue;
                }

                state.TurnOrderQueue.Clear();
                state.PendingPactOffers.Clear();
                state.PledgedEcologyContribution = false;
                state.DonatedEcologyThisRound = false;
                state.AirCreditIssuedThisRound = false;
                state.SanctionedNextRound = false;
            }

            OnRoundStarted?.Invoke(Round);
            AddJournal("Раунд " + Round + ": бункер-дипломатия, " + (int)PhaseTimeLeft + " сек.", JournalTone.System);
        }

        private void BeginSummit()
        {
            _readyPlayers.Clear();
            SignAcceptedPactOffers();
            AddJournal("Раунд " + Round + ": Саммит ООН, " + (int)PhaseTimeLeft + " сек.", JournalTone.System);
        }

        private void BeginReckoning()
        {
            _readyPlayers.Clear();
            _reckoningFastForward = false;
            StopAllCoroutines();
            AddJournal("Раунд " + Round + ": расчёт по порядку.", JournalTone.System);
            StartCoroutine(ResolveReckoning());
        }

        #endregion

        #region Фаза переговоров: звонки, деньги, разведка, прослушка

        public bool TransferMoney(CountryId actor, CountryId receiver, int amount)
        {
            if (!PrepareMutation(NetOp.TransferMoney, actor, (int)receiver, amount))
            {
                return true;
            }

            if (Phase != GamePhase.BunkerDiplomacy)
            {
                return false;
            }

            PlayerState sender = GetPlayer(actor);
            PlayerState target = GetPlayer(receiver);
            if (!IsUsable(sender) || !IsUsable(target) || receiver == actor || amount <= 0)
            {
                return false;
            }

            int sum = Mathf.Min(amount, sender.Money);
            if (sum <= 0)
            {
                return false;
            }

            // Комиссию берёт держава с пассивкой TransferTax, если она не сторона перевода.
            PlayerState taxHolder = FindPlayerByPassive(CountryPassiveKind.TransferTax);
            int tax = taxHolder != null && taxHolder.Definition != null
                ? taxHolder.Definition.CalculateTransferTax(sum, actor, receiver)
                : 0;

            sender.Money -= sum;
            target.Money += sum - tax;

            if (tax > 0 && taxHolder != null)
            {
                taxHolder.Money += tax;
            }

            OnMoneyTransferred?.Invoke(actor, receiver, sum, tax);
            AddJournal(GetShortName(actor) + " → перевод " + sum + "$ " + GetShortName(receiver)
                        + (tax > 0 ? " (комиссия " + GetShortName(taxHolder.Country) + " " + tax + "$)" : string.Empty),
                JournalTone.Info);
            return true;
        }

        /// <summary>Кредит из воздуха: пассивка Золотого Халифата Дюн.</summary>
        public bool IssueAirCredit(CountryId issuer, CountryId partner)
        {
            if (!PrepareMutation(NetOp.IssueAirCredit, issuer, (int)partner))
            {
                return true;
            }

            if (Phase != GamePhase.BunkerDiplomacy)
            {
                return false;
            }

            PlayerState caliphate = GetPlayer(issuer);
            PlayerState target = GetPlayer(partner);
            if (!IsUsable(caliphate) || !IsUsable(target) || caliphate.Definition == null)
            {
                return false;
            }

            if (!caliphate.Definition.CanIssueAirCredit(partner, false, caliphate.AirCreditIssuedThisRound))
            {
                return false;
            }

            int partnerGain = caliphate.Definition.GetAirCreditPartnerGain(partner, false, caliphate.AirCreditIssuedThisRound);
            int selfGain = caliphate.Definition.GetAirCreditSelfGain(caliphate.AirCreditIssuedThisRound);
            if (partnerGain <= 0)
            {
                return false;
            }

            target.Money += partnerGain;
            caliphate.Money += selfGain;
            caliphate.AirCreditIssuedThisRound = true;
            caliphate.AirCreditPartner = partner;

            OnAirCreditIssued?.Invoke(issuer, partner);
            AddJournal(GetShortName(issuer) + " выдал кредит из воздуха " + GetShortName(partner)
                        + ": +" + partnerGain + "$ / +" + selfGain + "$", JournalTone.Good);
            return true;
        }

        public bool BuyRecon(CountryId actor, CountryId target)
        {
            if (!PrepareMutation(NetOp.BuyRecon, actor, (int)target))
            {
                return true;
            }

            if (Phase != GamePhase.BunkerDiplomacy || Config.Costs == null)
            {
                return false;
            }

            PlayerState spy = GetPlayer(actor);
            PlayerState victim = GetPlayer(target);
            if (!IsUsable(spy) || !IsUsable(victim) || target == actor || spy.Money < Config.Costs.Recon)
            {
                return false;
            }

            spy.Money -= Config.Costs.Recon;
            spy.ReconKnown.Add(target);

            OnReconPerformed?.Invoke(actor, target);
            AddJournal(GetShortName(actor) + " купил разведку: " + GetShortName(target)
                        + " — " + victim.Money + "$", JournalTone.Info);
            return true;
        }

        public bool BuyWiretap(CountryId actor, CountryId target)
        {
            if (!PrepareMutation(NetOp.BuyWiretap, actor, (int)target))
            {
                return true;
            }

            if (Phase != GamePhase.BunkerDiplomacy || Config.Costs == null)
            {
                return false;
            }

            PlayerState spy = GetPlayer(actor);
            PlayerState victim = GetPlayer(target);
            if (!IsUsable(spy) || !IsUsable(victim) || target == actor)
            {
                return false;
            }

            bool free = spy.FreeWiretapsLeft > 0;
            if (!free && spy.Money < Config.Costs.Wiretap)
            {
                return false;
            }

            if (!free)
            {
                spy.Money -= Config.Costs.Wiretap;
            }
            else
            {
                spy.FreeWiretapsLeft--;
            }

            spy.WiretapKnown.Add(target);
            SecretDoctrine doctrine = GetDoctrine(target);

            OnWiretapPerformed?.Invoke(actor, target);
            AddJournal(GetShortName(actor) + " прослушал " + GetShortName(target)
                        + (doctrine != null ? ": доктрина «" + doctrine.DisplayName + "»" : string.Empty)
                        + (free ? " (пассивка)" : string.Empty),
                JournalTone.Secret);
            return true;
        }

        /// <summary>Обещание вклада в экологию. Не сдержал — триггер лжеца в расчёте.</summary>
        public bool PledgeEcology(CountryId actor)
        {
            if (!PrepareMutation(NetOp.PledgeEcology, actor))
            {
                return true;
            }

            if (Phase != GamePhase.BunkerDiplomacy && Phase != GamePhase.SummitUN)
            {
                return false;
            }

            PlayerState state = GetPlayer(actor);
            if (!IsUsable(state) || state.PledgedEcologyContribution)
            {
                return false;
            }

            state.PledgedEcologyContribution = true;
            OnEcologyPledgeMade?.Invoke(actor);
            AddJournal(GetShortName(actor) + " публично пообещал вклад в экологию.", JournalTone.Info);
            return true;
        }

        /// <summary>Телефонный звонок: событие для Voice/UI плюс необязательная взятка.</summary>
        public bool PlacePhoneCall(CountryId actor, CountryId callee, CallKind kind, string message, int bribeAmount)
        {
            if (!PrepareMutation(NetOp.PlacePhoneCall, actor, (int)callee, (int)kind, bribeAmount, 0f, message))
            {
                return true;
            }

            if (Phase != GamePhase.BunkerDiplomacy)
            {
                return false;
            }

            PlayerState caller = GetPlayer(actor);
            PlayerState target = GetPlayer(callee);
            if (!IsUsable(caller) || !IsUsable(target))
            {
                return false;
            }

            string text = string.IsNullOrEmpty(message) ? DefaultCallLine(kind) : message;
            OnPhoneCall?.Invoke(actor, callee, kind, text);
            AddJournal("☎ " + GetShortName(actor) + " → " + GetShortName(callee) + ": «" + text + "»", JournalTone.Info);

            if (bribeAmount > 0)
            {
                TransferMoney(actor, callee, bribeAmount);
            }

            return true;
        }

        /// <summary>Предложение пакта по телефону. Подписывается на саммите после принятия.</summary>
        public bool OfferPact(CountryId from, CountryId to)
        {
            if (!PrepareMutation(NetOp.OfferPact, from, (int)to))
            {
                return true;
            }

            if (Phase != GamePhase.BunkerDiplomacy)
            {
                return false;
            }

            PlayerState proposer = GetPlayer(from);
            PlayerState listener = GetPlayer(to);
            if (!IsUsable(proposer) || !IsUsable(listener) || from == to || proposer.HasPact(to))
            {
                return false;
            }

            PactOffer offer = new PactOffer();
            offer.From = from;
            offer.To = to;
            listener.PendingPactOffers.Add(offer);

            OnPactOffered?.Invoke(from, to);
            AddJournal(GetShortName(from) + " предложил пакт " + GetShortName(to) + ".", JournalTone.Info);
            return true;
        }

        public bool RespondToPactOffer(CountryId actor, int offerIndex, bool accept)
        {
            if (!PrepareMutation(NetOp.RespondToPactOffer, actor, offerIndex, accept ? 1 : 0))
            {
                return true;
            }

            PlayerState state = GetPlayer(actor);
            if (state == null || offerIndex < 0 || offerIndex >= state.PendingPactOffers.Count)
            {
                return false;
            }

            PactOffer offer = state.PendingPactOffers[offerIndex];
            if (accept)
            {
                offer.Accepted = true;
                AddJournal(GetShortName(actor) + " принял пакт от " + GetShortName(offer.From)
                            + " — подпись на саммите.", JournalTone.Good);
            }
            else
            {
                state.PendingPactOffers.RemoveAt(offerIndex);
                AddJournal(GetShortName(actor) + " отказал " + GetShortName(offer.From) + " в пакте.", JournalTone.Info);
            }

            return true;
        }

        #endregion

        #region Фаза саммита: санкции и подписание пактов

        public bool CastSanctionVote(CountryId actor, CountryId target)
        {
            if (!PrepareMutation(NetOp.CastSanctionVote, actor, (int)target))
            {
                return true;
            }

            if (Phase != GamePhase.SummitUN)
            {
                return false;
            }

            PlayerState voter = GetPlayer(actor);
            PlayerState victim = GetPlayer(target);
            if (!IsUsable(voter) || !IsUsable(victim) || actor == target || _voters.Contains(actor))
            {
                return false;
            }

            _voters.Add(actor);
            SanctionVote vote = new SanctionVote();
            vote.Voter = actor;
            vote.Target = target;
            _sanctionVotes.Add(vote);

            OnSanctionVoteCast?.Invoke(actor, target);
            AddJournal(GetShortName(actor) + " голосует за санкции против " + GetShortName(target) + ".", JournalTone.Info);
            return true;
        }

        public bool SignPact(CountryId actor, CountryId counterparty)
        {
            if (!PrepareMutation(NetOp.SignPact, actor, (int)counterparty))
            {
                return true;
            }

            if (Phase != GamePhase.SummitUN)
            {
                return false;
            }

            return ApplyPact(actor, counterparty);
        }

        /// <summary>Расторжение пакта по доброй воле (знак протеста после предательства).</summary>
        public bool BreakPact(CountryId actor, CountryId counterparty)
        {
            if (!PrepareMutation(NetOp.BreakPact, actor, (int)counterparty))
            {
                return true;
            }

            PlayerState state = GetPlayer(actor);
            PlayerState other = GetPlayer(counterparty);
            if (!IsUsable(state) || !IsUsable(other) || !state.HasPact(counterparty))
            {
                return false;
            }

            state.RemovePact(counterparty);
            AddJournal(GetShortName(actor) + " разорвал пакт с " + GetShortName(counterparty) + " сам.", JournalTone.Info);
            return true;
        }

        private void SignAcceptedPactOffers()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state.IsDefeated || state.PendingPactOffers.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < state.PendingPactOffers.Count; j++)
                {
                    PactOffer offer = state.PendingPactOffers[j];
                    if (offer.Accepted)
                    {
                        ApplyPact(offer.From, offer.To);
                    }
                }
            }
        }

        private bool ApplyPact(CountryId a, CountryId b)
        {
            PlayerState first = GetPlayer(a);
            PlayerState second = GetPlayer(b);
            if (!IsUsable(first) || !IsUsable(second) || a == b || first.HasPact(b))
            {
                return false;
            }

            first.AddPact(b);
            second.AddPact(a);
            OnPactSigned?.Invoke(a, b);
            AddJournal("Пакт о ненападении подписан: " + GetShortName(a) + " ↔ " + GetShortName(b) + ".", JournalTone.Good);
            return true;
        }

        private void FinalizeSanctionVote()
        {
            if (_sanctionVotes.Count == 0)
            {
                return;
            }

            int aliveCount = CountAlivePlayers();
            int threshold = aliveCount / 2 + 1;

            Dictionary<CountryId, int> tally = new Dictionary<CountryId, int>();
            for (int i = 0; i < _sanctionVotes.Count; i++)
            {
                SanctionVote vote = _sanctionVotes[i];
                int value;
                tally.TryGetValue(vote.Target, out value);
                tally[vote.Target] = value + 1;
            }

            CountryId? bestTarget = null;
            int bestCount = 0;
            foreach (KeyValuePair<CountryId, int> pair in tally)
            {
                if (pair.Value > bestCount)
                {
                    bestCount = pair.Value;
                    bestTarget = pair.Key;
                }
            }

            if (!bestTarget.HasValue || bestCount < threshold)
            {
                AddJournal("Санкции не набрали большинство голосов.", JournalTone.Info);
                return;
            }

            PlayerState target = GetPlayer(bestTarget.Value);
            if (target == null || target.IsDefeated)
            {
                return;
            }

            target.SanctionedNextRound = true;
            OnSanctionImposed?.Invoke(bestTarget.Value, bestCount);
            AddJournal("САНКЦИИ против " + GetShortName(bestTarget.Value) + ": " + bestCount + " голосов. "
                        + "Доход изгою срезан на " + EconomyConstants.SanctionIncomePenaltyPercent + "% в следующем раунде.",
                JournalTone.Bad);
        }

        #endregion

        #region Фаза ввода приказов

        public bool SubmitOrder(CountryId actor, PlayerOrder order)
        {
            if (!PrepareMutation(NetOp.SubmitOrder, actor,
                    order != null ? (int)order.Type : 0,
                    order != null ? order.CityIndex : -1,
                    order != null ? (int)order.TargetCountry : 0,
                    order != null ? order.TargetCityIndex : -1))
            {
                return true;
            }

            if (Phase != GamePhase.OrderInput || order == null)
            {
                return false;
            }

            PlayerState state = GetPlayer(actor);
            if (!IsUsable(state))
            {
                return false;
            }

            if (state.TurnOrderQueue.Count >= EconomyConstants.MaxOrdersPerPlayerPerRound)
            {
                AddJournal(GetShortName(actor) + ": очередь приказов переполнена.", JournalTone.Info);
                return false;
            }

            if (!IsOrderValid(state, order))
            {
                AddJournal(GetShortName(actor) + ": приказ отклонён — «" + order.Describe(GetShortName) + "».", JournalTone.Info);
                return false;
            }

            order = order.Clone();
            order.OrderId = ++_nextOrderId;
            state.TurnOrderQueue.Add(order);

            OnOrderQueued?.Invoke(actor, order);
            AddJournal(GetShortName(actor) + ": в приказ добавлено «" + order.Describe(GetShortName) + "».", JournalTone.Info);
            return true;
        }

        public bool CancelOrder(CountryId actor, int orderId)
        {
            if (!PrepareMutation(NetOp.CancelOrder, actor, orderId))
            {
                return true;
            }

            PlayerState state = GetPlayer(actor);
            if (state == null)
            {
                return false;
            }

            for (int i = 0; i < state.TurnOrderQueue.Count; i++)
            {
                if (state.TurnOrderQueue[i].OrderId == orderId)
                {
                    state.TurnOrderQueue.RemoveAt(i);
                    OnOrderCancelled?.Invoke(actor, orderId);
                    return true;
                }
            }

            return false;
        }

        public void ClearOrders(CountryId actor)
        {
            if (!PrepareMutation(NetOp.ClearOrders, actor))
            {
                return;
            }

            PlayerState state = GetPlayer(actor);
            if (state == null)
            {
                return;
            }

            state.TurnOrderQueue.Clear();
        }

        public int GetOrderCost(CountryId actor, PlayerOrder order)
        {
            if (order == null || Config.Costs == null)
            {
                return 0;
            }

            PlayerState state = GetPlayer(actor);
            switch (order.Type)
            {
                case OrderType.DevelopCity:
                    return Config.Costs.CityDevelopment;
                case OrderType.BuyAegisShield:
                    return Config.Costs.AegisShield;
                case OrderType.AssembleMissile:
                    return state != null && state.Definition != null
                        ? state.Definition.ResolveMissileAssemblyCost()
                        : Config.Costs.MissileAssembly;
                case OrderType.DonateEcology:
                    return Config.Costs.EcologyDonation;
                case OrderType.LaunchMissile:
                    return Config.Costs.MissileLaunch;
                default:
                    return 0;
            }
        }

        private bool IsOrderValid(PlayerState actor, PlayerOrder order)
        {
            if (GetOrderCost(actor.Country, order) > actor.Money)
            {
                return false;
            }

            switch (order.Type)
            {
                case OrderType.DevelopCity:
                case OrderType.BuyAegisShield:
                {
                    CityState city = actor.GetCity(order.CityIndex);
                    if (city == null || !city.IsAlive)
                    {
                        return false;
                    }

                    return order.Type != OrderType.BuyAegisShield || !city.HasAAShield;
                }

                case OrderType.AssembleMissile:
                case OrderType.DonateEcology:
                    return true;

                case OrderType.LaunchMissile:
                {
                    PlayerState victim = GetPlayer(order.TargetCountry);
                    CityState city = victim != null ? victim.GetCity(order.TargetCityIndex) : null;
                    return victim != null && victim.IsAliveCountry && order.TargetCountry != actor.Country
                           && city != null && city.IsAlive;
                }

                default:
                    return false;
            }
        }

        #endregion

        #region Фаза расчёта

        private IEnumerator ResolveReckoning()
        {
            int estimatedSteps = EstimateReckoningSteps();
            float delay = Mathf.Clamp(
                EconomyConstants.ReckoningSeconds * 0.5f / Mathf.Max(1, estimatedSteps),
                EconomyConstants.ReckoningStepSecondsMin,
                EconomyConstants.ReckoningStepSecondsMax);

            // 1. Экология: вклады и проверка обещаний.
            yield return WaitStep(delay);
            ResolveEcologyContributions();
            AddJournal("Мировая экология: " + Mathf.RoundToInt(WorldEcology) + "%.", JournalTone.Info);
            yield return WaitStep(delay);

            // 2. Пошаговое исполнение приказов.
            List<ExecutionItem> backlog = CollectExecutionBacklog();
            for (int i = 0; i < backlog.Count; i++)
            {
                ExecuteOrder(backlog[i].Actor, backlog[i].Order);
                yield return WaitStep(delay);
            }

            // 3. Возврат кредита из воздуха, если партнёр ударил кредитора.
            ResolveAirCreditClawback();
            yield return WaitStep(delay);

            // 4. Обвал мировой экологии.
            if (WorldEcology <= 0f)
            {
                ExecuteEcologyCollapse();
            }

            yield return WaitStep(delay);

            // 5. Итоги раунда: выбывание, доход, победитель раунда.
            UpdateElimination();
            if (Phase == GamePhase.GameOver)
            {
                yield break;
            }

            PayRoundIncome();
            UpdateRoundWinner();
            yield return WaitStep(delay);

            // 6. Досрочная победа по секретной доктрине.
            CheckDoctrineVictory();
            if (Phase == GamePhase.GameOver)
            {
                yield break;
            }

            if (Round >= Mathf.Max(1, Config.TotalRounds))
            {
                FinishMatchByScore();
                yield break;
            }

            Round++;
            AdvanceTo(GamePhase.BunkerDiplomacy);
        }

        private int EstimateReckoningSteps()
        {
            int steps = 6;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null)
                {
                    steps += _players[i].TurnOrderQueue.Count;
                }
            }

            return steps;
        }

        private IEnumerator WaitStep(float delay)
        {
            if (_reckoningFastForward)
            {
                yield return null;
                yield break;
            }

            yield return new WaitForSeconds(delay);
        }

        /// <summary>Порядок исполнения фиксирован: сначала все державы по CountryId, затем их очереди.</summary>
        private List<ExecutionItem> CollectExecutionBacklog()
        {
            List<ExecutionItem> backlog = new List<ExecutionItem>();
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state.IsDefeated)
                {
                    continue;
                }

                for (int j = 0; j < state.TurnOrderQueue.Count; j++)
                {
                    PlayerOrder order = state.TurnOrderQueue[j];

                    // Вклады в экологию исполнены отдельным шагом раньше — они сняты из очереди.
                    if (order.Type == OrderType.DonateEcology)
                    {
                        continue;
                    }

                    ExecutionItem item = new ExecutionItem();
                    item.Actor = state.Country;
                    item.Order = order;
                    backlog.Add(item);
                }
            }

            return backlog;
        }

        private void ResolveEcologyContributions()
        {
            _lastContributions.Clear();
            float donationGain = Config.Ecology != null ? Config.Ecology.GainOnDonation : EconomyConstants.EcologyGainOnDonation;
            int donationCost = Config.Costs != null ? Config.Costs.EcologyDonation : EconomyConstants.EcologyDonationCost;

            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state.IsDefeated)
                {
                    continue;
                }

                bool donated = false;
                for (int j = state.TurnOrderQueue.Count - 1; j >= 0; j--)
                {
                    PlayerOrder order = state.TurnOrderQueue[j];
                    if (order.Type != OrderType.DonateEcology)
                    {
                        continue;
                    }

                    // Вклад снимается из очереди в любом случае: не хватило денег — вклада не будет.
                    state.TurnOrderQueue.RemoveAt(j);

                    if (state.Money >= donationCost)
                    {
                        state.Money -= donationCost;
                        donated = true;
                        state.DonatedEcologyThisRound = true;
                        AddJournal(GetShortName(state.Country) + " внёс " + donationCost
                                    + "$ в экологию (+" + donationGain + "%).", JournalTone.Good);
                    }
                }

                if (donated)
                {
                    WorldEcology = EconomyConstants.ClampWorldEcology(WorldEcology + donationGain);
                }

                EcologyContributionRecord record = new EcologyContributionRecord();
                record.Country = state.Country;
                record.Donated = donated;
                record.BrokePromise = state.PledgedEcologyContribution && !donated;
                record.EcologyAfter = WorldEcology;
                _lastContributions.Add(record);
            }

            OnEcologyContributionsRevealed?.Invoke(_lastContributions);

            for (int i = 0; i < _lastContributions.Count; i++)
            {
                EcologyContributionRecord record = _lastContributions[i];
                if (!record.BrokePromise)
                {
                    continue;
                }

                OnEcologyPromiseBroken?.Invoke(record.Country);
                AddJournal("ЛЖЕЦ: " + GetShortName(record.Country) + " обещал вклад и не внёс.", JournalTone.Bad);
            }
        }

        private void ExecuteOrder(CountryId actorId, PlayerOrder order)
        {
            PlayerState actor = GetPlayer(actorId);
            if (actor == null || order == null)
            {
                return;
            }

            if (actor.IsDefeated)
            {
                OnOrderResolved?.Invoke(actorId, order, false);
                return;
            }

            int cost = GetOrderCost(actorId, order);
            if (cost > actor.Money)
            {
                AddJournal(GetShortName(actorId) + ": не хватает денег на «" + order.Describe(GetShortName) + "».",
                    JournalTone.Info);
                OnOrderResolved?.Invoke(actorId, order, false);
                return;
            }

            bool executed;
            switch (order.Type)
            {
                case OrderType.DevelopCity:
                    executed = ExecuteDevelop(actor, order);
                    break;
                case OrderType.BuyAegisShield:
                    executed = ExecuteBuyShield(actor, order);
                    break;
                case OrderType.AssembleMissile:
                    executed = ExecuteAssemble(actor, cost);
                    break;
                case OrderType.LaunchMissile:
                    executed = ExecuteLaunch(actor, order);
                    break;
                default:
                    executed = false;
                    break;
            }

            if (executed)
            {
                actor.TurnOrderQueue.Remove(order);
            }

            OnOrderResolved?.Invoke(actorId, order, executed);
        }

        private bool ExecuteDevelop(PlayerState actor, PlayerOrder order)
        {
            CityState city = actor.GetCity(order.CityIndex);
            if (city == null || !city.IsAlive)
            {
                return false;
            }

            actor.Money -= Config.Costs.CityDevelopment;
            city.DevelopmentLevel++;
            AddJournal(GetShortName(actor.Country) + ": развит " + city.GetDisplayName(GetShortName(actor.Country))
                        + ", уровень " + city.DevelopmentLevel + ".", JournalTone.Good);
            return true;
        }

        private bool ExecuteBuyShield(PlayerState actor, PlayerOrder order)
        {
            CityState city = actor.GetCity(order.CityIndex);
            if (city == null || !city.IsAlive || city.HasAAShield)
            {
                return false;
            }

            actor.Money -= Config.Costs.AegisShield;
            city.HasAAShield = true;
            AddJournal(GetShortName(actor.Country) + ": щит ПВО над " + city.GetDisplayName(GetShortName(actor.Country))
                        + ".", JournalTone.Good);
            return true;
        }

        private bool ExecuteAssemble(PlayerState actor, int cost)
        {
            actor.Money -= cost;
            actor.MissileStock++;

            float loss = Config.Ecology != null
                ? Config.Ecology.LossOnAssemble
                : EconomyConstants.EcologyLossOnAssemble;
            if (actor.Definition != null)
            {
                loss = actor.Definition.ResolveEcologyLossOnAssemble(loss);
            }

            WorldEcology = EconomyConstants.ClampWorldEcology(WorldEcology - loss);
            AddJournal(GetShortName(actor.Country) + ": ракета собрана, экология -"
                        + Mathf.RoundToInt(loss) + "% (склад: " + actor.MissileStock + ").", JournalTone.Good);
            return true;
        }

        private bool ExecuteLaunch(PlayerState actor, PlayerOrder order)
        {
            PlayerState victim = GetPlayer(order.TargetCountry);
            CityState target = victim != null ? victim.GetCity(order.TargetCityIndex) : null;

            if (actor.MissileStock <= 0 || victim == null || target == null
                || !victim.IsAliveCountry || order.TargetCountry == actor.Country || !target.IsAlive)
            {
                return false;
            }

            actor.MissileStock--;
            actor.MissilesLaunchedTotal++;

            float loss = Config.Ecology != null
                ? Config.Ecology.LossOnLaunch
                : EconomyConstants.EcologyLossOnLaunch;
            if (actor.Definition != null)
            {
                loss = actor.Definition.ResolveEcologyLossOnLaunch(loss);
            }

            WorldEcology = EconomyConstants.ClampWorldEcology(WorldEcology - loss);
            _attackedThisRound.Add(victim.Country);

            OnMissileLaunched?.Invoke(actor.Country, victim.Country, order.TargetCityIndex);
            AddJournal("ПУСК: " + GetShortName(actor.Country) + " → " + target.GetDisplayName(GetShortName(victim.Country))
                        + ", экология -" + Mathf.RoundToInt(loss) + "%", JournalTone.Bad);

            // Предательство: пакт остаётся в бумагах, но держава получает метку.
            if (actor.HasPact(victim.Country))
            {
                actor.IsTraitorsMark = true;
                actor.BetrayalsCommitted++;
                OnTreatyBetrayed?.Invoke(actor.Country, victim.Country);
                AddJournal("ПРЕДАТЕЛЬСТВО: " + GetShortName(actor.Country) + " ударил по союзнику "
                            + GetShortName(victim.Country) + " — в шредер летит подписанный пакт.", JournalTone.Bad);
            }

            if (target.HasAAShield)
            {
                target.HasAAShield = false;
                OnCityHitOrShielded?.Invoke(actor.Country, victim.Country, order.TargetCityIndex, true);
                AddJournal("Щит ПВО поглотил удар: " + target.GetDisplayName(GetShortName(victim.Country)) + " выжил.",
                    JournalTone.Good);
                return true;
            }

            target.IsDestroyed = true;
            victim.CitiesLostCount++;
            GlobalCitiesDestroyed++;
            OnCityHitOrShielded?.Invoke(actor.Country, victim.Country, order.TargetCityIndex, false);
            AddJournal("ГОРОД УНИЧТОЖЕН: " + target.GetDisplayName(GetShortName(victim.Country))
                        + " (" + GetShortName(actor.Country) + ")", JournalTone.Bad);
            return true;
        }

        private void ResolveAirCreditClawback()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState issuer = _players[i];
                if (issuer == null || !issuer.AirCreditIssuedThisRound || issuer.Definition == null)
                {
                    continue;
                }

                if (!_attackedThisRound.Contains(issuer.Country))
                {
                    continue;
                }

                PlayerState partner = GetPlayer(issuer.AirCreditPartner);
                if (partner == null || partner.IsDefeated)
                {
                    continue;
                }

                int clawback = Mathf.Min(Mathf.Max(0, issuer.Definition.AirCreditShare), partner.Money);
                if (clawback <= 0)
                {
                    continue;
                }

                partner.Money -= clawback;
                issuer.Money += clawback;
                OnAirCreditClawback?.Invoke(partner.Country, issuer.Country);
                AddJournal("Кредит из воздуха возвращён: " + GetShortName(partner.Country) + " ударил "
                            + GetShortName(issuer.Country) + ", " + clawback + "$ удержано.", JournalTone.Bad);
            }
        }

        private void ExecuteEcologyCollapse()
        {
            int lost = 0;

            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || !state.IsAliveCountry)
                {
                    continue;
                }

                // Культ Апокалипсиса: выполненная доктрина оставляет все города пепельца живыми.
                if (IsApocalypseCultSpared(state))
                {
                    AddJournal(GetShortName(state.Country) + " укрылся за щитами ПВО: коллапс его не тронул.",
                        JournalTone.Secret);
                    continue;
                }

                if (state.Definition != null && state.Definition.SurvivesWorldCollapse)
                {
                    // «Последний курорт»: уцелеет только один, последний живой город.
                    int keepIndex = FindLastAliveCityIndex(state);
                    for (int c = 0; c < state.Cities.Length; c++)
                    {
                        CityState city = state.Cities[c];
                        if (city != null && city.IsAlive && city.Index != keepIndex)
                        {
                            city.IsDestroyed = true;
                            lost++;
                        }
                    }

                    AddJournal("Последний курорт уцелел: " + GetShortName(state.Country) + ", город "
                                + (keepIndex + 1), JournalTone.Secret);
                    continue;
                }

                for (int c = 0; c < state.Cities.Length; c++)
                {
                    CityState city = state.Cities[c];
                    if (city != null && city.IsAlive)
                    {
                        city.IsDestroyed = true;
                        lost++;
                    }
                }

                AddJournal(GetShortName(state.Country) + ": все города сгорели в экологическом коллапсе.", JournalTone.Bad);
            }

            GlobalCitiesDestroyed += lost;
            OnEcologyCollapse?.Invoke(true, lost);
            AddJournal("ЭКОЛОГИЧЕСКИЙ КОЛЛАПС: планету накрыло пеплом, потеряно городов: " + lost + ".", JournalTone.Bad);
        }

        private bool IsApocalypseCultSpared(PlayerState state)
        {
            SecretDoctrine doctrine = GetDoctrine(state.Country);
            if (doctrine == null || doctrine.CheckKind != DoctrineCheckKind.ZeroEcologyWithAegisShields)
            {
                return false;
            }

            DoctrineEvaluationStats stats = BuildStats(state.Country);
            return SecretDoctrineEvaluator.Evaluate(doctrine, stats);
        }

        private static int FindLastAliveCityIndex(PlayerState state)
        {
            for (int i = state.Cities.Length - 1; i >= 0; i--)
            {
                if (state.Cities[i] != null && state.Cities[i].IsAlive)
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpdateElimination()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state.IsDefeated || state.AliveCityCount > 0)
                {
                    continue;
                }

                state.IsDefeated = true;
                state.TurnOrderQueue.Clear();
                OnCountryEliminated?.Invoke(state.Country);
                AddJournal("СТРАНА УНИЧТОЖЕНА: " + GetShortName(state.Country) + " больше не в игре.", JournalTone.Bad);
            }

            int alive = CountAlivePlayers();
            if (alive == 1)
            {
                FinishMatch(GetFirstAliveCountry(), "Последняя держава планеты");
            }
            else if (alive == 0)
            {
                FinishMatch(null, "Планета стёрта в пепел");
            }
        }

        private void PayRoundIncome()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state.IsDefeated)
                {
                    continue;
                }

                int income = state.GetRoundIncome();
                if (state.SanctionedNextRound)
                {
                    int penalty = Mathf.RoundToInt(EconomyConstants.ApplyPercent(income, EconomyConstants.SanctionIncomePenaltyPercent));
                    income -= penalty;
                    state.SanctionedNextRound = false;
                    AddJournal("Санкции срезали доход " + GetShortName(state.Country) + " на " + penalty + "$.",
                        JournalTone.Bad);
                }

                if (income <= 0)
                {
                    continue;
                }

                state.Money += income;
                AddJournal("Доход: " + GetShortName(state.Country) + " +" + income + "$ (городов: "
                            + state.AliveCityCount + ").", JournalTone.Info);
            }
        }

        private void UpdateRoundWinner()
        {
            PlayerState best = null;
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state.IsDefeated)
                {
                    continue;
                }

                if (best == null || GetRoundScore(state.Country) > GetRoundScore(best.Country))
                {
                    best = state;
                }
            }

            if (best == null)
            {
                return;
            }

            if (_lastRoundWinner.HasValue && _lastRoundWinner.Value == best.Country)
            {
                best.ConsecutiveRoundWins++;
            }
            else
            {
                best.ConsecutiveRoundWins = 1;
            }

            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state != null && state.Country != best.Country && state.ConsecutiveRoundWins > 0)
                {
                    state.ConsecutiveRoundWins = 0;
                }
            }

            _lastRoundWinner = best.Country;
            AddJournal("Победитель раунда " + Round + ": " + GetShortName(best.Country)
                        + " (" + GetRoundScore(best.Country) + " очков, серия " + best.ConsecutiveRoundWins + ").",
                JournalTone.Good);
        }

        /// <summary>
        /// Проверка доктрин после расчёта. Выполненных доктрин может быть несколько
        /// (например, «Бумажный Тигр» сорвал серию, а кто-то накопил деньги) —
        /// побеждает та, у кого лучше итоговый счёт, чтобы более поздняя по слоту
        /// держава не голодала из-за порядка обхода.
        /// </summary>
        private void CheckDoctrineVictory()
        {
            PlayerState best = null;
            SecretDoctrine bestDoctrine = null;

            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state.IsDefeated || state.DoctrineFulfilled)
                {
                    continue;
                }

                SecretDoctrine doctrine = GetDoctrine(state.Country);
                if (doctrine == null || doctrine.CheckKind == DoctrineCheckKind.None)
                {
                    continue;
                }

                if (!SecretDoctrineEvaluator.Evaluate(doctrine, BuildStats(state.Country)))
                {
                    continue;
                }

                if (best == null || GetVictoryScore(state.Country) > GetVictoryScore(best.Country))
                {
                    best = state;
                    bestDoctrine = doctrine;
                }
            }

            if (best == null || bestDoctrine == null)
            {
                return;
            }

            // Остальные выполненные доктрины тоже отмечаем — их показывают в итогах.
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state == best || state.IsDefeated || state.DoctrineFulfilled)
                {
                    continue;
                }

                SecretDoctrine doctrine = GetDoctrine(state.Country);
                if (doctrine == null || doctrine.CheckKind == DoctrineCheckKind.None)
                {
                    continue;
                }

                if (SecretDoctrineEvaluator.Evaluate(doctrine, BuildStats(state.Country)))
                {
                    state.DoctrineFulfilled = true;
                    OnDoctrineFulfilled?.Invoke(state.Country, doctrine.Type);
                }
            }

            best.DoctrineFulfilled = true;
            OnDoctrineFulfilled?.Invoke(best.Country, bestDoctrine.Type);
            AddJournal("ДОКТРИНА ВЫПОЛНЕНА: " + GetShortName(best.Country) + " — «" + bestDoctrine.DisplayName + "»!",
                JournalTone.Secret);
            FinishMatch(best.Country, "Секретная доктрина «" + bestDoctrine.DisplayName + "»");
        }

        private void FinishMatchByScore()
        {
            PlayerState best = null;
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || state.IsDefeated)
                {
                    continue;
                }

                if (best == null || GetVictoryScore(state.Country) > GetVictoryScore(best.Country))
                {
                    best = state;
                }
            }

            if (best == null)
            {
                FinishMatch(null, "Никто не дожил до финала");
                return;
            }

            FinishMatch(best.Country, "Победа по итогам " + Config.TotalRounds + " раундов");
        }

        private void FinishMatch(CountryId? winner, string reason)
        {
            if (Phase == GamePhase.GameOver)
            {
                return;
            }

            StopAllCoroutines();
            Winner = winner;
            WinReason = reason ?? string.Empty;
            BuildStandings();

            string headline = winner.HasValue
                ? GetShortName(winner.Value) + " победил. " + WinReason
                : "Победителя нет. " + WinReason;
            AddJournal("МАТЧ ОКОНЧЕН. " + headline, JournalTone.System);
            OnMatchFinished?.Invoke(winner);
            AdvanceTo(GamePhase.GameOver);
        }

        private void BuildStandings()
        {
            _standings.Clear();
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null)
                {
                    continue;
                }

                MatchStanding row = new MatchStanding();
                row.Country = state.Country;
                row.Money = state.Money;
                row.AliveCities = state.AliveCityCount;
                row.ShieldedCities = state.ShieldedCityCount;
                row.Launches = state.MissilesLaunchedTotal;
                row.Doctrine = state.DoctrineType;
                row.DoctrineFulfilled = state.DoctrineFulfilled;
                row.Score = GetVictoryScore(state.Country);
                _standings.Add(row);
            }

            _standings.Sort(CompareStandings);
        }

        private static int CompareStandings(MatchStanding a, MatchStanding b)
        {
            if (b.Score != a.Score)
            {
                return b.Score.CompareTo(a.Score);
            }

            return a.Country.CompareTo(b.Country);
        }

        #endregion

        #region Запросы для UI, ботов и сериализации снимка

        public PlayerState GetPlayer(CountryId id)
        {
            int index = (int)id;
            if (index < 0 || index >= _players.Length)
            {
                return null;
            }

            return _players[index];
        }

        public PlayerState GetLocalPlayerState()
        {
            return GetPlayer(LocalPlayer);
        }

        public SecretDoctrine GetDoctrine(CountryId id)
        {
            PlayerState state = GetPlayer(id);
            if (state == null || state.DoctrineType == SecretDoctrineType.None)
            {
                return null;
            }

            return Config.GetDoctrine(state.DoctrineType);
        }

        public string GetShortName(CountryId id)
        {
            CountryDefinition definition = Config.GetCountry(id);
            return definition != null ? definition.ShortName : id.ToString();
        }

        public string FormatMoney(int amount)
        {
            return amount + " $";
        }

        public int GetRoundScore(CountryId id)
        {
            PlayerState state = GetPlayer(id);
            if (state == null)
            {
                return 0;
            }

            return state.Money + state.AliveCityCount * EconomyConstants.RoundScorePerAliveCity;
        }

        public int GetVictoryScore(CountryId id)
        {
            PlayerState state = GetPlayer(id);
            if (state == null)
            {
                return 0;
            }

            return state.Money + state.AliveCityCount * EconomyConstants.VictoryScorePerAliveCity;
        }

        public int CountAlivePlayers()
        {
            int count = 0;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].IsAliveCountry)
                {
                    count++;
                }
            }

            return count;
        }

        public CountryId GetFirstAliveCountry()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].IsAliveCountry)
                {
                    return _players[i].Country;
                }
            }

            return LocalPlayer;
        }

        public IReadOnlyList<SanctionVote> GetSanctionVotes()
        {
            return _sanctionVotes;
        }

        public IReadOnlyList<MatchLogEntry> GetJournalTail(int count)
        {
            if (count <= 0 || _journal.Count == 0)
            {
                return new List<MatchLogEntry>();
            }

            int start = Mathf.Max(0, _journal.Count - count);
            return _journal.GetRange(start, _journal.Count - start);
        }

        public string GetNetworkStatus()
        {
            if (NetRole == NetRole.Client)
            {
                return _transport != null ? _transport.StatusText : "нет соединения";
            }

            if (NetRole == NetRole.Host && _transport != null)
            {
                return _transport.StatusText;
            }

            LocalLoopbackTransport loopback = _transport as LocalLoopbackTransport;
            return loopback != null ? loopback.StatusText : "локальный стенд";
        }

        public bool IsOnlineSession()
        {
            return NetRole == NetRole.Host || NetRole == NetRole.Client;
        }

        public DoctrineEvaluationStats BuildStats(CountryId id)
        {
            PlayerState state = GetPlayer(id);
            if (state == null)
            {
                return default(DoctrineEvaluationStats);
            }

            int otherMoney = 0;
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState other = _players[i];
                if (other != null && other.Country != state.Country && !other.IsDefeated)
                {
                    otherMoney += other.Money;
                }
            }

            DoctrineEvaluationStats stats = new DoctrineEvaluationStats();
            stats.Round = Round;
            stats.TotalRounds = Config.TotalRounds;
            stats.IsFinale = Round >= Mathf.Max(1, Config.TotalRounds);
            stats.OwnCitiesAlive = state.AliveCityCount;
            stats.OwnCitiesLost = state.CitiesLostCount;
            stats.OwnAegisShields = state.ShieldedCityCount;
            stats.OwnMissilesLaunched = state.MissilesLaunchedTotal;
            stats.OwnMoney = state.Money;
            stats.GlobalCitiesDestroyed = GlobalCitiesDestroyed;
            stats.OtherMoneyTotal = otherMoney;
            stats.ConsecutiveWins = state.ConsecutiveRoundWins;
            stats.ActivePactsCount = state.ActivePacts.Count;
            stats.BetrayalsCommitted = state.BetrayalsCommitted;
            stats.WorldEcology = WorldEcology;
            return stats;
        }

        private PlayerState FindPlayerByPassive(CountryPassiveKind kind)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state != null && state.Definition != null && state.Definition.PassiveKind == kind)
                {
                    return state;
                }
            }

            return null;
        }

        private bool IsUsable(PlayerState state)
        {
            return state != null && state.IsAliveCountry;
        }

        private static string DefaultCallLine(CallKind kind)
        {
            switch (kind)
            {
                case CallKind.PactOffer:
                    return "Подпишем пакт? Это же очевидная выгода.";
                case CallKind.Bribe:
                    return "Здесь сумма. Не хотите?";
                case CallKind.Threat:
                    return "Следующий звонок будет последним.";
                case CallKind.Flattery:
                    return "Вы лучший диктатор континента, не сомневайтесь.";
                case CallKind.Warning:
                    return "У нас плохие новости, держитесь.";
                default:
                    return "Поговорим?";
            }
        }

        public void AddJournal(string text, JournalTone tone)
        {
            MatchLogEntry entry = new MatchLogEntry();
            entry.Round = Round;
            entry.Phase = Phase;
            entry.Text = text ?? string.Empty;
            entry.Tone = tone;
            _journal.Add(entry);

            if (_journal.Count > EconomyConstants.MatchJournalCapacity)
            {
                _journal.RemoveRange(0, _journal.Count - EconomyConstants.MatchJournalCapacity);
            }
        }

        #endregion

        #region Сеть: хост, клиент, локальный стенд

        /// <summary>Поднимает сервер матча. Локальный игрок остаётся за хостом, слоты ботов доступны клиентам.</summary>
        public bool StartHosting(int port)
        {
            StopNetworking();
            TcpNetworkTransport transport = new TcpNetworkTransport(NetRole.Host);
            try
            {
                transport.StartHosting(port);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogPrefix + "Не удалось запустить хост: " + exception.Message);
                transport.Dispose();
                return false;
            }

            _transport = transport;
            NetRole = NetRole.Host;
            _peerSlots.Clear();
            EnterLobby();
            AddJournal("Сервер матча запущен (" + transport.StatusText + ").", JournalTone.System);
            return true;
        }

        /// <summary>Подключение к серверу матча по TCP.</summary>
        public bool JoinServer(string address, int port)
        {
            StopNetworking();
            TcpNetworkTransport transport = new TcpNetworkTransport(NetRole.Client);
            try
            {
                transport.Connect(address, port, 3000);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogPrefix + "Не удалось подключиться: " + exception.Message);
                transport.Dispose();
                return false;
            }

            _transport = transport;
            NetRole = NetRole.Client;
            SendCommand(NetOp.Hello, LocalPlayer);
            AddJournal("Подключение к " + address + ":" + port + ".", JournalTone.System);
            return true;
        }

        /// <summary>Возврат к локальному стенду (1 игрок + 5 ботов).</summary>
        public void ReturnToSolo()
        {
            StopNetworking();
            NetRole = NetRole.Solo;
            _transport = new LocalLoopbackTransport();
            EnterLobby();
        }

        public void StopNetworking()
        {
            if (_transport == null)
            {
                return;
            }

            try
            {
                _transport.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogPrefix + "Ошибка закрытия транспорта: " + exception.Message);
            }

            _transport = null;
        }

        /// <summary>
        /// Единая точка входа для любой мутации состояния.
        /// Локальный стенд прогоняет команду через тот же JSON-канал, что и сеть,
        /// поэтому локальная партия всегда проверяет сетевой путь.
        /// </summary>
        private bool PrepareMutation(NetOp op, CountryId actor, int a = 0, int b = 0, int c = 0, float f = 0f, string s = null)
        {
            if (_applyingRemoteCommand || NetRole == NetRole.Host)
            {
                return true;
            }

            if (NetRole == NetRole.Client)
            {
                SendCommand(op, actor, a, b, c, f, s);
                return false;
            }

            // Solo: команда уходит в loopback и тут же применяется как входящая из сети.
            SendCommand(op, actor, a, b, c, f, s);
            PumpIncoming();
            return false;
        }

        public void SendCommand(NetOp op, CountryId actor, int a = 0, int b = 0, int c = 0, float f = 0f, string s = null)
        {
            if (_transport == null)
            {
                Debug.LogWarning(LogPrefix + "Команда отброшена: транспорт не настроен.");
                return;
            }

            NetMessage message = NetMessage.Create(op, actor);
            message.A = a;
            message.B = b;
            message.C = c;
            message.F = f;
            message.S = s;

            try
            {
                _transport.SendToServer(JsonUtility.ToJson(message));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogPrefix + "Ошибка отправки команды: " + exception.Message);
            }
        }

        private void PumpIncoming()
        {
            while (_transport != null && _transport.TryReceive(out string peerId, out string payload))
            {
                HandleIncoming(peerId, payload);
            }
        }

        private void HandleIncoming(string peerId, string payload)
        {
            NetMessage message;
            try
            {
                message = JsonUtility.FromJson<NetMessage>(payload);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogPrefix + "Сообщение не разобрано: " + exception.Message);
                return;
            }

            if (message == null)
            {
                return;
            }

            CountryId actor = (CountryId)message.Actor;
            _applyingRemoteCommand = true;

            try
            {
                DispatchCommand(peerId, message, actor);
            }
            finally
            {
                _applyingRemoteCommand = false;
            }
        }

        private void DispatchCommand(string peerId, NetMessage message, CountryId actor)
        {
            if (!IsAuthoritative)
            {
                switch (message.Op)
                {
                    case NetOp.AssignSlot:
                        SetLocalPlayer(actor);
                        AddJournal("Вы играете за " + GetShortName(actor) + ".", JournalTone.System);
                        break;
                    case NetOp.NoFreeSlots:
                        AddJournal("Сервер: свободных слотов нет.", JournalTone.Bad);
                        break;
                }

                return;
            }

            switch (message.Op)
            {
                case NetOp.Hello:
                    HandleHello(peerId);
                    break;
                case NetOp.StartMatch:
                    StartMatch();
                    break;
                case NetOp.EndPhaseEarly:
                    EndPhaseEarly(actor);
                    break;
                case NetOp.ForceAdvance:
                    ForceAdvancePhase();
                    break;
                case NetOp.TransferMoney:
                    TransferMoney(actor, (CountryId)message.A, message.B);
                    break;
                case NetOp.IssueAirCredit:
                    IssueAirCredit(actor, (CountryId)message.A);
                    break;
                case NetOp.BuyRecon:
                    BuyRecon(actor, (CountryId)message.A);
                    break;
                case NetOp.BuyWiretap:
                    BuyWiretap(actor, (CountryId)message.A);
                    break;
                case NetOp.PledgeEcology:
                    PledgeEcology(actor);
                    break;
                case NetOp.PlacePhoneCall:
                    PlacePhoneCall(actor, (CountryId)message.A, (CallKind)message.B, message.S, message.C);
                    break;
                case NetOp.OfferPact:
                    OfferPact(actor, (CountryId)message.A);
                    break;
                case NetOp.RespondToPactOffer:
                    RespondToPactOffer(actor, message.A, message.B > 0);
                    break;
                case NetOp.CastSanctionVote:
                    CastSanctionVote(actor, (CountryId)message.A);
                    break;
                case NetOp.SignPact:
                    SignPact(actor, (CountryId)message.A);
                    break;
                case NetOp.BreakPact:
                    BreakPact(actor, (CountryId)message.A);
                    break;
                case NetOp.SubmitOrder:
                    SubmitOrder(actor, DecodeOrder(message));
                    break;
                case NetOp.CancelOrder:
                    CancelOrder(actor, message.A);
                    break;
                case NetOp.ClearOrders:
                    ClearOrders(actor);
                    break;
            }
        }

        private void HandleHello(string peerId)
        {
            if (_peerSlots.ContainsKey(peerId))
            {
                return;
            }

            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null || !state.IsAI)
                {
                    continue;
                }

                state.IsAI = false;
                _peerSlots[peerId] = state.Country;
                if (_transport != null)
                {
                    _transport.SendToPeer(peerId, JsonUtility.ToJson(NetMessage.Create(NetOp.AssignSlot, state.Country)));
                }

                AddJournal("Новый игрок в партии: " + GetShortName(state.Country) + ".", JournalTone.System);
                FlushSnapshot();
                return;
            }

            if (_transport != null)
            {
                _transport.SendToPeer(peerId, JsonUtility.ToJson(NetMessage.Create(NetOp.NoFreeSlots, LocalPlayer)));
            }
        }

        private static PlayerOrder DecodeOrder(NetMessage message)
        {
            PlayerOrder order = new PlayerOrder((OrderType)message.A);
            order.CityIndex = message.B;
            order.TargetCountry = (CountryId)message.C;
            order.TargetCityIndex = Mathf.RoundToInt(message.F);
            return order;
        }

        private void FlushSnapshot()
        {
            if (_transport == null || NetRole != NetRole.Host)
            {
                return;
            }

            try
            {
                _transport.Broadcast(JsonUtility.ToJson(BuildSnapshot()));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogPrefix + "Ошибка рассылки снимка: " + exception.Message);
            }
        }

        private MatchSnapshot BuildSnapshot()
        {
            MatchSnapshot snapshot = new MatchSnapshot();
            snapshot.Round = Round;
            snapshot.Phase = (int)Phase;
            snapshot.Time = PhaseTimeLeft;
            snapshot.Ecology = WorldEcology;
            snapshot.CitiesDestroyed = GlobalCitiesDestroyed;
            snapshot.LocalCountry = (int)LocalPlayer;
            snapshot.Winner = Winner.HasValue ? (int)Winner.Value : -1;
            snapshot.Reason = WinReason;

            for (int i = 0; i < _players.Length; i++)
            {
                PlayerState state = _players[i];
                if (state == null)
                {
                    continue;
                }

                PlayerSnapshot playerSnapshot = new PlayerSnapshot();
                playerSnapshot.Country = (int)state.Country;
                playerSnapshot.Money = state.Money;
                playerSnapshot.Stock = state.MissileStock;
                playerSnapshot.Defeated = state.IsDefeated;
                playerSnapshot.Ai = state.IsAI;
                playerSnapshot.Traitor = state.IsTraitorsMark;
                playerSnapshot.Sanctioned = state.SanctionedNextRound;
                playerSnapshot.Pledged = state.PledgedEcologyContribution;
                playerSnapshot.DoctrineDone = state.DoctrineFulfilled;
                playerSnapshot.Doctrine = state.Country == LocalPlayer ? (int)state.DoctrineType : -1;

                for (int c = 0; c < state.Cities.Length; c++)
                {
                    CityState city = state.Cities[c];
                    CitySnapshot citySnapshot = new CitySnapshot();
                    citySnapshot.Index = city.Index;
                    citySnapshot.Destroyed = city.IsDestroyed;
                    citySnapshot.Development = city.DevelopmentLevel;
                    citySnapshot.Shield = city.HasAAShield;
                    playerSnapshot.Cities.Add(citySnapshot);
                }

                foreach (CountryId pact in state.ActivePacts)
                {
                    playerSnapshot.Pacts.Add((int)pact);
                }

                snapshot.Players.Add(playerSnapshot);
            }

            IReadOnlyList<MatchLogEntry> tail = GetJournalTail(SnapshotJournalTail);
            for (int i = 0; i < tail.Count; i++)
            {
                JournalSnapshot journalSnapshot = new JournalSnapshot();
                journalSnapshot.Text = tail[i].Text;
                journalSnapshot.Tone = (int)tail[i].Tone;
                journalSnapshot.Round = tail[i].Round;
                snapshot.Journal.Add(journalSnapshot);
            }

            return snapshot;
        }

        /// <summary>Применение присланного снимка на клиенте: UI рисует чужое состояние как своё.</summary>
        public void ApplySnapshot(string json)
        {
            MatchSnapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<MatchSnapshot>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogPrefix + "Снимок не разобран: " + exception.Message);
                return;
            }

            if (snapshot == null || snapshot.Version != NetKinds.SnapshotVersion)
            {
                return;
            }

            Round = snapshot.Round;
            Phase = (GamePhase)snapshot.Phase;
            PhaseTimeLeft = snapshot.Time;
            WorldEcology = snapshot.Ecology;
            GlobalCitiesDestroyed = snapshot.CitiesDestroyed;
            Winner = snapshot.Winner < 0 ? (CountryId?)null : (CountryId)snapshot.Winner;
            if (!string.IsNullOrEmpty(snapshot.Reason))
            {
                WinReason = snapshot.Reason;
            }

            for (int i = 0; i < snapshot.Players.Count; i++)
            {
                PlayerSnapshot playerSnapshot = snapshot.Players[i];
                PlayerState state = GetPlayer((CountryId)playerSnapshot.Country);
                if (state == null)
                {
                    continue;
                }

                state.Money = playerSnapshot.Money;
                state.MissileStock = playerSnapshot.Stock;
                state.IsDefeated = playerSnapshot.Defeated;
                state.IsAI = playerSnapshot.Ai;
                state.IsTraitorsMark = playerSnapshot.Traitor;
                state.SanctionedNextRound = playerSnapshot.Sanctioned;
                state.PledgedEcologyContribution = playerSnapshot.Pledged;
                state.DoctrineFulfilled = playerSnapshot.DoctrineDone;

                for (int c = 0; c < playerSnapshot.Cities.Count; c++)
                {
                    CitySnapshot citySnapshot = playerSnapshot.Cities[c];
                    CityState city = state.GetCity(citySnapshot.Index);
                    if (city == null)
                    {
                        continue;
                    }

                    city.IsDestroyed = citySnapshot.Destroyed;
                    city.DevelopmentLevel = citySnapshot.Development;
                    city.HasAAShield = citySnapshot.Shield;
                }

                state.ActivePacts.Clear();
                for (int p = 0; p < playerSnapshot.Pacts.Count; p++)
                {
                    state.ActivePacts.Add((CountryId)playerSnapshot.Pacts[p]);
                }
            }

            _journal.Clear();
            for (int i = 0; i < snapshot.Journal.Count; i++)
            {
                JournalSnapshot journalSnapshot = snapshot.Journal[i];
                MatchLogEntry entry = new MatchLogEntry();
                entry.Round = journalSnapshot.Round;
                entry.Phase = Phase;
                entry.Text = journalSnapshot.Text;
                entry.Tone = (JournalTone)journalSnapshot.Tone;
                _journal.Add(entry);
            }
        }

        #endregion
    }
}