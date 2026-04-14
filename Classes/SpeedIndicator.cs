using BepInEx;
using GorillaLocomotion;
using UnityEngine;

namespace ghop.Classes
{
    public class SpeedIndicator : MonoBehaviour
    {
        private bool visible = true;
        private bool lastMKey = false;
        private Font customFont;
        private GUIStyle style;
        private Texture2D bgTex;

        private void Start()
        {
            customFont = Resources.Load<Font>("font");
        }

        private void Update()
        {
            bool mHeld = UnityInput.Current.GetKey(KeyCode.M);
            if (mHeld && !lastMKey)
                visible = !visible;
            lastMKey = mHeld;
        }

        private void OnGUI()
        {
            if (!visible) return;

            if (bgTex == null)
            {
                int size = 128;
                bgTex = new Texture2D(size, size);
                Vector2 center = new Vector2(size / 2f, size / 2f);
                for (int px = 0; px < size; px++)
                {
                    for (int py = 0; py < size; py++)
                    {
                        float dist = Vector2.Distance(new Vector2(px, py), center) / (size / 2f);
                        float alpha = Mathf.Clamp01(1f - dist) * 0.6f;
                        bgTex.SetPixel(px, py, new Color(0f, 0f, 0f, alpha));
                    }
                }
                bgTex.Apply();
            }

            float bgW = 140f;
            float bgH = 50f;
            float bgX = Screen.width / 2f - bgW / 2f;
            float bgY = Screen.height - 80f;
            GUI.DrawTexture(new Rect(bgX, bgY, bgW, bgH), bgTex, ScaleMode.StretchToFill);

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                if (customFont != null)
                    style.font = customFont;
                style.fontSize = 20;
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.LowerCenter;
            }

            Rigidbody rb = GTPlayer.Instance.GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector3 vel = rb.linearVelocity;
            float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
            string text = $"{horizontalSpeed:F1} u/s";

            float w = 200f;
            float h = 40f;
            float x = Screen.width / 2f - w / 2f;
            float y = Screen.height - 80f;
            GUI.Label(new Rect(x, y, w, h), text, style);
        }
    }
}