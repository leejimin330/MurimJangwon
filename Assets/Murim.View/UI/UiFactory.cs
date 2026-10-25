using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murim.View.UI
{
    /// <summary>uGUI 요소를 코드로 만든다. 프리팹을 쓰지 않는다.</summary>
    public static class UiFactory
    {
        public static Font Sans;
        public static Font Serif;
        static Sprite circle;

        public static Canvas CreateCanvas(Transform parent, Camera camera)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            if (camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
            }
            else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Fill(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>부모 기준 비율 영역 (0~1).</summary>
        public static RectTransform Area(this RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static LayoutElement Size(this Component c, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0) { le.preferredWidth = preferredWidth; le.minWidth = preferredWidth; }
            if (preferredHeight >= 0) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static Image Panel(Transform parent, Color color, string name = "Panel")
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size = Theme.BodySize, Color? color = null,
            bool serif = false, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(parent, "Label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = serif ? Serif : Sans;
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Theme.Ink;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.15f;
            t.raycastTarget = false;
            return t;
        }

        public static VerticalLayoutGroup VStack(Transform target, float spacing = 8, int padding = 0, bool expandWidth = true)
        {
            var v = target.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(Transform target, float spacing = 8, int padding = 0)
        {
            var h = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }

        /// <summary>가로 한 줄 컨테이너.</summary>
        public static RectTransform Row(Transform parent, float spacing = 8, float height = -1)
        {
            var rt = Rect(parent, "Row");
            HStack(rt, spacing);
            if (height > 0) rt.Size(-1, height);
            return rt;
        }

        public static Button Button(Transform parent, string text, Action onClick, bool enabled = true, string disabledReason = null,
            int size = Theme.BodySize, float width = -1, float height = 44)
        {
            var img = Panel(parent, enabled ? Theme.PanelRaised : Theme.Disabled, "Button");
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = Color.white;
            b.colors = colors;
            b.interactable = enabled;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var label = Label(img.transform, text, size, enabled ? Theme.Ink : Theme.InkFaint, align: TextAnchor.MiddleCenter);
            label.rectTransform.Fill(12, 0, 12, 0);
            img.Size(width, height);
            if (!enabled && !string.IsNullOrEmpty(disabledReason)) Tooltip.Attach(img.gameObject, disabledReason);
            return b;
        }

        /// <summary>선택 상태가 보이는 버튼 (토글 대용).</summary>
        public static Button Choice(Transform parent, string text, bool selected, Action onClick, bool enabled = true,
            string disabledReason = null, float width = -1, float height = 44)
        {
            var b = Button(parent, text, onClick, enabled, disabledReason, Theme.BodySize, width, height);
            if (selected)
            {
                b.GetComponent<Image>().color = Theme.Gold;
                b.GetComponentInChildren<Text>().color = Theme.Ground;
            }
            return b;
        }

        public static InputField Input(Transform parent, string value, string placeholder, Action<string> onChanged, float width = -1)
        {
            var bg = Panel(parent, Theme.Ground, "Input");
            bg.raycastTarget = true;
            bg.Size(width, 44);
            var text = Label(bg.transform, "", Theme.BodySize);
            text.rectTransform.Fill(12, 0, 12, 0);
            text.supportRichText = false;
            var ph = Label(bg.transform, placeholder, Theme.BodySize, Theme.InkFaint);
            ph.rectTransform.Fill(12, 0, 12, 0);
            var input = bg.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = ph;
            input.text = value ?? "";
            input.caretColor = Theme.Gold;
            input.selectionColor = new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.35f);
            if (onChanged != null) input.onValueChanged.AddListener(v => onChanged(v));
            return input;
        }

        /// <summary>세로 스크롤 목록. 돌려주는 content에 자식을 넣는다.</summary>
        public static RectTransform ScrollList(Transform parent, float spacing = 6, int padding = 0)
        {
            var root = Rect(parent, "Scroll");
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            var viewport = Rect(root, "Viewport").Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VStack(content, spacing, padding);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        /// <summary>가로 막대 (0~1).</summary>
        public static RectTransform Bar(Transform parent, float value, Color color, float height = 8)
        {
            var bg = Panel(parent, Theme.Ground, "Bar");
            bg.Size(-1, height);
            var fill = Panel(bg.transform, color, "Fill");
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return bg.rectTransform;
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                float r = n / 2f - 1;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - n / 2f + 0.5f) * (x - n / 2f + 0.5f) + (y - n / 2f + 0.5f) * (y - n / 2f + 0.5f));
                        byte a = (byte)(Mathf.Clamp01(r - d) * 255);
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
                return circle;
            }
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }

    /// <summary>마우스를 올리면 사유를 띄운다.</summary>
    public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Text;
        static Text shared;

        public static void Attach(GameObject go, string text)
        {
            var t = go.GetComponent<Tooltip>() ?? go.AddComponent<Tooltip>();
            t.Text = text;
            var g = go.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;
            if (shared == null)
            {
                var bg = UiFactory.Panel(canvas.transform, Theme.Ground, "Tooltip");
                bg.raycastTarget = false;
                var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                UiFactory.VStack(bg.transform, 0, 10);
                shared = UiFactory.Label(bg.transform, "", Theme.SmallSize, Theme.Ink);
                bg.rectTransform.pivot = new Vector2(0, 1);
            }
            shared.text = Text;
            var rt = (RectTransform)shared.transform.parent;
            rt.SetAsLastSibling();
            rt.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, e.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local + new Vector2(16, -16);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (shared != null) shared.transform.parent.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (shared != null) shared.transform.parent.gameObject.SetActive(false);
        }
    }
}
