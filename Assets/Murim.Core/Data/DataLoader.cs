using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Murim.Core.Data
{
    public static class DataLoader
    {
        public static readonly string[] FileNames =
        {
            "regions.json", "factions.json", "traits.json", "scenarios.json", "names.json", "faction_name_parts.json",
        };

        public static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public static DataLoadResult LoadFromDirectory(string dir)
        {
            var files = new Dictionary<string, string>();
            var readErrors = new List<string>();
            foreach (var name in FileNames)
            {
                var path = Path.Combine(dir, name);
                try
                {
                    if (File.Exists(path)) files[name] = File.ReadAllText(path);
                }
                catch (Exception e)
                {
                    readErrors.Add($"{name}: 파일을 읽을 수 없습니다 - {e.Message}");
                }
            }
            var loaded = LoadFromStrings(files);
            loaded.Errors.InsertRange(0, readErrors);
            return loaded;
        }

        public static DataLoadResult LoadFromStrings(IReadOnlyDictionary<string, string> filesByName)
        {
            var result = new DataLoadResult { Data = new GameData() };
            var d = result.Data;
            LoadList(filesByName, "regions.json", result, (RegionDef x) => x.Id, d.Regions);
            LoadList(filesByName, "factions.json", result, (FactionDef x) => x.Id, d.Factions);
            LoadList(filesByName, "traits.json", result, (TraitDef x) => x.Id, d.Traits);
            LoadList(filesByName, "scenarios.json", result, (ScenarioDef x) => x.Id, d.Scenarios);
            d.Names = Parse<NameData>(filesByName, "names.json", result) ?? new NameData();
            d.FactionNameParts = Parse<FactionNameParts>(filesByName, "faction_name_parts.json", result) ?? new FactionNameParts();
            return result;
        }

        static T Parse<T>(IReadOnlyDictionary<string, string> files, string name, DataLoadResult result) where T : class
        {
            if (!files.TryGetValue(name, out var text))
            {
                result.Errors.Add($"{name}: 파일이 없습니다");
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<T>(text, Settings);
            }
            catch (JsonReaderException e)
            {
                result.Errors.Add($"{name}: JSON 형식 오류 (line {e.LineNumber}, position {e.LinePosition}) - {e.Message}");
            }
            catch (JsonException e)
            {
                result.Errors.Add($"{name}: 값 오류 - {e.Message}");
            }
            return null;
        }

        static void LoadList<T>(IReadOnlyDictionary<string, string> files, string name, DataLoadResult result,
            Func<T, string> id, IDictionary<string, T> target) where T : class
        {
            var list = Parse<List<T>>(files, name, result);
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var key = list[i] == null ? null : id(list[i]);
                if (string.IsNullOrEmpty(key))
                {
                    result.Errors.Add($"{name}: {i}번째 항목에 id가 없습니다");
                    continue;
                }
                if (target.ContainsKey(key))
                {
                    result.Errors.Add($"{name}: id '{key}'가 중복됩니다");
                    continue;
                }
                target[key] = list[i];
            }
        }
    }
}
