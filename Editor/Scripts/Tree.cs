#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameSpear.ProjectDesigner.Editor
{
    // Draws tree connector lines in the Project window. Structure (siblings / depth / last-sibling) is
    // derived from the asset path; the lines are drawn procedurally (no texture assets) using the
    // Project Designer's own style settings, so the package is fully self-contained.
    internal static class Tree
    {
        internal const float BaseIndentX = 16f;

        private static readonly Dictionary<string, List<string>> siblingsCache = new();

        public static void ClearCache() => siblingsCache.Clear();

        public static bool CheckTwoColumn() => PD_ProjectBrowser.IsTwoColumn();

        // True only when the item's visual indent matches its real asset depth, i.e. we are in a genuine
        // tree view (one-column tree or two-column folder panel) and not the flat right-panel listing.
        public static bool ShouldDraw(string path, Rect selectionRect, out int depth)
        {
            depth = GetDepth(path);
            if (depth < 1) return false;
            int visualDepth = Mathf.RoundToInt((selectionRect.x - BaseIndentX) / Settings.IndentWidth);
            return visualDepth == depth;
        }

        public static void Draw(string path, Rect selectionRect, int depth)
        {
            Color previous = GUI.color;
            GUI.color = Settings.TreeColor;

            float indent = Settings.IndentWidth;
            float thickness = Settings.LineThickness;
            PD_LineStyle style = Settings.LineStyle;

            // The two-column layout's left panel lists folders only; the one-column tree also lists files.
            // Match it so the "last sibling" and foldout decisions reflect what is actually displayed,
            // otherwise a hidden trailing file makes the last visible folder render as a mid-branch.
            bool foldersOnly = PD_ProjectBrowser.IsTwoColumn();

            float top = selectionRect.y;
            float height = selectionRect.height;
            float midY = top + (height * 0.5f);

            // The connector column for this item, aligned to the foldout-arrow column. This matches the
            // original placement (one indent plus half a row left of the icon); the branch textures had
            // their line on their left edge, so the effective position was indent + halfRow, not indent.
            float itemX = selectionRect.x - indent - (height * 0.5f);

            if (Settings.TreeMode == PD_TreeMode.Minimal)
            {
                // Plain vertical guide at the item's level.
                Editor.Draw.VerticalLine(itemX, top, height, thickness, style);
            }
            else
            {
                // Default: an elbow into the item. Last sibling => "L" (top-half vertical); otherwise "T" (full vertical).
                bool last = IsLastSibling(path, foldersOnly);
                Editor.Draw.VerticalLine(itemX, top, last ? (height * 0.5f) : height, thickness, style);

                // End the elbow at the foldout-arrow column for expandable folders so the connector meets
                // the arrow instead of being drawn across (through) it; for arrow-less items (files, empty
                // folders) run it all the way to the icon so they still read as connected.
                float horizontalEnd = HasFoldoutArrow(path, foldersOnly) ? (selectionRect.x - indent) : selectionRect.x;
                Editor.Draw.HorizontalLine(itemX, midY, horizontalEnd - itemX, thickness, style);
            }

            // Vertical continuation lines for every ancestor that still has siblings below it.
            float columnX = itemX;
            for (int ancestorDepth = depth - 1; ancestorDepth >= 1; ancestorDepth--)
            {
                columnX -= indent;
                string ancestor = GetAncestorAtDepth(path, ancestorDepth);
                if (!IsLastSibling(ancestor, foldersOnly))
                {
                    Editor.Draw.VerticalLine(columnX, top, height, thickness, style);
                }
            }

            GUI.color = previous;
        }

        internal static int GetDepth(string path)
        {
            int slashes = 0;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '/') slashes++;
            }
            return slashes;
        }

        private static string GetAncestorAtDepth(string path, int targetDepth)
        {
            int index = -1;
            for (int i = 0; i < targetDepth + 1; i++)
            {
                index = path.IndexOf('/', index + 1);
                if (index < 0) return path;
            }
            return path.Substring(0, index);
        }

        private static string GetParentPath(string path)
        {
            int index = path.LastIndexOf('/');
            return index <= 0 ? null : path.Substring(0, index);
        }

        private static bool IsLastSibling(string path, bool foldersOnly)
        {
            string parent = GetParentPath(path);
            if (parent == null) return true;
            List<string> siblings = GetOrderedSiblings(parent, foldersOnly);
            return siblings.Count == 0 || siblings[siblings.Count - 1] == path;
        }

        // A folder shows a foldout arrow when the Project window can expand it, i.e. it has a visible
        // child. The two-column folder panel shows only sub-folders; the one-column tree also shows files.
        // Reuses the (cached) sibling enumeration for the matching view.
        private static bool HasFoldoutArrow(string path, bool foldersOnly)
        {
            return AssetDatabase.IsValidFolder(path) && GetOrderedSiblings(path, foldersOnly).Count > 0;
        }

        // Direct children of a folder as the Project window lists them (natural, case-insensitive name
        // sort). 'foldersOnly' mirrors the two-column left panel, which omits files; the one-column tree
        // includes them. Cached separately per view so a layout switch picks up the right set.
        private static List<string> GetOrderedSiblings(string parentFolder, bool foldersOnly)
        {
            string key = (foldersOnly ? "F|" : "A|") + parentFolder;
            if (siblingsCache.TryGetValue(key, out List<string> cached)) return cached;

            List<string> children = new();
            children.AddRange(AssetDatabase.GetSubFolders(parentFolder));

            if (!foldersOnly && parentFolder.StartsWith("Assets"))
            {
                string absolute = Application.dataPath + parentFolder.Substring("Assets".Length);
                if (Directory.Exists(absolute))
                {
                    foreach (string file in Directory.EnumerateFiles(absolute))
                    {
                        if (file.EndsWith(".meta")) continue;
                        string relative = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
                        children.Add(relative);
                    }
                }
            }

            children.Sort((a, b) => EditorUtility.NaturalCompare(GetName(a), GetName(b)));
            siblingsCache[key] = children;
            return children;
        }

        private static string GetName(string path)
        {
            int index = path.LastIndexOf('/');
            return index < 0 ? path : path.Substring(index + 1);
        }

        // Reads the Project Browser's current layout via an internal field so connectors can match what
        // the window actually shows (the two-column left panel lists folders only). Defensive: if the
        // field is unavailable on this Unity version it reports false (one-column / include files),
        // preserving the previous behavior. Reflection is throttled to a few times per second.
        private static class PD_ProjectBrowser
        {
            private static System.Type type;
            private static FieldInfo viewModeField;
            private static double lastCheck = -1d;
            private static bool twoColumn;

            public static bool IsTwoColumn()
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - lastCheck < 0.25d) return twoColumn;
                lastCheck = now;

                if (type == null)
                {
                    type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
                    viewModeField = type?.GetField("m_ViewMode", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (viewModeField == null) { twoColumn = false; return false; }

                // Prefer the focused browser; otherwise (the common single-browser case) require the open
                // browsers to agree, falling back to the previous behavior when they don't.
                EditorWindow focused = EditorWindow.focusedWindow;
                if (focused != null && focused.GetType() == type)
                {
                    twoColumn = IsTwoColumns(focused);
                    return twoColumn;
                }

                Object[] all = Resources.FindObjectsOfTypeAll(type);
                bool anyOne = false, anyTwo = false;
                foreach (Object window in all)
                {
                    if (IsTwoColumns((EditorWindow)window)) anyTwo = true;
                    else anyOne = true;
                }
                twoColumn = anyTwo && !anyOne;
                return twoColumn;
            }

            private static bool IsTwoColumns(EditorWindow browser)
            {
                object value = viewModeField.GetValue(browser);
                return value != null && (int)value == 1; // ProjectBrowser.ViewMode.TwoColumns
            }
        }
    }
}
#endif
