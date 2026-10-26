using System;
using System.Linq;
using Murim.Core.Martial;
using Murim.Core.Text;
using Murim.Core.World;

namespace Murim.Core.Events
{
    /// <summary>무공 사건 효과: 무공 배우기, 성취, 내공, 깨달음, 돌파, 폐관.</summary>
    static class ArtEffects
    {
        static Character Alive(EventContext ctx, string role)
        {
            var c = ctx.Get(role);
            return c != null && c.IsAlive ? c : null;
        }

        public static void Apply(string kind, FxArgs args, EventContext ctx)
        {
            var w = ctx.W;
            var d = ctx.D;
            switch (kind)
            {
                case "learnArt":
                {
                    var x = (LearnArtFx)args;
                    var who = Alive(ctx, x.Who);
                    if (who == null) break;
                    var art = PickArt(ctx, x, who);
                    if (art == null)
                    {
                        ctx.Log.Add($"{Josa.EunNeun(who.FullName)} 새로 배울 만한 무공을 찾지 못했다.");
                        break;
                    }
                    if (!ArtGranting.Learn(d, who, art.Id, 1))
                    {
                        ctx.Log.Add($"{Josa.EunNeun(who.FullName)} {Josa.EulReul(art.Name)} 엿보았으나 더 익힐 여력이 없었다.");
                        break;
                    }
                    if (who.MainArtId == null && !art.IsInner) who.MainArtId = art.Id;
                    ctx.Log.Add($"{Josa.IGa(who.FullName)} {ArtNames.Grade(art.Grade)} {ArtNames.Type(art.Type)} {Josa.EulReul(art.Name)} 익혔다.");
                    break;
                }
                case "mastery":
                {
                    var x = (MasteryFx)args;
                    var who = Alive(ctx, x.Who);
                    var art = who == null ? null : x.Art != null ? ArtGranting.Def(d, x.Art) : ArtGranting.MainArt(d, who);
                    var known = art == null ? null : who.KnownArt(art.Id);
                    if (known == null) break;
                    int before = known.Mastery;
                    known.Mastery = Math.Max(1, Math.Min(ArtGranting.MaxMastery, known.Mastery + x.Amount));
                    if (known.Mastery > before) ctx.Log.Add($"{who.FullName}의 {art.Name} 성취가 {known.Mastery}성에 이르렀다.");
                    break;
                }
                case "qi":
                {
                    var x = (QiFx)args;
                    var who = Alive(ctx, x.Who);
                    if (who == null) break;
                    who.Qi = Math.Max(0, who.Qi + x.Years * 12);
                    ctx.Log.Add(x.Years >= 0 ? $"{who.FullName}의 내공이 {x.Years}년 늘었다." : $"{who.FullName}의 내공이 {-x.Years}년 줄었다.");
                    break;
                }
                case "enlighten":
                {
                    var x = (WhoAmountFx)args;
                    var who = Alive(ctx, x.Who);
                    if (who != null) who.Enlightenment = Math.Max(0, Math.Min(Cultivation.EnlightenmentMax, who.Enlightenment + x.Amount));
                    break;
                }
                case "breakthrough":
                {
                    var x = (BreakthroughFx)args;
                    var who = Alive(ctx, x.Who);
                    if (who == null) break;
                    if (!Cultivation.CanBreakthrough(who))
                    {
                        ctx.Log.Add("아직 때가 무르익지 않았다.");
                        Effects.Apply(x.Fail, ctx);
                        break;
                    }
                    var outcome = Cultivation.TryBreakthrough(w, d, ctx.Rng, who, x.Bonus, false);
                    switch (outcome)
                    {
                        case BreakthroughOutcome.Success:
                            ctx.Log.Add($"{Josa.EunNeun(who.FullName)} 마침내 벽을 넘어 {Titles.Realm(who.Realm)}의 경지에 올랐다!");
                            break;
                        case BreakthroughOutcome.Failed:
                            ctx.Log.Add($"{Josa.EunNeun(who.FullName)} 벽을 넘지 못했다. 쌓은 깨달음이 흩어졌다.");
                            break;
                        case BreakthroughOutcome.Deviation:
                            ctx.Log.Add($"{Josa.EunNeun(who.FullName)} 벽에 부딪혀 주화입마에 빠졌다.");
                            break;
                        default:
                            ctx.Log.Add($"{Josa.EunNeun(who.FullName)} 주화입마를 이기지 못하고 숨을 거두었다.");
                            break;
                    }
                    Effects.Apply(outcome == BreakthroughOutcome.Success ? x.Success : x.Fail, ctx);
                    break;
                }
                case "closedTraining":
                {
                    var x = (ClosedTrainingFx)args;
                    var who = Alive(ctx, x.Who);
                    if (who == null || who.InTraining(w.Month)) break;
                    Cultivation.BeginTraining(w, who, x.Months);
                    ctx.Log.Add($"{Josa.EunNeun(who.FullName)} 석실 문을 닫고 {x.Months}개월의 폐관에 들어갔다.");
                    break;
                }
            }
        }

        static ArtDef PickArt(EventContext ctx, LearnArtFx x, Character who)
        {
            var d = ctx.D;
            if (x.Art != null)
            {
                var a = ArtGranting.Def(d, x.Art);
                return a != null && who.KnownArt(a.Id) == null ? a : null;
            }
            if (x.From != null)
            {
                var teacher = ctx.Get(x.From);
                if (teacher == null) return null;
                var pool = teacher.Arts.Select(k => ArtGranting.Def(d, k.ArtId))
                    .Where(a => a != null && who.KnownArt(a.Id) == null && ArtGranting.MeetsRequires(a, who))
                    .OrderBy(a => a.IsInner ? 1 : 0).ThenBy(a => a.Id, StringComparer.Ordinal).ToList();
                var externals = pool.Where(a => !a.IsInner).ToList();
                return externals.Count > 0 ? ctx.Rng.Pick(externals) : pool.FirstOrDefault();
            }
            var align = ConditionCheck.AlignmentOf(ctx.W, who);
            var random = d.Arts.Values.Where(a => who.KnownArt(a.Id) == null && ArtGranting.MeetsRequires(a, who)
                && (x.GradeMin == null || a.Grade >= x.GradeMin) && (x.GradeMax == null || a.Grade <= x.GradeMax)
                && (x.Alignment != null ? a.Alignment == x.Alignment : ArtGranting.AlignmentAllows(align, a.Alignment))).ToList();
            return random.Count == 0 ? null : ctx.Rng.Pick(random);
        }
    }
}
