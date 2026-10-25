namespace Murim.Core.World
{
    public class ChronicleEntry
    {
        public int Month;
        /// <summary>문장 종류. Chronicle 템플릿 메서드 이름과 같다 (예: "Married").</summary>
        public string Kind;
        public string Text;
        public string[] InvolvedIds;
        /// <summary>0 낮음, 1 보통, 2 높음.</summary>
        public int Importance;
    }
}
