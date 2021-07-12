using UnityEditor;
using UnityEngine;

namespace UniMob
{
    internal static class Styles
    {
        public static readonly GUIStyle LeftButton;
        public static readonly GUIStyle BigTitle;

        static Styles()
        {
            BigTitle = new GUIStyle("IN BigTitle");
            var border = BigTitle.margin;
            border.top = 0;
            BigTitle.margin = border;

            LeftButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
            };
        }
    }
}