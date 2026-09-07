using UnityEngine;
using UnityEngine.UI;

namespace Hollowmere.UI
{
    /// <summary>
    /// Small helpers for assembling uGUI from code. The project has no prefabs, so every
    /// widget is built here. Legacy uGUI rather than TextMeshPro on purpose: TMP needs an
    /// "import essential resources" step in the editor before it renders anything, which
    /// would break a fresh clone.
    ///
    /// Factory methods are prefixed Make* so they never collide with the UI type of the
    /// same name - "Slider.Direction" inside a class with a Slider() method does not compile.
    /// </summary>
    public static class UIBuilder
    {
        static Font _font;
        static Sprite _white;

        public static Font UiFont
        {
            get
            {
                if (_font != null) return _font;

                // Renamed from Arial.ttf in 2022.2; keep the old name as a fallback.
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        /// <summary>
        /// A 1x1 white sprite. Image.type = Filled needs a real sprite to generate its
        /// geometry from - with a null sprite a filled bar renders nothing at all.
        /// </summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (_white != null) return _white;

                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();

                _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                return _white;
            }
        }

        public static readonly Color Ink = new Color(0.88f, 0.87f, 0.84f, 1f);
        public static readonly Color Dim = new Color(0.60f, 0.59f, 0.57f, 1f);
        public static readonly Color Accent = new Color(0.85f, 0.62f, 0.25f, 1f);
        public static readonly Color Danger = new Color(0.82f, 0.24f, 0.20f, 1f);

        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Stretch(GameObject go, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static RectTransform Place(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image MakePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text MakeLabel(Transform parent, string name, string content, int size,
            TextAnchor alignment = TextAnchor.MiddleCenter, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = UiFont;
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color ?? Ink;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button MakeButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.10f, 0.10f, 0.11f, 0.92f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.2f, 0.9f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (onClick != null) button.onClick.AddListener(onClick);

            var text = MakeLabel(go.transform, "Text", label, 30);
            Stretch(text.gameObject);

            return button;
        }

        /// <summary>
        /// Slider with a fill but no handle - it reads as a bar rather than a control,
        /// which suits a settings list better than uGUI's default knob.
        /// </summary>
        public static Slider MakeSlider(Transform parent, Vector2 anchoredPosition, Vector2 size,
            float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var go = new GameObject("Slider");
            go.transform.SetParent(parent, false);
            Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);

            var background = go.AddComponent<Image>();
            background.color = new Color(0.16f, 0.16f, 0.17f, 0.95f);

            var slider = go.AddComponent<Slider>();
            slider.targetGraphic = background;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;

            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(go.transform, false);
            Stretch(fillArea, 2f, 2f, 2f, 2f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);

            var fillImage = fill.AddComponent<Image>();
            fillImage.color = Accent;
            fillImage.raycastTarget = false;

            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.sizeDelta = Vector2.zero;

            slider.fillRect = fillRect;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);

            return slider;
        }

        /// <summary>Non-interactive meter, used for battery and stamina on the HUD.</summary>
        public static Image MakeBar(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color,
            Vector2 anchor, Vector2 pivot)
        {
            var back = MakePanel(parent, name + "_bg", new Color(0f, 0f, 0f, 0.55f));
            Place(back.gameObject, anchor, pivot, anchoredPosition, size);
            back.raycastTarget = false;

            var fill = MakePanel(back.transform, name + "_fill", color);
            var rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);

            fill.sprite = WhiteSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            return fill;
        }
    }
}
