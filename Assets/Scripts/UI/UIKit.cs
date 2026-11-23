using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Backrooms
{
    /// <summary>Tiny code-built uGUI kit: text, buttons, sliders, bars and panels in the game's sickly-yellow style.</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.95f, 0.91f, 0.72f);
        public static readonly Color Dim = new Color(0.7f, 0.66f, 0.5f);
        public static readonly Color Accent = new Color(0.79f, 0.64f, 0.15f);
        public static readonly Color Panel = new Color(0.06f, 0.055f, 0.03f, 0.86f);
        public static readonly Color Danger = new Color(0.8f, 0.12f, 0.1f);

        private static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform RT(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = RT(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Img(Transform parent, string name, Color c, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var rt = RT(parent, name, anchorMin, anchorMax, pos, size, pivot);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 boxSize, FontStyle style = FontStyle.Normal)
        {
            var rt = RT(parent, "Text", anchorMin, anchorMax, pos, boxSize);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Btn(Transform parent, string label, Vector2 pos, Vector2 size, UnityAction onClick, int fontSize = 30)
        {
            var img = Img(parent, label, new Color(0, 0, 0, 0.0f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = new Color(1, 1, 1, 0f);
            colors.highlightedColor = new Color(Accent.r, Accent.g, Accent.b, 0.25f);
            colors.pressedColor = new Color(Accent.r, Accent.g, Accent.b, 0.45f);
            colors.selectedColor = new Color(Accent.r, Accent.g, Accent.b, 0.15f);
            b.colors = colors;
            img.color = Color.white;
            var t = Label(img.transform, label, fontSize, Ink, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(18, 0), Vector2.zero, FontStyle.Bold);
            t.rectTransform.offsetMin = new Vector2(18, 0); t.rectTransform.offsetMax = Vector2.zero;
            var bar = Img(img.transform, "Bar", Accent, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(4, 0), new Vector2(0, 0.5f));
            bar.raycastTarget = false;
            b.onClick.AddListener(onClick);
            return b;
        }

        public static Slider Slide(Transform parent, string label, Vector2 pos, float min, float max, float value, Action<float> onChange, Func<float, string> format)
        {
            var row = RT(parent, label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(700, 60));
            Label(row, label, 26, Ink, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(110, 0), new Vector2(220, 0));
            var valueText = Label(row, format(value), 24, Dim, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-50, 0), new Vector2(100, 0));
            var track = Img(row, "Track", new Color(1, 1, 1, 0.15f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(240, 0), new Vector2(340, 8), new Vector2(0, 0.5f));
            var fillArea = RT(track.transform, "Fill Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = Img(fillArea, "Fill", Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleArea = RT(track.transform, "Handle Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handle = Img(handleArea, "Handle", Ink, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(18, 28));
            var s = track.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => { valueText.text = format(v); onChange(v); });
            return s;
        }

        /// <summary>Thin horizontal meter, anchored bottom-left.</summary>
        public static Image Meter(Transform parent, string label, Vector2 pos, Color c, out CanvasGroup group)
        {
            var row = RT(parent, label, Vector2.zero, Vector2.zero, pos, new Vector2(300, 34), Vector2.zero);
            group = row.gameObject.AddComponent<CanvasGroup>();
            Label(row, label, 16, Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -8), new Vector2(300, 20));
            Img(row, "Back", new Color(1, 1, 1, 0.12f), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(260, 6), Vector2.zero);
            var fill = Img(row, "Fill", c, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(260, 6), Vector2.zero);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.sprite = WhiteSprite;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            return fill;
        }

        private static Sprite _white;
        public static Sprite WhiteSprite
        {
            get
            {
                if (_white != null) return _white;
                var t = Texture2D.whiteTexture;
                _white = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
                return _white;
            }
        }
    }
}
