using Murim.Core.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Murim.Core.Persistence
{
    public static class SaveSystem
    {
        public static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static string Serialize(WorldState w) => JsonConvert.SerializeObject(w, Settings);
    }
}
