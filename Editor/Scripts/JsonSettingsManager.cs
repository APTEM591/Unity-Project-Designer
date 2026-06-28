#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameSpear.ProjectDesigner.Editor
{
    internal static class JsonSettingsManager
    {
        private static readonly string SettingsFolderPath = Path.Combine(Application.dataPath, "Project Designer", "Settings");
        private static readonly string SettingsFilePath = Path.Combine(SettingsFolderPath, "ProjectDesigner.json");

        private static SettingsData _cache;
        private static bool _isLoaded;

        [System.Serializable]
        private class SettingsData : ISerializationCallbackReceiver
        {
            public List<string> boolKeys = new List<string>();
            public List<bool> boolValues = new List<bool>();
            public List<string> intKeys = new List<string>();
            public List<int> intValues = new List<int>();
            public List<string> floatKeys = new List<string>();
            public List<float> floatValues = new List<float>();
            public List<string> stringKeys = new List<string>();
            public List<string> stringValues = new List<string>();

            public Dictionary<string, bool> boolDict = new Dictionary<string, bool>();
            public Dictionary<string, int> intDict = new Dictionary<string, int>();
            public Dictionary<string, float> floatDict = new Dictionary<string, float>();
            public Dictionary<string, string> stringDict = new Dictionary<string, string>();

            public void OnBeforeSerialize()
            {
                boolKeys.Clear(); boolValues.Clear();
                foreach (var kvp in boolDict) { boolKeys.Add(kvp.Key); boolValues.Add(kvp.Value); }
                
                intKeys.Clear(); intValues.Clear();
                foreach (var kvp in intDict) { intKeys.Add(kvp.Key); intValues.Add(kvp.Value); }
                
                floatKeys.Clear(); floatValues.Clear();
                foreach (var kvp in floatDict) { floatKeys.Add(kvp.Key); floatValues.Add(kvp.Value); }
                
                stringKeys.Clear(); stringValues.Clear();
                foreach (var kvp in stringDict) { stringKeys.Add(kvp.Key); stringValues.Add(kvp.Value); }
            }

            public void OnAfterDeserialize()
            {
                boolDict.Clear();
                for (int i = 0; i < Math.Min(boolKeys.Count, boolValues.Count); i++) boolDict[boolKeys[i]] = boolValues[i];
                
                intDict.Clear();
                for (int i = 0; i < Math.Min(intKeys.Count, intValues.Count); i++) intDict[intKeys[i]] = intValues[i];
                
                floatDict.Clear();
                for (int i = 0; i < Math.Min(floatKeys.Count, floatValues.Count); i++) floatDict[floatKeys[i]] = floatValues[i];
                
                stringDict.Clear();
                for (int i = 0; i < Math.Min(stringKeys.Count, stringValues.Count); i++) stringDict[stringKeys[i]] = stringValues[i];
            }
        }

        static JsonSettingsManager()
        {
            Load();
        }

        private static void Load()
        {
            if (_isLoaded) return;

            _cache = new SettingsData();
            _isLoaded = true;

            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    _cache = JsonUtility.FromJson<SettingsData>(json) ?? new SettingsData();
                }
                catch
                {
                    _cache = new SettingsData();
                }
            }
        }

        private static void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsFolderPath))
                {
                    Directory.CreateDirectory(SettingsFolderPath);
                }

                string json = JsonUtility.ToJson(_cache, prettyPrint: true);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
            }
        }

        #region Accessors
        public static bool GetBool(string key, bool fallback)
        {
            Load();
            return _cache.boolDict.TryGetValue(key, out bool value) ? value : fallback;
        }

        public static void SetBool(string key, bool value)
        {
            Load();
            _cache.boolDict[key] = value;
            Save();
        }

        public static int GetInt(string key, int fallback)
        {
            Load();
            return _cache.intDict.TryGetValue(key, out int value) ? value : fallback;
        }

        public static void SetInt(string key, int value)
        {
            Load();
            _cache.intDict[key] = value;
            Save();
        }

        public static float GetFloat(string key, float fallback)
        {
            Load();
            return _cache.floatDict.TryGetValue(key, out float value) ? value : fallback;
        }

        public static void SetFloat(string key, float value)
        {
            Load();
            _cache.floatDict[key] = value;
            Save();
        }

        public static string GetString(string key, string fallback)
        {
            Load();
            return _cache.stringDict.TryGetValue(key, out string value) ? value : fallback;
        }

        public static void SetString(string key, string value)
        {
            Load();
            _cache.stringDict[key] = value ?? string.Empty;
            Save();
        }

        public static Color GetColor(string key, Color fallback)
        {
            string stored = GetString(key, string.Empty);
            return !string.IsNullOrEmpty(stored) && ColorUtility.TryParseHtmlString(stored, out Color parsed) ? parsed : fallback;
        }

        public static void SetColor(string key, Color value)
        {
            SetString(key, "#" + ColorUtility.ToHtmlStringRGBA(value));
        }

        public static void DeleteKey(string key)
        {
            Load();
            _cache.boolDict.Remove(key);
            _cache.intDict.Remove(key);
            _cache.floatDict.Remove(key);
            _cache.stringDict.Remove(key);
            Save();
        }
        #endregion
    }
}
#endif