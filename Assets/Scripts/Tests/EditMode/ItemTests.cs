using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 아이템(유물·일회성) 효과. 목록은 `리사이클라이프_아이템리스트csv.csv`의 R01~R10 / C01~C02.
    ///
    /// 유물은 전부 <see cref="RunModifiers"/>의 숫자를 올리고, Core의 각 부분이 그 숫자만 읽는다.
    /// 그래서 여기서는 "숫자가 실제 동작으로 이어지는가"를 본다.
    /// </summary>
    public sealed class ItemTests
    {
        private static FakeBoardConfig Board()
            => new FakeBoardConfig
            {
                RowsOnStart = 0,
                BlocksPerSpawn = 0,
                PlayerStart = new Vector2Int(4, 5),
            };

        private static GameLoop Loop(FakeBoardConfig config, RunModifiers mods)
            => GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom(), null, mods);

        // ── R01 / R02 튼튼한 몸 · 강인한 몸 ──────────────────────────────

        [Test]
        public void MaxHpRelic_RaisesTheStartingMaxHp()
        {
            var config = Board();
            config.MaxHp = 3;

            var mods = new RunModifiers();
            mods.AddMaxHp(2);                       // R02 강인한 몸

            Assert.AreEqual(5, Loop(config, mods).Player.MaxHp);
        }

        [Test]
        public void MaxHpRelic_FillsTheNewHeartsToo()
        {
            // 돈 내고 산 칸이 비어 있으면 산 느낌이 안 난다.
            var player = new RecycleLife.Core.Player(3, 2);
            player.TakeDamage(1);                   // 2/3

            player.IncreaseMaxHp(2);

            Assert.AreEqual(5, player.MaxHp);
            Assert.AreEqual(4, player.Hp);
        }

        // ── R03 황금 손 ──────────────────────────────────────────────────

        [Test]
        public void GoldRelic_MultipliesWhatYouEarn()
        {
            var mods = new RunModifiers();
            mods.AddGoldPercent(25);                // 100 -> 125%

            var player = new RecycleLife.Core.Player(3, 2);
            player.AttachModifiers(mods);
            player.AddGold(8);

            Assert.AreEqual(10, player.Gold, "8의 125%는 10이다");
        }

        [Test]
        public void WithoutTheGoldRelic_NothingChanges()
        {
            var player = new RecycleLife.Core.Player(3, 2);
            player.AttachModifiers(new RunModifiers());
            player.AddGold(8);

            Assert.AreEqual(8, player.Gold);
        }

        // ── R04 재생의 씨앗 ──────────────────────────────────────────────

        [Test]
        public void ReviveRelic_BringsYouBackOnce()
        {
            var mods = new RunModifiers();
            mods.AddRevive(2);

            var player = new RecycleLife.Core.Player(3, 2);
            player.AttachModifiers(mods);
            player.TakeDamage(99);
            Assert.IsTrue(player.IsDead, "먼저 죽어 있어야 하는 전제");

            Assert.IsTrue(player.TryRevive());
            Assert.AreEqual(2, player.Hp);
            Assert.IsFalse(player.IsDead);
        }

        [Test]
        public void ReviveRelic_OnlyWorksOnce()
        {
            var mods = new RunModifiers();
            mods.AddRevive(2);

            var player = new RecycleLife.Core.Player(3, 2);
            player.AttachModifiers(mods);
            player.TakeDamage(99);
            player.TryRevive();

            player.TakeDamage(99);
            Assert.IsFalse(player.TryRevive(), "부활은 한 번뿐이다");
            Assert.IsTrue(player.IsDead);
        }

        [Test]
        public void TheLoopRevivesInsteadOfEnding()
        {
            var config = Board();
            var mods = new RunModifiers();
            mods.AddRevive(1);

            GameLoop loop = Loop(config, mods);

            // 반격으로 확실히 죽는 적.
            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Make.Trash(TrashType.Paper, maxHp: 99, attack: 99), below);

            loop.Step(Direction.Down);

            Assert.IsFalse(loop.IsOver, "부활 유물이 있으면 판이 끝나면 안 된다");
            Assert.AreEqual(1, loop.Player.Hp);
        }

        // ── R05 대형 폭약 ────────────────────────────────────────────────

        [Test]
        public void BlastRadiusRelic_WidensTheExplosion()
        {
            var config = Board();
            config.BlastRadius = 1;                 // 3x3

            var mods = new RunModifiers();
            mods.AddBlastRadius(1);                 // -> 5x5

            Assert.AreEqual(2, new ModifiedBombConfig(config, mods).BlastRadius);
        }

        // ── R06 진한 포션 ────────────────────────────────────────────────

        [Test]
        public void PotionRelic_OnlyBoostsItems()
        {
            var stats = new FakeTrashStats(maxHp: 2, attack: 1)
                .SetPotion(TrashType.Potion, 2);

            var mods = new RunModifiers();
            mods.AddPotionHeal(1);

            var boosted = new ModifiedTrashStats(stats, mods);

            Assert.AreEqual(3, boosted.For(TrashType.Potion).Heal, "포션은 2 -> 3");
            Assert.AreEqual(2, boosted.For(TrashType.Paper).MaxHp, "적의 값은 그대로여야 한다");
            Assert.AreEqual(0, boosted.For(TrashType.Paper).Heal);
        }

        // ── R08 폭탄 주머니 ──────────────────────────────────────────────

        [Test]
        public void BombRelic_RaisesTheStartingCount()
        {
            var config = Board();
            config.StartingCount = 3;

            var mods = new RunModifiers();
            mods.AddBombs(5);

            Assert.AreEqual(8, Loop(config, mods).Player.Bombs);
        }

        // ── R09 방폭 장비 ────────────────────────────────────────────────

        [Test]
        public void BlastImmunityRelic_TurnsOffSelfDamage()
        {
            var config = Board();
            config.DamagesPlayer = true;

            var mods = new RunModifiers();
            mods.GrantBlastImmunity();

            Assert.IsFalse(new ModifiedBombConfig(config, mods).DamagesPlayer,
                "CSV 비고: 자신의 폭탄 피해 무효");
        }

        // ── C01 정화의 물약 ──────────────────────────────────────────────

        [Test]
        public void CleansingPotion_DamagesEveryEnemy()
        {
            var config = Board();
            GameLoop loop = Loop(config, new RunModifiers());

            // 적 둘(체력 1)과 벽 하나를 깔아 둔다.
            loop.Grid.Place(Make.Trash(TrashType.Paper, 1, 1), new Vector2Int(0, 8));
            loop.Grid.Place(Make.Trash(TrashType.Plastic, 1, 1), new Vector2Int(1, 8));
            loop.Grid.Place(Make.Trash(TrashType.Wood, 5, 0), new Vector2Int(2, 8));

            int hit = loop.DamageAllEnemies(1);

            Assert.AreEqual(2, hit, "적만 맞아야 한다");
            Assert.IsNull(loop.Grid[new Vector2Int(0, 8)]);
            Assert.IsNull(loop.Grid[new Vector2Int(1, 8)]);
            Assert.IsNotNull(loop.Grid[new Vector2Int(2, 8)], "벽은 그대로여야 한다");
        }

        [Test]
        public void CleansingPotion_PaysGoldForWhatItKills()
        {
            var config = Board();
            GameLoop loop = Loop(config, new RunModifiers());

            loop.Grid.Place(
                new Trash(TrashType.Paper, new TrashStats(1, 1, 0, 0, true, 7)),
                new Vector2Int(0, 8));

            loop.DamageAllEnemies(1);

            Assert.AreEqual(7, loop.Player.Gold);
        }

        // ── C02 대청소 물약 ──────────────────────────────────────────────

        [Test]
        public void SweepPotion_KillsUpToTheGivenCount()
        {
            var config = Board();
            GameLoop loop = Loop(config, new RunModifiers());

            for (int c = 0; c < 6; c++)
            {
                loop.Grid.Place(Make.Trash(TrashType.Paper, 9, 1), new Vector2Int(c, 8));
            }

            int killed = loop.KillEnemies(5);

            Assert.AreEqual(5, killed, "체력이 남아 있어도 즉사시킨다");
        }

        [Test]
        public void SweepPotion_StopsWhenThereAreNoEnemiesLeft()
        {
            var config = Board();
            GameLoop loop = Loop(config, new RunModifiers());

            loop.Grid.Place(Make.Trash(TrashType.Paper, 1, 1), new Vector2Int(0, 8));

            Assert.AreEqual(1, loop.KillEnemies(5), "한 마리뿐이면 한 마리만");
        }

        [Test]
        public void SweepPotion_LeavesWallsAlone()
        {
            var config = Board();
            GameLoop loop = Loop(config, new RunModifiers());

            loop.Grid.Place(Make.Trash(TrashType.Wood, 3, 0), new Vector2Int(0, 8));

            Assert.AreEqual(0, loop.KillEnemies(5), "벽은 '일반 몬스터'가 아니다");
            Assert.IsNotNull(loop.Grid[new Vector2Int(0, 8)]);
        }

        // ── 효과가 없는 유물 ─────────────────────────────────────────────

        [Test]
        public void StatusImmunityRelic_IsInertForNow()
        {
            // R10 정화 필터. 상태이상 기믹 자체가 미구현이라 표시만 된다.
            var mods = new RunModifiers();
            mods.GrantStatusImmunity();

            Assert.IsTrue(mods.ImmuneToStatus, "값은 서 있어야 기믹이 생겼을 때 바로 읽힌다");
        }
    }
}
