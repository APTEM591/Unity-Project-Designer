#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameSpear.ProjectDesigner.Editor
{
    // Classifies a folder by the dominant type of asset it directly contains, then resolves a matching
    // built-in Unity icon. Results are cached and invalidated whenever the project changes.
    internal static class FolderIcon
    {
        internal enum ContentCategory
        {
            None,
            Script,
            Scene,
            Prefab,
            Texture,
            Material,
            Audio,
            Model,
            Animation,
            AnimatorController,
            Shader,
            Font,
            Text,
            ScriptableObject,
        }

        // All meaningful categories (everything except None), used by the settings window.
        public static readonly ContentCategory[] AllCategories =
        {
            ContentCategory.Script, ContentCategory.Scene, ContentCategory.Prefab, ContentCategory.Texture,
            ContentCategory.Material, ContentCategory.Audio, ContentCategory.Model, ContentCategory.Animation,
            ContentCategory.AnimatorController, ContentCategory.Shader, ContentCategory.Font, ContentCategory.Text,
            ContentCategory.ScriptableObject,
        };

        private static readonly Dictionary<string, ContentCategory> _categoryCache = new();

        public static void ClearCache() => _categoryCache.Clear();

        public static ContentCategory GetCategory(string folderPath)
        {
            if (_categoryCache.TryGetValue(folderPath, out ContentCategory cached)) return cached;
            ContentCategory category = Classify(folderPath);
            _categoryCache[folderPath] = category;
            return category;
        }

        // The icon shown for a category: a user-assigned override texture if set, otherwise the
        // matching built-in Unity icon.
        public static Texture2D GetCategoryIcon(ContentCategory category)
        {
            string overrideGuid = Settings.GetIconOverrideGuid(category);
            if (!string.IsNullOrEmpty(overrideGuid))
            {
                string overridePath = AssetDatabase.GUIDToAssetPath(overrideGuid);
                if (!string.IsNullOrEmpty(overridePath))
                {
                    Texture2D custom = AssetDatabase.LoadAssetAtPath<Texture2D>(overridePath);
                    if (custom != null) return custom;
                }
            }
            return Util.LoadBuiltinIcon(GetDefaultIconName(category));
        }

        public static string GetDefaultIconName(ContentCategory category)
        {
            return category switch
            {
                ContentCategory.Script => "cs Script Icon",
                ContentCategory.Scene => "SceneAsset Icon",
                ContentCategory.Prefab => "Prefab Icon",
                ContentCategory.Texture => "Texture Icon",
                ContentCategory.Material => "Material Icon",
                ContentCategory.Audio => "AudioClip Icon",
                ContentCategory.Model => "Mesh Icon",
                ContentCategory.Animation => "AnimationClip Icon",
                ContentCategory.AnimatorController => "AnimatorController Icon",
                ContentCategory.Shader => "Shader Icon",
                ContentCategory.Font => "Font Icon",
                ContentCategory.Text => "TextAsset Icon",
                ContentCategory.ScriptableObject => "ScriptableObject Icon",
                _ => null,
            };
        }

        // Bounds the recursive scan so first-paint of very large folders stays cheap (results are cached).
        private const int MaxFilesScanned = 1000;

        private static ContentCategory Classify(string folderPath)
        {
            // Only classify project assets; leave package / special roots untouched.
            if (!folderPath.StartsWith("Assets")) return ContentCategory.None;

            string absolute = Application.dataPath + folderPath.Substring("Assets".Length);
            if (!Directory.Exists(absolute)) return ContentCategory.None;

            Dictionary<ContentCategory, int> counts = new();
            int scanned = 0;
            // Recursive (default): a folder reflects the dominant asset type anywhere beneath it
            // (so e.g. a "Scripts" folder whose code lives in sub-folders is still recognised).
            SearchOption searchOption = Settings.RecursiveClassification ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (string file in Directory.EnumerateFiles(absolute, "*", searchOption))
            {
                if (file.EndsWith(".meta")) continue;
                if (++scanned > MaxFilesScanned) break;

                ContentCategory category = CategoryFromExtension(Path.GetExtension(file));
                if (category == ContentCategory.None) continue;
                counts.TryGetValue(category, out int current);
                counts[category] = current + 1;
            }

            ContentCategory best = ContentCategory.None;
            int bestCount = 0;
            foreach (KeyValuePair<ContentCategory, int> pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    bestCount = pair.Value;
                    best = pair.Key;
                }
            }
            return best;
        }

        private static ContentCategory CategoryFromExtension(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".cs":
                case ".js":
                case ".boo":
                    return ContentCategory.Script;
                case ".unity":
                    return ContentCategory.Scene;
                case ".prefab":
                    return ContentCategory.Prefab;
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd":
                case ".gif":
                case ".bmp":
                case ".tif":
                case ".tiff":
                case ".exr":
                case ".hdr":
                case ".webp":
                    return ContentCategory.Texture;
                case ".mat":
                    return ContentCategory.Material;
                case ".wav":
                case ".mp3":
                case ".ogg":
                case ".aiff":
                case ".aif":
                case ".flac":
                    return ContentCategory.Audio;
                case ".fbx":
                case ".obj":
                case ".blend":
                case ".dae":
                case ".3ds":
                case ".ma":
                case ".mb":
                    return ContentCategory.Model;
                case ".anim":
                    return ContentCategory.Animation;
                case ".controller":
                case ".overridecontroller":
                    return ContentCategory.AnimatorController;
                case ".shader":
                case ".shadergraph":
                case ".compute":
                case ".cginc":
                case ".hlsl":
                case ".glslinc":
                    return ContentCategory.Shader;
                case ".ttf":
                case ".otf":
                    return ContentCategory.Font;
                case ".txt":
                case ".json":
                case ".xml":
                case ".csv":
                case ".md":
                case ".yaml":
                case ".yml":
                case ".html":
                    return ContentCategory.Text;
                case ".asset":
                    return ContentCategory.ScriptableObject;
                default:
                    return ContentCategory.None;
            }
        }
    }
}
#endif
