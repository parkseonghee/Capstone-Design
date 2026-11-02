using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 스크랩 수급(재화 문서 §1·§3-2). 몹 종류가 아니라 <b>스테이지별 확률</b>로 떨어지고,
    /// 런 중 상점은 이걸로 산다.
    /// </summary>
    public sealed class ScrapTests
    {
        private static FakeBoardConfig Board()
            => new FakeBoardConfig
            {
                RowsOnStart = 0,
                BlocksPerSpawn = 0,
                PlayerStart = new Vector2Int(4, 5),
            };

        private static Trash Mob(TrashType type, int maxHp, int attack)
            => new Trash(type, new TrashStats(maxHp, attack, 0, 0, true));

        /// <summary>스테이지 번호(1부터) 순서대로 확률을 들고 있는 표.</summary>
        private sealed class FakeScrapTable : IScrapDropTable
        {
            private readonly int[] _chances;

            public FakeScrapTable(int amount, params int[] chances)
            {
                AmountPerDrop = amount;
                _chances = chances;
            }

            public int AmountPerDrop { get; }

            public int ChanceFor(int stageNumber)
                => stageNumber >= 1 && stageNumber <= _chances.Length ? _chances[stageNumber - 1] : 0;
        }

        /// <summary>스테이지 번호만 의미 있는 최소 웨이브.</summary>
        private sealed class StageWave : IWaveConfig
        {
            public StageWave(int stage) { StageNumber = stage; }

            public int StageNumber { get; }

            public int WaveNumber => 1;

            public int KillGoal => 99;

            public int SpawnWeightFor(TrashType type) => type == TrashType.Paper ? 10 : 0;
        }

        /// <summary>MinRandom은 굴림이 늘 0이라, 확률이 1%만 넘어도 무조건 떨어진다.</summary>
        private static GameLoop Loop(FakeBoardConfig config, IScrapDropTable table, WaveRunner waves = null)
            => GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom(), waves, scrapDrops: table);

        private static Vector2Int Below(GameLoop loop)
            => new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);

        // ── 확률 판정 (ScrapDropResolver) ─────────────────────────────────

        [Test]
        public void ARollUnderTheChanceDrops()
        {
            var resolver = new ScrapDropResolver(new FakeScrapTable(1, 25), new ScriptedRandom(24));

            Assert.AreEqual(1, resolver.Roll(Mob(TrashType.Paper, 1, 1)));
        }

        [Test]
        public void ARollAtTheChanceMisses()
        {
            // 0~99 중 chance 미만만 당첨이다. 25%면 0~24가 맞고 25는 빗나간다.
            var resolver = new ScrapDropResolver(new FakeScrapTable(1, 25), new ScriptedRandom(25));

            Assert.AreEqual(0, resolver.Roll(Mob(TrashType.Paper, 1, 1)));
        }

        [Test]
        public void TheChanceFollowsTheCurrentStage()
        {
            // 공원 0% / 한강 100% — 지금 웨이브가 2스테이지면 한강 확률을 써야 한다.
            var table = new FakeScrapTable(1, 0, 100);
            var runner = new WaveRunner(new IWaveConfig[] { new StageWave(2) });
            var resolver = new ScrapDropResolver(table, new MinRandom(), runner);

            Assert.AreEqual(1, resolver.Roll(Mob(TrashType.Paper, 1, 1)));
        }

        [Test]
        public void WithoutWavesItUsesTheFirstStage()
        {
            var resolver = new ScrapDropResolver(new FakeScrapTable(1, 100, 0), new MinRandom());

            Assert.AreEqual(1, resolver.Roll(Mob(TrashType.Paper, 1, 1)));
        }

        [Test]
        public void WallsNeverDropScrap()
        {
            // 공격력 0 = 벽. 스크랩은 몹 드랍이다.
            var resolver = new ScrapDropResolver(new FakeScrapTable(1, 100), new MinRandom());

            Assert.AreEqual(0, resolver.Roll(Mob(TrashType.Wood, 1, 0)));
        }

        [Test]
        public void TheAmountPerDropComesFromTheTable()
        {
            var resolver = new ScrapDropResolver(new FakeScrapTable(3, 100), new MinRandom());

            Assert.AreEqual(3, resolver.Roll(Mob(TrashType.Paper, 1, 1)));
        }

        // ── 처치로 모이는 스크랩 ─────────────────────────────────────────

        [Test]
        public void KillingAnEnemyDropsScrap()
        {
            var config = Board();
            GameLoop loop = Loop(config, new FakeScrapTable(1, 100));
            loop.Grid.Place(Mob(TrashType.Paper, 1, 1), Below(loop));

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(1, result.Scrap);
            Assert.AreEqual(1, loop.Player.Scrap);
        }

        [Test]
        public void SurvivingAnEnemyDropsNothing()
        {
            var config = Board();
            GameLoop loop = Loop(config, new FakeScrapTable(1, 100));

            // 체력 9라 한 대로는 안 죽는다. 스크랩은 죽어야 나온다.
            loop.Grid.Place(Mob(TrashType.Paper, 9, 1), Below(loop));

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(0, result.Scrap);
            Assert.AreEqual(0, loop.Player.Scrap);
        }

        [Test]
        public void ChainKillsRollOncePerEnemy()
        {
            var config = Board();
            GameLoop loop = Loop(config, new FakeScrapTable(1, 100));

            // 같은 종류 셋을 세로로 이어 붙이면 한 번에 묶여 맞는다.
            for (int i = 1; i <= 3; i++)
            {
                loop.Grid.Place(
                    Mob(TrashType.Paper, 1, 1),
                    new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + i));
            }

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(3, result.Killed, "셋이 연쇄로 죽어야 한다");
            Assert.AreEqual(3, result.Scrap, "죽은 수만큼 굴려야 한다");
        }

        [Test]
        public void WithoutATableNothingDrops()
        {
            // 표를 안 꽂으면(구형 배선·테스트) 스크랩 기믹이 통째로 꺼진다.
            var config = Board();
            GameLoop loop = Loop(config, null);
            loop.Grid.Place(Mob(TrashType.Paper, 1, 1), Below(loop));

            Assert.AreEqual(0, loop.Step(Direction.Down).Scrap);
        }

        [Test]
        public void BombKillsAlsoDropScrap()
        {
            var config = Board();
            config.FuseTurns = 1;
            config.Damage = 5;
            config.BlastRadius = 1;
            config.DamagesPlayer = false;   // 플레이어가 먼저 죽으면 스텝이 끊긴다

            GameLoop loop = Loop(config, new FakeScrapTable(1, 100));

            var player = loop.Player;
            player.AddBombs(1);

            // 폭탄을 옆에 놓고, 그 폭탄 옆에 적을 둔다.
            var spot = new Vector2Int(player.Position.x - 1, player.Position.y);
            loop.Grid.Place(Mob(TrashType.Paper, 1, 1), new Vector2Int(spot.x - 1, spot.y));
            loop.PlaceBomb(spot);

            // 때려서 점화한 뒤, 도화선이 다 탈 때까지 돌린다.
            loop.Step(Direction.Left);

            int guard = 0;
            while (player.Scrap == 0 && guard++ < 10)
            {
                loop.Step(Direction.Up);
            }

            Assert.AreEqual(1, player.Scrap, "폭탄으로 죽인 적도 스크랩을 떨어뜨려야 한다");
        }

        [Test]
        public void ItemKillsAlsoDropScrap()
        {
            // 전체 피해 아이템(C 계열)으로 죽여도 같은 규칙이다 — 죽는 곳이 어디든 한 리졸버를 거친다.
            var config = Board();
            GameLoop loop = Loop(config, new FakeScrapTable(1, 100));
            loop.Grid.Place(Mob(TrashType.Paper, 1, 1), new Vector2Int(0, 8));

            loop.DamageAllEnemies(1);

            Assert.AreEqual(1, loop.Player.Scrap);
        }

        // ── 지갑 ─────────────────────────────────────────────────────────

        [Test]
        public void SpendingTakesFromTheWallet()
        {
            var player = new RecycleLife.Core.Player(3, 2);
            player.AddScrap(10);

            Assert.IsTrue(player.SpendScrap(4));
            Assert.AreEqual(6, player.Scrap);
        }

        [Test]
        public void SpendingMoreThanYouHaveDoesNothing()
        {
            var player = new RecycleLife.Core.Player(3, 2);
            player.AddScrap(3);

            Assert.IsFalse(player.SpendScrap(5));
            Assert.AreEqual(3, player.Scrap, "실패한 결제가 잔액을 건드리면 안 된다");
        }

        [Test]
        public void AddingNegativeScrapDoesNothing()
        {
            var player = new RecycleLife.Core.Player(3, 2);
            player.AddScrap(5);
            player.AddScrap(-100);

            Assert.AreEqual(5, player.Scrap);
        }

        [Test]
        public void DepositSkipsTheScrapRelic()
        {
            // 재료 변환·환불은 줍는 돈이 아니다. R03을 먹이면 녹였다 사는 것만으로 돈이 불어난다.
            var mods = new RunModifiers();
            mods.AddGoldPercent(100);       // 200%

            var player = new RecycleLife.Core.Player(3, 2);
            player.AttachModifiers(mods);
            player.DepositScrap(10);

            Assert.AreEqual(10, player.Scrap);
        }

        [Test]
        public void DepositingNothingDoesNothing()
        {
            var player = new RecycleLife.Core.Player(3, 2);
            player.DepositScrap(5);
            player.DepositScrap(-3);

            Assert.AreEqual(5, player.Scrap);
        }

        // ── 스테이지 넘어갈 때 지갑 정산 (BankScrap) ─────────────────────
        //
        // 클리어할 때 스크랩을 골드 총액으로 이관하고 나면 지갑에서 지워야
        // 다음 스테이지에서 또 이관되지 않는다.

        [Test]
        public void BankScrapResetsTheWalletToZero()
        {
            var player = new RecycleLife.Core.Player(3, 2);
            player.AddScrap(24);

            player.BankScrap();

            Assert.AreEqual(0, player.Scrap);
        }

        [Test]
        public void BankScrapDoesNotTouchHpOrBombs()
        {
            var player = new RecycleLife.Core.Player(10, 2, bombs: 3);
            player.TakeDamage(4);
            player.AddScrap(50);

            player.BankScrap();

            Assert.AreEqual(6, player.Hp, "스크랩 정산이 체력을 건드리면 안 된다");
            Assert.AreEqual(3, player.Bombs, "스크랩 정산이 폭탄 수를 건드리면 안 된다");
        }

        [Test]
        public void WithSpawnWeightKeepsEveryOtherField()
        {
            var original = new TrashStats(4, 2, 1, 3, false, true, true);

            TrashStats copy = original.WithSpawnWeight(7);

            Assert.AreEqual(7, copy.SpawnWeight);
            Assert.AreEqual(4, copy.MaxHp);
            Assert.AreEqual(2, copy.Attack);
            Assert.AreEqual(1, copy.Heal);
            Assert.IsFalse(copy.ChainsWithSameType);
            Assert.IsTrue(copy.LeavesTrap);
            Assert.IsTrue(copy.LeavesPoison);
        }
    }
}
