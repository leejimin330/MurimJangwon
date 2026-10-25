using Murim.Core.Data;
using UnityEngine;

namespace Murim.View.UI
{
    /// <summary>청록 산수화 팔레트. 붉은 인장색은 "다음 달" 버튼에만 쓴다.</summary>
    public static class Theme
    {
        public static readonly Color Ground = Hex("#1C2628");
        public static readonly Color Panel = Hex("#273634");
        public static readonly Color PanelRaised = Hex("#32443F");
        public static readonly Color Line = Hex("#41564F");
        public static readonly Color Ink = Hex("#E4DFCF");
        public static readonly Color InkDim = Hex("#A3A898");
        public static readonly Color InkFaint = Hex("#6E7A72");
        public static readonly Color Gold = Hex("#C9A24A");
        public static readonly Color Seal = Hex("#A8322D");
        public static readonly Color SealInk = Hex("#F3E6D8");
        public static readonly Color Righteous = Hex("#5C86B8");
        public static readonly Color Unorthodox = Hex("#C08A2E");
        public static readonly Color Demonic = Hex("#9E2B35");
        public static readonly Color Unclaimed = Hex("#56605B");
        public static readonly Color Disabled = Hex("#2A3331");

        public const int TitleSize = 64;
        public const int HeadingSize = 30;
        public const int SubheadingSize = 24;
        public const int BodySize = 20;
        public const int SmallSize = 17;

        public static Color Of(Alignment a) => a switch
        {
            Alignment.Righteous => Righteous,
            Alignment.Unorthodox => Unorthodox,
            _ => Demonic,
        };

        public static string AlignmentName(Alignment a) => a switch
        {
            Alignment.Righteous => "정파",
            Alignment.Unorthodox => "사파",
            _ => "마도",
        };

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
