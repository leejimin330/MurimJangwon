using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Murim.Core.World
{
    /// <summary>저장되는 세계 전체 상태.</summary>
    public class WorldState
    {
        public const int CurrentSaveVersion = 3;
        public const int OldestLoadableVersion = 1;

        public int SaveVersion = CurrentSaveVersion;
        public int Month;
        public ulong RngState;
        public string PlayerCharacterId;
        public bool GameOver;

        public SortedDictionary<string, Character> Characters = new SortedDictionary<string, Character>(StringComparer.Ordinal);
        public SortedDictionary<string, Faction> Factions = new SortedDictionary<string, Faction>(StringComparer.Ordinal);
        public SortedDictionary<string, RegionState> Regions = new SortedDictionary<string, RegionState>(StringComparer.Ordinal);
        public OpinionStore Opinions = new OpinionStore();
        /// <summary>세력 쌍(PairKey) → 동맹 점수.</summary>
        public SortedDictionary<string, int> AllianceScores = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public SortedSet<string> Alliances = new SortedSet<string>(StringComparer.Ordinal);
        public List<ChronicleEntry> Chronicle = new List<ChronicleEntry>();
        public EventState Events = new EventState();

        public int NextCharacterNumber = 1;
        public int NextFactionNumber = 1;

        public string NewCharacterId() => "c" + (NextCharacterNumber++).ToString("D6");
        public string NewFactionId() => "f" + (NextFactionNumber++).ToString("D4");

        public static string PairKey(string a, string b) =>
            string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

        [JsonIgnore] public Character Player => PlayerCharacterId != null && Characters.TryGetValue(PlayerCharacterId, out var c) ? c : null;

        public Character Get(string characterId) =>
            characterId != null && Characters.TryGetValue(characterId, out var c) ? c : null;

        public Faction GetFaction(string factionId) =>
            factionId != null && Factions.TryGetValue(factionId, out var f) ? f : null;

        public int AllianceScore(string a, string b) => AllianceScores.TryGetValue(PairKey(a, b), out var v) ? v : 0;

        public void AddAllianceScore(string a, string b, int delta)
        {
            var key = PairKey(a, b);
            AllianceScores.TryGetValue(key, out var v);
            AllianceScores[key] = v + delta;
        }

        public bool AreAllied(string a, string b) => Alliances.Contains(PairKey(a, b));
    }
}
