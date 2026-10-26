using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Martial;
using UnityEngine;

namespace Murim.View.UI.Panels
{
    /// <summary>결투 경과를 한 합씩 보여 준다. 사건 결과 창, 소식 창, 연대기 펼침에 함께 쓴다.</summary>
    public static class DuelLogView
    {
        /// <param name="height">0보다 크면 그 높이의 스크롤 영역, 아니면 내용만큼.</param>
        public static RectTransform Build(Transform parent, GameData d, IList<string> lines, float height = 0)
        {
            var box = UiFactory.Panel(parent, Theme.Ground, "DuelLog");
            RectTransform list;
            if (height > 0)
            {
                box.Size(-1, height);
                list = UiFactory.ScrollList(box.transform, 4, 12);
                ((RectTransform)list.parent.parent).Fill();
            }
            else
            {
                list = box.rectTransform;
                UiFactory.VStack(list, 4, 12);
                list.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            }
            var deathTails = (d.Narration.TryGetValue(NarrationKeys.Death, out var pool) ? pool : new string[0])
                .Select(t => t.Substring(t.LastIndexOf('}') + 1).Trim()).Where(t => t.Length > 0).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                var text = lines[i];
                if (string.IsNullOrEmpty(text))
                {
                    UiFactory.Rect(list, "Gap").Size(-1, 8);
                    continue;
                }
                bool last = i == lines.Count - 1 || string.IsNullOrEmpty(lines[i + 1]);
                bool death = deathTails.Any(t => text.Contains(t));
                var color = death ? Theme.Seal : last || i == 0 ? Theme.Gold : Theme.Ink;
                var style = text.Contains("절초") || last ? FontStyle.Bold : FontStyle.Normal;
                UiFactory.Label(list, text, Theme.SmallSize + 1, color, style: style).name = "DuelLine";
            }
            return box.rectTransform;
        }
    }
}
