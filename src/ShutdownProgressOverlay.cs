using UnityEngine;

namespace SevenDebug
{
    /// <summary>
    /// IMGUI overlay shown during the save-and-quit drain window.
    /// Drawn via OnGUI so it works without any XUi/atlas wiring.
    /// </summary>
    public class ShutdownProgressOverlay : MonoBehaviour
    {
        public float TotalSeconds;
        public float Elapsed;
        public string Message = "Saving world before exit";

        private Texture2D _dimTex;
        private Texture2D _panelTex;
        private Texture2D _barBgTex;
        private Texture2D _barFillTex;

        private GUIStyle _titleStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _countdownStyle;

        private void OnGUI()
        {
            EnsureTextures();
            EnsureStyles();

            GUI.depth = -1000;

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _dimTex);

            float w = 460f;
            float h = 150f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;
            GUI.DrawTexture(new Rect(x, y, w, h), _panelTex);

            GUI.Label(new Rect(x, y + 14f, w, 30f), "Shutting down", _titleStyle);

            float remaining = Mathf.Max(0f, TotalSeconds - Elapsed);
            GUI.Label(new Rect(x, y + 48f, w, 22f), Message, _messageStyle);
            GUI.Label(new Rect(x, y + 72f, w, 22f), $"Exiting in {remaining:F1}s", _countdownStyle);

            float barW = w - 48f;
            float barH = 18f;
            float barX = x + 24f;
            float barY = y + h - 32f;
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), _barBgTex);
            float pct = TotalSeconds > 0f ? Mathf.Clamp01(Elapsed / TotalSeconds) : 0f;
            if (pct > 0f)
                GUI.DrawTexture(new Rect(barX, barY, barW * pct, barH), _barFillTex);
        }

        private void EnsureTextures()
        {
            if (_dimTex == null) _dimTex = MakeTex(new Color(0f, 0f, 0f, 0.55f));
            if (_panelTex == null) _panelTex = MakeTex(new Color(0.08f, 0.08f, 0.08f, 0.95f));
            if (_barBgTex == null) _barBgTex = MakeTex(new Color(0.18f, 0.18f, 0.18f, 1f));
            if (_barFillTex == null) _barFillTex = MakeTex(new Color(0.85f, 0.55f, 0.15f, 1f));
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = Color.white;

            _messageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
            };
            _messageStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _countdownStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _countdownStyle.normal.textColor = new Color(0.95f, 0.75f, 0.4f);
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void OnDestroy()
        {
            if (_dimTex != null) Destroy(_dimTex);
            if (_panelTex != null) Destroy(_panelTex);
            if (_barBgTex != null) Destroy(_barBgTex);
            if (_barFillTex != null) Destroy(_barFillTex);
        }
    }
}
