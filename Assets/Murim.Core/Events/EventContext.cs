using System;
using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Random;
using Murim.Core.World;

namespace Murim.Core.Events
{
    /// <summary>역할을 채운 사건 한 건. 해결할 때까지 이 묶음이 효과·본문의 기준이 된다.</summary>
    public class EventContext
    {
        public WorldState W;
        public GameData D;
        public GameRandom Rng;
        public EventDef Def;
        public SortedDictionary<string, string> Roles = new SortedDictionary<string, string>(StringComparer.Ordinal);
        /// <summary>이 사건이 새로 만든 인물. 세계에 들이지 않으면 해결 뒤 사라진다.</summary>
        public List<Character> Spawned = new List<Character>();
        /// <summary>세계에 남길 새 인물 id.</summary>
        public HashSet<string> Keep = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>효과가 덧붙인 결과 문장.</summary>
        public List<string> Log = new List<string>();
        /// <summary>이 사건에서 벌어진 결투의 경과 (여러 번이면 빈 줄로 나눈다).</summary>
        public List<string> DuelLines = new List<string>();
        /// <summary>강제로 띄우는 사건 (테스트·에디터)은 쿨다운을 보지 않는다.</summary>
        public bool IgnoreCooldown;

        public Character Get(string role)
        {
            if (role == null || !Roles.TryGetValue(role, out var id)) return null;
            return W.Get(id) ?? Spawned.FirstOrDefault(c => c.Id == id);
        }

        public bool IsSpawned(Character c) => c != null && Spawned.Any(s => s.Id == c.Id);

        public Character Decider => Get(Def.Decider);
        public bool IsPlayerDecider => Decider != null && Decider.Id == W.PlayerCharacterId;

        /// <summary>새 인물의 호칭 (본문 {role.title}에 쓴다).</summary>
        public string EpithetOf(string role) =>
            Def.Roles.TryGetValue(role, out var r) && r.Pick == EventPicks.New ? r.Spawn?.Epithet : null;
    }
}
