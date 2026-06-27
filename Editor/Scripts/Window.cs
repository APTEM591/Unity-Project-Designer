#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameSpear.ProjectDesigner.Editor
{
    // Customization window for the Project Designer: toggles, folder-emblem size/corner/icon overrides,
    // and tree-line styling. Fully self-contained.
    internal sealed class Window : EditorWindow
    {
        private Vector2 _scroll;

        // Above this capture rate, warn that previews get heavier (more frames per atlas, more repaints).
        private const int HighFpsWarning = 18;

        [MenuItem("Window/Project Designer")]
        public static void Open()
        {
            Window window = GetWindow<Window>(false, "Project Designer");
            window.minSize = new Vector2(320f, 380f);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Project Designer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Folder content icons, tree lines and row striping for the Project window.", EditorStyles.miniLabel);
            EditorGUILayout.Space(6f);

            DrawGeneral();
            EditorGUILayout.Space(8f);
            DrawFolderIcons();
            EditorGUILayout.Space(8f);
            DrawFolderColors();
            EditorGUILayout.Space(8f);
            DrawTree();
            EditorGUILayout.Space(8f);
            DrawRows();
            EditorGUILayout.Space(12f);

            if (GUILayout.Button("Reset to Defaults") &&
                EditorUtility.DisplayDialog("Project Designer", "Reset all Project Designer settings to their defaults?", "Reset", "Cancel"))
            {
                Settings.ResetToDefaults();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawGeneral()
        {
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            Settings.Enabled = EditorGUILayout.Toggle("Enabled", Settings.Enabled);
            using (new EditorGUI.DisabledScope(!Settings.Enabled))
            {
                Settings.FolderIconsEnabled = EditorGUILayout.Toggle("Folder Content Icons", Settings.FolderIconsEnabled);
                Settings.TreeEnabled = EditorGUILayout.Toggle("Tree Branch Lines", Settings.TreeEnabled);
                Settings.RowsEnabled = EditorGUILayout.Toggle(new GUIContent("Alternating Rows", "Faint row striping. Best in one-column layout."), Settings.RowsEnabled);
                Settings.FolderColorsEnabled = EditorGUILayout.Toggle(new GUIContent("Folder Colors", "Tint specific folders' icons a custom color."), Settings.FolderColorsEnabled);
                Settings.UiPreviewEnabled = EditorGUILayout.Toggle(new GUIContent("UI Prefab Previews", "Render a thumbnail for UI/Canvas prefabs, which Unity leaves blank in the Project window. Other prefabs use Unity's own preview."), Settings.UiPreviewEnabled);
                Settings.ParticlePreviewEnabled = EditorGUILayout.Toggle(new GUIContent("Particle Previews", "Render a short looping animation for particle-system prefabs, which Unity shows un-simulated."), Settings.ParticlePreviewEnabled);
                using (new EditorGUI.DisabledScope(!Settings.ParticlePreviewEnabled))
                {
                    EditorGUI.indentLevel++;
                    Settings.ParticlePreviewFps = EditorGUILayout.IntSlider(new GUIContent("Animation FPS", "Frames sampled per second of the effect. Higher captures MORE frames (smoother) — the preview always plays at real-time speed, so it doesn't get faster. Repaints only while a particle preview is visible."), Settings.ParticlePreviewFps, 2, 24);
                    if (Settings.ParticlePreviewEnabled && Settings.ParticlePreviewFps >= HighFpsWarning)
                        EditorGUILayout.HelpBox("High FPS captures more frames per preview (more memory, slower to generate) and repaints the Project window more often while a particle preview is visible. 12 is usually plenty.", MessageType.Warning);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private static void DrawFolderIcons()
        {
            EditorGUILayout.LabelField("Folder Content Icons", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Settings.Enabled || !Settings.FolderIconsEnabled))
            {
                Settings.EmblemSize = EditorGUILayout.Slider(new GUIContent("Emblem Size", "Emblem size as a fraction of the folder icon."), Settings.EmblemSize, 0.25f, 1f);
                Settings.EmblemCorner = (EmblemCorner)EditorGUILayout.EnumPopup("Emblem Corner", Settings.EmblemCorner);
                Settings.RecursiveClassification = EditorGUILayout.Toggle(new GUIContent("Recursive Detection", "Classify by all assets beneath the folder, not just its direct children."), Settings.RecursiveClassification);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(new GUIContent("Icon Overrides", "Assign a custom texture per content type. Leave empty to use the built-in icon."), EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                foreach (FolderIcon.ContentCategory category in FolderIcon.AllCategories)
                {
                    DrawIconOverrideRow(category);
                }
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawIconOverrideRow(FolderIcon.ContentCategory category)
        {
            string guid = Settings.GetIconOverrideGuid(category);
            Texture2D current = null;
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) current = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            EditorGUI.BeginChangeCheck();
            Texture2D picked = (Texture2D)EditorGUILayout.ObjectField(category.ToString(), current, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                string newGuid = string.Empty;
                if (picked != null)
                {
                    string path = AssetDatabase.GetAssetPath(picked);
                    if (!string.IsNullOrEmpty(path)) newGuid = AssetDatabase.AssetPathToGUID(path);
                }
                Settings.SetIconOverrideGuid(category, newGuid);
            }
        }

        // A pleasant default applied when a folder is first added to the override list.
        private static readonly Color DefaultFolderColor = new Color(0.40f, 0.60f, 0.95f);

        private static void DrawFolderColors()
        {
            EditorGUILayout.LabelField("Folder Colors", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Settings.Enabled || !Settings.FolderColorsEnabled))
            {
                Settings.FolderColorStrength = EditorGUILayout.Slider(new GUIContent("Tint Strength", "How strongly the color recolors the folder icon."), Settings.FolderColorStrength, 0.25f, 1f);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(new GUIContent("Per-Folder Overrides", "Assign a color to specific folders. Tip: right-click a folder in the Project window → Project Designer → Folder Color."), EditorStyles.miniBoldLabel);

                EditorGUI.indentLevel++;
                string removeGuid = null;
                foreach (string guid in Settings.GetFolderColorGuids())
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) { removeGuid = guid; continue; } // folder deleted -> drop stale entry
                    if (DrawFolderColorRow(guid, path)) removeGuid = guid;
                }
                if (removeGuid != null) Settings.ClearFolderColor(removeGuid);

                // Drop a folder here (or pick one) to start tracking it with the default color.
                DefaultAsset added = (DefaultAsset)EditorGUILayout.ObjectField("Add Folder", null, typeof(DefaultAsset), false);
                if (added != null)
                {
                    string path = AssetDatabase.GetAssetPath(added);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        if (!Settings.TryGetFolderColor(guid, out _)) Settings.SetFolderColor(guid, DefaultFolderColor);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        // One override row: folder name (click to ping), color swatch, remove button. Returns true when
        // the user pressed remove.
        private static bool DrawFolderColorRow(string guid, string path)
        {
            Settings.TryGetFolderColor(guid, out Color current);
            bool remove = false;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(System.IO.Path.GetFileName(path), path), EditorStyles.label, GUILayout.MinWidth(60f)))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
            }

            EditorGUI.BeginChangeCheck();
            Color edited = EditorGUILayout.ColorField(GUIContent.none, current, true, false, false, GUILayout.Width(70f));
            if (EditorGUI.EndChangeCheck()) Settings.SetFolderColor(guid, edited);

            if (GUILayout.Button(new GUIContent("✕", "Remove"), GUILayout.Width(22f))) remove = true;
            EditorGUILayout.EndHorizontal();
            return remove;
        }

        private static void DrawRows()
        {
            EditorGUILayout.LabelField("Alternating Rows", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Settings.Enabled || !Settings.RowsEnabled))
            {
                Settings.RowColor = EditorGUILayout.ColorField(new GUIContent("Row Color", "Tint applied to every other row. Lower the alpha for subtler striping."), Settings.RowColor);
            }
        }

        private static void DrawTree()
        {
            EditorGUILayout.LabelField("Tree Branch Lines", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Settings.Enabled || !Settings.TreeEnabled))
            {
                Settings.TreeMode = (PD_TreeMode)EditorGUILayout.EnumPopup(new GUIContent("Tree Mode", "Minimal: vertical guides only. Default: elbows (T / L) into each item."), Settings.TreeMode);
                Settings.LineStyle = (PD_LineStyle)EditorGUILayout.EnumPopup("Line Style", Settings.LineStyle);
                Settings.LineThickness = EditorGUILayout.Slider("Line Thickness", Settings.LineThickness, 1f, 3f);
                Settings.TreeColor = EditorGUILayout.ColorField("Line Color", Settings.TreeColor);
                Settings.IndentWidth = EditorGUILayout.Slider(new GUIContent("Indent Width", "Pixels per tree level. Adjust if the lines don't sit under the foldout arrows."), Settings.IndentWidth, 8f, 24f);
            }
        }
    }
}
#endif
