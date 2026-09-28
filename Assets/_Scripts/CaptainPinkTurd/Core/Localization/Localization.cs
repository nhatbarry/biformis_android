using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaptainPinkTurd.Core.Localization
{
    public enum ELanguage
    {
        Vietnamese = 0,
        English = 1,
    }

    /// <summary>
    /// Looks up UI strings in Resources/Localization/Strings.txt (tab-separated columns: key, vi, en).
    /// The table is plain tab-separated text so it can be edited in any spreadsheet app.
    /// A missing English cell falls back to Vietnamese; a missing key returns the key itself.
    /// </summary>
    public static class Localization
    {
        private const string LANGUAGE_PREF_KEY = "language";
        private const string TABLE_RESOURCE_PATH = "Localization/Strings";

        private static Dictionary<string, string[]> table;
        private static ELanguage? currentLanguage;

        public static event Action OnLanguageChanged;

        public static ELanguage CurrentLanguage
        {
            get
            {
                currentLanguage ??= (ELanguage)PlayerPrefs.GetInt(LANGUAGE_PREF_KEY, (int)ELanguage.Vietnamese);
                return currentLanguage.Value;
            }
        }

        /// <summary>
        /// Same rule MobileControlsHUD uses to decide whether touch controls are shown,
        /// so control hints always describe the controls the player actually has.
        /// </summary>
        public static bool IsTouchPlatform
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return true;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        public static void SetLanguage(ELanguage language)
        {
            if (language == CurrentLanguage) return;

            currentLanguage = language;
            PlayerPrefs.SetInt(LANGUAGE_PREF_KEY, (int)language);
            PlayerPrefs.Save();
            OnLanguageChanged?.Invoke();
        }

        public static void ToggleLanguage()
        {
            SetLanguage(CurrentLanguage == ELanguage.Vietnamese ? ELanguage.English : ELanguage.Vietnamese);
        }

        public static bool TryGet(string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;

            table ??= LoadTable();
            if (!table.TryGetValue(key, out var columns)) return false;

            int column = (int)CurrentLanguage;
            value = column < columns.Length && columns[column].Length > 0 ? columns[column] : columns[0];
            return true;
        }

        public static string Get(string key) => TryGet(key, out var value) ? value : key;

        public static string Get(string key, params object[] args) => string.Format(Get(key), args);

        private static Dictionary<string, string[]> LoadTable()
        {
            var result = new Dictionary<string, string[]>();
            var asset = Resources.Load<TextAsset>(TABLE_RESOURCE_PATH);
            if (!asset)
            {
                Debug.LogError($"Localization table not found at Resources/{TABLE_RESOURCE_PATH}");
                return result;
            }

            foreach (var rawLine in asset.text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var cells = line.Split('\t');
                if (cells.Length < 2) continue;

                var values = new string[cells.Length - 1];
                for (int i = 1; i < cells.Length; i++)
                {
                    values[i - 1] = cells[i].Replace("\\n", "\n");
                }
                result[cells[0].Trim()] = values;
            }
            return result;
        }

        //Enter Play Mode Options may skip the domain reload, so drop static state between play sessions
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            table = null;
            currentLanguage = null;
            OnLanguageChanged = null;
        }
    }
}
