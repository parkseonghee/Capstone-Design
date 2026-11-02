using NUnit.Framework;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 런 종료 정산(재화 문서 §1). 남은 스크랩 + 재료를 녹인 스크랩이 1:1로 골드가 된다.
    /// </summary>
    public sealed class RunSettlementTests
    {
        [Test]
        public void LeftoverAndMaterialScrapBothBecomeGold()
        {
            var player = new Player(3, 2);
            player.AddScrap(40);

            RunSettlement settlement = RunSettlement.Settle(player, 25);

            Assert.AreEqual(40, settlement.LeftoverScrap);
            Assert.AreEqual(25, settlement.MaterialScrap);
            Assert.AreEqual(65, settlement.TotalScrap);
            Assert.AreEqual(65, settlement.Gold, "스크랩 → 골드는 1:1이다");
        }

        [Test]
        public void SettlingEmptiesTheWallet()
        {
            // 비우지 않으면 같은 스크랩이 다음 정산에서 또 골드가 된다.
            var player = new Player(3, 2);
            player.AddScrap(12);

            RunSettlement.Settle(player, 0);

            Assert.AreEqual(0, player.Scrap);
        }

        [Test]
        public void SettlingTwiceDoesNotPayTwice()
        {
            var player = new Player(3, 2);
            player.AddScrap(30);

            RunSettlement.Settle(player, 0);
            RunSettlement second = RunSettlement.Settle(player, 0);

            Assert.AreEqual(0, second.Gold);
        }

        [Test]
        public void MaterialScrapIsNotMultipliedByTheScrapRelic()
        {
            // R03 배율은 런 중에 줍는 스크랩에만 먹는다. 정산에서 또 먹이면 두 번 계산된다.
            var mods = new RunModifiers();
            mods.AddGoldPercent(100);       // 200%

            var player = new Player(3, 2);
            player.AttachModifiers(mods);

            Assert.AreEqual(20, RunSettlement.Settle(player, 20).Gold);
        }

        [Test]
        public void NegativeMaterialScrapCountsAsZero()
        {
            var player = new Player(3, 2);
            player.AddScrap(5);

            Assert.AreEqual(5, RunSettlement.Settle(player, -10).Gold);
        }

        [Test]
        public void SettlingDoesNotTouchHpOrBombs()
        {
            var player = new Player(10, 2, bombs: 3);
            player.TakeDamage(4);
            player.AddScrap(50);

            RunSettlement.Settle(player, 10);

            Assert.AreEqual(6, player.Hp);
            Assert.AreEqual(3, player.Bombs);
        }
    }
}
