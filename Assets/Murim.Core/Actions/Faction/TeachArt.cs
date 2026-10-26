using System.Linq;
using Murim.Core.Data;
using Murim.Core.Martial;
using Murim.Core.Random;
using Murim.Core.Story;
using Murim.Core.Text;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    /// <summary>무공 전수: 수장이 아는 무공을 소속 인물에게 1성으로 가르친다. 같은 대상에게는 1년에 한 번.</summary>
    public class TeachArt : FactionAction
    {
        public const int CooldownMonths = 12;
        public const int OpinionGain = 10;
        public readonly string TargetId;
        public readonly string ArtId;

        public TeachArt(string actorId, string targetId, string artId) : base(actorId)
        {
            TargetId = targetId;
            ArtId = artId;
        }

        public override string Key => ActionKeys.TeachArt;
        public static string CooldownKey(string targetId) => "teach|" + targetId;

        public override string Describe(WorldState w) => $"{w.Get(TargetId)?.FullName}에게 무공 전수";

        protected override ActionCheck Check(WorldState w, GameData d, Character actor, Faction own)
        {
            var target = Member(w, own, TargetId);
            if (target == null) return ActionCheck.Fail("우리 세력 인물이 아닙니다");
            if (target.Id == actor.Id) return ActionCheck.Fail("자기 자신입니다");
            var art = ArtGranting.Def(d, ArtId);
            if (art == null || actor.KnownArt(ArtId) == null) return ActionCheck.Fail("내가 익힌 무공이 아닙니다");
            if (target.KnownArt(ArtId) != null) return ActionCheck.Fail("이미 익힌 무공입니다");
            if (!ArtGranting.MeetsRequires(art, target)) return ActionCheck.Fail("아직 익힐 그릇이 못 됩니다");
            if (art.IsInner)
            {
                var inner = ArtGranting.Inner(d, target);
                if (inner != null && inner.Grade >= art.Grade) return ActionCheck.Fail("더 나은 심법을 이미 익혔습니다");
            }
            else if (target.Arts.Count >= ArtGranting.MaxArts) return ActionCheck.Fail("더 익힐 여력이 없습니다");
            if (w.Events.IsCooling(CooldownKey(target.Id), w.Month)) return ActionCheck.Fail("올해는 이미 가르쳤습니다");
            return ActionCheck.Pass;
        }

        protected override void Run(WorldState w, GameData d, GameRandom rng, Character actor, Faction own)
        {
            var target = w.Get(TargetId);
            var art = ArtGranting.Def(d, ArtId);
            ArtGranting.Learn(d, target, ArtId, 1);
            if (target.MainArtId == null && !art.IsInner) target.MainArtId = art.Id;
            w.Opinions.Add(target.Id, actor.Id, OpinionGain);
            w.Events.SetCooldown(CooldownKey(target.Id), w.Month + CooldownMonths);
            Chronicle.ArtTaught(w, actor, target, art.Name);
        }
    }
}
