using System.Collections.Generic;
using System.Linq;
using Murim.Core.Story;
using Murim.Core.World;

namespace Murim.Core.Events
{
    /// <summary>AI의 선택: 점수 = ai + 결정자 특성의 aiTraits 합 + 난수 0~5. 같으면 앞의 선택지.</summary>
    public static class EventAI
    {
        public static int Choose(EventContext ctx)
        {
            var decider = ctx.Decider;
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < ctx.Def.Choices.Count; i++)
            {
                if (EventResolver.CanChoose(ctx, i) != null) continue;
                var c = ctx.Def.Choices[i];
                float s = c.Ai + ctx.Rng.Next(0, 6);
                if (decider != null && c.AiTraits != null)
                    foreach (var t in decider.TraitIds)
                        if (c.AiTraits.TryGetValue(t, out var v)) s += v;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            return best < 0 ? 0 : best;
        }
    }

    public static class EventResolver
    {
        /// <summary>null이면 고를 수 있다. 아니면 비활성 사유.</summary>
        public static string CanChoose(EventContext ctx, int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= ctx.Def.Choices.Count) return "없는 선택지입니다";
            var req = ctx.Def.Choices[choiceIndex].Requires;
            if (req == null) return null;
            foreach (var kv in req)
            {
                var why = ConditionCheck.Why(kv.Value, ctx.Get(kv.Key), ctx, false, true);
                if (why != null) return why;
            }
            return null;
        }

        /// <summary>선택지 이름 앞에 붙일 특성 이름 (예: "[다혈질] 칼을 뽑는다").</summary>
        public static string ChoiceLabel(EventContext ctx, int choiceIndex)
        {
            var c = ctx.Def.Choices[choiceIndex];
            var text = EventText.Format(c.Text, ctx);
            var traits = c.Requires?.Values.SelectMany(r => (r?.TraitsAll ?? new string[0]).Concat(r?.TraitsAny ?? new string[0]))
                .Select(t => ctx.D.Traits.TryGetValue(t, out var def) ? def.Name : t).Distinct().ToList();
            return traits != null && traits.Count > 0 ? $"[{string.Join("·", traits)}] {text}" : text;
        }

        /// <summary>효과 적용, 연대기 기록, 새 인물 정리. 결과 문장을 돌려준다.</summary>
        /// <param name="body">이미 확정한 본문 (대기 사건). 없으면 지금 만든다.</param>
        public static string Resolve(EventContext ctx, int choiceIndex, string body = null)
        {
            var w = ctx.W;
            var choice = ctx.Def.Choices[choiceIndex];
            body ??= EventText.Format(ctx.Def.Text, ctx);
            ctx.Log.Clear();
            Effects.Apply(choice.Effects, ctx);

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(choice.Result)) parts.Add(EventText.Format(choice.Result, ctx));
            parts.AddRange(ctx.Log);
            string result = string.Join(" ", parts);
            string line = body + " [" + EventText.Format(choice.Text, ctx) + "]" + (result.Length > 0 ? " " + result : "");

            var ids = ctx.Roles.Values.Where(id => w.Characters.ContainsKey(id)).ToList();
            var faction = ctx.Decider?.FactionId;
            if (faction != null) ids.Add(faction);
            Chronicle.EventLine(w, "Event:" + ctx.Def.Id, ctx.Def.Importance, line, ids.Distinct().ToArray());

            // 세계에 들이지 않은 새 인물은 사라진다.
            foreach (var s in ctx.Spawned)
                if (!ctx.Keep.Contains(s.Id)) w.Characters.Remove(s.Id);
            return result;
        }

        /// <summary>결정자 쿨다운과 전역 쿨다운을 건다.</summary>
        public static void MarkFired(EventContext ctx)
        {
            var e = ctx.Def;
            var w = ctx.W;
            var decider = ctx.Decider;
            if (decider != null && e.CooldownMonths > 0) w.Events.SetCooldown(CooldownKey(e.Id, decider.Id), w.Month + e.CooldownMonths);
            if (e.GlobalCooldownMonths > 0) w.Events.SetCooldown(CooldownKey(e.Id, "*"), w.Month + e.GlobalCooldownMonths);
        }

        public static string CooldownKey(string eventId, string deciderId) => eventId + "|" + deciderId;
    }
}
