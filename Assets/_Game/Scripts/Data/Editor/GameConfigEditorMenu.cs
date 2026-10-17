// World Crisis | Слой: Data (Editor) | Ответственный: OpenCode
// Редакторные утилиты слоя данных: сгенерировать дефолтный конфиг из кода и проверить целостность.
// Код компилируется только в редакторе (Assets/_Game/Scripts/Data/Editor -> Assembly-CSharp-Editor).

#if UNITY_EDITOR

using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

namespace WorldCrisis
{
    /// <summary>
    /// Меню World Crisis: операции над конфигом матча.
    /// Единственный способ получить конфиг — сгенерировать его из кода, вручную инспектор не нужен.
    /// </summary>
    public static class GameConfigEditorMenu
    {
        /// <summary>
        /// Автогенерация конфига при загрузке домена: файл создаётся только если его ещё нет,
        /// поэтому ручные правки дизайна не перетираются. Игра готова к запуску без действий пользователя.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoEnsureConfigAsset()
        {
            if (GameDefinitions.EnsureConfigAsset())
            {
                Debug.Log("[WorldCrisis] Дефолтный конфиг сгенерирован автоматически: " + GameDefinitions.ConfigAssetPath);
            }
        }

        [MenuItem("World Crisis/Сгенерировать дефолтный конфиг", false, 10)]
        public static void GenerateDefaultConfig()
        {
            bool saved = GameDefinitions.SaveConfigToAsset(GameDefinitions.CreateDefaultConfig());
            if (saved)
            {
                Debug.Log("[WorldCrisis] Конфиг создан: " + GameDefinitions.ConfigAssetPath);
            }
        }

        [MenuItem("World Crisis/Проверить и восстановить конфиг", false, 11)]
        public static void ValidateAndRepairConfig()
        {
            GameConfig config = GameDefinitions.LoadConfig();

            string error;
            if (!config.IsValid(out error))
            {
                Debug.LogWarning("[WorldCrisis] Конфиг неполный: " + error + " Добавляю недостающие сущности.");
            }

            GameDefinitions.EnsureCompleteness(config);

            if (config.IsValid(out error))
            {
                Debug.Log("[WorldCrisis] Конфиг валиден. Держав: " + config.Countries.Count
                          + ", доктрин: " + config.Doctrines.Count
                          + ", раундов: " + config.TotalRounds + ".");
                return;
            }

            GameDefinitions.SaveConfigToAsset(GameDefinitions.CreateDefaultConfig());
            Debug.Log("[WorldCrisis] Конфиг перезаписан дефолтным. Причина: " + error);
        }

        [MenuItem("World Crisis/Печать баланса в консоль", false, 30)]
        public static void PrintBalanceToConsole()
        {
            GameConfig config = GameDefinitions.GetConfig();
            ActionCosts costs = config.Costs;

            List<string> lines = new List<string>
            {
                "[WorldCrisis] ==== Баланс матча ====",
                "Раундов: " + config.TotalRounds,
                "Стартовые деньги: " + config.StartingMoney + "$, городов на страну: " + config.StartingCitiesPerCountry,
                "Экология: старт " + config.Ecology.StartingValue + "%, макс " + config.Ecology.MaxValue + "%",
                "Развитие города: " + costs.CityDevelopment + "$ (+" + EconomyConstants.CityDevelopmentIncomeBonus + "$/раунд)",
                "Щит ПВО: " + costs.AegisShield + "$",
                "Сборка ракеты: " + costs.MissileAssembly + "$ (−" + config.Ecology.LossOnAssemble + "% экологии)",
                "Пуск ракеты: " + costs.MissileLaunch + "$ (−" + config.Ecology.LossOnLaunch + "% экологии)",
                "Разведка: " + costs.Recon + "$",
                "Прослушка: " + costs.Wiretap + "$",
                "Вклад в экологию: " + costs.EcologyDonation + "$ (+" + config.Ecology.GainOnDonation + "% экологии)"
            };

            lines.Add("---- Державы ----");
            for (int i = 0; i < config.Countries.Count; i++)
            {
                CountryDefinition country = config.Countries[i];
                if (country == null)
                {
                    continue;
                }

                lines.Add(country.Id + ": " + country.DisplayName + " — " + country.PassiveName + ". " + country.PassiveDescription);
            }

            lines.Add("---- Секретные доктрины ----");
            for (int i = 0; i < config.Doctrines.Count; i++)
            {
                SecretDoctrine doctrine = config.Doctrines[i];
                if (doctrine == null)
                {
                    continue;
                }

                lines.Add(doctrine.Type + ": " + doctrine.DisplayName + " — " + doctrine.ConditionText);
            }

            Debug.Log(string.Join("\n", lines.ToArray()));
        }
    }
}

#endif