using Murim.Core.Data;

namespace Murim.Core.World
{
    /// <summary>원한·은혜 기억. 호감도에 반영되고 자식에게 대물림된다.</summary>
    public class Memory
    {
        public MemoryKind Kind;
        public string TargetId;
        public string Reason;
        public int Month;
        public int Strength;
    }
}
