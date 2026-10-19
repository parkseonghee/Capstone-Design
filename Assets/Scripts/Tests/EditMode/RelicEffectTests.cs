using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 조합레시피 문서로 새로 생긴 유물 효과 4종.
    ///  - R09 날카로운 삽: 공격력 +1
    ///  - R12 튼튼한 장화: 덫 효과 면역
    ///  - R10 넝마주이: 재료 드랍 확률 증가
    ///  - R11 재활용 장갑: 한 번의 공격으로 N마리 이상 잡으면 체력 회복
    /// </summary>
    public sealed class RelicEffectTests
    {
        private static FakeBoardConfig QuietBoard(Vector2Int playerStart)
            => new FakeBoardConfig { RowsOnStart = 0, BlocksPerSpawn = 0, PlayerStart = playerStart };

        // ── R09 공격력 ─────────────────────────────────────────────────────

        [Test]
        public void AttackBonus_IsBakedIntoThePlayer_EvenOnANewWave()
        {
            // 플레이어는 웨이브마다 새로 만들어진다. 생성 시점에 보너스가 얹혀야 다음 웨이브에도 남는다.
            var config = QuietBoard(new Vector2Int(4, 5));
            var mods = new RunModifiers();
            mods.AddAttack(1);

            GameLoop loop = GameLoopFactory.CreateWeek1(config, config, config, config, config, new MinRandom(), null, mods);

            Assert.AreEqual(config.Attack + 1, loop.Player.Attack);
        }

        [Test]
        public void PlayerAttack_GoesUpAndDown_ButNeverBelowZero()
        {
            var player = new Player(10, 1);

            player.IncreaseAttack(2);
            Assert.AreEqual(3, player.Attack);

            player.DecreaseAttack(10);
            Assert.AreEqual(0, player.Attack);
        }

        // ── R12 덫 면역 ───────────────────────────────────────────────────

        private static MoveResult StepOntoTrap(bool immune, out Player player, out BoardGrid grid)
        {
            var config = new FakeBoardConfig();
            player = Make.Player(10, 1);
            var mods = new RunModifiers();
            if (immune) { mods.GrantTrapImmunity(); }
            player.AttachModifiers(mods);

            grid = new BoardGrid(config.Cols, config.Rows);
            grid.Place(player, new Vector2Int(4, 5));
            grid.Place(new Trap(), new Vector2Int(4, 6));

            var resolver = new CombatMoveResolver(
                grid, player, config,
                new FakeCharacter(player.MaxHp, player.Attack, Offsets.BumpedOnly),
                new ChainFinder(grid, new SameTypeChainRule(), config.FirstPlayableRow),
                null,
                new TrapResolver(grid, new MinRandom()));

            return resolver.Resolve(Direction.Down);
        }

        [Test]
        public void WithoutBoots_ATrapThrowsThePlayerElsewhere()
        {
            Player player;
            BoardGrid grid;
            MoveResult result = StepOntoTrap(false, out player, out grid);

            Assert.AreEqual(MoveOutcome.Teleported, result.Outcome);
        }

        [Test]
        public void WithBoots_ThePlayerWalksOntoTheTrapAndItBreaks()
        {
            Player player;
            BoardGrid grid;
            MoveResult result = StepOntoTrap(true, out player, out grid);

            Assert.AreEqual(MoveOutcome.Moved, result.Outcome);
            Assert.AreEqual(new Vector2Int(4, 6), player.Position, "덫 칸으로 그냥 걸어 들어간다");
            Assert.AreSame(player, grid[new Vector2Int(4, 6)], "덫은 밟혀 사라진다");
        }

        // ── R10 드랍 확률 ─────────────────────────────────────────────────

        private sealed class RecordingTable : IMaterialDropTable
        {
            public int LastBonus = -1;

            public bool TryRoll(TrashType type, IRandomSource random, int bonusChance, out string materialId)
            {
                LastBonus = bonusChance;
                materialId = "M01";
                return true;
            }

            public int GuaranteeEvery => 0;

            public bool TryPickGuaranteed(IRandomSource random, out string materialId)
            {
                materialId = null;
                return false;
            }
        }

        [Test]
        public void DropChanceBonus_IsHandedToTheDropTable()
        {
            var table = new RecordingTable();
            var mods = new RunModifiers();
            mods.AddDropChance(5);
            var drops = new MaterialDropResolver(table, new MinRandom(), mods);

            drops.MaybeDrop(Make.Trash(TrashType.Paper));

            Assert.AreEqual(5, table.LastBonus);
        }

        [Test]
        public void WithoutModifiers_TheTableRollsAsIs()
        {
            var table = new RecordingTable();
            var drops = new MaterialDropResolver(table, new MinRandom());

            drops.MaybeDrop(Make.Trash(TrashType.Paper));

            Assert.AreEqual(0, table.LastBonus);
        }

        // ── R11 연쇄 회복 ─────────────────────────────────────────────────

        /// <summary>플레이어 바로 아래에 붙어 있는 같은 종류 적 count마리를 깔고 한 번 친다.</summary>
        private static int HpAfterChainKill(int count, int threshold)
        {
            var config = QuietBoard(new Vector2Int(4, 7));
            var mods = new RunModifiers();
            mods.SetChainHeal(threshold, 1);

            GameLoop loop = GameLoopFactory.CreateWeek1(config, config, config, config, config, new MinRandom(), null, mods);
            loop.Player.TakeDamage(2);

            // 맨 아래 줄에 가로로 붙여 깐다 — 바로 아래 칸을 치면 한 덩어리로 같이 죽는다.
            int row = config.Rows - 1;
            for (int i = 0; i < count; i++)
            {
                loop.Grid.Place(Make.Trash(TrashType.Paper, 1, 1), new Vector2Int(4 - count / 2 + i, row));
            }

            loop.Step(Direction.Down);
            return loop.Player.Hp;
        }

        [Test]
        public void ChainKillAtTheThreshold_Heals()
        {
            int before = new FakeBoardConfig().MaxHp - 2;
            Assert.AreEqual(before + 1, HpAfterChainKill(count: 3, threshold: 3));
        }

        [Test]
        public void ChainKillBelowTheThreshold_DoesNotHeal()
        {
            int before = new FakeBoardConfig().MaxHp - 2;
            Assert.AreEqual(before, HpAfterChainKill(count: 2, threshold: 3));
        }

        [Test]
        public void RemovingTheNewBonuses_UndoesThem()
        {
            var mods = new RunModifiers();
            mods.AddAttack(1);
            mods.GrantTrapImmunity();
            mods.AddDropChance(5);
            mods.SetChainHeal(5, 1);

            mods.RemoveAttack(1);
            mods.RevokeTrapImmunity();
            mods.RemoveDropChance(5);
            mods.ClearChainHeal();

            Assert.AreEqual(0, mods.BonusAttack);
            Assert.IsFalse(mods.ImmuneToTraps);
            Assert.AreEqual(0, mods.BonusDropChance);
            Assert.AreEqual(0, mods.ChainHealThreshold);
        }
    }
}
