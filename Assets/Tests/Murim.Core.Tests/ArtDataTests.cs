using System.Collections.Generic;
using System.Linq;
using Murim.Core.Data;
using Murim.Core.Martial;
using NUnit.Framework;

namespace Murim.Core.Tests
{
    public class ArtDataTests
    {
        static GameData Shipped => TestPaths.LoadShippedData();

        /// <summary>최소 데이터에 무공 파일 한 조각을 얹어 검증 오류를 본다.</summary>
        static List<string> ErrorsFor(string artsJson)
        {
            var files = TestJson.MinimalFiles();
            files["martial_arts.json"] = artsJson;
            var r = DataLoader.LoadFromStrings(files);
            var errors = new List<string>(r.Errors);
            errors.AddRange(ArtValidator.Validate(r.Data));
            return errors;
        }

        static string File(string arts, string narration = null) =>
            "{\"arts\":[" + arts + "],\"narration\":" + (narration ?? TestJson.MinimalNarration) + "}";

        const string Good = "{\"id\":\"s\",\"name\":\"검\",\"type\":\"Sword\",\"grade\":1,\"alignment\":\"Righteous\"," +
            "\"cards\":[{\"id\":\"s1\",\"name\":\"찌르기\",\"kind\":\"Attack\",\"style\":\"Fast\",\"power\":10,\"cost\":2}," +
            "{\"id\":\"s2\",\"name\":\"막기\",\"kind\":\"Defense\",\"style\":\"Hard\",\"power\":10,\"cost\":2}]}";

        [Test]
        public void Validate_GoodFile_NoErrors() => Assert.That(ErrorsFor(File(Good)), Is.Empty);

        [Test]
        public void Validate_DuplicateCardId_Error()
        {
            var dup = Good.Replace("\"id\":\"s\"", "\"id\":\"t\"");
            Assert.That(ErrorsFor(File(Good + "," + dup)), Has.Some.Contains("중복"));
        }

        [Test]
        public void Validate_InnerWithCards_Error() =>
            Assert.That(ErrorsFor(File(Good.Replace("\"Sword\"", "\"Inner\""))), Has.Some.Contains("내공심법에는 카드가 없어야"));

        [Test]
        public void Validate_ExternalWithOneCard_Error()
        {
            var one = "{\"id\":\"s\",\"name\":\"검\",\"type\":\"Sword\",\"grade\":1,\"alignment\":\"Righteous\"," +
                "\"cards\":[{\"id\":\"s1\",\"name\":\"찌르기\",\"kind\":\"Attack\",\"style\":\"Fast\",\"power\":10,\"cost\":2}]}";
            Assert.That(ErrorsFor(File(one)), Has.Some.Contains("2장 이상"));
        }

        [Test]
        public void Validate_UnknownFaction_Error() =>
            Assert.That(ErrorsFor(File(Good.Replace("\"grade\":1", "\"grade\":1,\"faction\":\"nowhere\""))), Has.Some.Contains("'nowhere'"));

        [Test]
        public void Validate_GradeOutOfRange_Error() =>
            Assert.That(ErrorsFor(File(Good.Replace("\"grade\":1", "\"grade\":6"))), Has.Some.Contains("1~5 밖"));

        [Test]
        public void Validate_NarrationBadToken_Error() =>
            Assert.That(ErrorsFor(File(Good, TestJson.MinimalNarration.Replace("{a:와}", "{zzz}"))), Has.Some.Contains("{zzz}"));

        [Test]
        public void Shipped_ArtsAbout60()
        {
            var arts = Shipped.Arts.Values.ToList();
            Assert.That(arts.Count, Is.InRange(55, 70));
            for (int g = 1; g <= 5; g++) Assert.That(arts.Any(a => a.IsInner && a.Grade == g), $"등급 {g} 내공심법");
            Assert.That(arts.Count(a => a.Alignment == Alignment.Demonic), Is.AtLeast(5));
        }

        [Test]
        public void Shipped_SecretArts_Cover25Factions() =>
            Assert.That(Shipped.Arts.Values.Where(a => a.Faction != null).Select(a => a.Faction).Distinct().Count(), Is.AtLeast(25));

        [Test]
        public void Shipped_EveryFactionArtHasStarterCard()
        {
            foreach (var a in Shipped.Arts.Values.Where(a => a.Faction != null && !a.IsInner))
                Assert.That(a.Cards.Count(c => c.MasteryMin <= 1), Is.AtLeast(2), a.Id);
        }

        [Test]
        public void Shipped_NarrationHasAllKeys()
        {
            foreach (var key in NarrationKeys.All)
            {
                Assert.That(Shipped.Narration.ContainsKey(key), key);
                int min = key == NarrationKeys.Win || key == NarrationKeys.Death || key == NarrationKeys.Timeout ? 2 : 3;
                Assert.That(Shipped.Narration[key].Length, Is.AtLeast(min), key);
            }
        }

        [Test]
        public void DuelText_QuotesCardAndAttachesJosa()
        {
            var words = new Dictionary<string, string> { ["a"] = "장삼", ["card"] = "운중월", ["guard"] = "태극원환" };
            Assert.That(DuelText.Format("{a:이} {card:을} 펼쳤고 {guard:으로} 막았다.", words),
                Is.EqualTo("장삼이 '운중월'을 펼쳤고 '태극원환'으로 막았다."));
        }
    }
}
