// World Crisis | Слой: Data | Ответственный: OpenCode
// Слой данных: перечисления, константы баланса, описания 6 держав и 8 секретных доктрин,
// конфиг матча и статический генератор дефолтного конфига (игра запускается без ручной настройки инспектора).

using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
#endif

namespace WorldCrisis
{
    #region Перечисления

    /// <summary>
    /// Игровые фазы матча. Единственный источник истины по фазе — Core/PhaseController.
    /// </summary>
    public enum GamePhase
    {
        Lobby = 0,
        BunkerDiplomacy = 1,
        SummitUN = 2,
        OrderInput = 3,
        Reckoning = 4,
        GameOver = 5
    }

    /// <summary>
    /// Идентификаторы держав. Значения зафиксированы и используются как индексы в данных и сети.
    /// </summary>
    public enum CountryId
    {
        NeoSilicon = 0,
        GrandCosta = 1,
        PermafrostOrder = 2,
        GoldenCaliphate = 3,
        RustGearsUnion = 4,
        FreeResortsAtoll = 5
    }

    /// <summary>
    /// Тип пассивной способности державы. Позволяет UI и Core проверять эффект без switch по CountryId.
    /// </summary>
    public enum CountryPassiveKind
    {
        None = 0,
        FreeWiretapPerMatch = 1,
        TransferTax = 2,
        DiscountedMissiles = 3,
        AirCredit = 4,
        FreeAegisStart = 5,
        LastCitySurvivor = 6
    }

    /// <summary>
    /// Тип секретной доктрины. Помимо 8 доктрин есть служебное значение None.
    /// </summary>
    public enum SecretDoctrineType
    {
        None = 0,
        DeathTrader = 1,
        Parasite = 2,
        ApocalypseCult = 3,
        Oathbreaker = 4,
        PaperTiger = 5,
        PactSpider = 6,
        InsuranceFraud = 7,
        SilentBunker = 8
    }

    /// <summary>
    /// Машино-читаемый тип проверки условия доктрины (для автоматической проверки в Core/UI).
    /// </summary>
    public enum DoctrineCheckKind
    {
        None = 0,
        GlobalCitiesDestroyedWithoutOwnLaunch = 1,
        RicherThanEveryoneCombined = 2,
        ZeroEcologyWithAegisShields = 3,
        PactWithAllThenBetrayOne = 4,
        WinStreakWithoutLaunch = 5,
        KeepManyPactsToFinale = 6,
        RichWithoutLosingCities = 7,
        SurviveToFinaleUnarmed = 8
    }

    #endregion

    #region Внешний облик диктатора

    /// <summary>
    /// Тип головы-маски диктатора. Задаётся в данных, чтобы графический слой не ветвился
    /// по CountryId: новая держава с уже существующей головой — это правка только данных.
    /// </summary>
    public enum DictatorHeadStyle
    {
        /// <summary>ЭЛТ-монитор вместо головы (Синдикат «Нео-Кремний»).</summary>
        ScreenTv = 0,

        /// <summary>Гротескный генерал в высокой фуражке (Империя Гранд-Коста).</summary>
        GrotesqueGeneral = 1,

        /// <summary>Чугунный респиратор под меховой ушанкой (Орден Вечной Мерзлоты).</summary>
        FrostRespirator = 2,

        /// <summary>Чеканная золотая маска с золотой бородой (Золотой Халифат Дюн).</summary>
        GoldenMask = 3,

        /// <summary>Ржавый противогаз с манометром во лбу (Союз Ржавых Шестеренок).</summary>
        RustedGasMask = 4,

        /// <summary>Глянцевый пластиковый манекен курорта (Атолл Свободных Курортов).</summary>
        ResortDoll = 5
    }

    /// <summary>Тип мундира/костюма диктатора. Определяет набор деталей торса.</summary>
    public enum DictatorUniformKind
    {
        /// <summary>Пиджак поверх чёрной водолазки.</summary>
        TurtleneckSuit = 0,

        /// <summary>Строгий парадный мундир с орденами и погонами.</summary>
        CeremonialUniform = 1,

        /// <summary>Тяжёлая меховая парка с наростами инея.</summary>
        HeavyParka = 2,

        /// <summary>Чёрно-золотое одеяние под золотой маской.</summary>
        BlackGoldRobe = 3,

        /// <summary>Потёртый плащ с высоким воротником.</summary>
        RustCloak = 4,

        /// <summary>Гавайская рубашка под тактическим бронежилетом.</summary>
        HawaiianVest = 5
    }

    /// <summary>
    /// Описание внешности диктатора для процедурной генерации 3D-модели.
    /// Чисто данные: фабрика мешей читает их и не знает ни про державы, ни про баланс.
    /// </summary>
    [Serializable]
    public class DictatorVisualLook
    {
        /// <summary>Конструкция головы-маски.</summary>
        public DictatorHeadStyle HeadStyle = DictatorHeadStyle.ScreenTv;

        /// <summary>Крой мундира/костюма.</summary>
        public DictatorUniformKind UniformKind = DictatorUniformKind.TurtleneckSuit;

        /// <summary>Основная ткань: сукно мундира, ткань костюма, плащ.</summary>
        public Color ClothColor = new Color(0.16f, 0.17f, 0.19f);

        /// <summary>Тёмная вторая ткань: подкладка, брюки, водолазка.</summary>
        public Color ClothShadeColor = new Color(0.07f, 0.07f, 0.08f);

        /// <summary>Акцентная окантовка: погоны, канты, шнуровка.</summary>
        public Color TrimColor = new Color(0.55f, 0.42f, 0.12f);

        /// <summary>Металл посадки: золото, латунь, серебро, чугун.</summary>
        public Color MetalColor = new Color(0.72f, 0.72f, 0.75f);

        /// <summary>Цвет «кожи» персонажа.</summary>
        public Color SkinColor = new Color(0.72f, 0.58f, 0.48f);

        /// <summary>Цвет свечения головы и деталей: линзы, неон, экраны.</summary>
        public Color GlowColor = new Color(0f, 0.9f, 0.46f);

        /// <summary>Масштаб головы относительно базового (фуражка Гранд-Коста выше).</summary>
        public float HeadScale = 1f;

        /// <summary>Объём торса: 1 — базовый, больше — широкие плечи.</summary>
        public float BulkScale = 1f;
    }

    #endregion

    #region Константы баланса

    /// <summary>
    /// Неизменяемые значения баланса. Всё, что может редактироваться дизайном, живёт в GameConfig.
    /// Значения обязаны совпадать с таблицей в AGENTS.md (§5).
    /// </summary>
    public static class EconomyConstants
    {
        // --- Общие параметры матча ---
        public const int StartingMoney = 1000;
        public const int StartingCitiesPerCountry = 4;
        public const int TotalRounds = 6;
        public const int StartingWorldEcology = 100;
        public const int MaxWorldEcology = 100;

        // --- Стоимость действий ---
        public const int CityDevelopmentCost = 300;
        public const int AegisShieldCost = 400;
        public const int MissileAssemblyCost = 350;
        public const int MissileLaunchCost = 0;
        public const int ReconCost = 200;
        public const int WiretapCost = 150;
        public const int EcologyDonationCost = 200;
        public const int MoneyTransferCost = 0;

        // --- Доход ---
        /// <summary>Базовый доход развитого города за раунд.</summary>
        public const int CityBaseIncome = 150;

        /// <summary>Прирост дохода города от действия «Развитие города».</summary>
        public const int CityDevelopmentIncomeBonus = 150;

        // --- Экология, проценты мировой шкалы ---
        public const float EcologyLossOnAssemble = 8f;
        public const float EcologyLossOnLaunch = 12f;
        public const float EcologyGainOnDonation = 10f;

        // --- Длительности фаз раунда, секунды ---
        public const int BunkerDiplomacySeconds = 120;
        public const int SummitUNSeconds = 90;
        public const int OrderInputSeconds = 45;
        public const int ReckoningSeconds = 45;

        // --- Санкции Круглого Зала ---
        /// <summary>Процент урезания дохода державы под санкциями (следующий раунд).</summary>
        public const float SanctionIncomePenaltyPercent = 30f;

        // --- Подсчёт очков ---
        /// <summary>Вес живого города при определении победителя раунда.</summary>
        public const int RoundScorePerAliveCity = 250;

        /// <summary>Вес живого города в итоговой таблице матча.</summary>
        public const int VictoryScorePerAliveCity = 1000;

        // --- Ограничения ввода приказов ---
        public const int MaxOrdersPerPlayerPerRound = 12;

        /// <summary>Сумма перевода по умолчанию в фазе переговоров.</summary>
        public const int DefaultTransferAmount = 100;

        // --- Темп показа фазы «Расчёт» ---
        public const float ReckoningStepSecondsMin = 0.05f;
        public const float ReckoningStepSecondsMax = 0.45f;

        // --- Сеть (Core/NetworkTransport) ---
        public const int DefaultServerPort = 7777;

        // --- Пассивки держав ---
        public const int NeoSiliconFreeWiretaps = 1;
        public const float GrandCostaTransferTaxPercent = 10f;
        public const int PermafrostMissileAssemblyCost = 280;
        public const int PermafrostCityIncomePenalty = 25;
        public const int GoldenCaliphateAirCreditTotal = 200;
        public const int GoldenCaliphateAirCreditShare = 100;
        public const int RustGearsStartingAegisShields = 1;
        public const float RustGearsEcologyDamageMultiplier = 2f;

        // --- Пороги секретных доктрин ---
        public const int DeathTraderCitiesDestroyed = 10;
        public const int ApocalypseCultAegisShields = 4;
        public const int PaperTigerWinStreak = 3;
        public const int PactSpiderActivePacts = 4;
        public const int OathbreakerPactsWithEveryone = 5;
        public const int OathbreakerBetrayals = 1;
        public const int InsuranceFraudMoney = 2000;
        public const int SilentBunkerCitiesAlive = 3;

        // --- Служебные пределы ---
        public const int MinMoney = 0;
        public const int MaxCitiesPerCountry = 12;

        /// <summary>Ёмкость журнала матча (последние записи показываются в HUD).</summary>
        public const int MatchJournalCapacity = 200;

        /// <summary>Ограничивает мировую экологию диапазоном 0..100.</summary>
        public static float ClampWorldEcology(float value)
        {
            return Mathf.Clamp(value, 0f, MaxWorldEcology);
        }

        /// <summary>Применяет процент к базовому значению: 350 * 10% = 35.</summary>
        public static float ApplyPercent(float baseValue, float percent)
        {
            return baseValue * percent * 0.01f;
        }
    }

    /// <summary>
    /// Канонические имена объектов графического пайплайна (Assets/GeneratedProps и т.д.).
    /// Единственный разрешённый способ обратиться к этим объектам — динамически по имени
    /// через SceneVisualBinder (Core). Список синхронизирован с AGENTS.md (§3).
    /// </summary>
    public static class GeneratedObjectNames
    {
        public const string PhoneRed = "Phone_Red";
        public const string PhoneYellow = "Phone_Yellow";
        public const string PhoneBlack = "Phone_Black";
        public const string CrtMonitor = "CRT_Monitor";
        public const string NukeButton = "Nuke_Button";
        public const string LidMesh = "LidMesh";
        public const string ButtonMesh = "ButtonMesh";
        public const string DesktopShredder = "Desktop_Shredder";
        public const string TreatyMesh = "TreatyMesh";
        public const string ToggleSwitch = "Toggle_Switch";
        public const string DictatorGeneral = "Dictator_General";
        public const string DictatorTvHead = "Dictator_TVHead";
        public const string DictatorGasMask = "Dictator_GasMask";

        // --- Локация №1: личный бункер (приборы) ---
        public const string SwitchboardUnit = "Switchboard_Unit";
        public const string OrdersCrtTerminal = "Orders_CRT_Terminal";
        public const string DotMatrixPrinter = "DotMatrix_Printer";
        public const string NuclearLaunchBox = "Nuclear_Launch_Box";
        public const string StateStatusBoard = "State_Status_Board";
        public const string LidsMesh = "LidMesh";

        /// <summary>Потолочный плафон над столом игрока (узел Bunker_Ceiling_Lamp).</summary>
        public const string BunkerCeilingLamp = "Bunker_Ceiling_Lamp";

        /// <summary>Светящаяся лампа внутри плафона над столом.</summary>
        public const string BunkerCeilingBulb = "Bunker_Ceiling_Bulb";

        /// <summary>Гирлянда провисающих кабелей под потолком бункера.</summary>
        public const string BunkerCeilingCables = "Bunker_Ceiling_Cables";

        /// <summary>Ниша гермодвери в дальней стене бункера.</summary>
        public const string BunkerDoorNiche = "Bunker_DoorNiche";

        /// <summary>Трубные банки и щитки с индикаторами вдоль дальней стены.</summary>
        public const string BunkerWallServices = "Bunker_WallServices";

        /// <summary>Техническая документация, лежащая на столешнице у монитора.</summary>
        public const string BunkerTechDoc = "Bunker_Tech_Doc";

        // --- Узлы диктатора ---
        public const string TorsoShared = "Torso_Shared";
        public const string HeadUpper = "Head_Upper";
        public const string JawPivot = "Jaw_Pivot";
        public const string ArmrestBase = "Armrest_Base";

        // --- Узлы приборов бункера ---
        public const string DialWheel = "Dial_Wheel";
        public const string HandsetPivot = "Handset_Pivot";
        public const string LineLampPrefix = "LineLamp_";
        public const string ScreenSurface = "Screen_Surface";
        public const string PaperStripPivot = "PaperStrip_Pivot";
        public const string SafetyCoverPivot = "Safety_Cover_Pivot";
        public const string LaunchKeyPivot = "Launch_Key_Pivot";
        public const string RedButtonPlunger = "Red_Button_Plunger";
        public const string OrderSwitchPrefix = "Switch_";
        public const string CityDioramaPrefix = "CityDiorama_";
        public const string AaShieldDome = "AAShield_Dome";
        public const string RuinsState = "Ruins_State";

        /// <summary>Макет живого города внутри секции-диорамы (включается, пока город цел).</summary>
        public const string CityStateModel = "City_State_Model";

        /// <summary>Светящийся голографический глобус в колодце Круглого стола.</summary>
        public const string HologramGlobe = "Hologram_Globe";

        // --- Локация №2: Круглый зал саммита ---
        public const string SummitRoundTable = "Summit_Round_Table";
        public const string DoomsdayHexClock = "Doomsday_Hex_Clock";
        public const string GooseneckMic = "Gooseneck_Mic";
        public const string WaterGlass = "Water_Glass";
        public const string LieDetectorRecorder = "LieDetector_Recorder";
        public const string PactFolder = "Pact_Folder";
        public const string ArchipelagoPrefix = "Archipelago_";
        public const string CityMarkerPrefix = "CityMarker_";

        /// <summary>Три люминесцентных светильника под потолком зала саммита.</summary>
        public const string SummitFluorescentFixtures = "Summit_Fluorescent_Fixtures";

        /// <summary>Латунная пепельница на настольном наборе.</summary>
        public const string SummitAshtray = "Summit_Ashtray";

        /// <summary>Ручка в держателе на настольном наборе.</summary>
        public const string SummitPen = "Summit_Pen";

        /// <summary>Кресло дипломата за столом; нумерация 1..5, место игрока пустое.</summary>
        public const string SummitChairPrefix = "Summit_Chair_";

        /// <summary>Полный список имён для инициализации SceneVisualBinder.</summary>
        public static readonly string[] All =
        {
            PhoneRed,
            PhoneYellow,
            PhoneBlack,
            CrtMonitor,
            NukeButton,
            LidMesh,
            ButtonMesh,
            DesktopShredder,
            TreatyMesh,
            ToggleSwitch,
            DictatorGeneral,
            DictatorTvHead,
            DictatorGasMask,
            SwitchboardUnit,
            OrdersCrtTerminal,
            DotMatrixPrinter,
            NuclearLaunchBox,
            StateStatusBoard,
            TorsoShared,
            HeadUpper,
            JawPivot,
            DialWheel,
            HandsetPivot,
            ScreenSurface,
            PaperStripPivot,
            SafetyCoverPivot,
            LaunchKeyPivot,
            RedButtonPlunger,
            AaShieldDome,
            RuinsState,
            CityStateModel,
            SummitRoundTable,
            HologramGlobe,
            DoomsdayHexClock,
            GooseneckMic,
            WaterGlass,
            LieDetectorRecorder,
            PactFolder
        };

        /// <summary>Индексированные имена, которые генерируются с числовым суффиксом.</summary>
        public const int LineLampCount = 6;
        public const int OrderSwitchCount = 3;
        public const int CityDioramaCount = 4;
        public const int CountryCount = 6;

        /// <summary>Имя лампы линии по индексу (0..5).</summary>
        public static string LineLamp(int index)
        {
            return LineLampPrefix + index;
        }

        /// <summary>Имя тумблера приказа по индексу (0..2).</summary>
        public static string OrderSwitch(int index)
        {
            return OrderSwitchPrefix + (index + 1).ToString("00");
        }

        /// <summary>Имя секции-диорамы табло статуса по индексу (0..3).</summary>
        public static string CityDiorama(int index)
        {
            return CityDioramaPrefix + index;
        }

        /// <summary>Имя корня рига диктатора для державы.</summary>
        public static string DictatorRoot(CountryId id)
        {
            return "DictatorRoot_" + id;
        }

        /// <summary>Имя голографического архипелага державы на столе саммита.</summary>
        public static string Archipelago(CountryId id)
        {
            return ArchipelagoPrefix + id;
        }

        /// <summary>Проверяет, что имя входит в разрешённый контракт.</summary>
        public static bool IsKnown(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], objectName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    #endregion

    #region Конфигурации (сериализуемые в JSON)

    /// <summary>
    /// Стоимость действий в долларах. Дублируют EconomyConstants, но живут в конфиге,
    /// чтобы дизайнер мог править баланс в Resources/Config без правки кода.
    /// </summary>
    [Serializable]
    public class ActionCosts
    {
        public int CityDevelopment = EconomyConstants.CityDevelopmentCost;
        public int AegisShield = EconomyConstants.AegisShieldCost;
        public int MissileAssembly = EconomyConstants.MissileAssemblyCost;
        public int MissileLaunch = EconomyConstants.MissileLaunchCost;
        public int Recon = EconomyConstants.ReconCost;
        public int Wiretap = EconomyConstants.WiretapCost;
        public int EcologyDonation = EconomyConstants.EcologyDonationCost;
        public int MoneyTransfer = EconomyConstants.MoneyTransferCost;
    }

    /// <summary>
    /// Правила мировой экологии в процентах шкалы 0..100.
    /// </summary>
    [Serializable]
    public class EcologyRules
    {
        public int StartingValue = EconomyConstants.StartingWorldEcology;
        public float MaxValue = EconomyConstants.MaxWorldEcology;
        public float LossOnAssemble = EconomyConstants.EcologyLossOnAssemble;
        public float LossOnLaunch = EconomyConstants.EcologyLossOnLaunch;
        public float GainOnDonation = EconomyConstants.EcologyGainOnDonation;
    }

    #endregion

    #region Державы

    /// <summary>
    /// Описание сатирической державы и её пассивной способности.
    /// Поля публичные и помечены [Serializable] — обязательное требование JsonUtility.
    /// </summary>
    [Serializable]
    public class CountryDefinition
    {
        public CountryId Id;
        public string DisplayName;
        public string ShortName;
        public string Ideology;
        public string PassiveName;
        public string PassiveDescription;
        public CountryPassiveKind PassiveKind;
        public Color AccentColor;

        // Базовые значения (копируются из конфига при создании)
        public int StartingMoney;
        public int StartingCities;
        public int StartingAegisShields;

        // Параметры пассивки. Ноль/единица означает «не применяется».
        public int FreeActionsPerMatch;
        public float TransferTaxPercent;
        public int MissileAssemblyCost;
        public int CityIncomeModifier;
        public int AirCreditTotal;
        public int AirCreditShare;
        public float EcologyDamageMultiplier;
        public bool EcologyDamageOnAssemblyOnly;
        public bool SurvivesWorldCollapse;

        /// <summary>
        /// Внешний облик для процедурного 3D-конструктора. Чистые данные: Core/Visuals
        /// читает их и не ветвится по CountryId.
        /// </summary>
        public DictatorVisualLook Visual = new DictatorVisualLook();

        public CountryDefinition()
        {
            DisplayName = string.Empty;
            ShortName = string.Empty;
            Ideology = string.Empty;
            PassiveName = string.Empty;
            PassiveDescription = string.Empty;
            AccentColor = Color.white;
            StartingMoney = EconomyConstants.StartingMoney;
            StartingCities = EconomyConstants.StartingCitiesPerCountry;
            StartingAegisShields = 0;
            EcologyDamageMultiplier = 1f;
        }

        /// <summary>
        /// Единственное место в проекте, где ветвление по внешности диктатора разрешено.
        /// 3D-фабрика в Core/Visuals читает эти данные и собирает голову/мундир по
        /// </summary>
        public static CountryDefinition CreateDefault(CountryId id)
        {
            CountryDefinition def = new CountryDefinition();
            def.Id = id;
            def.PassiveKind = CountryPassiveKind.None;
            def.StartingMoney = EconomyConstants.StartingMoney;
            def.StartingCities = EconomyConstants.StartingCitiesPerCountry;
            def.StartingAegisShields = 0;
            def.FreeActionsPerMatch = 0;
            def.TransferTaxPercent = 0f;
            def.MissileAssemblyCost = 0;
            def.CityIncomeModifier = 0;
            def.AirCreditTotal = 0;
            def.AirCreditShare = 0;
            def.EcologyDamageMultiplier = 1f;
            def.EcologyDamageOnAssemblyOnly = false;
            def.SurvivesWorldCollapse = false;
            def.Visual = CreateDefaultVisualLook(id);

            switch (id)
            {
                case CountryId.NeoSilicon:
                    def.DisplayName = "Синдикат «Нео-Кремний»";
                    def.ShortName = "Нео-Кремний";
                    def.Ideology = "Кремниевые кластеры, майоры с оптовыми ценами на демократию.";
                    def.PassiveKind = CountryPassiveKind.FreeWiretapPerMatch;
                    def.PassiveName = "Прослушка в подарок";
                    def.PassiveDescription = "1 бесплатная прослушка за матч (стоимость 150$ не списывается).";
                    def.AccentColor = new Color(0.20f, 0.85f, 0.45f);
                    def.FreeActionsPerMatch = EconomyConstants.NeoSiliconFreeWiretaps;
                    break;

                case CountryId.GrandCosta:
                    def.DisplayName = "Империя Гранд-Коста";
                    def.ShortName = "Гранд-Коста";
                    def.Ideology = "Солнечные курорты и капитал, выросший из чужих списаний за тропики.";
                    def.PassiveKind = CountryPassiveKind.TransferTax;
                    def.PassiveName = "Накладной расход";
                    def.PassiveDescription = "10% комиссии со всех денежных переводов между другими странами.";
                    def.AccentColor = new Color(1.00f, 0.78f, 0.15f);
                    def.TransferTaxPercent = EconomyConstants.GrandCostaTransferTaxPercent;
                    break;

                case CountryId.PermafrostOrder:
                    def.DisplayName = "Орден Вечной Мерзлоты";
                    def.ShortName = "Мерзлота";
                    def.Ideology = "Ледяные храмы вмёрзших ракет и монахи, отпускающие грехи за кругляш.";
                    def.PassiveKind = CountryPassiveKind.DiscountedMissiles;
                    def.PassiveName = "Холодный расчёт";
                    def.PassiveDescription = "Сборка ракеты стоит 280$ вместо 350$, но базовый доход города на 25$ ниже.";
                    def.AccentColor = new Color(0.55f, 0.85f, 1.00f);
                    def.MissileAssemblyCost = EconomyConstants.PermafrostMissileAssemblyCost;
                    def.CityIncomeModifier = -EconomyConstants.PermafrostCityIncomePenalty;
                    break;

                case CountryId.GoldenCaliphate:
                    def.DisplayName = "Золотой Халифат Дюн";
                    def.ShortName = "Дюн";
                    def.Ideology = "Барханы, нефтяные деньги и бухгалтерия, не признающая физику.";
                    def.PassiveKind = CountryPassiveKind.AirCredit;
                    def.PassiveName = "Кредит из воздуха";
                    def.PassiveDescription = "Раз в раунд выдаёт кредит 200$ из воздуха: по 100$ себе и партнёру, если партнёр не атакует Халифат в этом раунде.";
                    def.AccentColor = new Color(0.95f, 0.62f, 0.10f);
                    def.AirCreditTotal = EconomyConstants.GoldenCaliphateAirCreditTotal;
                    def.AirCreditShare = EconomyConstants.GoldenCaliphateAirCreditShare;
                    break;

                case CountryId.RustGearsUnion:
                    def.DisplayName = "Союз Ржавых Шестеренок";
                    def.ShortName = "Шестерёнки";
                    def.Ideology = "Заводы в дымке: шестерёнки перемалывают и металл, и леса.";
                    def.PassiveKind = CountryPassiveKind.FreeAegisStart;
                    def.PassiveName = "Толстая крышка";
                    def.PassiveDescription = "1 бесплатный щит ПВО на старте, но удвоенный урон по экологии при сборке ракет.";
                    def.AccentColor = new Color(0.65f, 0.45f, 0.35f);
                    def.StartingAegisShields = EconomyConstants.RustGearsStartingAegisShields;
                    def.EcologyDamageMultiplier = EconomyConstants.RustGearsEcologyDamageMultiplier;
                    def.EcologyDamageOnAssemblyOnly = true;
                    break;

                case CountryId.FreeResortsAtoll:
                    def.DisplayName = "Атолл Свободных Курортов";
                    def.ShortName = "Атолл";
                    def.Ideology = "Тюбинг, нефтяные лифты и дыхание в масках за чужой счёт.";
                    def.PassiveKind = CountryPassiveKind.LastCitySurvivor;
                    def.PassiveName = "Последний курорт";
                    def.PassiveDescription = "При падении мировой экологии до 0% его последний живой город выживает.";
                    def.AccentColor = new Color(0.25f, 0.95f, 0.85f);
                    def.SurvivesWorldCollapse = true;
                    break;

                default:
                Debug.LogWarning("[WorldCrisis] Неизвестная держава в CreateDefault: " + id);
                    def.DisplayName = id.ToString();
                    def.ShortName = id.ToString();
                    def.Ideology = string.Empty;
                    def.PassiveName = string.Empty;
                    def.PassiveDescription = string.Empty;
                    break;
            }

            return def;
        }

        /// <summary>
        /// Единственное место в проекте, где ветвление по внешности диктатора разрешено.
        /// 3D-фабрика в Core/Visuals читает эти данные и собирает голову/мундир по
        /// <see cref="DictatorHeadStyle"/> и <see cref="DictatorUniformKind"/>, не зная про CountryId.
        /// </summary>
        public static DictatorVisualLook CreateDefaultVisualLook(CountryId id)
        {
            DictatorVisualLook look = new DictatorVisualLook();

            switch (id)
            {
                case CountryId.NeoSilicon:
                    // Чёрная водолазка под тёмно-зелёным пиджаком, рубашечный ворот.
                    look.HeadStyle = DictatorHeadStyle.ScreenTv;
                    look.UniformKind = DictatorUniformKind.TurtleneckSuit;
                    look.ClothColor = Hex(0x1B2A22);
                    look.ClothShadeColor = Hex(0x0B0D0C);
                    look.TrimColor = Hex(0x2E6B45);
                    look.MetalColor = Hex(0xBFC4BE);
                    look.SkinColor = Hex(0xD6CDB4);
                    look.GlowColor = Hex(0x00E676);
                    look.HeadScale = 1.05f;
                    look.BulkScale = 0.96f;
                    break;

                case CountryId.GrandCosta:
                    // Тёмно-оливковый парадный мундир, золото, загорелая кожа, сигара.
                    look.HeadStyle = DictatorHeadStyle.GrotesqueGeneral;
                    look.UniformKind = DictatorUniformKind.CeremonialUniform;
                    look.ClothColor = Hex(0x2F3320);
                    look.ClothShadeColor = Hex(0x16180E);
                    look.TrimColor = Hex(0xC9A227);
                    look.MetalColor = Hex(0xD8B64A);
                    look.SkinColor = Hex(0xC08B62);
                    look.GlowColor = Hex(0xFFB000);
                    look.HeadScale = 1.1f;
                    look.BulkScale = 1.22f;
                    break;

                case CountryId.PermafrostOrder:
                    // Свинцово-серая парка, сталь, бледная кожа, янтарные линзы.
                    look.HeadStyle = DictatorHeadStyle.FrostRespirator;
                    look.UniformKind = DictatorUniformKind.HeavyParka;
                    look.ClothColor = Hex(0x3B4550);
                    look.ClothShadeColor = Hex(0x1A2026);
                    look.TrimColor = Hex(0x8FA6B5);
                    look.MetalColor = Hex(0x6E7378);
                    look.SkinColor = Hex(0xB9C4CC);
                    look.GlowColor = Hex(0xFFB000);
                    look.HeadScale = 1.02f;
                    look.BulkScale = 1.14f;
                    break;

                case CountryId.GoldenCaliphate:
                    // Чёрно-золотое одеяние, полированное золото, тёмная кожа.
                    look.HeadStyle = DictatorHeadStyle.GoldenMask;
                    look.UniformKind = DictatorUniformKind.BlackGoldRobe;
                    look.ClothColor = Hex(0x14100A);
                    look.ClothShadeColor = Hex(0x080604);
                    look.TrimColor = Hex(0xC9962F);
                    look.MetalColor = Hex(0xE0B74A);
                    look.SkinColor = Hex(0x8A6238);
                    look.GlowColor = Hex(0xFFD34D);
                    look.HeadScale = 1.04f;
                    look.BulkScale = 1.06f;
                    break;

                case CountryId.RustGearsUnion:
                    // Ржаво-бурый плащ, латунь, загорелая кожа, дым из трубы.
                    look.HeadStyle = DictatorHeadStyle.RustedGasMask;
                    look.UniformKind = DictatorUniformKind.RustCloak;
                    look.ClothColor = Hex(0x54341F);
                    look.ClothShadeColor = Hex(0x241608);
                    look.TrimColor = Hex(0x8C5A2B);
                    look.MetalColor = Hex(0x9A7B4A);
                    look.SkinColor = Hex(0xA9714A);
                    look.GlowColor = Hex(0xFF8A3D);
                    look.HeadScale = 1.12f;
                    look.BulkScale = 1.18f;
                    break;

                case CountryId.FreeResortsAtoll:
                    // Гавайская рубашка под кевларовым бронежилетом, загорелый пластик, неон.
                    look.HeadStyle = DictatorHeadStyle.ResortDoll;
                    look.UniformKind = DictatorUniformKind.HawaiianVest;
                    look.ClothColor = Hex(0xE4D6B8);
                    look.ClothShadeColor = Hex(0x1E5F63);
                    look.TrimColor = Hex(0x19BFAE);
                    look.MetalColor = Hex(0x4A4F52);
                    look.SkinColor = Hex(0xD69C6C);
                    look.GlowColor = Hex(0x3FF0DC);
                    look.HeadScale = 1.0f;
                    look.BulkScale = 1.1f;
                    break;

                default:
                Debug.LogWarning("[WorldCrisis] Неизвестная держава в CreateDefaultVisualLook: " + id);
                    look.HeadStyle = DictatorHeadStyle.ScreenTv;
                    look.UniformKind = DictatorUniformKind.TurtleneckSuit;
                    break;
            }

            return look;
        }

        /// <summary>Разбирает 0xRRGGBB в линейный Color. Единственный источник «цветов Art Bible».</summary>
        public static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }

        /// <summary>Цена сборки ракеты с учётом скидок державы.</summary>
        public int ResolveMissileAssemblyCost()
        {
            if (MissileAssemblyCost > 0)
            {
                return MissileAssemblyCost;
            }

            return EconomyConstants.MissileAssemblyCost;
        }

        /// <summary>Доход города за раунд с учётом штрафа державы.</summary>
        public int ResolveCityIncome(int baseIncome)
        {
            return Mathf.Max(0, baseIncome + CityIncomeModifier);
        }

        /// <summary>Урон экологии при сборке ракеты с учётом множителя державы.</summary>
        public float ResolveEcologyLossOnAssemble(float baseLoss)
        {
            return baseLoss * EcologyDamageMultiplier;
        }

        /// <summary>Урон экологии при пуске ракеты. Удвоение действует только на сборку.</summary>
        public float ResolveEcologyLossOnLaunch(float baseLoss)
        {
            if (EcologyDamageOnAssemblyOnly)
            {
                return baseLoss;
            }

            return baseLoss * EcologyDamageMultiplier;
        }

        /// <summary>Количество бесплатных действий (прослушек) за матч.</summary>
        public int GetFreeActionCount()
        {
            return Mathf.Max(0, FreeActionsPerMatch);
        }

        /// <summary>
        /// Комиссия Империи Гранд-Коста. Налог берётся только от переводов между другими странами,
        /// то есть если сам Атолл участвует в переводе — комиссия не начисляется.
        /// </summary>
        public int CalculateTransferTax(int amount, CountryId sender, CountryId receiver)
        {
            if (PassiveKind != CountryPassiveKind.TransferTax || amount <= 0)
            {
                return 0;
            }

            if (sender == Id || receiver == Id)
            {
                return 0;
            }

            return Mathf.RoundToInt(EconomyConstants.ApplyPercent(amount, TransferTaxPercent));
        }

        /// <summary>Может ли этот раунд выдать кредит из воздуха выбранному партнёру.</summary>
        public bool CanIssueAirCredit(CountryId partnerId, bool partnerAttackedThisRound, bool creditUsedThisRound)
        {
            if (PassiveKind != CountryPassiveKind.AirCredit || creditUsedThisRound)
            {
                return false;
            }

            return partnerId != Id && !partnerAttackedThisRound;
        }

        /// <summary>Сколько получает партнёр при выдаче кредита (0 — кредит не выдаётся).</summary>
        public int GetAirCreditPartnerGain(CountryId partnerId, bool partnerAttackedThisRound, bool creditUsedThisRound)
        {
            if (!CanIssueAirCredit(partnerId, partnerAttackedThisRound, creditUsedThisRound))
            {
                return 0;
            }

            return AirCreditShare;
        }

        /// <summary>Сколько получает сам Халифат при выдаче кредита (0 — кредит не выдаётся).</summary>
        public int GetAirCreditSelfGain(bool creditUsedThisRound)
        {
            if (PassiveKind != CountryPassiveKind.AirCredit || creditUsedThisRound)
            {
                return 0;
            }

            return Mathf.Max(0, AirCreditTotal - AirCreditShare);
        }

        /// <summary>
        /// <summary>Проверка пассивки «Последний курорт»: при нулевой экологии выживает последний живой город.</summary>
        /// </summary>
        public bool ShouldSurviveCollapse(int aliveCities)
        {
            return SurvivesWorldCollapse && aliveCities > 0;
        }
    }

    #endregion

    #region Секретные доктрины

    /// <summary>
    /// Описание секретной доктрины: условие, подсказка и машино-читаемый тип проверки.
    /// </summary>
    [Serializable]
    public class SecretDoctrine
    {
        public SecretDoctrineType Type;
        public string DisplayName;
        public string SecretHint;
        public string ConditionText;
        public DoctrineCheckKind CheckKind;
        public bool IsSecret = true;
        public int RequiredInt;
        public int RequiredIntSecondary;
        public float RequiredFloat;

        public SecretDoctrine()
        {
            DisplayName = string.Empty;
            SecretHint = string.Empty;
            ConditionText = string.Empty;
            CheckKind = DoctrineCheckKind.None;
            IsSecret = true;
            RequiredInt = 0;
            RequiredIntSecondary = 0;
            RequiredFloat = 0f;
        }

        /// <summary>Фабрика дефолтного описания доктрины. Единственное место в проекте,</summary>
        public static SecretDoctrine CreateDefault(SecretDoctrineType type)
        {
            SecretDoctrine doctrine = new SecretDoctrine();
            doctrine.Type = type;
            doctrine.IsSecret = true;

            switch (type)
            {
                case SecretDoctrineType.DeathTrader:
                    doctrine.DisplayName = "Торговец Смертью";
                    doctrine.ConditionText = "10 уничтоженных городов в мире при 0 своих пусках.";
                    doctrine.SecretHint = "Кто-то продаёт разрушения, не нажимая кнопку.";
                    doctrine.CheckKind = DoctrineCheckKind.GlobalCitiesDestroyedWithoutOwnLaunch;
                    doctrine.RequiredInt = EconomyConstants.DeathTraderCitiesDestroyed;
                    break;

                case SecretDoctrineType.Parasite:
                    doctrine.DisplayName = "Паразит";
                    doctrine.ConditionText = "Держать больше денег, чем у всех остальных вместе, к концу 6 раунда.";
                    doctrine.SecretHint = "Кто-то богатеет на чужих страданиях.";
                    doctrine.CheckKind = DoctrineCheckKind.RicherThanEveryoneCombined;
                    doctrine.RequiredInt = EconomyConstants.TotalRounds;
                    break;

                case SecretDoctrineType.ApocalypseCult:
                    doctrine.DisplayName = "Культ Апокалипсиса";
                    doctrine.ConditionText = "Обрушить мировую экологию до 0% при 4 своих щитах ПВО.";
                    doctrine.SecretHint = "Кто-то прикрывается щитами и мечтает о пепле.";
                    doctrine.CheckKind = DoctrineCheckKind.ZeroEcologyWithAegisShields;
                    doctrine.RequiredInt = EconomyConstants.ApocalypseCultAegisShields;
                    break;

                case SecretDoctrineType.Oathbreaker:
                    doctrine.DisplayName = "Клятвопреступник";
                    doctrine.ConditionText = "Подписать пакты со всеми и предать ровно одного в финале.";
                    doctrine.SecretHint = "Кто-то клянётся всем и режет одного.";
                    doctrine.CheckKind = DoctrineCheckKind.PactWithAllThenBetrayOne;
                    doctrine.RequiredInt = EconomyConstants.OathbreakerPactsWithEveryone;
                    doctrine.RequiredIntSecondary = EconomyConstants.OathbreakerBetrayals;
                    break;

                case SecretDoctrineType.PaperTiger:
                    doctrine.DisplayName = "Бумажный Тигр";
                    doctrine.ConditionText = "Выиграть 3 раунда подряд, ни разу не выпустив ракету.";
                    doctrine.SecretHint = "Кто-то побеждает улыбкой и атомной бездарностью.";
                    doctrine.CheckKind = DoctrineCheckKind.WinStreakWithoutLaunch;
                    doctrine.RequiredInt = EconomyConstants.PaperTigerWinStreak;
                    break;

                case SecretDoctrineType.PactSpider:
                    doctrine.DisplayName = "Паутина Пактов";
                    doctrine.ConditionText = "Держать действующие пакты с 4 другими державами до финала.";
                    doctrine.SecretHint = "Кто-то опутал всех договорами.";
                    doctrine.CheckKind = DoctrineCheckKind.KeepManyPactsToFinale;
                    doctrine.RequiredInt = EconomyConstants.PactSpiderActivePacts;
                    break;

                case SecretDoctrineType.InsuranceFraud:
                    doctrine.DisplayName = "Страховой Мошенник";
                    doctrine.ConditionText = "Дожить до финала с 2000$ и не потерять ни одного города.";
                    doctrine.SecretHint = "Кто-то богатеет, не теряя ни одного жителя.";
                    doctrine.CheckKind = DoctrineCheckKind.RichWithoutLosingCities;
                    doctrine.RequiredInt = EconomyConstants.InsuranceFraudMoney;
                    break;

                case SecretDoctrineType.SilentBunker:
                    doctrine.DisplayName = "Немой Бункер";
                    doctrine.ConditionText = "Дожить до финала с 3+ живыми городами, 0 щитов ПВО и 0 пусков.";
                    doctrine.SecretHint = "Кто-то до конца сидит под крышкой.";
                    doctrine.CheckKind = DoctrineCheckKind.SurviveToFinaleUnarmed;
                    doctrine.RequiredInt = EconomyConstants.SilentBunkerCitiesAlive;
                    break;

                case SecretDoctrineType.None:
                    doctrine.DisplayName = "Нет доктрины";
                    doctrine.ConditionText = string.Empty;
                    doctrine.SecretHint = string.Empty;
                    doctrine.CheckKind = DoctrineCheckKind.None;
                    doctrine.IsSecret = false;
                    break;

                default:
                Debug.LogWarning("[WorldCrisis] Неизвестная доктрина в CreateDefault: " + type);
                    doctrine.DisplayName = type.ToString();
                    doctrine.ConditionText = string.Empty;
                    doctrine.SecretHint = string.Empty;
                    doctrine.CheckKind = DoctrineCheckKind.None;
                    break;
            }

            return doctrine;
        }
    }

    /// <summary>
    /// Снимок состояния матча, необходимый для машинной проверки условий доктрин.
    /// Заполняется Core-слоем один раз в момент проверки.
    /// </summary>
    [Serializable]
    public struct DoctrineEvaluationStats
    {
        public int Round;
        public int TotalRounds;
        public bool IsFinale;

        public int OwnCitiesAlive;
        public int OwnCitiesLost;
        public int OwnAegisShields;
        public int OwnMissilesLaunched;
        public int OwnMoney;

        public int GlobalCitiesDestroyed;
        public int OtherMoneyTotal;
        public int ConsecutiveWins;
        public int ActivePactsCount;
        public int BetrayalsCommitted;
        public float WorldEcology;
    }

    /// <summary>
    /// Машино-читаемая проверка условий доктрин и краткий прогресс для HUD.
    /// </summary>
    public static class SecretDoctrineEvaluator
    {
        /// <summary>Возвращает true, если условие доктрины выполнено прямо сейчас.</summary>
        public static bool Evaluate(SecretDoctrine doctrine, in DoctrineEvaluationStats stats)
        {
            if (doctrine == null || doctrine.CheckKind == DoctrineCheckKind.None)
            {
                return false;
            }

            switch (doctrine.CheckKind)
            {
                case DoctrineCheckKind.GlobalCitiesDestroyedWithoutOwnLaunch:
                    return stats.GlobalCitiesDestroyed >= doctrine.RequiredInt && stats.OwnMissilesLaunched <= 0;

                case DoctrineCheckKind.RicherThanEveryoneCombined:
                    return stats.IsFinale && stats.OwnMoney > stats.OtherMoneyTotal;

                case DoctrineCheckKind.ZeroEcologyWithAegisShields:
                    return stats.WorldEcology <= 0f && stats.OwnAegisShields >= doctrine.RequiredInt;

                case DoctrineCheckKind.PactWithAllThenBetrayOne:
                    return stats.IsFinale
                           && stats.ActivePactsCount >= doctrine.RequiredInt
                           && stats.BetrayalsCommitted == doctrine.RequiredIntSecondary;

                case DoctrineCheckKind.WinStreakWithoutLaunch:
                    return stats.ConsecutiveWins >= doctrine.RequiredInt && stats.OwnMissilesLaunched <= 0;

                case DoctrineCheckKind.KeepManyPactsToFinale:
                    return stats.IsFinale && stats.ActivePactsCount >= doctrine.RequiredInt;

                case DoctrineCheckKind.RichWithoutLosingCities:
                    // «За матч»: накопится к финалу, иначе 2000$ набираются уже ко 2-му раунду
                    // (1000$ старта + 600$ дохода) и партия заканчивается вслепую.
                    return stats.IsFinale
                           && stats.OwnMoney >= doctrine.RequiredInt
                           && stats.OwnCitiesLost <= 0;

                case DoctrineCheckKind.SurviveToFinaleUnarmed:
                    return stats.IsFinale
                           && stats.OwnCitiesAlive >= doctrine.RequiredInt
                           && stats.OwnAegisShields <= 0
                           && stats.OwnMissilesLaunched <= 0;

                default:
                    return false;
            }
        }

        /// <summary>Краткий прогресс условия для UI (например, «7/10 городов»).</summary>
        public static string DescribeProgress(SecretDoctrine doctrine, in DoctrineEvaluationStats stats)
        {
            if (doctrine == null)
            {
                return string.Empty;
            }

            switch (doctrine.CheckKind)
            {
                case DoctrineCheckKind.GlobalCitiesDestroyedWithoutOwnLaunch:
                    return Mathf.Min(stats.GlobalCitiesDestroyed, doctrine.RequiredInt) + "/" + doctrine.RequiredInt + " городов, своих пусков: " + stats.OwnMissilesLaunched;

                case DoctrineCheckKind.ZeroEcologyWithAegisShields:
                    return "Экология " + Mathf.RoundToInt(stats.WorldEcology) + "%, щитов ПВО: " + stats.OwnAegisShields + "/" + doctrine.RequiredInt;

                case DoctrineCheckKind.WinStreakWithoutLaunch:
                    return "Побед подряд: " + stats.ConsecutiveWins + "/" + doctrine.RequiredInt + ", своих пусков: " + stats.OwnMissilesLaunched;

                case DoctrineCheckKind.PactWithAllThenBetrayOne:
                    return "Пактов: " + stats.ActivePactsCount + "/" + doctrine.RequiredInt + ", предательств: " + stats.BetrayalsCommitted + "/" + doctrine.RequiredIntSecondary;

                case DoctrineCheckKind.KeepManyPactsToFinale:
                    return "Пактов в финале: " + stats.ActivePactsCount + "/" + doctrine.RequiredInt;

                case DoctrineCheckKind.RichWithoutLosingCities:
                    return "Свои деньги: " + stats.OwnMoney + "$, у остальных: " + stats.OtherMoneyTotal + "$";

                case DoctrineCheckKind.SurviveToFinaleUnarmed:
                    return "Городов: " + stats.OwnCitiesAlive + "/" + doctrine.RequiredInt + ", щитов: " + stats.OwnAegisShields + ", пусков: " + stats.OwnMissilesLaunched;

                case DoctrineCheckKind.RicherThanEveryoneCombined:
                    return "Деньги у финала: " + stats.OwnMoney + "$, у остальных: " + stats.OtherMoneyTotal + "$";

                default:
                    return string.Empty;
            }
        }
    }

    #endregion

    #region Генератор и загрузка конфига

    /// <summary>
    /// Точка входа слоя данных: генерация дефолтного конфига, загрузка из Resources и кэширование.
    /// Создаёт/перезаписывает Resources/Config/default_game_config.json.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public string ConfigVersion = "1.0.0";
        public int TotalRounds = EconomyConstants.TotalRounds;
        public int StartingMoney = EconomyConstants.StartingMoney;
        public int StartingCitiesPerCountry = EconomyConstants.StartingCitiesPerCountry;
        public ActionCosts Costs = new ActionCosts();
        public EcologyRules Ecology = new EcologyRules();
        public List<CountryDefinition> Countries = new List<CountryDefinition>();
        public List<SecretDoctrine> Doctrines = new List<SecretDoctrine>();

        public CountryDefinition GetCountry(CountryId id)
        {
            if (Countries == null)
            {
                return null;
            }

            for (int i = 0; i < Countries.Count; i++)
            {
                CountryDefinition country = Countries[i];
                if (country != null && country.Id == id)
                {
                    return country;
                }
            }

            return null;
        }

        /// <summary>Кэшированный конфиг. Загружается автоматически при первом обращении.</summary>
        public CountryDefinition GetOrCreateCountry(CountryId id)
        {
            CountryDefinition country = GetCountry(id);
            if (country != null)
            {
                return country;
            }

            country = CountryDefinition.CreateDefault(id);
            if (Countries == null)
            {
                Countries = new List<CountryDefinition>();
            }

            Countries.Add(country);
            return country;
        }

        public SecretDoctrine GetDoctrine(SecretDoctrineType type)
        {
            if (Doctrines == null)
            {
                return null;
            }

            for (int i = 0; i < Doctrines.Count; i++)
            {
                SecretDoctrine doctrine = Doctrines[i];
                if (doctrine != null && doctrine.Type == type)
                {
                    return doctrine;
                }
            }

            return null;
        }

        /// <summary>Создаёт/перезаписывает Resources-конфиг из кода. Только в редакторе.</summary>
        public bool IsValid(out string error)
        {
            if (TotalRounds <= 0)
            {
                error = "TotalRounds должен быть больше нуля.";
                return false;
            }

            if (StartingMoney < 0 || StartingCitiesPerCountry < 0)
            {
                error = "Отсутствует блок стоимостей или правил экологии.";
                return false;
            }

            if (Costs == null || Ecology == null)
            {
                error = "У державы не задан DisplayName (поле обязательное для UI).";
                return false;
            }

            if (Countries == null || Countries.Count != GameDefinitions.AllCountryIds.Length)
            {
                error = "Ожидается " + GameDefinitions.AllCountryIds.Length + " держав в конфиге.";
                return false;
            }

            for (int i = 0; i < Countries.Count; i++)
            {
                CountryDefinition country = Countries[i];
                if (country == null)
                {
                    error = "Пустая запись державы в конфиге (индекс " + i + ").";
                    return false;
                }

                if (string.IsNullOrEmpty(country.DisplayName))
                {
                    error = "У державы " + country.Id + " не задан DisplayName.";
                    return false;
                }
            }

            if (Doctrines == null || Doctrines.Count != GameDefinitions.AllDoctrineTypes.Length)
            {
                error = "Ожидается " + GameDefinitions.AllDoctrineTypes.Length + " секретных доктрин в конфиге.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    #endregion

    #region Загрузка и валидация конфига

    /// <summary>
    /// Чистая загрузка конфига из Resources. Значения по умолчанию живут в EconomyConstants,'
    /// </summary>
    public static class GameDefinitions
    {
        public const string ConfigResourceName = "Config/default_game_config";
        public const string ConfigFileName = "default_game_config.json";
        public const string ConfigFolder = "Assets/_Game/Resources/Config";
        public const string ConfigAssetPath = ConfigFolder + "/" + ConfigFileName;

        public static readonly CountryId[] AllCountryIds =
        {
            CountryId.NeoSilicon,
            CountryId.GrandCosta,
            CountryId.PermafrostOrder,
            CountryId.GoldenCaliphate,
            CountryId.RustGearsUnion,
            CountryId.FreeResortsAtoll
        };

        public static readonly SecretDoctrineType[] AllDoctrineTypes =
        {
            SecretDoctrineType.DeathTrader,
            SecretDoctrineType.Parasite,
            SecretDoctrineType.ApocalypseCult,
            SecretDoctrineType.Oathbreaker,
            SecretDoctrineType.PaperTiger,
            SecretDoctrineType.PactSpider,
            SecretDoctrineType.InsuranceFraud,
            SecretDoctrineType.SilentBunker
        };

        private static GameConfig s_CachedConfig;

        /// <summary>Дополняет загруженный конфиг недостающими державами и доктринами.</summary>
        public static GameConfig GetConfig()
        {
            if (s_CachedConfig == null)
            {
                s_CachedConfig = LoadConfig();
            }

            return s_CachedConfig;
        }

        /// <summary>Сбрасывает кэш (нужно в редакторе после правки конфига).</summary>
        public static void ResetCache()
        {
            s_CachedConfig = null;
        }

        /// <summary>
    /// Создаёт полностью рабочий конфиг по умолчанию: 6 держав, 8 доктрин, все балансные значения.
    /// Игра полностью работоспособна на этом конфиге без ручной настройки инспектора.
        /// </summary>
        public static GameConfig CreateDefaultConfig()
        {
            GameConfig config = new GameConfig();
            config.ConfigVersion = "1.0.0";
            config.TotalRounds = EconomyConstants.TotalRounds;
            config.StartingMoney = EconomyConstants.StartingMoney;
            config.StartingCitiesPerCountry = EconomyConstants.StartingCitiesPerCountry;
            config.Costs = new ActionCosts();
            config.Ecology = new EcologyRules();
            config.Countries = new List<CountryDefinition>();
            config.Doctrines = new List<SecretDoctrine>();

            for (int i = 0; i < AllCountryIds.Length; i++)
            {
                config.Countries.Add(CountryDefinition.CreateDefault(AllCountryIds[i]));
            }

            for (int i = 0; i < AllDoctrineTypes.Length; i++)
            {
                config.Doctrines.Add(SecretDoctrine.CreateDefault(AllDoctrineTypes[i]));
            }

            return config;
        }

        /// <summary>
        /// Загружает конфиг из Resources. Если ресурса нет или он повреждён —
        /// использует сгенерированный дефолт (в редакторе конфиг-файл создаётся автоматически).
        /// </summary>
        public static GameConfig LoadConfig()
        {
            GameConfig parsed = null;
            TextAsset asset = null;

            try
            {
                asset = Resources.Load<TextAsset>(ConfigResourceName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WorldCrisis] Не удалось загрузить конфиг из Resources: " + exception.Message);
            }

            if (asset != null && !string.IsNullOrEmpty(asset.text))
            {
                string error;
                if (TryParse(asset.text, out parsed) && parsed != null && parsed.IsValid(out error))
                {
                    EnsureCompleteness(parsed);
                    return parsed;
                }

                Debug.LogWarning("[WorldCrisis] Конфиг Resources/" + ConfigResourceName + " повреждён, используется дефолтный.");
                parsed = null;
            }

#if UNITY_EDITOR
            EnsureConfigAsset();
#endif

            return CreateDefaultConfig();
        }

        /// <summary>
        /// Дополняет загруженный конфиг недостающими державами и доктринами.
        /// Нужно для обратной совместимости старых JSON после добавления нового контента.
        /// </summary>
        public static void EnsureCompleteness(GameConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (config.Countries == null)
            {
                config.Countries = new List<CountryDefinition>();
            }

            if (config.Doctrines == null)
            {
                config.Doctrines = new List<SecretDoctrine>();
            }

            for (int i = 0; i < AllCountryIds.Length; i++)
            {
                CountryId id = AllCountryIds[i];
                bool found = false;

                for (int j = 0; j < config.Countries.Count; j++)
                {
                    CountryDefinition country = config.Countries[j];
                    if (country != null && country.Id == id)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    config.Countries.Add(CountryDefinition.CreateDefault(id));
                }
            }

            for (int i = 0; i < AllDoctrineTypes.Length; i++)
            {
                SecretDoctrineType type = AllDoctrineTypes[i];
                bool found = false;

                for (int j = 0; j < config.Doctrines.Count; j++)
                {
                    SecretDoctrine doctrine = config.Doctrines[j];
                    if (doctrine != null && doctrine.Type == type)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    config.Doctrines.Add(SecretDoctrine.CreateDefault(type));
                }
            }
        }

        public static string ToJson(GameConfig config, bool prettyPrint)
        {
            if (config == null)
            {
                return string.Empty;
            }

            return JsonUtility.ToJson(config, prettyPrint);
        }

        /// <summary>Парсит JSON в GameConfig. Возвращает false вместо исключения при битом JSON.</summary>
        public static bool TryParse(string json, out GameConfig config)
        {
            config = null;

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                config = JsonUtility.FromJson<GameConfig>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WorldCrisis] JSON конфига не распознан: " + exception.Message);
                config = null;
            }

            return config != null;
        }

        /// <summary>Короткое имя державы без создания дефолтного описания.</summary>
        public static string GetCountryShortName(CountryId id)
        {
            CountryDefinition country = GetConfig().GetCountry(id);
            return country != null ? country.ShortName : id.ToString();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Редакторный код: создаёт/перезаписывает Resources-конфиг из кода. Только в редакторе:
        /// в билде Resources только читаются.
        /// </summary>
        public static bool SaveConfigToAsset(GameConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[WorldCrisis] SaveConfigToAsset: передан пустой конфиг.");
                return false;
            }

            try
            {
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }

                File.WriteAllText(ConfigAssetPath, ToJson(config, true), Encoding.UTF8);
                AssetDatabase.Refresh();
                ResetCache();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[WorldCrisis] Не удалось сохранить конфиг: " + exception.Message);
                return false;
            }
        }

        /// <summary>Создаёт конфиг-файл, если его ещё нет. Возвращает true, если файл был создан.</summary>
        public static bool EnsureConfigAsset()
        {
            try
            {
                if (File.Exists(ConfigAssetPath))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WorldCrisis] Проверка конфиг-файла не удалась: " + exception.Message);
                return false;
            }

            return SaveConfigToAsset(CreateDefaultConfig());
        }
#endif
    }

    #endregion
}
