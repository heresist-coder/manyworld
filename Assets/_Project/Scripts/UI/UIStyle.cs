using UnityEngine;

namespace Manyworld
{
    /// <summary>IMGUI 공통 스타일. 프로토타입이라 IMGUI로 간다. 한글은 OS 폰트(맑은 고딕)를 쓴다.</summary>
    public static class UIStyle
    {
        static Font font;
        static GUIStyle label, title, box, button, mono, small;
        static Texture2D white;

        public static readonly Color Paper = new Color(0.93f, 0.90f, 0.82f);
        public static readonly Color Ink = new Color(0.12f, 0.11f, 0.10f);
        public static readonly Color Amber = new Color(1f, 0.72f, 0.22f);
        public static readonly Color Red = new Color(0.85f, 0.2f, 0.18f);
        public static readonly Color Green = new Color(0.45f, 0.8f, 0.4f);

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
                }
                return font;
            }
        }

        public static Texture2D White
        {
            get
            {
                if (white == null)
                {
                    white = new Texture2D(1, 1);
                    white.SetPixel(0, 0, Color.white);
                    white.Apply();
                }
                return white;
            }
        }

        public static GUIStyle Label => label ??= Make(18, Color.white, FontStyle.Normal, TextAnchor.UpperLeft);
        public static GUIStyle Small => small ??= Make(14, new Color(1, 1, 1, 0.8f), FontStyle.Normal, TextAnchor.UpperLeft);
        public static GUIStyle Title => title ??= Make(28, Color.white, FontStyle.Bold, TextAnchor.UpperLeft);
        public static GUIStyle Mono => mono ??= Make(26, Amber, FontStyle.Bold, TextAnchor.MiddleRight);

        public static GUIStyle Box
        {
            get
            {
                if (box == null)
                {
                    box = new GUIStyle(GUI.skin.box) { font = Font, fontSize = 16, padding = new RectOffset(12, 12, 10, 10), alignment = TextAnchor.UpperLeft, wordWrap = true };
                    box.normal.textColor = Color.white;
                }
                return box;
            }
        }

        public static GUIStyle Button
        {
            get
            {
                if (button == null)
                {
                    button = new GUIStyle(GUI.skin.button) { font = Font, fontSize = 18, padding = new RectOffset(12, 12, 8, 8), wordWrap = true };
                }
                return button;
            }
        }

        static GUIStyle Make(int size, Color color, FontStyle fs, TextAnchor anchor)
        {
            var s = new GUIStyle { font = Font, fontSize = size, fontStyle = fs, alignment = anchor, wordWrap = true, richText = true };
            s.normal.textColor = color;
            return s;
        }

        public static GUIStyle With(GUIStyle baseStyle, Color color, int size = 0, TextAnchor? anchor = null)
        {
            var s = new GUIStyle(baseStyle);
            s.normal.textColor = color;
            if (size > 0) s.fontSize = size;
            if (anchor.HasValue) s.alignment = anchor.Value;
            return s;
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        /// <summary>1920x1080 기준 좌표계로 그린다.</summary>
        public static void BeginScaled()
        {
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
        }

        public static float ScaledWidth => Screen.width / (Screen.height / 1080f);

        public static string Won(int amount) => $"₩{amount:N0}";
    }
}
