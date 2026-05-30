#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameSpear.ProjectDesigner.Editor
{
    internal static class Util
    {
        // Faint alternating-row tint, matched to the editor skin.
        public static Color RowColor => EditorGUIUtility.isProSkin
            ? new Color(0f, 0f, 0f, 0.08f)
            : new Color(1f, 1f, 1f, 0.125f);

        // The built-in folder icon (dark-skin variant on Pro), re-drawn tinted to recolor folders.
        public static Texture2D FolderIcon => LoadBuiltinIcon("Folder Icon");

        // Loads a Unity built-in editor icon by name, preferring the dark-skin ("d_") variant on the Pro skin.
        public static Texture2D LoadBuiltinIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;

            if (EditorGUIUtility.isProSkin)
            {
                GUIContent dark = EditorGUIUtility.IconContent("d_" + iconName);
                if (dark != null && dark.image != null) return dark.image as Texture2D;
            }

            GUIContent content = EditorGUIUtility.IconContent(iconName);
            return content != null ? content.image as Texture2D : null;
        }

        // Fills a rect with a (possibly translucent) color, honoring alpha.
        public static void FillRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = previous;
        }
    }
}
#endif
