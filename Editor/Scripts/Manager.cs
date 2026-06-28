#if UNITY_EDITOR
using GameSpear.ProjectDesigner.Editor;
using UnityEditor;
using UnityEngine;

namespace GameSpear.ProjectDesigner.Editor
{
    // Core of the Project Designer. Hooks the Project window draw callback and layers on, in order:
    //   1. alternating row shading   2. tree-branch connector lines   3. content-based folder icons.
    // Fully self-contained; no dependency on any other package.
    internal static class Manager
    {
        private const float LIST_ROW_MAX_HEIGHT = 20f;
        private const float LIST_ICON_SIZE = 16f;

        public static void Initialize()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            PrefabPreview.Initialize();
        }

        private static void OnProjectChanged()
        {
            FolderIcon.ClearCache();
            Tree.ClearCache();
            PrefabPreview.ClearCache();
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (!Settings.Enabled) return;
            if (Event.current.type != EventType.Repaint) return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            bool isListMode = selectionRect.height <= LIST_ROW_MAX_HEIGHT;
            bool isRow = false;

            // 1. Alternating rows.
            if (Settings.RowsEnabled && isListMode)
            {
                isRow = (((int)(selectionRect.y / selectionRect.height)) & 1) == 1;
                if (isRow)
                {
                    // Overshoot the reported view width so the stripe reaches the panel's right edge
                    // (it is clipped to the panel) rather than stopping a scrollbar-width short.
                    Util.FillRect(new Rect(0f, selectionRect.y, EditorGUIUtility.currentViewWidth + 32f, selectionRect.height), Settings.RowColor);
                }
            }

            // 2. Tree branch connector lines.
            if (Settings.TreeEnabled && isListMode)
            {
                if (Tree.ShouldDraw(path, selectionRect, out int depth))
                {
                    Tree.Draw(path, selectionRect, depth);
                }
            }

            bool isFolder = AssetDatabase.IsValidFolder(path);
            
            // For two-column mode icon rect calculations, determine if this item is in a tree view
            // (left panel) or flat listing (right panel). In a tree view, visualDepth matches depth.
            // In a flat listing, they differ (this is where icons need correction).
            bool isInTreeView = false;
            int itemDepth = Tree.GetDepth(path);
            if (itemDepth > 0 && isListMode && Tree.CheckTwoColumn())
            {
                int visualDepth = Mathf.RoundToInt((selectionRect.x - Tree.BaseIndentX) / Settings.IndentWidth);
                isInTreeView = (visualDepth == itemDepth);
            }

            // 3. Prefab preview: draw a custom thumbnail for prefabs Unity leaves blank (UI/Canvas) and an
            //    animated one for particle systems, in both list and grid layouts. TryGetPreview returns
            //    true only for those (respecting each type's toggle, with 'uv' selecting the current
            //    animation frame); every other prefab keeps Unity's own native preview / generic icon.
            if (!isFolder && PrefabPreview.IsPrefab(path) &&
                PrefabPreview.TryGetPreview(guid, out Texture2D preview, out Rect previewUv))
            {
                Rect iconRect = GetIconRect(selectionRect, isListMode, isInTreeView);
                Color previousColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTextureWithTexCoords(iconRect, preview, previewUv);
                GUI.color = previousColor;
            }

            // 4. Folder color: recolor the folder icon for folders the user has assigned a color.
            if (Settings.FolderColorsEnabled && isFolder &&
                Settings.TryGetFolderColor(guid, out Color folderColor))
            {
                Texture2D folderTex = Util.FolderIcon;
                if (folderTex != null)
                {
                    Rect iconRect = GetIconRect(selectionRect, isListMode, isInTreeView);
                    Color previousColor = GUI.color;
                    // Multiply tint via GUI.color, drawn at the configured strength (used as alpha) over
                    // Unity's own folder icon, so the result blends toward a soft tint rather than a hard,
                    // dark multiply, while keeping the folder's built-in shading.
                    GUI.color = new Color(folderColor.r, folderColor.g, folderColor.b, Settings.FolderColorStrength);
                    GUI.DrawTexture(iconRect, folderTex, ScaleMode.ScaleToFit);
                    GUI.color = previousColor;
                }
            }

            // 5. Content-based folder icons: a small emblem over the folder icon's corner, keeping
            //    Unity's folder icon (skip the "Assets"/"Packages" roots at depth 0).
            if (Settings.FolderIconsEnabled && isFolder && Tree.GetDepth(path) >= 1)
            {
                FolderIcon.ContentCategory category = FolderIcon.GetCategory(path);
                if (category != FolderIcon.ContentCategory.None)
                {
                    Texture2D icon = FolderIcon.GetCategoryIcon(category);
                    if (icon != null)
                    {
                        Rect iconRect = GetIconRect(selectionRect, isListMode, isInTreeView);
                        Rect emblem = GetEmblemRect(iconRect);
                        Color previousColor = GUI.color;

                        // Slight drop shadow: the same emblem tinted black and offset down-right, drawn
                        // underneath. A black GUI.color zeroes the texture's RGB while keeping its (halved)
                        // alpha, so the shadow exactly follows the icon's silhouette. Offset scales with the
                        // emblem so it stays subtle at any icon size.
                        float shadowOffset = Mathf.Max(1f, emblem.width * 0.08f);
                        Rect shadow = new Rect(emblem.x + shadowOffset, emblem.y + shadowOffset, emblem.width, emblem.height);
                        GUI.color = new Color(0f, 0f, 0f, 0.5f);
                        GUI.DrawTexture(shadow, icon, ScaleMode.ScaleToFit);

                        GUI.color = Color.white;
                        GUI.DrawTexture(emblem, icon, ScaleMode.ScaleToFit);
                        GUI.color = previousColor;
                    }
                }
            }
        }

        // The asset icon's square within an item's rect: a 16px icon at selectionRect.x, vertically
        // centered, in list mode; or the top (width-sized) square of the cell in grid mode (the extra
        // cell height below it is the label). Used for folder recolor/emblems and for prefab previews.
        // In two-column mode's right panel, items are shown flat and visualDepth != itemDepth.
        // We must recalculate icon position based on tree depth for consistency.
        private static Rect GetIconRect(Rect selectionRect, bool isListMode, bool isInTreeView)
        {
            if (isListMode)
            {
                float side = Mathf.Min(LIST_ICON_SIZE, selectionRect.height);
                float top = selectionRect.y + (selectionRect.height - side) * 0.5f;
                float left = selectionRect.x;
                
                // In two-column mode, check if we're in the flat right panel by comparing
                // visual depth to actual depth. If they differ, we're in the flat listing.
                if (!isInTreeView)
                {
                    // We're in the flat right panel listing. Recalculate icon position based on tree depth.
                    float baseIndent = Tree.BaseIndentX;
                    left = baseIndent;
                }
                
                return new Rect(left, top, side, side);
            }

            float iconSide = selectionRect.width;
            return new Rect(selectionRect.x, selectionRect.y, iconSide, iconSide);
        }

        // The emblem rect: a square of EmblemSize anchored to the configured corner of the folder icon.
        private static Rect GetEmblemRect(Rect folderIconRect)
        {
            return CornerRect(folderIconRect, folderIconRect.width * Settings.EmblemSize);
        }

        // A square of the given size placed in the configured corner of 'host'.
        private static Rect CornerRect(Rect host, float size)
        {
            EmblemCorner corner = Settings.EmblemCorner;
            bool right = corner is EmblemCorner.BottomRight or EmblemCorner.TopRight;
            bool bottom = corner is EmblemCorner.BottomLeft or EmblemCorner.BottomRight;
            float x = right ? host.xMax - size : host.xMin;
            float y = bottom ? host.yMax - size : host.yMin;
            return new Rect(x, y, size, size);
        }
    }
}
#endif
