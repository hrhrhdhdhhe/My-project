// World Crisis | Слой: Core | Ответственный: OpenCode
// ИИ-диктаторы: заполняют пустые слоты лобби и ведут партию по правилам своей секретной доктрины.
// Звонят по телефонам в бункере, предлагают пакты и взятки, голосуют на саммите и бьют в спину в расчёте.
// Стратегия ветвится только по машино-читаемому DoctrineCheckKind — новые доктрины не требуют правок здесь.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    [DisallowMultipleComponent]
    public class AIDictatorController : MonoBehaviour
    {
        // --- Тайминги поведения ботов (настройка ИИ, не игровой баланс) ---
        private const float CallDelayMin = 3f;
        private const float CallDelayMax = 18f;
        private const float OfferDelayMin = 5f;
        private const float OfferDelayMax = 25f;
        private const float VoteDelayMin = 2f;
        private const float VoteDelayMax = 12f;
        private const float OrderStepDelay = 0.3f;
        private const float OfferPollInterval = 2.5f;
        private const int MaxCallsPerDiplomacy = 2;
        private const int PhoneLineChance = 55;
        private const int ReconChance = 30;
        private const int WiretapChance = 18;
        private const int PledgeChance = 35;
        private const int SanctionVoteChance = 60;
        private const int RetreatMoney = EconomyConstants.DefaultTransferAmount;
        private const int SoftReserve = 150;
        private const int PactLimitSilentBunker = 3;
        private const int PactLimitOathbreaker = 5;
        private const int InsuranceMoneyTarget = EconomyConstants.InsuranceFraudMoney;

        /// <summary>Порог живых городов для «Немого бункера»: доктрина требует дотянуть до финала с 3+ городами.</summary>
        private const int SilentBunkerCityTarget = EconomyConstants.SilentBunkerCitiesAlive;
        private const int BribeSharePercent = 10;
        private const int MaxMissileStock = 2;
        private const float EcologySafetyMargin = 30f;
        private const int BlindShotChance = 30;

        /// <summary>
        /// Стоит ли державе тратить экологию. Ниже порога безопасности ракету не собираем:
        /// обвал до нуля сжигает все города, включая собственные.
        /// </summary>
        private bool CanAffordEcologyDamage(PlayerState me, float alreadyPlannedDamage)
        {
            alreadyPlannedDamage += _plannedEcologyThisRound;
            EcologyRules rules = _match.Config.Ecology;
            if (rules == null)
            {
                return true;
            }

            float oneMissile = me.Definition != null
                ? me.Definition.ResolveEcologyLossOnAssemble(rules.LossOnAssemble)
                : rules.LossOnAssemble;

            // Экология после всех уже запланированных сборок этого раунда.
            float projected = _match.WorldEcology - alreadyPlannedDamage - oneMissile;

            if (projected > EcologySafetyMargin)
            {
                return true;
            }

            // На самой грани идём на риск — диктаторы тоже нервничают.
            return projected > 0f;
        }

        private sealed class ScheduledAction
        {
            public float DueTime;
            public Action Run;

            /// <summary>Отметка «фаза сделана» — такие действия не исполняются при досрочном переходе.</summary>
            public bool IsReadyMark;
        }

        private sealed class BotBrain
        {
            public CountryId Country;
            public DoctrineCheckKind Doctrine;
            public System.Random Rng;

            public float AttackAppetite;
            public float PactAppetite;
            public float Hoard;

            public bool Fortifies;
            public bool SeeksCollapse;
            public bool Oathbreaker;
            public bool WarmsThePot;
            public bool Honest;

            public readonly List<ScheduledAction> Schedule = new List<ScheduledAction>();
            public readonly Dictionary<CountryId, int> Trust = new Dictionary<CountryId, int>();
            public readonly HashSet<CountryId> KnownTraitors = new HashSet<CountryId>();

            public CountryId RevengeTarget;
            public float NextOfferPoll;
            public int CallsThisPhase;

            public int Roll(int percent)
            {
                return Rng.Next(0, 100);
            }

            public float Range(float min, float max)
            {
                return min + (float)Rng.NextDouble() * (max - min);
            }
        }

        private readonly List<BotBrain> _brains = new List<BotBrain>();
        private MatchManager _match;

        /// <summary>Урон по экологии, уже запланированный ботами в текущем раунде.</summary>
        private float _plannedEcologyThisRound;

        private void Start()
        {
            _match = MatchManager.Instance;
            if (_match == null)
            {
                Debug.LogWarning("[WorldCrisis] AIDictatorController: нет MatchManager, боты выключены.");
                enabled = false;
                return;
            }

            _match.OnPhaseChanged += HandlePhaseChanged;
            _match.OnPhaseEnding += HandlePhaseEnding;
            _match.OnTreatyBetrayed += HandleTreatyBetrayed;
            _match.OnCityHitOrShielded += HandleCityHitOrShielded;
            _match.OnSanctionImposed += HandleSanctionImposed;
            _match.OnEcologyPromiseBroken += HandleEcologyPromiseBroken;
            _match.OnMatchFinished += HandleMatchFinished;
        }

        private void OnDestroy()
        {
            if (_match == null)
            {
                return;
            }

            _match.OnPhaseChanged -= HandlePhaseChanged;
            _match.OnPhaseEnding -= HandlePhaseEnding;
            _match.OnTreatyBetrayed -= HandleTreatyBetrayed;
            _match.OnCityHitOrShielded -= HandleCityHitOrShielded;
            _match.OnSanctionImposed -= HandleSanctionImposed;
            _match.OnEcologyPromiseBroken -= HandleEcologyPromiseBroken;
            _match.OnMatchFinished -= HandleMatchFinished;
        }

        private void Update()
        {
            if (_match == null || !_match.IsAuthoritative)
            {
                return;
            }

            float now = Time.time;

            for (int i = 0; i < _brains.Count; i++)
            {
                BotBrain brain = _brains[i];
                if (brain == null)
                {
                    continue;
                }

                for (int j = brain.Schedule.Count - 1; j >= 0; j--)
                {
                    ScheduledAction action = brain.Schedule[j];
                    if (now < action.DueTime)
                    {
                        continue;
                    }

                    brain.Schedule.RemoveAt(j);
                    try
                    {
                        action.Run();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("[WorldCrisis] Сбой действия ИИ " + brain.Country + ": " + exception.Message);
                    }
                }

                if (_match.Phase == GamePhase.BunkerDiplomacy && now >= brain.NextOfferPoll)
                {
                    brain.NextOfferPoll = now + OfferPollInterval;
                    HandleIncomingOffers(brain);
                }
            }
        }

        #region Подписки на события матча

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (!_match.IsAuthoritative)
            {
                return;
            }

            if (phase == GamePhase.GameOver)
            {
                ClearAllSchedules();
                return;
            }

            SyncBrains();

            if (phase == GamePhase.OrderInput)
            {
                _plannedEcologyThisRound = 0f;
            }

            for (int i = 0; i < _brains.Count; i++)
            {
                BotBrain brain = _brains[i];
                brain.Schedule.Clear();
                brain.CallsThisPhase = 0;

                if (brain.Country != _match.LocalPlayer)
                {
                    PlanForPhase(brain, phase);
                }
            }
        }

        /// <summary>Перед сменой фазы досрочно выполняем всё, что бот ещё не успел наделать.</summary>
        private void HandlePhaseEnding(GamePhase phase)
        {
            for (int i = 0; i < _brains.Count; i++)
            {
                BotBrain brain = _brains[i];
                List<ScheduledAction> pending = new List<ScheduledAction>(brain.Schedule);
                brain.Schedule.Clear();

                for (int j = 0; j < pending.Count; j++)
                {
                    ScheduledAction action = pending[j];

                    // Отметку «готов» во время перехода выполнять нельзя: фаза и так заканчивается,
                    // иначе бот своим же EndPhaseEarly проскочит через следующую фазу.
                    if (action.IsReadyMark)
                    {
                        continue;
                    }

                    try
                    {
                        action.Run();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("[WorldCrisis] Сбой досрочного действия ИИ: " + exception.Message);
                    }
                }
            }
        }

        private void HandleTreatyBetrayed(CountryId attacker, CountryId victim)
        {
            for (int i = 0; i < _brains.Count; i++)
            {
                BotBrain brain = _brains[i];
                AdjustTrust(brain, attacker, -60);

                if (brain.Country == victim)
                {
                    brain.KnownTraitors.Add(attacker);
                    brain.RevengeTarget = attacker;
                    _match.BreakPact(brain.Country, attacker);
                }
            }
        }

        private void HandleCityHitOrShielded(CountryId attacker, CountryId target, int cityIndex, bool shielded)
        {
            for (int i = 0; i < _brains.Count; i++)
            {
                BotBrain brain = _brains[i];
                AdjustTrust(brain, attacker, -12);

                if (brain.Country == target)
                {
                    brain.RevengeTarget = attacker;

                    // Отражённый щитом удар — ещё не предательство, но доверие падает.
                    if (!shielded)
                    {
                        brain.KnownTraitors.Add(attacker);
                    }

                    _match.BreakPact(brain.Country, attacker);
                }
            }
        }

        private void HandleSanctionImposed(CountryId target, int votes)
        {
            for (int i = 0; i < _brains.Count; i++)
            {
                BotBrain brain = _brains[i];
                if (brain.Country == target)
                {
                    continue;
                }

                AdjustTrust(brain, target, -20);
            }
        }

        private void HandleEcologyPromiseBroken(CountryId liar)
        {
            for (int i = 0; i < _brains.Count; i++)
            {
                BotBrain brain = _brains[i];
                if (brain.Country == liar)
                {
                    continue;
                }

                AdjustTrust(brain, liar, -25);
            }
        }

        private void HandleMatchFinished(CountryId? winner)
        {
            ClearAllSchedules();
        }

        private void ClearAllSchedules()
        {
            for (int i = 0; i < _brains.Count; i++)
            {
                _brains[i].Schedule.Clear();
            }
        }

        private static void AdjustTrust(BotBrain brain, CountryId country, int delta)
        {
            int value;
            brain.Trust.TryGetValue(country, out value);
            brain.Trust[country] = Mathf.Clamp(value + delta, -100, 100);
        }

        private static int GetTrust(BotBrain brain, CountryId country)
        {
            int value;
            return brain.Trust.TryGetValue(country, out value) ? value : 0;
        }

        #endregion

        #region Синхронизация слотов и личности ботов

        /// <summary>Создаёт мозги для новых слотов ботов и подстраивает их под живые партии.</summary>
        private void SyncBrains()
        {
            IReadOnlyList<PlayerState> players = _match.AllPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerState state = players[i];
                if (state == null || !state.IsAI)
                {
                    continue;
                }

                BotBrain brain = FindBrain(_brains, state.Country);
                if (brain != null)
                {
                    continue;
                }

                brain = new BotBrain();
                brain.Country = state.Country;
                brain.Rng = new System.Random((int)state.Country * 7919 + Environment.TickCount);
                brain.RevengeTarget = CountryId.NeoSilicon;
                ConfigurePersonality(brain, state);
                _brains.Add(brain);
            }

            for (int i = _brains.Count - 1; i >= 0; i--)
            {
                PlayerState state = _match.GetPlayer(_brains[i].Country);
                if (state == null || !state.IsAI)
                {
                    _brains.RemoveAt(i);
                }
            }
        }

        /// <summary>Личность бота выводится из его доктрины: аппетит к войне, пактам и деньгам.</summary>
        private void ConfigurePersonality(BotBrain brain, PlayerState state)
        {
            SecretDoctrine doctrine = _match.GetDoctrine(state.Country);
            brain.Doctrine = doctrine != null ? doctrine.CheckKind : DoctrineCheckKind.None;

            // Значения по умолчанию для державы без доктрины.
            brain.AttackAppetite = 0.5f;
            brain.PactAppetite = 0.5f;
            brain.Hoard = 0.3f;
            brain.Fortifies = true;
            brain.Honest = true;

            switch (brain.Doctrine)
            {
                case DoctrineCheckKind.GlobalCitiesDestroyedWithoutOwnLaunch:
                    // Торговец Смертью: чужие пуски нужны ему, свои — нет.
                    brain.AttackAppetite = 0f;
                    brain.PactAppetite = 0.2f;
                    brain.Hoard = 0.6f;
                    brain.Fortifies = false;
                    brain.WarmsThePot = true;
                    brain.Honest = false;
                    break;

                case DoctrineCheckKind.RicherThanEveryoneCombined:
                    // Паразит: копит, не тратится, метит самых богатых на санкции.
                    brain.AttackAppetite = 0f;
                    brain.PactAppetite = 0.4f;
                    brain.Hoard = 0.9f;
                    brain.Fortifies = false;
                    brain.Honest = false;
                    break;

                case DoctrineCheckKind.ZeroEcologyWithAegisShields:
                    // Культ Апокалипсиса: 4 щита и обвал экологии до нуля.
                    brain.AttackAppetite = 1f;
                    brain.PactAppetite = 0.1f;
                    brain.Hoard = 0.1f;
                    brain.SeeksCollapse = true;
                    brain.Honest = false;
                    break;

                case DoctrineCheckKind.PactWithAllThenBetrayOne:
                    // Клятвопреступник: подписывает всех и режет ровно одного в финале.
                    brain.AttackAppetite = 0.4f;
                    brain.PactAppetite = 1f;
                    brain.Hoard = 0.2f;
                    brain.Oathbreaker = true;
                    break;

                case DoctrineCheckKind.WinStreakWithoutLaunch:
                    // Бумажный тигр: побеждает счётом и щитами, ракету не трогает.
                    brain.AttackAppetite = 0f;
                    brain.PactAppetite = 0.7f;
                    brain.Hoard = 0.5f;
                    break;

                case DoctrineCheckKind.KeepManyPactsToFinale:
                    // Паутина пактов: держит сети договоров до финала.
                    brain.AttackAppetite = 0.1f;
                    brain.PactAppetite = 1f;
                    brain.Hoard = 0.3f;
                    break;

                case DoctrineCheckKind.RichWithoutLosingCities:
                    // Страховой мошенник: деньги поберечь, города прикрыть.
                    brain.AttackAppetite = 0f;
                    brain.PactAppetite = 0.5f;
                    brain.Hoard = 0.8f;
                    break;

                case DoctrineCheckKind.SurviveToFinaleUnarmed:
                    // Немой бункер: ни щитов, ни пусков, только города и доход.
                    brain.AttackAppetite = 0f;
                    brain.PactAppetite = 0.8f;
                    brain.Hoard = 0.2f;
                    brain.Fortifies = false;
                    break;
            }

            if (state.Definition != null && state.Definition.PassiveKind == CountryPassiveKind.AirCredit)
            {
                // Халифат дружелюбнее остальных: кредит выдаёт охотно.
                brain.PactAppetite = Mathf.Max(brain.PactAppetite, 0.7f);
            }
        }

        #endregion

        #region Планирование действий по фазам

        private void PlanForPhase(BotBrain brain, GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.BunkerDiplomacy:
                    PlanDiplomacy(brain);
                    break;
                case GamePhase.SummitUN:
                    PlanSummit(brain);
                    break;
                case GamePhase.OrderInput:
                    PlanOrders(brain);
                    break;
            }
        }

        private void PlanDiplomacy(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry)
            {
                return;
            }

            float now = Time.time;
            int callCount = brain.Roll(100) < 40 ? MaxCallsPerDiplomacy : 1;
            for (int i = 0; i < callCount; i++)
            {
                float due = now + brain.Range(CallDelayMin, CallDelayMax) * (i + 1) * 0.5f;
                Schedule(brain, due, () => MakePhoneCall(brain));
            }

            if (brain.WarmsThePot)
            {
                Schedule(brain, now + CallDelayMax * 0.6f, () => FundWarmongers(brain));
            }

            Schedule(brain, now + OfferDelayMin, () => OfferPacts(brain));
            Schedule(brain, now + OfferDelayMax * 0.7f, () => BuyIntel(brain));
            Schedule(brain, now + CallDelayMin * 0.5f, () => MaybeIssueAirCredit(brain));
            Schedule(brain, now + 1f, () => MaybePledge(brain));

            brain.NextOfferPoll = now + OfferPollInterval;
            ScheduleReady(brain, now + brain.Range(CallDelayMin, CallDelayMax));
        }

        private void PlanSummit(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry)
            {
                return;
            }

            float now = Time.time;
            Schedule(brain, now + brain.Range(VoteDelayMin, VoteDelayMax), () => VoteSanctions(brain));
            Schedule(brain, now + VoteDelayMin, () => SignMorePacts(brain));
            ScheduleReady(brain, now + VoteDelayMax * 0.8f);
        }

        private void PlanOrders(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry)
            {
                return;
            }

            List<PlayerOrder> plan = BuildOrderPlan(brain, me);
            float now = Time.time;

            for (int i = 0; i < plan.Count; i++)
            {
                PlayerOrder order = plan[i];
                Schedule(brain, now + i * OrderStepDelay, () => _match.SubmitOrder(brain.Country, order));
            }

            ScheduleReady(brain, now + Mathf.Max(1f, plan.Count * OrderStepDelay));
        }

        #endregion

        #region Переговоры: звонки, пакты, взятки, разведка

        private void MakePhoneCall(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry || _match.Phase != GamePhase.BunkerDiplomacy)
            {
                return;
            }

            brain.CallsThisPhase++;
            CountryId callee = PickCallTarget(brain);
            if (callee == me.Country)
            {
                return;
            }

            CallKind kind = PickCallKind(brain, callee);
            string line = PickCallLine(brain, kind);
            int bribe = 0;

            if (kind == CallKind.Bribe && me.Money > RetreatMoney * 4)
            {
                bribe = Mathf.Max(1, me.Money * BribeSharePercent / 100);
            }

            _match.PlacePhoneCall(brain.Country, callee, kind, line, bribe);

            if (kind == CallKind.PactOffer)
            {
                OfferPactTo(brain, callee);
            }
        }

        private CountryId PickCallTarget(BotBrain brain)
        {
            List<CountryId> candidates = new List<CountryId>();
            IReadOnlyList<PlayerState> players = _match.AllPlayers;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState state = players[i];
                if (state == null || !state.IsAliveCountry || state.Country == brain.Country)
                {
                    continue;
                }

                candidates.Add(state.Country);
            }

            if (candidates.Count == 0)
            {
                return brain.Country;
            }

            // Человека диктаторы дёргают чаще: с ним интереснее всего.
            if (brain.Roll(40) < 40 && _match.GetPlayer(_match.LocalPlayer) != null)
            {
                PlayerState human = _match.GetPlayer(_match.LocalPlayer);
                if (human != null && human.IsAliveCountry && human.Country != brain.Country)
                {
                    return human.Country;
                }
            }

            candidates.Sort((a, b) => GetTrust(brain, b).CompareTo(GetTrust(brain, a)));
            return candidates[brain.Rng.Next(0, candidates.Count)];
        }

        private CallKind PickCallKind(BotBrain brain, CountryId callee)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null)
            {
                return CallKind.Bluster;
            }

            if (brain.KnownTraitors.Contains(callee) || GetTrust(brain, callee) < -40)
            {
                return CallKind.Threat;
            }

            if (!me.HasPact(callee) && brain.Roll(100) < brain.PactAppetite * 60f)
            {
                return CallKind.PactOffer;
            }

            if (brain.Honest && brain.Roll(100) < 25)
            {
                return CallKind.Flattery;
            }

            if (brain.Roll(100) < PhoneLineChance)
            {
                return CallKind.Bluster;
            }

            return brain.Hoard < 0.5f ? CallKind.Bribe : CallKind.Warning;
        }

        private string PickCallLine(BotBrain brain, CallKind kind)
        {
            switch (kind)
            {
                case CallKind.PactOffer:
                    return PickLine(brain, PactLines);

                case CallKind.Threat:
                    return PickLine(brain, ThreatLines);

                case CallKind.Flattery:
                    return PickLine(brain, FlatteryLines);

                case CallKind.Warning:
                    return PickLine(brain, WarningLines);

                default:
                    return PickLine(brain, BlusterLines);
            }
        }

        private string PickLine(BotBrain brain, string[] pool)
        {
            return pool.Length == 0 ? string.Empty : pool[brain.Rng.Next(0, pool.Length)];
        }

        /// <summary>Торговец Смертью подкармливает тех, кто уже стреляет.</summary>
        private void FundWarmongers(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry || me.Money <= RetreatMoney)
            {
                return;
            }

            CountryId? hottest = null;
            int hottestLaunches = 0;
            IReadOnlyList<PlayerState> players = _match.AllPlayers;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState state = players[i];
                if (state == null || !state.IsAliveCountry || state.Country == brain.Country)
                {
                    continue;
                }

                if (state.MissilesLaunchedTotal > hottestLaunches)
                {
                    hottestLaunches = state.MissilesLaunchedTotal;
                    hottest = state.Country;
                }
            }

            if (hottest.HasValue && hottestLaunches > 0)
            {
                _match.TransferMoney(brain.Country, hottest.Value, RetreatMoney);
            }
        }

        private void OfferPacts(BotBrain brain)
        {
            int limit = GetPactLimit(brain);
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry || me.ActivePacts.Count >= limit)
            {
                return;
            }

            List<CountryId> targets = CollectPactCandidates(brain, limit);
            for (int i = 0; i < targets.Count && me.ActivePacts.Count + i < limit; i++)
            {
                OfferPactTo(brain, targets[i]);
            }
        }

        private void OfferPactTo(BotBrain brain, CountryId target)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || me.HasPact(target))
            {
                return;
            }

            if (_match.OfferPact(brain.Country, target))
            {
                _match.PlacePhoneCall(brain.Country, target, CallKind.PactOffer,
                    "Давайте подпишем пакт прямо на саммите.", 0);
            }
        }

        private void SignMorePacts(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry || _match.Phase != GamePhase.SummitUN)
            {
                return;
            }

            int limit = GetPactLimit(brain);
            List<CountryId> targets = CollectPactCandidates(brain, limit);

            for (int i = 0; i < targets.Count && me.ActivePacts.Count < limit; i++)
            {
                _match.SignPact(brain.Country, targets[i]);
            }
        }

        private int GetPactLimit(BotBrain brain)
        {
            switch (brain.Doctrine)
            {
                case DoctrineCheckKind.PactWithAllThenBetrayOne:
                    return PactLimitOathbreaker;

                case DoctrineCheckKind.KeepManyPactsToFinale:
                    return EconomyConstants.PactSpiderActivePacts;

                case DoctrineCheckKind.SurviveToFinaleUnarmed:
                    return PactLimitSilentBunker;

                default:
                    return brain.PactAppetite > 0.6f ? 3 : 1;
            }
        }

        private List<CountryId> CollectPactCandidates(BotBrain brain, int limit)
        {
            List<CountryId> candidates = new List<CountryId>();
            IReadOnlyList<PlayerState> players = _match.AllPlayers;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState state = players[i];
                if (state == null || !state.IsAliveCountry || state.Country == brain.Country)
                {
                    continue;
                }

                if (brain.KnownTraitors.Contains(state.Country))
                {
                    continue;
                }

                PlayerState me = _match.GetPlayer(brain.Country);
                if (me != null && me.HasPact(state.Country))
                {
                    continue;
                }

                candidates.Add(state.Country);
            }

            candidates.Sort((a, b) => GetTrust(brain, b).CompareTo(GetTrust(brain, a)));

            int head = Mathf.Min(candidates.Count, Mathf.Max(1, limit));
            return candidates.GetRange(0, head);
        }

        private void HandleIncomingOffers(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || me.PendingPactOffers.Count == 0)
            {
                return;
            }

            int limit = GetPactLimit(brain);

            for (int i = me.PendingPactOffers.Count - 1; i >= 0; i--)
            {
                PactOffer offer = me.PendingPactOffers[i];
                if (offer.Accepted)
                {
                    continue;
                }

                bool accept = ShouldAcceptOffer(brain, offer) && me.ActivePacts.Count < limit;

                if (accept && brain.Honest)
                {
                    _match.PlacePhoneCall(brain.Country, offer.From, CallKind.Flattery,
                        "Ваши условия принимаю. Договорились.", 0);
                }
                else if (!accept && brain.Roll(100) < 40)
                {
                    _match.PlacePhoneCall(brain.Country, offer.From, CallKind.Threat,
                        "Пакты? Нет. У меня своя политика.", 0);
                }

                _match.RespondToPactOffer(brain.Country, i, accept);
            }
        }

        private bool ShouldAcceptOffer(BotBrain brain, PactOffer offer)
        {
            PlayerState proposer = _match.GetPlayer(offer.From);
            if (proposer == null || !proposer.IsAliveCountry)
            {
                return false;
            }

            if (brain.KnownTraitors.Contains(offer.From))
            {
                return false;
            }

            if (brain.AttackAppetite <= 0.2f && proposer.MissilesLaunchedTotal >= 2)
            {
                return false;
            }

            return brain.Roll(100) < 40 + brain.PactAppetite * 60f;
        }

        private void BuyIntel(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry || _match.Phase != GamePhase.BunkerDiplomacy)
            {
                return;
            }

            CountryId target = PickIntelTarget(brain, me, true);
            if (brain.Roll(100) < ReconChance && _match.Config.Costs != null
                && me.Money > _match.Config.Costs.Recon * 2)
            {
                _match.BuyRecon(brain.Country, target);
            }

            CountryId tapTarget = PickIntelTarget(brain, me, false);
            if (brain.Roll(100) < WiretapChance && _match.Config.Costs != null
                && (me.FreeWiretapsLeft > 0 || me.Money > _match.Config.Costs.Wiretap * 3))
            {
                _match.BuyWiretap(brain.Country, tapTarget);
            }
        }

        private CountryId PickIntelTarget(BotBrain brain, PlayerState me, bool recon)
        {
            IReadOnlyList<PlayerState> players = _match.AllPlayers;
            CountryId? best = null;
            int bestValue = -1;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState state = players[i];
                if (state == null || !state.IsAliveCountry || state.Country == brain.Country)
                {
                    continue;
                }

                bool known = recon ? me.ReconKnown.Contains(state.Country) : me.WiretapKnown.Contains(state.Country);
                if (known)
                {
                    continue;
                }

                int value = state.Money + state.AliveCityCount * EconomyConstants.CityBaseIncome;
                if (state.Definition != null && state.Definition.PassiveKind == CountryPassiveKind.AirCredit)
                {
                    value += EconomyConstants.GoldenCaliphateAirCreditTotal;
                }

                if (value > bestValue)
                {
                    bestValue = value;
                    best = state.Country;
                }
            }

            return best ?? me.Country;
        }

        private void MaybeIssueAirCredit(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || me.Definition == null
                || me.Definition.PassiveKind != CountryPassiveKind.AirCredit
                || me.AirCreditIssuedThisRound)
            {
                return;
            }

            if (me.Money <= EconomyConstants.GoldenCaliphateAirCreditTotal)
            {
                return;
            }

            List<CountryId> candidates = CollectPactCandidates(brain, GetPactLimit(brain));
            if (candidates.Count == 0)
            {
                return;
            }

            CountryId partner = candidates[0];
            if (_match.IssueAirCredit(brain.Country, partner))
            {
                _match.PlacePhoneCall(brain.Country, partner, CallKind.Flattery,
                    "Кредит из воздуха уже у вас. Не подведите.", 0);
            }
        }

        private void MaybePledge(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry)
            {
                return;
            }

            if (brain.SeeksCollapse || brain.Hoard > 0.8f)
            {
                // Таким вклады экологии только мешают.
                return;
            }

            if (brain.Roll(100) < PledgeChance)
            {
                _match.PledgeEcology(brain.Country);
            }
        }

        #endregion

        #region Саммит: санкции

        private void VoteSanctions(BotBrain brain)
        {
            PlayerState me = _match.GetPlayer(brain.Country);
            if (me == null || !me.IsAliveCountry || _match.Phase != GamePhase.SummitUN)
            {
                return;
            }

            if (brain.Roll(100) >= SanctionVoteChance)
            {
                return;
            }

            CountryId target = PickSanctionTarget(brain);
            if (target == brain.Country)
            {
                return;
            }

            _match.CastSanctionVote(brain.Country, target);
        }

        private CountryId PickSanctionTarget(BotBrain brain)
        {
            IReadOnlyList<PlayerState> players = _match.AllPlayers;
            PlayerState me = _match.GetPlayer(brain.Country);
            CountryId? best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState state = players[i];
                if (state == null || !state.IsAliveCountry || state.Country == brain.Country)
                {
                    continue;
                }

                int score;
                switch (brain.Doctrine)
                {
                    case DoctrineCheckKind.RicherThanEveryoneCombined:
                        // Паразиту нужна нищета соседей.
                        score = state.Money;
                        break;

                    case DoctrineCheckKind.GlobalCitiesDestroyedWithoutOwnLaunch:
                        score = state.Money / 2 + state.MissilesLaunchedTotal * 100;
                        break;

                    case DoctrineCheckKind.RichWithoutLosingCities:
                        score = state.MissilesLaunchedTotal * 200 + state.AliveCityCount;
                        break;

                    default:
                        score = state.MissilesLaunchedTotal * 150 + state.Money / 3 + GetTrust(brain, state.Country);
                        break;
                }

                if (me != null && me.HasPact(state.Country))
                {
                    score -= 500;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = state.Country;
                }
            }

            return best ?? brain.Country;
        }

        #endregion

        #region Формирование приказов

        private List<PlayerOrder> BuildOrderPlan(BotBrain brain, PlayerState me)
        {
            List<PlayerOrder> plan = new List<PlayerOrder>();
            ActionCosts costs = _match.Config.Costs;
            if (costs == null)
            {
                return plan;
            }

            int reserve = Mathf.RoundToInt(SoftReserve * brain.Hoard);

            // Планируем на виртуальном бюджете: настоящий бюджет трогает только MatchManager.
            int budget = me.Money;

            // Экология общая для всех держав: учитываем урон, уже запланированный ботами в этом раунде.

            // 1. Экология: честные платят, лжецы только обещают.
            if (ShouldDonateEcology(brain, me) && budget >= costs.EcologyDonation)
            {
                plan.Add(PlayerOrder.DonateEcology());
                budget -= costs.EcologyDonation;
            }

            // 2. Щиты ПВО: их любят все, кроме «Немого бункера», и особенно Культ Апокалипсиса.
            if (brain.Fortifies || brain.SeeksCollapse)
            {
                int targetShields = brain.SeeksCollapse ? EconomyConstants.ApocalypseCultAegisShields : me.AliveCityCount;
                for (int i = 0; i < me.Cities.Length && me.ShieldedCityCount + CountPlanned(plan, OrderType.BuyAegisShield) < targetShields; i++)
                {
                    CityState city = me.Cities[i];
                    if (city == null || !city.IsAlive || city.HasAAShield)
                    {
                        continue;
                    }

                    if (budget < costs.AegisShield + reserve)
                    {
                        break;
                    }

                    plan.Add(PlayerOrder.Shield(city.Index));
                    budget -= costs.AegisShield;
                }
            }

            // 3. Развитие городов ради дохода.
            if (ShouldDevelopCity(brain, me, reserve))
            {
                int cityIndex = FindWeakestCity(me);
                if (cityIndex >= 0 && budget >= costs.CityDevelopment + reserve)
                {
                    plan.Add(PlayerOrder.Develop(cityIndex));
                    budget -= costs.CityDevelopment;
                }
            }

            // 4. Ракеты: сборка и пуск по чужому городу.
            if (brain.AttackAppetite > 0f)
            {
                AppendMissilePlans(brain, me, plan, budget, reserve);
                _plannedEcologyThisRound += CountPlannedEcologyDamage(me, plan);
            }

            return plan;
        }

        /// <summary>Суммарный урон по экологии от приказов бота (сборка + пуск).</summary>
        private float CountPlannedEcologyDamage(PlayerState me, List<PlayerOrder> plan)
        {
            EcologyRules rules = _match.Config.Ecology;
            if (rules == null)
            {
                return 0f;
            }

            float assembleLoss = rules.LossOnAssemble;
            float launchLoss = rules.LossOnLaunch;
            if (me.Definition != null)
            {
                assembleLoss = me.Definition.ResolveEcologyLossOnAssemble(assembleLoss);
                launchLoss = me.Definition.ResolveEcologyLossOnLaunch(launchLoss);
            }

            float total = 0f;
            for (int i = 0; i < plan.Count; i++)
            {
                if (plan[i].Type == OrderType.AssembleMissile)
                {
                    total += assembleLoss;
                }
                else if (plan[i].Type == OrderType.LaunchMissile)
                {
                    total += launchLoss;
                }
            }

            return total;
        }

        private static int CountPlanned(List<PlayerOrder> plan, OrderType type)
        {
            int count = 0;
            for (int i = 0; i < plan.Count; i++)
            {
                if (plan[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private void AppendMissilePlans(BotBrain brain, PlayerState me, List<PlayerOrder> plan, int budget, int reserve)
        {
            ActionCosts costs = _match.Config.Costs;
            int missileCost = me.Definition != null
                ? me.Definition.ResolveMissileAssemblyCost()
                : costs.MissileAssembly;

            // Экология — оружие против всех сразу. Кроме Культа Апокалипсиса её берегут.
            if (!brain.SeeksCollapse && !CanAffordEcologyDamage(me, 0f))
            {
                return;
            }

            int stock = me.MissileStock;

            // Собираем, пока хватает денег и есть смысл (запас боеголовок небольшой).
            while (stock < MaxMissileStock && budget >= missileCost + reserve)
            {
                budget -= missileCost;
                plan.Add(PlayerOrder.Assemble());
                stock++;
            }

            if (stock <= 0 || brain.Roll(100) >= brain.AttackAppetite * 100f)
            {
                return;
            }

            while (stock > 0)
            {
                CountryId? target = PickLaunchTarget(brain, me);
                if (!target.HasValue)
                {
                    return;
                }

                CityState city = PickTargetCity(brain, _match.GetPlayer(target.Value));
                if (city == null)
                {
                    return;
                }

                plan.Add(PlayerOrder.Launch(target.Value, city.Index));
                stock--;
            }
        }

        private bool ShouldDonateEcology(BotBrain brain, PlayerState me)
        {
            ActionCosts costs = _match.Config.Costs;

            if (!brain.Honest || brain.SeeksCollapse || brain.Hoard > 0.7f)
            {
                return false;
            }

            if (brain.Doctrine == DoctrineCheckKind.RichWithoutLosingCities)
            {
                return false;
            }

            // Не жертвуем последним деньгам: приоритет — щиты и ракеты.
            return me.Money > costs.EcologyDonation * 2 && brain.Roll(100) < 35;
        }

        private bool ShouldDevelopCity(BotBrain brain, PlayerState me, int reserve)
        {
            ActionCosts costs = _match.Config.Costs;

            switch (brain.Doctrine)
            {
                case DoctrineCheckKind.SurviveToFinaleUnarmed:
                    // Держим города живыми и качаем доход, щиты не покупаем.
                    return me.AliveCityCount < SilentBunkerCityTarget ? me.Money >= costs.CityDevelopment * 2 : true;

                case DoctrineCheckKind.RicherThanEveryoneCombined:
                case DoctrineCheckKind.RichWithoutLosingCities:
                    // Страховому мошеннику деньги нужны сами: копим до порога и перестаём тратить.
                    return me.Money < InsuranceMoneyTarget && me.Money >= costs.CityDevelopment * 3;

                case DoctrineCheckKind.GlobalCitiesDestroyedWithoutOwnLaunch:
                    return me.Money >= costs.CityDevelopment * 4;

                default:
                    return me.Money >= costs.CityDevelopment + reserve * 2;
            }
        }

        private static int FindWeakestCity(PlayerState me)
        {
            int bestIndex = -1;
            int bestLevel = int.MaxValue;

            for (int i = 0; i < me.Cities.Length; i++)
            {
                CityState city = me.Cities[i];
                if (city == null || !city.IsAlive)
                {
                    continue;
                }

                if (city.DevelopmentLevel < bestLevel)
                {
                    bestLevel = city.DevelopmentLevel;
                    bestIndex = city.Index;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Город цели. По умолчанию — самый незащищённый, но иногда диктатор целится наугад:
        /// разведка врёт, и в реальной войне щит ПВО тоже должен что-то ловить.
        /// </summary>
        private CityState PickTargetCity(BotBrain brain, PlayerState victim)
        {
            if (victim == null)
            {
                return null;
            }

            if (brain.Roll(100) < BlindShotChance)
            {
                List<CityState> alive = new List<CityState>();

                for (int i = 0; i < victim.Cities.Length; i++)
                {
                    if (victim.Cities[i] != null && victim.Cities[i].IsAlive)
                    {
                        alive.Add(victim.Cities[i]);
                    }
                }

                return alive.Count > 0 ? alive[brain.Rng.Next(0, alive.Count)] : null;
            }

            int softIndex = victim.GetFirstUnshieldedAliveCityIndex();
            if (softIndex >= 0)
            {
                return victim.GetCity(softIndex);
            }

            for (int i = 0; i < victim.Cities.Length; i++)
            {
                if (victim.Cities[i] != null && victim.Cities[i].IsAlive)
                {
                    return victim.Cities[i];
                }
            }

            return null;
        }

        /// <summary>Выбор цели пуска: месть, финальное предательство или самый богатый.</summary>
        private CountryId? PickLaunchTarget(BotBrain brain, PlayerState me)
        {
            IReadOnlyList<PlayerState> players = _match.AllPlayers;
            bool finale = _match.Round >= _match.Config.TotalRounds;

            // Клятвопреступник в финале режет ровно одного партнёра по пакту.
            if (brain.Oathbreaker && finale)
            {
                CountryId? weakestPartner = null;
                int weakestScore = int.MaxValue;

                for (int i = 0; i < players.Count; i++)
                {
                    PlayerState state = players[i];
                    if (state == null || !state.IsAliveCountry || !me.HasPact(state.Country))
                    {
                        continue;
                    }

                    int score = state.Money + state.AliveCityCount * EconomyConstants.CityBaseIncome + state.ShieldedCityCount;
                    if (score < weakestScore)
                    {
                        weakestScore = score;
                        weakestPartner = state.Country;
                    }
                }

                if (weakestPartner.HasValue)
                {
                    return weakestPartner;
                }
            }

            PlayerState revenge = _match.GetPlayer(brain.RevengeTarget);
            if (revenge != null && revenge.IsAliveCountry && revenge.Country != me.Country)
            {
                return revenge.Country;
            }

            CountryId? best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState state = players[i];
                if (state == null || !state.IsAliveCountry || state.Country == me.Country)
                {
                    continue;
                }

                if (me.HasPact(state.Country) && !brain.Oathbreaker)
                {
                    continue;
                }

                int score = state.Money + state.AliveCityCount * EconomyConstants.CityBaseIncome
                            - state.ShieldedCityCount * EconomyConstants.AegisShieldCost;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = state.Country;
                }
            }

            return best;
        }

        #endregion

        #region Реплики диктаторов

        private static readonly string[] BlusterLines =
        {
            "У нас полная готовность. Проверьте, если хватит духу.",
            "Ваш бюджет мы уже посчитали. Не интересно? Тем более.",
            "Бункер крепкий, кофе горячий, нервы — нет.",
            "Предлагаю обсудить ваши планы. И пересмотреть их.",
            "Дипломатия — это просто стрельба с вежливостью."
        };

        private static readonly string[] PactLines =
        {
            "Пакт — это бумага, но бумага с печатью.",
            "Подпишем, если не стреляете первым.",
            "Никаких ракет. До конца матча. Верите?",
            "Один человек не должен воевать в одиночку.",
            "Давайте договоримся, как взрослые."
        };

        private static readonly string[] ThreatLines =
        {
            "Вы нарушили пакт. Мы это записали.",
            "Следующий звонок будет последним, предупреждаю.",
            "Ваш последний город мы уже нарисовали на карте.",
            "Берегитесь, у нас смена караула через минуту.",
            "Одумайтесь. Документы не сгорают."
        };

        private static readonly string[] FlatteryLines =
        {
            "Вы лучший диктатор континента, не сомневайтесь.",
            "Ваш город краше всех, я видел лично.",
            "Нам повезло, что вы на нашей стороне.",
            "С таким президентом не страшно даже экологии.",
            "Поклон за ваш ум и дальновидность."
        };

        private static readonly string[] WarningLines =
        {
            "У нас плохие новости, держитесь.",
            "Экология падает. Считайте сами.",
            "Скоро будет тихо. Слишком тихо.",
            "Проверьте свои щиты, пока не поздно.",
            "Слухи о чужих ракетах. Просто слухи."
        };

        #endregion

        private static BotBrain FindBrain(List<BotBrain> brains, CountryId country)
        {
            for (int i = 0; i < brains.Count; i++)
            {
                if (brains[i].Country == country)
                {
                    return brains[i];
                }
            }

            return null;
        }

        /// <summary>Ставит отложенное действие бота: дрожит телефон, потом подпись, потом голос.</summary>
        private void Schedule(BotBrain brain, float dueTime, Action action)
        {
            Schedule(brain, dueTime, action, false);
        }

        /// <summary>Отложенная отметка «фаза сделана». Игнорируется при досрочном переходе.</summary>
        private void ScheduleReady(BotBrain brain, float dueTime)
        {
            Schedule(brain, dueTime, () => _match.EndPhaseEarly(brain.Country), true);
        }

        private void Schedule(BotBrain brain, float dueTime, Action action, bool isReadyMark)
        {
            ScheduledAction item = new ScheduledAction();
            item.DueTime = dueTime;
            item.Run = action;
            item.IsReadyMark = isReadyMark;
            brain.Schedule.Add(item);
        }
    }
}