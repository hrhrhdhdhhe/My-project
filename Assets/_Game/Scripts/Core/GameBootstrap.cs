// World Crisis | Слой: Core | Ответственный: OpenCode
// Автозапуск игры по F5/Play даже в пустой сцене + отладочный HUD (F1 — скрыть/показать).
// HUD только отображает состояние MatchManager и вызывает его методы, своей логики не содержит.

using System.Collections.Generic;
using UnityEngine;

namespace WorldCrisis
{
    [DisallowMultipleComponent]
    public class GameBootstrap : MonoBehaviour
    {
        // HUD намеренно узкий и прижат к левому верхнему углу: широкая панель
        // закрывала приборы на столе, ради которых всё и затевалось.
        private const float PanelWidth = 340f;
        private const float LogWidth = 340f;
        private const float HeaderHeight = 40f;
        private const float Margin = 6f;
        private const float ScrollHeight = 150f;

        private static GameBootstrap s_Instance;

        private MatchManager _match;
        private bool _hudVisible = true;
        private int _botCount = 5;
        private CountryId _selectedCountry = CountryId.NeoSilicon;
        private int _selectedLaunchCity;
        private int _transferAmount = EconomyConstants.DefaultTransferAmount;
        private Vector2 _logScroll;
        private Vector2 _orderScroll;

        private GUIStyle _headerStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _goodStyle;
        private GUIStyle _badStyle;
        private GUIStyle _secretStyle;
        private GUIStyle _smallStyle;

        /// <summary>
        /// Точка входа: создаёт сервисы ядра после загрузки сцены.
        /// Если в сцене уже есть MatchManager — просто дооснащает его ботами и HUD.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapGame()
        {
            if (s_Instance != null)
            {
                return;
            }

            if (MatchManager.Instance != null)
            {
                MatchManager existing = MatchManager.Instance;
                EnsureComponent<AIDictatorController>(existing.gameObject);
                EnsureComponent<GameBootstrap>(existing.gameObject);
                EnsureWorld(existing.gameObject);
                return;
            }

            GameObject host = new GameObject("[WorldCrisis.Core]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<MatchManager>();
            host.AddComponent<AIDictatorController>();
            host.AddComponent<GameBootstrap>();

            // Сначала камера: RetroRenderPipelineController ищет Camera.main в Awake,
            // поэтому добавлять его раньше бессмысленно — ретро-вывод просто выключится.
            EnsureSceneCamera();
            EnsureRetroPipeline(host);
            EnsureWorld(host);
        }

        /// <summary>
        /// Поднимает ретро-пайплайн (Art Bible, раздел 6), если он ещё не настроен в сцене.
        /// Отдельный компонент в сцене всегда побеждает: он создавался под ручной контроль.
        /// FindAnyObjectByType, а не FindFirstObjectByType: последний помечен устаревшим
        /// в Unity 6 (warning CS0618) из-за зависимости от порядка InstanceID.
        /// </summary>
        private static void EnsureRetroPipeline(GameObject host)
        {
            if (FindAnyObjectByType<RetroRenderPipelineController>() != null)
            {
                return;
            }

            host.AddComponent<RetroRenderPipelineController>();
        }

        /// <summary>
        /// Собирает обе локации и ставит режиссёра фазового света. Порядок важен:
        /// сборщик создаёт геометрию в Awake, а директор в Awake ищет камеру и узлы мира,
        /// поэтому он должен появиться после сборщика.
        /// </summary>
        private static void EnsureWorld(GameObject host)
        {
            if (FindAnyObjectByType<BunkerWorldBuilder>() != null)
            {
                return;
            }

            host.AddComponent<BunkerWorldBuilder>();
            host.AddComponent<PhaseLightingDirector>();
        }

        private static void EnsureComponent<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() == null)
            {
                target.AddComponent<T>();
            }
        }

        /// <summary>Пустая сцена без камеры — чёрный экран. Создаём камеру и свет для стенда.</summary>
        private static void EnsureSceneCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("DebugCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 6f, -12f);
            cameraObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            GameObject lightObject = new GameObject("DebugLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this);
                return;
            }

            s_Instance = this;
            _match = MatchManager.Instance;

            if (_match == null)
            {
                Debug.LogWarning("[WorldCrisis] GameBootstrap: MatchManager не найден, HUD отключён.");
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        #region OnGUI

        private void OnGUI()
        {
            if (_match == null)
            {
                return;
            }

            HandleHotkeys();

            if (!_hudVisible)
            {
                return;
            }

            EnsureStyles();
            DrawHeader();

            GUILayout.BeginArea(new Rect(Margin, HeaderHeight + Margin, LogWidth,
                Screen.height - HeaderHeight - Margin * 2f), GUI.skin.box);
            GUILayout.Label("Хроника матча", _headerStyle);
            DrawJournal();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(Margin + LogWidth + Margin, HeaderHeight + Margin, PanelWidth, Screen.height - HeaderHeight - Margin * 2f), GUI.skin.box);
            DrawControls();
            GUILayout.EndArea();
        }

        private void HandleHotkeys()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown)
            {
                return;
            }

            switch (current.keyCode)
            {
                case KeyCode.F1:
                case KeyCode.F3:
                    _hudVisible = !_hudVisible;
                    current.Use();
                    break;

                case KeyCode.F2:
                    _match.ForceAdvancePhase();
                    current.Use();
                    break;
            }
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.fontSize = 14;
            _headerStyle.normal.textColor = Color.white;

            _infoStyle = new GUIStyle(GUI.skin.label);
            _infoStyle.wordWrap = false;

            _goodStyle = new GUIStyle(_infoStyle);
            _goodStyle.normal.textColor = new Color(0.45f, 0.95f, 0.5f);

            _badStyle = new GUIStyle(_infoStyle);
            _badStyle.normal.textColor = new Color(1f, 0.45f, 0.4f);

            _secretStyle = new GUIStyle(_infoStyle);
            _secretStyle.normal.textColor = new Color(1f, 0.55f, 0.95f);
            _secretStyle.fontStyle = FontStyle.Bold;

            _smallStyle = new GUIStyle(_infoStyle);
            _smallStyle.fontSize = 11;
        }

        private void DrawHeader()
        {
            GUILayout.BeginArea(new Rect(Margin, Margin, Screen.width - Margin * 2f, HeaderHeight - Margin), GUI.skin.box);
            GUILayout.Label("WORLD CRISIS — отладочный HUD (F1 — скрыть, F2 — пропустить фазу)", _headerStyle);

            string phaseName = GetPhaseName(_match.Phase);
            string text = "Фаза: " + phaseName
                          + "   Раунд: " + Mathf.Max(0, _match.Round) + "/" + Mathf.Max(1, _match.Config.TotalRounds)
                          + "   Время: " + Mathf.CeilToInt(_match.PhaseTimeLeft) + " с"
                          + "   Экология: " + Mathf.RoundToInt(_match.WorldEcology) + "%"
                          + "   Сеть: " + _match.GetNetworkStatus();

            GUILayout.Label(text, _infoStyle);
            GUILayout.EndArea();
        }

        private void DrawJournal()
        {
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(ScrollHeight));
            IReadOnlyList<MatchLogEntry> entries = _match.GetJournalTail(40);

            for (int i = 0; i < entries.Count; i++)
            {
                MatchLogEntry entry = entries[i];
                GUILayout.Label("[" + entry.Round + "] " + entry.Text, GetToneStyle(entry.Tone));
            }

            GUILayout.EndScrollView();
        }

        private void DrawControls()
        {
            switch (_match.Phase)
            {
                case GamePhase.Lobby:
                    DrawLobby();
                    break;
                case GamePhase.BunkerDiplomacy:
                    DrawDiplomacy();
                    break;
                case GamePhase.SummitUN:
                    DrawSummit();
                    break;
                case GamePhase.OrderInput:
                    DrawOrders();
                    break;
                case GamePhase.Reckoning:
                    DrawReckoning();
                    break;
                case GamePhase.GameOver:
                    DrawGameOver();
                    break;
            }

            DrawPlayerStatus();
            DrawFooter();
        }

        private void DrawLobby()
        {
            GUILayout.Label("Лобби: выберите державу и количество ИИ-диктаторов", _headerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Играем за:", _infoStyle);
            if (GUILayout.Button("◀", GUILayout.Width(40f)))
            {
                CycleLocalCountry(-1);
            }

            GUILayout.Label(_match.GetShortName(_match.LocalPlayer), _infoStyle);
            if (GUILayout.Button("▶", GUILayout.Width(40f)))
            {
                CycleLocalCountry(1);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("ИИ-диктаторов: " + _botCount, _infoStyle);
            if (GUILayout.Button("−", GUILayout.Width(40f)))
            {
                _botCount = Mathf.Max(0, _botCount - 1);
            }

            if (GUILayout.Button("+", GUILayout.Width(40f)))
            {
                _botCount = Mathf.Min(5, _botCount + 1);
            }

            GUILayout.EndHorizontal();

            if (GUILayout.Button("СТАРТ ПАРТИИ"))
            {
                _match.StartSoloMatch(_match.LocalPlayer, _botCount);
            }

            GUILayout.Space(6f);
            GUILayout.Label("Сеть: хост слушает порт, клиент подключается к нему.", _smallStyle);

            if (!_match.IsOnlineSession() && GUILayout.Button("Запустить хост матча (" + EconomyConstants.DefaultServerPort + ")"))
            {
                _match.StartHosting(EconomyConstants.DefaultServerPort);
            }

            if (!_match.IsOnlineSession() && GUILayout.Button("Подключиться к 127.0.0.1:" + EconomyConstants.DefaultServerPort))
            {
                _match.JoinServer("127.0.0.1", EconomyConstants.DefaultServerPort);
            }

            if (_match.IsOnlineSession() && GUILayout.Button("Вернуться к локальному стенду"))
            {
                _match.ReturnToSolo();
            }

            GUILayout.Label("Статус сети: " + _match.GetNetworkStatus(), _smallStyle);
        }

        private void DrawDiplomacy()
        {
            GUILayout.Label("Бункер-дипломатия: звонки, переводы, кредиты, разведка", _headerStyle);
            DrawTargetSelector();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Перевод " + _transferAmount + "$"))
            {
                _match.TransferMoney(_match.LocalPlayer, _selectedCountry, _transferAmount);
            }

            if (GUILayout.Button("сумма −"))
            {
                _transferAmount = Mathf.Max(EconomyConstants.DefaultTransferAmount,
                    _transferAmount - EconomyConstants.DefaultTransferAmount);
            }

            if (GUILayout.Button("+"))
            {
                _transferAmount += EconomyConstants.DefaultTransferAmount;
            }

            if (GUILayout.Button("Разведка"))
            {
                _match.BuyRecon(_match.LocalPlayer, _selectedCountry);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Прослушка"))
            {
                _match.BuyWiretap(_match.LocalPlayer, _selectedCountry);
            }

            if (GUILayout.Button("Позвонить"))
            {
                _match.PlacePhoneCall(_match.LocalPlayer, _selectedCountry, CallKind.Bluster,
                    "Давайте решать вопросы по-хорошему.", 0);
            }

            GUILayout.EndHorizontal();

            PlayerState diplomacyMe = _match.GetLocalPlayerState();
            if (diplomacyMe != null)
            {
                if (diplomacyMe.Definition != null
                    && diplomacyMe.Definition.PassiveKind == CountryPassiveKind.AirCredit)
                {
                    if (GUILayout.Button("Кредит из воздуха → " + _match.GetShortName(_selectedCountry)))
                    {
                        _match.IssueAirCredit(_match.LocalPlayer, _selectedCountry);
                    }
                }

                if (!diplomacyMe.PledgedEcologyContribution && GUILayout.Button("Пообещать вклад в экологию"))
                {
                    _match.PledgeEcology(_match.LocalPlayer);
                }
            }

            DrawPendingOffers();
        }

        private void DrawSummit()
        {
            GUILayout.Label("Саммит ООН: санкции и подписание пактов", _headerStyle);
            DrawTargetSelector();

            if (GUILayout.Button("Голосовать за санкции: " + _match.GetShortName(_selectedCountry)))
            {
                _match.CastSanctionVote(_match.LocalPlayer, _selectedCountry);
            }

            if (GUILayout.Button("Подписать пакт: " + _match.GetShortName(_selectedCountry)))
            {
                _match.SignPact(_match.LocalPlayer, _selectedCountry);
            }

            PlayerState me = _match.GetLocalPlayerState();
            if (me != null && !me.PledgedEcologyContribution && GUILayout.Button("Пообещать вклад в экологию"))
            {
                _match.PledgeEcology(_match.LocalPlayer);
            }

            GUILayout.Space(4f);
            IReadOnlyList<SanctionVote> votes = _match.GetSanctionVotes();
            if (votes.Count == 0)
            {
                GUILayout.Label("Голосов пока нет.", _smallStyle);
            }
            else
            {
                for (int i = 0; i < votes.Count; i++)
                {
                    SanctionVote vote = votes[i];
                    GUILayout.Label(_match.GetShortName(vote.Voter) + " → санкции против "
                                    + _match.GetShortName(vote.Target), _smallStyle);
                }
            }
        }

        private void DrawOrders()
        {
            PlayerState me = _match.GetLocalPlayerState();
            if (me == null)
            {
                return;
            }

            GUILayout.Label("Терминал: скрытый ввод приказов", _headerStyle);

            for (int i = 0; i < me.Cities.Length; i++)
            {
                CityState city = me.Cities[i];
                if (city == null)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                string cityLabel = "Город " + (city.Index + 1)
                                   + (city.IsDestroyed ? " — пепел" : " ур." + city.DevelopmentLevel)
                                   + (city.HasAAShield ? " [ЩИТ]" : string.Empty);
                GUILayout.Label(cityLabel, city.IsDestroyed ? _badStyle : _infoStyle, GUILayout.Width(190f));

                using (new GuiDisabledScope(city.IsDestroyed || !CanAfford(me, PlayerOrder.Develop(city.Index))))
                {
                    if (GUILayout.Button("Развить", GUILayout.Width(80f)))
                    {
                        _match.SubmitOrder(_match.LocalPlayer, PlayerOrder.Develop(city.Index));
                    }
                }

                using (new GuiDisabledScope(city.IsDestroyed || city.HasAAShield
                                            || !CanAfford(me, PlayerOrder.Shield(city.Index))))
                {
                    if (GUILayout.Button("Щит", GUILayout.Width(60f)))
                    {
                        _match.SubmitOrder(_match.LocalPlayer, PlayerOrder.Shield(city.Index));
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            using (new GuiDisabledScope(!CanAfford(me, PlayerOrder.Assemble())))
            {
                if (GUILayout.Button("Собрать ракету"))
                {
                    _match.SubmitOrder(_match.LocalPlayer, PlayerOrder.Assemble());
                }
            }

            using (new GuiDisabledScope(!CanAfford(me, PlayerOrder.DonateEcology())))
            {
                if (GUILayout.Button("Вклад в экологию"))
                {
                    _match.SubmitOrder(_match.LocalPlayer, PlayerOrder.DonateEcology());
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            DrawTargetSelector();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Город цели:", _infoStyle);
            if (GUILayout.Button("◀", GUILayout.Width(40f)))
            {
                _selectedLaunchCity = Mathf.Clamp(_selectedLaunchCity - 1, 0, 3);
            }

            GUILayout.Label((_selectedLaunchCity + 1).ToString(), _infoStyle, GUILayout.Width(30f));
            if (GUILayout.Button("▶", GUILayout.Width(40f)))
            {
                _selectedLaunchCity = Mathf.Clamp(_selectedLaunchCity + 1, 0, 3);
            }

            PlayerOrder launch = PlayerOrder.Launch(_selectedCountry, _selectedLaunchCity);
            using (new GuiDisabledScope(me.MissileStock <= 0 || !CanAfford(me, launch)))
            {
                if (GUILayout.Button("ПУСК (склад " + me.MissileStock + ")"))
                {
                    _match.SubmitOrder(_match.LocalPlayer, launch);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("Очередь приказов:", _smallStyle);
            _orderScroll = GUILayout.BeginScrollView(_orderScroll, GUILayout.Height(120f));

            if (me.TurnOrderQueue.Count == 0)
            {
                GUILayout.Label("Пусто.", _smallStyle);
            }

            for (int i = 0; i < me.TurnOrderQueue.Count; i++)
            {
                PlayerOrder order = me.TurnOrderQueue[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(order.Describe(_match.GetShortName), _infoStyle, GUILayout.Width(330f));

                if (GUILayout.Button("×", GUILayout.Width(30f)))
                {
                    _match.CancelOrder(_match.LocalPlayer, order.OrderId);
                    break;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            if (GUILayout.Button("Очистить приказы"))
            {
                _match.ClearOrders(_match.LocalPlayer);
            }
        }

        private void DrawReckoning()
        {
            GUILayout.Label("РАСЧЁТ: экология → приказы → предательства → пуски", _headerStyle);

            IReadOnlyList<EcologyContributionRecord> records = _match.LastContributions;
            if (records.Count > 0)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    EcologyContributionRecord record = records[i];
                    GUILayout.Label(_match.GetShortName(record.Country)
                                    + (record.Donated ? " — внёс вклад" : " — без вклада")
                                    + (record.BrokePromise ? " ЛЖЕЦ" : string.Empty),
                        record.BrokePromise ? _badStyle : _infoStyle);
                }
            }
            else
            {
                GUILayout.Label("Вклады ещё не раскрыты.", _smallStyle);
            }

            GUILayout.Space(4f);
            GUILayout.Label("Уничтожено городов в мире: " + _match.GlobalCitiesDestroyed, _infoStyle);
            GUILayout.Label("Следите за журналом слева: пуски, щиты и шредер пактов.", _smallStyle);
        }

        private void DrawGameOver()
        {
            GUILayout.Label("МАТЧ ОКОНЧЕН", _headerStyle);
            string winner = _match.Winner.HasValue
                ? "Победитель: " + _match.GetShortName(_match.Winner.Value)
                : "Победителя нет";
            GUILayout.Label(winner, _goodStyle);
            GUILayout.Label(_match.WinReason, _infoStyle);
            GUILayout.Space(4f);

            IReadOnlyList<MatchStanding> standings = _match.Standings;
            for (int i = 0; i < standings.Count; i++)
            {
                MatchStanding row = standings[i];
                GUILayout.Label("1. " + row.Country + " — " + row.Money + "$, городов " + row.AliveCities
                                + ", очков " + row.Score, _infoStyle);
            }

            if (GUILayout.Button("Новая партия"))
            {
                _match.EnterLobby();
            }
        }

        private void DrawPlayerStatus()
        {
            PlayerState me = _match.GetLocalPlayerState();
            if (me == null)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("—" + _match.GetShortName(me.Country) + "—", _headerStyle);
            GUILayout.Label("Бюджет: " + _match.FormatMoney(me.Money)
                            + "   Доход за раунд: ~" + me.GetRoundIncome() + "$"
                            + "   Ракет на складе: " + me.MissileStock
                            + "   Пактов: " + me.ActivePacts.Count
                            + (me.IsDefeated ? "   [УНИЧТОЖЕНА]" : string.Empty),
                me.IsDefeated ? _badStyle : _infoStyle);

            if (me.IsTraitorsMark)
            {
                GUILayout.Label("Метка предателя: пакт был нарушен.", _badStyle);
            }

            if (me.SanctionedNextRound)
            {
                GUILayout.Label("Под санкциями: доход срезан на " + EconomyConstants.SanctionIncomePenaltyPercent + "%.",
                    _badStyle);
            }

            SecretDoctrine doctrine = _match.GetDoctrine(me.Country);
            if (doctrine != null)
            {
                GUILayout.Label("Доктрина: " + doctrine.DisplayName + " — " + doctrine.ConditionText, _secretStyle);
                GUILayout.Label("Прогресс: " + SecretDoctrineEvaluator.DescribeProgress(doctrine, _match.BuildStats(me.Country)),
                    _secretStyle);
            }

            DrawKnownIntel();
        }

        private void DrawKnownIntel()
        {
            PlayerState me = _match.GetLocalPlayerState();
            if (me == null)
            {
                return;
            }

            GUILayout.Label("Разведка и прослушки:", _smallStyle);
            IReadOnlyList<PlayerState> players = _match.AllPlayers;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState other = players[i];
                if (other == null || other.Country == me.Country || !other.IsAliveCountry)
                {
                    continue;
                }

                string line = "· " + _match.GetShortName(other.Country)
                              + (me.ReconKnown.Contains(other.Country)
                                  ? ": " + other.Money + "$, городов " + other.AliveCityCount
                                  : ": данные скрыты");

                SecretDoctrine known = me.WiretapKnown.Contains(other.Country)
                    ? _match.GetDoctrine(other.Country)
                    : null;

                if (known != null)
                {
                    line += ", доктрина «" + known.DisplayName + "»";
                }

                GUILayout.Label(line, _smallStyle);
            }
        }

        private void DrawFooter()
        {
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();

            if (MatchManager.IsTimedPhase(_match.Phase) && GUILayout.Button("Готов / пропустить фазу (F2)"))
            {
                if (_match.Phase == GamePhase.Reckoning)
                {
                    _match.ForceAdvancePhase();
                }
                else
                {
                    _match.EndPhaseEarly(_match.LocalPlayer);
                }
            }

            GUILayout.Label("Скорость ×" + Mathf.RoundToInt(_match.TimeScale), _smallStyle);
            if (GUILayout.Button("+", GUILayout.Width(30f)))
            {
                _match.TimeScale = Mathf.Min(50f, _match.TimeScale * 2f);
            }

            if (GUILayout.Button("−", GUILayout.Width(30f)))
            {
                _match.TimeScale = Mathf.Max(1f, _match.TimeScale * 0.5f);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawPendingOffers()
        {
            PlayerState me = _match.GetLocalPlayerState();
            if (me == null || me.PendingPactOffers.Count == 0)
            {
                return;
            }

            GUILayout.Space(4f);
            GUILayout.Label("Предложения пактов:", _smallStyle);

            for (int i = 0; i < me.PendingPactOffers.Count; i++)
            {
                PactOffer offer = me.PendingPactOffers[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(_match.GetShortName(offer.From) + (offer.Accepted ? " [принято]" : string.Empty),
                    _infoStyle, GUILayout.Width(240f));

                if (!offer.Accepted && GUILayout.Button("Да", GUILayout.Width(50f)))
                {
                    _match.RespondToPactOffer(_match.LocalPlayer, i, true);
                }

                if (!offer.Accepted && GUILayout.Button("Нет", GUILayout.Width(50f)))
                {
                    _match.RespondToPactOffer(_match.LocalPlayer, i, false);
                }

                GUILayout.EndHorizontal();
            }
        }

        private void DrawTargetSelector()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Цель:", _infoStyle);
            if (GUILayout.Button("◀", GUILayout.Width(40f)))
            {
                CycleTarget(-1);
            }

            GUILayout.Label(_match.GetShortName(_selectedCountry), _infoStyle, GUILayout.Width(180f));
            if (GUILayout.Button("▶", GUILayout.Width(40f)))
            {
                CycleTarget(1);
            }

            GUILayout.EndHorizontal();
        }

        private bool CanAfford(PlayerState me, PlayerOrder order)
        {
            return me != null && _match.GetOrderCost(me.Country, order) <= me.Money;
        }

        private void CycleLocalCountry(int direction)
        {
            IReadOnlyList<CountryId> ids = GameDefinitions.AllCountryIds;
            int index = (int)_match.LocalPlayer;

            for (int step = 0; step < ids.Count; step++)
            {
                index = (index + direction + ids.Count) % ids.Count;

                PlayerState state = _match.GetPlayer(ids[index]);
                if (state != null && state.IsAliveCountry)
                {
                    _selectedCountry = ids[index];
                    _match.SetLocalPlayer(ids[index]);
                    return;
                }
            }
        }

        private void CycleTarget(int direction)
        {
            IReadOnlyList<CountryId> ids = GameDefinitions.AllCountryIds;
            int index = (int)_selectedCountry;

            for (int step = 0; step < ids.Count; step++)
            {
                index = (index + direction + ids.Count) % ids.Count;

                PlayerState state = _match.GetPlayer(ids[index]);
                if (state != null && state.IsAliveCountry && ids[index] != _match.LocalPlayer)
                {
                    _selectedCountry = ids[index];
                    return;
                }
            }
        }

        private static string GetPhaseName(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Lobby:
                    return "Лобби";
                case GamePhase.BunkerDiplomacy:
                    return "Переговоры в бункере";
                case GamePhase.SummitUN:
                    return "Саммит ООН";
                case GamePhase.OrderInput:
                    return "Ввод приказов";
                case GamePhase.Reckoning:
                    return "Расчёт";
                case GamePhase.GameOver:
                    return "Игра окончена";
                default:
                    return phase.ToString();
            }
        }

        private GUIStyle GetToneStyle(JournalTone tone)
        {
            switch (tone)
            {
                case JournalTone.Good:
                    return _goodStyle;
                case JournalTone.Bad:
                    return _badStyle;
                case JournalTone.Secret:
                    return _secretStyle;
                case JournalTone.System:
                    return _headerStyle;
                default:
                    return _infoStyle;
            }
        }

        /// <summary>Обёртка для GUI.enabled: кнопка серая, когда действие недоступно.</summary>
        private sealed class GuiDisabledScope : System.IDisposable
        {
            public GuiDisabledScope(bool disabled)
            {
                GUI.enabled = !disabled;
            }

            public void Dispose()
            {
                GUI.enabled = true;
            }
        }

        #endregion
    }
}