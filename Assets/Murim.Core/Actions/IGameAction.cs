using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.World;

namespace Murim.Core.Actions
{
    public readonly struct ActionCheck
    {
        public readonly bool Ok;
        public readonly string Reason;

        ActionCheck(bool ok, string reason)
        {
            Ok = ok;
            Reason = reason;
        }

        public static ActionCheck Pass => new ActionCheck(true, null);
        public static ActionCheck Fail(string reason) => new ActionCheck(false, reason);
    }

    /// <summary>세계를 바꾸는 유일한 통로. Execute는 CanExecute가 Ok일 때만 호출한다.</summary>
    public interface IGameAction
    {
        /// <summary>행동하는 인물. 세력 행동이면 장문인.</summary>
        string ActorId { get; }
        string Key { get; }
        ActionCheck CanExecute(WorldState w, GameData d);
        void Execute(WorldState w, GameData d, GameRandom rng);
        string Describe(WorldState w);
    }

    /// <summary>이번 달 행동 전부를 쓰는 행동 (이동, 표국 호위).</summary>
    public interface IWholeMonthAction { }

    public static class ActionKeys
    {
        public const string Marriage = "marriage";
        public const string InternalMarriage = "internal_marriage";
        public const string Alliance = "alliance";
        public const string Gift = "gift";
        public const string Insult = "insult";
        public const string InsultUnorthodox = "insult_unorthodox";
        public const string RecruitNamed = "recruit_named";
        public const string RecruitUnnamed = "recruit_unnamed";
        public const string Dismiss = "dismiss";
        public const string DesignateHeir = "designate_heir";
        public const string Expand = "expand";
        public const string Quest = "quest";
        public const string Plunder = "plunder";
        public const string RepayDebt = "repay_debt";
        public const string Travel = "travel";
        public const string QuestChivalrous = "quest_chivalrous";
        public const string QuestContract = "quest_contract";
        public const string Escort = "escort";
        public const string Spar = "spar";
        public const string Befriend = "befriend";
        public const string JoinAsGuest = "join_guest";
        public const string FoundSect = "found_sect";
        public const string LeaveFaction = "leave_faction";
        public const string AcceptMarriage = "accept_marriage";
    }

    public static class ActionBudget
    {
        public static int Max(Character actor) => 2 + actor.Cunning / 8;
    }
}
