using Murim.Core.Text;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class JosaTests
    {
        [Test]
        public void IGa_ByFinalConsonant()
        {
            Assert.That(Josa.IGa("장삼"), Is.EqualTo("장삼이"));
            Assert.That(Josa.IGa("무당파"), Is.EqualTo("무당파가"));
            Assert.That(Josa.EulReul("남궁세가"), Is.EqualTo("남궁세가를"));
            Assert.That(Josa.EulReul("소림"), Is.EqualTo("소림을"));
            Assert.That(Josa.WaGwa("소림"), Is.EqualTo("소림과"));
            Assert.That(Josa.WaGwa("화산파"), Is.EqualTo("화산파와"));
            Assert.That(Josa.EunNeun("진룡"), Is.EqualTo("진룡은"));
            Assert.That(Josa.EunNeun("설화"), Is.EqualTo("설화는"));
            Assert.That(Josa.EuroRo("사천"), Is.EqualTo("사천으로"));
            Assert.That(Josa.EuroRo("감숙"), Is.EqualTo("감숙으로"));
            Assert.That(Josa.EuroRo("하남"), Is.EqualTo("하남으로"));
            Assert.That(Josa.EuroRo("호북"), Is.EqualTo("호북으로"));
            Assert.That(Josa.EuroRo("광서"), Is.EqualTo("광서로"));
            Assert.That(Josa.EuroRo("하늘"), Is.EqualTo("하늘로"));
        }

        [Test]
        public void NonHangul_UsesBothForms()
        {
            Assert.That(Josa.IGa("ABC"), Is.EqualTo("ABC이(가)"));
        }

        [Test]
        public void GameDate_Formats()
        {
            Assert.That(GameDate.Format(0), Is.EqualTo("1년 1월"));
            Assert.That(GameDate.Format(13), Is.EqualTo("2년 2월"));
        }
    }
}
