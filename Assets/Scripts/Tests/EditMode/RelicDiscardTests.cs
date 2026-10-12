using NUnit.Framework;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 유물을 쓰레기통에 버렸을 때 효과가 빠지는지. 걸었던 만큼만 빠지고,
    /// 기본값 밑으로는 내려가지 않아야 한다(다른 유물의 몫을 건드리면 안 된다).
    /// </summary>
    public sealed class RelicDiscardTests
    {
        [Test]
        public void RemovingBonuses_UndoesExactlyWhatWasAdded()
        {
            var mods = new RunModifiers();
            mods.AddMaxHp(3);
            mods.AddMaxHp(2);
            mods.AddBombs(5);
            mods.AddGoldPercent(20);
            mods.AddBlastRadius(1);
            mods.AddPotionHeal(1);

            mods.RemoveMaxHp(3);
            mods.RemoveBombs(5);
            mods.RemoveGoldPercent(20);
            mods.RemoveBlastRadius(1);
            mods.RemovePotionHeal(1);

            Assert.AreEqual(2, mods.BonusMaxHp, "다른 유물이 준 +2는 남아야 한다");
            Assert.AreEqual(0, mods.BonusBombs);
            Assert.AreEqual(100, mods.GoldPercent);
            Assert.AreEqual(0, mods.BonusBlastRadius);
            Assert.AreEqual(0, mods.BonusPotionHeal);
        }

        [Test]
        public void RemovingMoreThanAdded_StopsAtTheBaseValue()
        {
            var mods = new RunModifiers();

            mods.RemoveMaxHp(4);
            mods.RemoveGoldPercent(50);

            Assert.AreEqual(0, mods.BonusMaxHp);
            Assert.AreEqual(100, mods.GoldPercent, "골드 배율은 100% 밑으로 가지 않는다");
        }

        [Test]
        public void RevokingFlags_AndReviveAndWallHeal()
        {
            var mods = new RunModifiers();
            mods.GrantBlastImmunity();
            mods.GrantStatusImmunity();
            mods.AddRevive(3);
            mods.AddWallHeal(30, 1);

            mods.RevokeBlastImmunity();
            mods.RevokeStatusImmunity();
            mods.RemoveRevive();
            mods.ClearWallHeal();

            Assert.IsFalse(mods.ImmuneToBlast);
            Assert.IsFalse(mods.ImmuneToStatus);
            Assert.AreEqual(0, mods.Revives);
            Assert.IsFalse(mods.TrySpendRevive());
            Assert.AreEqual(0, mods.WallHealPercent);
            Assert.AreEqual(0, mods.WallHealAmount);
        }

        [Test]
        public void SpentRevive_CannotBeRemovedTwice()
        {
            var mods = new RunModifiers();
            mods.AddRevive(3);
            mods.TrySpendRevive();      // 이미 한 번 살아났다

            mods.RemoveRevive();

            Assert.AreEqual(0, mods.Revives);
        }

        [Test]
        public void DecreasingMaxHp_ClampsCurrentHp()
        {
            var player = new Player(10, 1);

            player.DecreaseMaxHp(3);

            Assert.AreEqual(7, player.MaxHp);
            Assert.AreEqual(7, player.Hp, "현재 체력이 새 최대치를 넘으면 깎인다");

            player.DecreaseMaxHp(100);
            Assert.AreEqual(1, player.MaxHp, "최대 체력은 1 밑으로 가지 않는다");
        }

        [Test]
        public void RemovingBombs_TakesOnlyWhatIsLeft()
        {
            var player = new Player(10, 1, bombs: 2);

            player.RemoveBombs(5);

            Assert.AreEqual(0, player.Bombs, "이미 쓴 폭탄까지 빚으로 남기지 않는다");
        }
    }
}
