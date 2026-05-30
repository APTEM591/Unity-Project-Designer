#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameSpear.ProjectDesigner.Editor
{
    internal enum EmblemCorner { BottomRight, BottomLeft, TopRight, TopLeft }
    internal enum PD_TreeMode { Minimal, Default }

    // Central, JSON-backed settings store for the Project Designer. Fully self-contained:
    // it has no dependency on any other package. All values are exposed in PD_Window.
    internal static class Settings
    {
        private const string Prefix = "ProjectDesigner_";
        public const string MenuRoot = "Tools/Project Designer/";

        #region General
        public static bool Enabled { get => GetBool("Enabled", true); set => SetBool("Enabled", value); }
        public static bool FolderIconsEnabled { get => GetBool("FolderIcons", true); set => SetBool("FolderIcons", value); }
        public static bool TreeEnabled { get => GetBool("Tree", true); set => SetBool("Tree", value); }
        // Off by default: alternating shading leaves a faint edge in the two-column layout.
        public static bool RowsEnabled { get => GetBool("Rows", false); set => SetBool("Rows", value); }
        public static bool FolderColorsEnabled { get => GetBool("FolderColors", true); set => SetBool("FolderColors", value); }
        #endregion

        #region Folder Emblem
        public static float EmblemSize { get => GetFloat("EmblemSize", 0.5f); set => SetFloat("EmblemSize", Mathf.Clamp(value, 0.25f, 1f)); }
        public static EmblemCorner EmblemCorner { get => (EmblemCorner)GetInt("EmblemCorner", (int)EmblemCorner.BottomRight); set => SetInt("EmblemCorner", (int)value); }

        public static bool RecursiveClassification
        {
            get => GetBool("Recursive", true);
            set { SetBool("Recursive", value); FolderIcon.ClearCache(); }
        }

        public static string GetIconOverrideGuid(FolderIcon.ContentCategory category) => JsonSettingsManager.GetString(Prefix + "Icon_" + category, string.Empty);
        public static void SetIconOverrideGuid(FolderIcon.ContentCategory category, string guid)
        {
            JsonSettingsManager.SetString(Prefix + "Icon_" + category, guid ?? string.Empty);
            Repaint();
        }
        #endregion

        #region Folder Colors
        // How strongly an assigned color recolors the folder icon (used as the tint's alpha).
        public static float FolderColorStrength { get => GetFloat("FolderColorStrength", 0.7f); set => SetFloat("FolderColorStrength", Mathf.Clamp(value, 0.25f, 1f)); }

        private const string FolderColorIndexKey = "FolderColorIndex";

        // Per-folder color overrides, keyed by asset GUID (stable across renames / moves). A separate
        // index key tracks which GUIDs currently carry a color, so the window can enumerate them and
        // ResetToDefaults can clear them.
        public static bool TryGetFolderColor(string guid, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(guid)) return false;
            string stored = JsonSettingsManager.GetString(Prefix + "FolderColor_" + guid, string.Empty);
            return !string.IsNullOrEmpty(stored) && ColorUtility.TryParseHtmlString(stored, out color);
        }

        public static void SetFolderColor(string guid, Color color)
        {
            if (string.IsNullOrEmpty(guid)) return;
            JsonSettingsManager.SetString(Prefix + "FolderColor_" + guid, "#" + ColorUtility.ToHtmlStringRGBA(color));
            AddToFolderColorIndex(guid);
            Repaint();
        }

        public static void ClearFolderColor(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            JsonSettingsManager.DeleteKey(Prefix + "FolderColor_" + guid);
            RemoveFromFolderColorIndex(guid);
            Repaint();
        }

        public static List<string> GetFolderColorGuids()
        {
            List<string> guids = new();
            string raw = JsonSettingsManager.GetString(Prefix + FolderColorIndexKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return guids;
            foreach (string guid in raw.Split(';'))
            {
                if (!string.IsNullOrEmpty(guid)) guids.Add(guid);
            }
            return guids;
        }

        private static void AddToFolderColorIndex(string guid)
        {
            List<string> guids = GetFolderColorGuids();
            if (guids.Contains(guid)) return;
            guids.Add(guid);
            JsonSettingsManager.SetString(Prefix + FolderColorIndexKey, string.Join(";", guids));
        }

        private static void RemoveFromFolderColorIndex(string guid)
        {
            List<string> guids = GetFolderColorGuids();
            if (guids.Remove(guid))
            {
                JsonSettingsManager.SetString(Prefix + FolderColorIndexKey, string.Join(";", guids));
            }
        }
        #endregion

        #region Alternating Rows
        // Tint applied to every other row. Falls back to a faint, skin-matched shade until the user picks
        // one; alpha controls how subtle the striping is.
        public static Color RowColor { get => GetColor("RowColor", Util.RowColor); set => SetColor("RowColor", value); }
        #endregion

        #region Tree
        public static float IndentWidth { get => GetFloat("IndentWidth", 14f); set => SetFloat("IndentWidth", Mathf.Clamp(value, 8f, 24f)); }
        public static PD_TreeMode TreeMode { get => (PD_TreeMode)GetInt("TreeMode", (int)PD_TreeMode.Minimal); set => SetInt("TreeMode", (int)value); }
        public static PD_LineStyle LineStyle { get => (PD_LineStyle)GetInt("LineStyle", (int)PD_LineStyle.Dotted); set => SetInt("LineStyle", (int)value); }
        public static float LineThickness { get => GetFloat("LineThickness", 1f); set => SetFloat("LineThickness", Mathf.Clamp(value, 1f, 3f)); }
        public static Color TreeColor { get => GetColor("TreeColor", new Color(1f, 1f, 1f, 0.43f)); set => SetColor("TreeColor", value); }
        #endregion

        #region Reset
        public static void ResetToDefaults()
        {
            Enabled = true;
            FolderIconsEnabled = true;
            TreeEnabled = true;
            RowsEnabled = false;
            // Drop any custom row tint so it reverts to the skin-matched default.
            JsonSettingsManager.DeleteKey(Prefix + "RowColor");
            EmblemSize = 0.5f;
            EmblemCorner = EmblemCorner.BottomRight;
            RecursiveClassification = true;
            IndentWidth = 14f;
            TreeMode = PD_TreeMode.Minimal;
            LineStyle = PD_LineStyle.Dotted;
            LineThickness = 1f;
            TreeColor = new Color(1f, 1f, 1f, 0.43f);
            FolderColorsEnabled = true;
            FolderColorStrength = 0.7f;
            foreach (FolderIcon.ContentCategory category in FolderIcon.AllCategories)
            {
                SetIconOverrideGuid(category, string.Empty);
            }
            foreach (string folderGuid in GetFolderColorGuids())
            {
                JsonSettingsManager.DeleteKey(Prefix + "FolderColor_" + folderGuid);
            }
            JsonSettingsManager.DeleteKey(Prefix + FolderColorIndexKey);
        }
        #endregion

        #region Storage Helpers
        private static bool GetBool(string key, bool fallback) => JsonSettingsManager.GetBool(Prefix + key, fallback);
        private static void SetBool(string key, bool value) { JsonSettingsManager.SetBool(Prefix + key, value); Repaint(); }
        private static int GetInt(string key, int fallback) => JsonSettingsManager.GetInt(Prefix + key, fallback);
        private static void SetInt(string key, int value) { JsonSettingsManager.SetInt(Prefix + key, value); Repaint(); }
        private static float GetFloat(string key, float fallback) => JsonSettingsManager.GetFloat(Prefix + key, fallback);
        private static void SetFloat(string key, float value) { JsonSettingsManager.SetFloat(Prefix + key, value); Repaint(); }
        private static Color GetColor(string key, Color fallback)
        {
            string stored = JsonSettingsManager.GetString(Prefix + key, string.Empty);
            return !string.IsNullOrEmpty(stored) && ColorUtility.TryParseHtmlString(stored, out Color parsed) ? parsed : fallback;
        }
        private static void SetColor(string key, Color value) { JsonSettingsManager.SetString(Prefix + key, "#" + ColorUtility.ToHtmlStringRGBA(value)); Repaint(); }
        private static void Repaint() => EditorApplication.RepaintProjectWindow();
        #endregion

        #region Menu
        [MenuItem(MenuRoot + "Settings...", priority = 0)]
        private static void OpenSettings() => Window.Open();

        [MenuItem(MenuRoot + "Enabled", priority = 20)]
        private static void ToggleEnabled() => Enabled = !Enabled;
        [MenuItem(MenuRoot + "Enabled", validate = true)]
        private static bool ToggleEnabledValidate() { Menu.SetChecked(MenuRoot + "Enabled", Enabled); return true; }

        [MenuItem(MenuRoot + "Folder Content Icons", priority = 21)]
        private static void ToggleFolderIcons() => FolderIconsEnabled = !FolderIconsEnabled;
        [MenuItem(MenuRoot + "Folder Content Icons", validate = true)]
        private static bool ToggleFolderIconsValidate() { Menu.SetChecked(MenuRoot + "Folder Content Icons", FolderIconsEnabled); return Enabled; }

        [MenuItem(MenuRoot + "Tree Branch Lines", priority = 22)]
        private static void ToggleTree() => TreeEnabled = !TreeEnabled;
        [MenuItem(MenuRoot + "Tree Branch Lines", validate = true)]
        private static bool ToggleTreeValidate() { Menu.SetChecked(MenuRoot + "Tree Branch Lines", TreeEnabled); return Enabled; }

        [MenuItem(MenuRoot + "Alternating Rows", priority = 23)]
        private static void ToggleRows() => RowsEnabled = !RowsEnabled;
        [MenuItem(MenuRoot + "Alternating Rows", validate = true)]
        private static bool ToggleRowsValidate() { Menu.SetChecked(MenuRoot + "Alternating Rows", RowsEnabled); return Enabled; }

        [MenuItem(MenuRoot + "Folder Colors", priority = 24)]
        private static void ToggleFolderColors() => FolderColorsEnabled = !FolderColorsEnabled;
        [MenuItem(MenuRoot + "Folder Colors", validate = true)]
        private static bool ToggleFolderColorsValidate() { Menu.SetChecked(MenuRoot + "Folder Colors", FolderColorsEnabled); return Enabled; }
        #endregion
    }
}
#endif
