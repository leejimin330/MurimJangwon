using System.Collections.Generic;
using Murim.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Murim.Core.Martial
{
    public enum ArtType { Sword, Saber, Spear, Fist, Finger, Movement, Inner, Hidden }
    public enum CardKind { Attack, Defense, Evade, Finisher }
    public enum CardStyle { Fast, Hard, Soft }

    public class ArtRequires
    {
        public int RealmMin;
        public int ConstitutionMin;
        public int InsightMin;
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;
    }

    /// <summary>초식 카드 하나. 결투에서 한 합에 하나씩 낸다.</summary>
    public class CardDef
    {
        public string Id;
        public string Name;
        public CardKind Kind;
        public CardStyle Style;
        public int Power;
        /// <summary>내력 소모.</summary>
        public int Cost;
        /// <summary>이 성취 이상이어야 쓸 수 있다.</summary>
        public int MasteryMin;
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;

        [JsonIgnore] public bool IsOffense => Kind == CardKind.Attack || Kind == CardKind.Finisher;
    }

    /// <summary>martial_arts.json의 무공 하나.</summary>
    public class ArtDef
    {
        public string Id;
        public string Name;
        public string Description;
        public ArtType Type;
        /// <summary>1 삼류, 2 이류, 3 일류, 4 절정, 5 신공.</summary>
        public int Grade = 1;
        public Alignment Alignment;
        /// <summary>비전 무공이면 세력 id.</summary>
        public string Faction;
        public ArtRequires Requires = new ArtRequires();
        public List<CardDef> Cards = new List<CardDef>();
        [JsonExtensionData] public IDictionary<string, JToken> Unknown;

        [JsonIgnore] public bool IsInner => Type == ArtType.Inner;
        [JsonIgnore] public bool IsMovement => Type == ArtType.Movement;
    }

    /// <summary>martial_arts.json 최상위.</summary>
    public class ArtFile
    {
        public List<ArtDef> Arts = new List<ArtDef>();
        public Dictionary<string, string[]> Narration = new Dictionary<string, string[]>();
    }

    public static class ArtNames
    {
        public static readonly string[] GradeNames = { "", "삼류", "이류", "일류", "절정", "신공" };

        public static string Grade(int g) => g >= 1 && g < GradeNames.Length ? GradeNames[g] : "?";

        public static string Type(ArtType t) => t switch
        {
            ArtType.Sword => "검법",
            ArtType.Saber => "도법",
            ArtType.Spear => "창법",
            ArtType.Fist => "권장",
            ArtType.Finger => "지공",
            ArtType.Movement => "신법",
            ArtType.Inner => "내공심법",
            _ => "암기",
        };

        public static string Kind(CardKind k) => k switch
        {
            CardKind.Attack => "공격",
            CardKind.Defense => "방어",
            CardKind.Evade => "회피",
            _ => "절초",
        };

        public static string Style(CardStyle s) => s switch
        {
            CardStyle.Fast => "쾌",
            CardStyle.Hard => "강",
            _ => "유",
        };
    }

    public static class NarrationKeys
    {
        public const string Open = "open", Hit = "hit", Graze = "graze", Block = "block", Evade = "evade", Clash = "clash", Probe = "probe",
            Counter = "counter", Finisher = "finisher", Exhausted = "exhausted", Win = "win", Death = "death", Timeout = "timeout";

        public static readonly string[] All = { Open, Hit, Graze, Block, Evade, Clash, Probe, Counter, Finisher, Exhausted, Win, Death, Timeout };
    }
}
