#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameSpear.ProjectDesigner.Editor
{
    internal enum PD_LineStyle { Solid, Dotted, Dashed }

    // Procedurally draws the tree connector lines (no texture assets), so the package is self-contained.
    // The caller sets GUI.color (the tree color) before calling; these draw with EditorGUIUtility.whiteTexture
    // which honors that color's alpha.
    internal static class Draw
    {
        public static void VerticalLine(float centerX, float y, float height, float thickness, PD_LineStyle style)
        {
            float x = centerX - (thickness * 0.5f);
            if (style == PD_LineStyle.Solid)
            {
                GUI.DrawTexture(new Rect(x, y, thickness, height), EditorGUIUtility.whiteTexture);
                return;
            }

            GetDashMetrics(style, out float dash, out float gap);
            for (float offset = 0f; offset < height; offset += dash + gap)
            {
                float segment = Mathf.Min(dash, height - offset);
                GUI.DrawTexture(new Rect(x, y + offset, thickness, segment), EditorGUIUtility.whiteTexture);
            }
        }

        public static void HorizontalLine(float x, float centerY, float width, float thickness, PD_LineStyle style)
        {
            float y = centerY - (thickness * 0.5f);
            if (style == PD_LineStyle.Solid)
            {
                GUI.DrawTexture(new Rect(x, y, width, thickness), EditorGUIUtility.whiteTexture);
                return;
            }

            GetDashMetrics(style, out float dash, out float gap);
            for (float offset = 0f; offset < width; offset += dash + gap)
            {
                float segment = Mathf.Min(dash, width - offset);
                GUI.DrawTexture(new Rect(x + offset, y, segment, thickness), EditorGUIUtility.whiteTexture);
            }
        }

        private static void GetDashMetrics(PD_LineStyle style, out float dash, out float gap)
        {
            if (style == PD_LineStyle.Dashed) { dash = 3f; gap = 3f; }
            else { dash = 1f; gap = 2f; } // Dotted
        }
    }
}
#endif
