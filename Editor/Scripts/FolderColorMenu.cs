#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameSpear.ProjectDesigner.Editor
{
    // Right-click a folder in the Project window -> Project Designer -> Folder Color -> assign or clear
    // its color. Operates on the whole selection; colors are stored per-folder by PD_Settings and drawn
    // by PdManager. Fully self-contained.
    internal static class FolderColorMenu
    {
        private const string Root = "Assets/Project Designer/Folder Color/";
        // Grouped low in the context menu, away from Unity's own asset actions.
        private const int Priority = 1100;

        [MenuItem(Root + "Red", priority = Priority)]
        private static void Red() => Apply(new Color(0.86f, 0.30f, 0.30f));
        [MenuItem(Root + "Orange", priority = Priority)]
        private static void Orange() => Apply(new Color(0.92f, 0.55f, 0.22f));
        [MenuItem(Root + "Yellow", priority = Priority)]
        private static void Yellow() => Apply(new Color(0.93f, 0.80f, 0.28f));
        [MenuItem(Root + "Green", priority = Priority)]
        private static void Green() => Apply(new Color(0.42f, 0.73f, 0.38f));
        [MenuItem(Root + "Blue", priority = Priority)]
        private static void Blue() => Apply(new Color(0.34f, 0.58f, 0.92f));
        [MenuItem(Root + "Purple", priority = Priority)]
        private static void Purple() => Apply(new Color(0.62f, 0.42f, 0.85f));

        [MenuItem(Root + "Clear", priority = Priority + 20)]
        private static void Clear()
        {
            foreach (string guid in Selection.assetGUIDs)
            {
                if (IsFolder(guid)) Settings.ClearFolderColor(guid);
            }
        }

        // Validators enable the items only when at least one folder is selected. Unity matches a validator
        // to its command by identical menu path, so each item needs its own.
        [MenuItem(Root + "Red", validate = true)] private static bool RedV() => AnyFolderSelected();
        [MenuItem(Root + "Orange", validate = true)] private static bool OrangeV() => AnyFolderSelected();
        [MenuItem(Root + "Yellow", validate = true)] private static bool YellowV() => AnyFolderSelected();
        [MenuItem(Root + "Green", validate = true)] private static bool GreenV() => AnyFolderSelected();
        [MenuItem(Root + "Blue", validate = true)] private static bool BlueV() => AnyFolderSelected();
        [MenuItem(Root + "Purple", validate = true)] private static bool PurpleV() => AnyFolderSelected();
        [MenuItem(Root + "Clear", validate = true)] private static bool ClearV() => AnyFolderSelected();

        private static void Apply(Color color)
        {
            foreach (string guid in Selection.assetGUIDs)
            {
                if (IsFolder(guid)) Settings.SetFolderColor(guid, color);
            }
        }

        private static bool AnyFolderSelected()
        {
            foreach (string guid in Selection.assetGUIDs)
            {
                if (IsFolder(guid)) return true;
            }
            return false;
        }

        private static bool IsFolder(string guid) => AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(guid));
    }
}
#endif
