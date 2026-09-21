using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 골드 수급. 종류마다 떨어지는 금액이 다르고, 값은 전부 TrashStatsConfig에서 온다.
    /// </summary>
    public sealed class GoldTests
    {
        private static FakeBoardConfig Board()
            => new FakeBoardConfig
            {
                RowsOnStart = 0,
                BlocksPerSpawn = 0,
                PlayerStart = new Vector2Int(4, 5),
            };

        private static Trash Mob(TrashType type, int maxHp, int attack, int gold)
            => new Trash(type, new TrashStats(maxHp, attack, 0, 0, true, gold));

        // ── 값이 흘러가는지 ──────────────────────────────────────────────

        [Test]
        public void TrashCarriesTheGoldFromItsStats()
        {
            Assert.AreEqual(5, Mob(TrashType.Sludge, 4, 2, 5).Gold);
        }

        [Test]
        public void NegativeGoldIsClampedToZero()
        {
            Assert.AreEqual(0, Mob(TrashType.Paper, 1, 1, -3).Gold);
        }

        [Test]
        public void TheOldConstructorStillMeansNoGold()
        {
            // gold를 안 넘기는 기존 호출이 조용히 돈을 주기 시작하면 안 된다.
            var stats = new TrashStats(2, 1, 0, 10, true);
            Assert.AreEqual(0, stats.Gold);
        }

        // ── 처치로 버는 골드 ─────────────────────────────────────────────

        [Test]
        public void KillingAnEnemyPaysItsOwnAmount()
        {
            var config = Board();
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Mob(TrashType.Paper, 1, 1, 7), below);

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(7, result.Gold);
            Assert.AreEqual(7, loop.Player.Gold);
        }

        [Test]
        public void SurvivingAnEnemyPaysNothing()
        {
            var config = Board();
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            // 체력 9라 한 대로는 안 죽는다. 돈은 죽어야 나온다.
            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Mob(TrashType.Paper, 9, 1, 7), below);

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(0, result.Gold);
            Assert.AreEqual(0, loop.Player.Gold);
        }

        [Test]
        public void DifferentTypesPayDifferentAmounts()
        {
            // 요청의 핵심: 몬스터마다 금액이 다르다.
            var stats = new FakeTrashStats()
                .SetGold(TrashType.Paper, 1)
                .SetGold(TrashType.Plastic, 4);

            Assert.AreEqual(1, stats.For(TrashType.Paper).Gold);
            Assert.AreEqual(4, stats.For(TrashType.Plastic).Gold);
        }

        [Test]
        public void ChainKillsAddUp()
        {
            var config = Board();
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            // 같은 종류 셋을 세로로 이어 붙이면 한 번에 묶여 맞는다.
            for (int i = 1; i <= 3; i++)
            {
                loop.Grid.Place(
                    Mob(TrashType.Paper, 1, 1, 3),
                    new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + i));
            }

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(3, result.Killed, "셋이 연쇄로 죽어야 한다");
            Assert.AreEqual(9, result.Gold, "죽은 수만큼 합산돼야 한다");
            Assert.AreEqual(9, loop.Player.Gold);
        }

        [Test]
        public void WallsPayNothingByDefault()
        {
            var config = Board();
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            // 공격력 0 = 벽. 기본 표에서 gold도 0이다.
            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Mob(TrashType.Wood, 1, 0, 0), below);

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(1, result.WallsDestroyed);
            Assert.AreEqual(0, result.Gold);
        }

        [Test]
        public void AWallCanBeGivenGoldWithoutTouchingCode()
        {
            // 벽 파괴 보상은 미정이다. 정해지면 표의 값만 올리면 되게 돼 있어야 한다.
            var config = Board();
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Mob(TrashType.Wood, 1, 0, 6), below);

            Assert.AreEqual(6, loop.Step(Direction.Down).Gold);
        }

        // ── 폭탄으로 죽여도 돈이 나온다 (폭탄 기획 §1-1) ─────────────────

        [Test]
        public void BombKillsAlsoPay()
        {
            var config = Board();
            config.FuseTurns = 1;
            config.Damage = 5;
            config.BlastRadius = 1;
            config.DamagesPlayer = false;   // 플레이어가 먼저 죽으면 스텝이 끊긴다

            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            var player = loop.Player;
            player.AddBombs(1);

            // 폭탄을 옆에 놓고, 그 폭탄 옆에 적을 둔다.
            var spot = new Vector2Int(player.Position.x - 1, player.Position.y);
            loop.Grid.Place(Mob(TrashType.Paper, 1, 1, 8), new Vector2Int(spot.x - 1, spot.y));
            loop.PlaceBomb(spot);

            // 때려서 점화한 뒤, 도화선이 다 탈 때까지 돌린다.
            loop.Step(Direction.Left);

            int guard = 0;
            while (player.Gold == 0 && guard++ < 10)
            {
                loop.Step(Direction.Up);
            }

            Assert.AreEqual(8, player.Gold, "폭탄으로 죽인 적도 돈을 떨어뜨려야 한다");
        }

        // ── 웨이브를 거쳐도 금액이 살아남는지 ────────────────────────────

        /// <summary>목표와 등장 종류만 들고 있는 최소 웨이브.</summary>
        private sealed class OneTypeWave : IWaveConfig
        {
            private readonly TrashType _type;

            public OneTypeWave(TrashType type) { _type = type; }

            public int StageNumber => 1;

            public int WaveNumber => 1;

            public int KillGoal => 99;

            public int SpawnWeightFor(TrashType type) => type == _type ? 10 : 0;
        }

        [Test]
        public void SwappingSpawnWeightsKeepsTheGold()
        {
            // 웨이브는 스폰 가중치만 덮어써야 한다. 예전엔 여기서 골드가 0으로 떨어져
            // 실제 플레이에서 돈이 한 푼도 안 들어왔다.
            var global = new FakeTrashStats().SetGold(TrashType.Paper, 6);
            var runner = new WaveRunner(new IWaveConfig[] { new OneTypeWave(TrashType.Paper) });

            TrashStats stats = new WaveSpawnWeights(global, runner).For(TrashType.Paper);

            Assert.AreEqual(10, stats.SpawnWeight, "가중치는 웨이브 값이어야 한다");
            Assert.AreEqual(6, stats.Gold, "골드는 전역 표 값이 그대로 살아야 한다");
        }

        [Test]
        public void WithSpawnWeightKeepsEveryOtherField()
        {
            var original = new TrashStats(4, 2, 1, 3, false, 9);

            TrashStats copy = original.WithSpawnWeight(7);

            Assert.AreEqual(7, copy.SpawnWeight);
            Assert.AreEqual(4, copy.MaxHp);
            Assert.AreEqual(2, copy.Attack);
            Assert.AreEqual(1, copy.Heal);
            Assert.AreEqual(9, copy.Gold);
            Assert.IsFalse(copy.ChainsWithSameType);
        }

        [Test]
        public void EnemiesSpawnedDuringAWaveStillPay()
        {
            // 위 단위 테스트의 통합판 — 실제 스폰 경로로 나온 블록이 돈을 들고 있는지.
            var config = Board();
            var stats = new FakeTrashStats(maxHp: 1, attack: 1).SetGold(TrashType.Paper, 6);
            var runner = new WaveRunner(new IWaveConfig[] { new OneTypeWave(TrashType.Paper) });

            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, stats, config, config, new MinRandom(), runner);

            var spawned = new WaveSpawnWeights(stats, runner).For(TrashType.Paper);
            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(new Trash(TrashType.Paper, spawned), below);

            Assert.AreEqual(6, loop.Step(Direction.Down).Gold);
        }

        // ── 지갑 ─────────────────────────────────────────────────────────

        [Test]
        public void SpendingTakesFromTheWallet()
        {
            var player = new RecycleLife.Core.Player(3, 2);
            player.AddGold(10);

            Assert.IsTrue(player.SpendGold(4));
            Assert.AreEqual(6, player.Gold);
        }

        [Test]
        public void SpendingMoreThanYouHaveDoesNothing()
        {
            var player = new RecycleLife.Core.Player(3, 2);
            player.AddGold(3);

            Assert.IsFalse(player.SpendGold(5));
            Assert.AreEqual(3, player.Gold, "실패한 결제가 잔액을 건드리면 안 된다");
        }

        [Test]
        public void AddingNegativeGoldDoesNothing()
        {
            var player = new RecycleLife.Core.Player(3, 2);
            player.AddGold(5);
            player.AddGold(-100);

            Assert.AreEqual(5, player.Gold);
        }
    }
}
