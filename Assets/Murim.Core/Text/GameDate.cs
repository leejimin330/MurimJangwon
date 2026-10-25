namespace Murim.Core.Text
{
    /// <summary>월 번호 0 = 1년 1월.</summary>
    public static class GameDate
    {
        public static int Year(int month) => month / 12 + 1;
        public static int MonthOfYear(int month) => month % 12 + 1;
        public static string Format(int month) => $"{Year(month)}년 {MonthOfYear(month)}월";
    }
}
