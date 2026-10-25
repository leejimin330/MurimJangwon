using Murim.Core.Data;
using Murim.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace Murim.View.UI.Panels
{
    /// <summary>임시 초상화: 성별·나이대에 따라 색과 체형이 다른 실루엣.</summary>
    public static class Portrait
    {
        public static RectTransform Build(Transform parent, Character c, int month, float size = 120)
        {
            var frame = UiFactory.Panel(parent, Theme.Ground, "Portrait");
            frame.Size(size, size);
            int age = c.AgeYears(month);
            Color tone = age < 16 ? Theme.Hex("#8FA79A") : age < 40 ? Theme.Hex("#B9B39C") : age < 60 ? Theme.Hex("#9C9682") : Theme.Hex("#7B7C76");
            if (!c.IsAlive) tone = Theme.InkFaint;
            float scale = age < 16 ? 0.78f : 1f;

            var body = UiFactory.Panel(frame.transform, tone * 0.85f, "Body");
            body.sprite = UiFactory.Circle;
            var brt = body.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0);
            float bw = size * (c.Sex == Sex.Male ? 0.86f : 0.72f) * scale;
            brt.sizeDelta = new Vector2(bw, bw * 0.9f);
            brt.anchoredPosition = new Vector2(0, bw * 0.12f);

            var head = UiFactory.Panel(frame.transform, tone, "Head");
            head.sprite = UiFactory.Circle;
            var hrt = head.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0);
            float hs = size * 0.38f * scale;
            hrt.sizeDelta = new Vector2(hs, hs);
            hrt.anchoredPosition = new Vector2(0, bw * 0.12f + bw * 0.45f + hs * 0.4f);

            if (c.Sex == Sex.Female)
            {
                var hair = UiFactory.Panel(frame.transform, Theme.Ground, "Hair");
                hair.sprite = UiFactory.Circle;
                var rt = hair.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(hs * 0.42f, hs * 0.42f);
                rt.anchoredPosition = hrt.anchoredPosition + new Vector2(0, hs * 0.55f);
            }
            if (age >= 60)
            {
                var beard = UiFactory.Panel(frame.transform, Theme.Ink * 0.9f, "Beard");
                var rt = beard.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(hs * 0.3f, hs * 0.35f);
                rt.anchoredPosition = hrt.anchoredPosition - new Vector2(0, hs * 0.55f);
            }
            frame.gameObject.AddComponent<RectMask2D>();
            return frame.rectTransform;
        }
    }
}
