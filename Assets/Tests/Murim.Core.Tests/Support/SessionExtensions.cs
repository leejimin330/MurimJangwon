using Murim.Core.Session;

namespace Murim.Core.Tests
{
    public static class SessionExtensions
    {
        /// <summary>여러 달을 돌리는 테스트용: 대기 사건을 AI 방식으로 바로 답한다.</summary>
        public static GameSession Auto(this GameSession s)
        {
            s.AutoResolveEvents = true;
            return s;
        }
    }
}
