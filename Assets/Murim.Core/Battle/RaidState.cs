using System;
using System.Collections.Generic;

namespace Murim.Core.Battle
{
    /// <summary>플레이어 장원을 노린 습격. 전투가 끝날 때까지 사건 갈래 효과를 미룬다 (저장된다).</summary>
    public class RaidState
    {
        public string EventId;
        public int ChoiceIndex;
        /// <summary>선택지 효과 목록에서 raid 효과의 위치 (최상위만).</summary>
        public int EffectIndex;
        public SortedDictionary<string, string> Roles = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public ulong Seed;
        public string AttackerLeaderId;
        public List<string> AttackerIds = new List<string>();
        public int AttackerGrunts;
        public int DefenderGrunts;
        /// <summary>창에 보일 습격자 이름 (세력 이름이나 두목 이름).</summary>
        public string AttackerName;
        public int Month;
    }

    /// <summary>습격 결과 요약 (화면·연대기).</summary>
    public class RaidOutcome
    {
        public bool DefendersWon;
        public List<string> Dead = new List<string>();
        public List<string> Injured = new List<string>();
        public int DefenderGruntsLost;
        public int AttackersDowned;
        public string Summary;
        /// <summary>갈래 효과가 덧붙인 결과 문장.</summary>
        public string Result;
        public string[] Lines = new string[0];
    }
}
