using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Murim.Core.Persistence
{
    public class SaveResult
    {
        public bool Ok;
        public string Error;
    }

    public class ReadResult
    {
        public bool Ok;
        public string Error;
        public WorldState World;
        /// <summary>파일에 적혀 있던 저장 버전 (올리기 전).</summary>
        public int LoadedVersion;
    }

    public static class SaveSystem
    {
        /// <summary>컬렉션은 생성자에서 만든 Ordinal 비교자 인스턴스를 그대로 채운다 (결정론 유지).</summary>
        public static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public static string Serialize(WorldState w) => JsonConvert.SerializeObject(w, Settings);

        public static SaveResult Write(WorldState w, string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, Serialize(w));
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return new SaveResult { Ok = true };
            }
            catch (Exception e)
            {
                return new SaveResult { Ok = false, Error = "저장하지 못했습니다: " + e.Message };
            }
        }

        public static ReadResult Read(GameData d, string path)
        {
            WorldState w;
            try
            {
                if (!File.Exists(path)) return Fail("저장 파일이 없습니다");
                w = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(path), Settings);
            }
            catch (Exception e)
            {
                return Fail("저장 파일이 손상되었습니다: " + e.Message);
            }
            if (w == null) return Fail("저장 파일이 비어 있습니다");
            if (w.SaveVersion < WorldState.OldestLoadableVersion || w.SaveVersion > WorldState.CurrentSaveVersion)
                return Fail($"저장 파일 버전({w.SaveVersion})이 게임 버전({WorldState.CurrentSaveVersion})과 다릅니다");
            // 버전 1에는 사건 상태가 없다: 기본값(이야기꾼 "균형", 빈 대기 목록)으로 채운다.
            w.Events ??= new EventState();
            int loaded = w.SaveVersion;
            w.SaveVersion = WorldState.CurrentSaveVersion;
            var problems = Validate(d, w);
            if (problems.Count > 0) return Fail("저장 파일이 현재 데이터와 맞지 않습니다: " + string.Join(", ", problems.Take(5)));
            return new ReadResult { Ok = true, World = w, LoadedVersion = loaded };
        }

        static ReadResult Fail(string error) => new ReadResult { Ok = false, Error = error };

        static List<string> Validate(GameData d, WorldState w)
        {
            var problems = new List<string>();
            foreach (var c in w.Characters.Values)
            {
                foreach (var t in c.TraitIds)
                    if (!d.Traits.ContainsKey(t)) problems.Add($"알 수 없는 특성 '{t}'");
                foreach (var a in c.Arts ?? new List<Martial.KnownArt>())
                    if (a == null || !d.Arts.ContainsKey(a.ArtId ?? "")) problems.Add($"알 수 없는 무공 '{a?.ArtId}'");
                if (c.MainArtId != null && !d.Arts.ContainsKey(c.MainArtId)) problems.Add($"알 수 없는 무공 '{c.MainArtId}'");
                if (c.LocationRegionId != null && !d.Regions.ContainsKey(c.LocationRegionId))
                    problems.Add($"알 수 없는 지역 '{c.LocationRegionId}'");
            }
            foreach (var f in w.Factions.Values)
            {
                if (!d.Regions.ContainsKey(f.HomeRegionId)) problems.Add($"알 수 없는 지역 '{f.HomeRegionId}'");
                foreach (var b in f.Manor?.Buildings ?? new List<Manor.BuildingInstance>())
                    if (!d.Buildings.ContainsKey(b.DefId ?? "")) problems.Add($"알 수 없는 건물 '{b.DefId}'");
            }
            foreach (var r in d.Regions.Keys)
                if (!w.Regions.ContainsKey(r)) problems.Add($"지역 '{r}' 상태 없음");
            if (w.PlayerCharacterId != null && !w.Characters.ContainsKey(w.PlayerCharacterId)) problems.Add("플레이어 인물 없음");
            if (w.PendingRaid != null && !d.Events.ContainsKey(w.PendingRaid.EventId ?? "")) problems.Add($"알 수 없는 사건 '{w.PendingRaid.EventId}'");
            return problems.Distinct().ToList();
        }
    }
}
