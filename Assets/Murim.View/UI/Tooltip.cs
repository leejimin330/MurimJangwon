using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murim.View.UI
{
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
