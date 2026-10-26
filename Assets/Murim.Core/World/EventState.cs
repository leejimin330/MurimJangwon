using System;
using System.Collections.Generic;
using System.Linq;

namespace Murim.Core.World
{
    /// <summary>저장되는 사건 상태: 이야기꾼, 답을 기다리는 사건, 예약된 후속 사건, 쿨다운.</summary>
    public class EventState
    {
        public string StorytellerId = "balanced";
        public int Tension;
        public int LastCrisisMonth = -9999;
        public List<PendingEvent> Pending = new List<PendingEvent>();
        public List<ScheduledEvent> Scheduled = new List<ScheduledEvent>();
        /// <summary>키 → 다시 가능해지는 월. "eventId|deciderId", "eventId|*", "insult|a|b".</summary>
        public SortedDictionary<string, int> Cooldowns = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public bool IsCooling(string key, int month) => Cooldowns.TryGetValue(key, out var until) && month < until;

        public void SetCooldown(string key, int untilMonth)
        {
            if (!Cooldowns.TryGetValue(key, out var v) || v < untilMonth) Cooldowns[key] = untilMonth;
        }

        public void PruneExpired(int month)
        {
            foreach (var key in Cooldowns.Where(kv => kv.Value <= month).Select(kv => kv.Key).ToList()) Cooldowns.Remove(key);
        }
    }

    /// <summary>플레이어의 답을 기다리는 사건, 또는 확인만 하면 되는 소식.</summary>
    public class PendingEvent
    {
        public string EventId;
        public SortedDictionary<string, string> Roles = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public bool IsNotice;
        public string Title;
        /// <summary>일어날 때 확정한 본문 (소식이면 결과까지 포함한 문장).</summary>
        public string Text;
        public int Month;
        /// <summary>이 사건이 새로 만든 인물. 세계에는 아직 없다.</summary>
        public List<Character> Spawned = new List<Character>();
        /// <summary>소식에 붙는 결투 경과. 없으면 null.</summary>
        public string[] Detail;
    }

    public class ScheduledEvent
    {
        public string EventId;
        public int Month;
        public SortedDictionary<string, string> Roles = new SortedDictionary<string, string>(StringComparer.Ordinal);
    }
}
