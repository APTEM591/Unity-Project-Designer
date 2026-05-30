#if UNITY_EDITOR
using UnityEditor;

namespace GameSpear.ProjectDesigner.Editor
{
    // Bootstraps the Project Designer when the editor loads.
    [InitializeOnLoad]
    internal static class Initializer
    {
        static Initializer()
        {
            Editor.Manager.Initialize();
        }
    }
}
#endif
