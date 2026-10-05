using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 독 몬스터 기믹. 기획 확정 다섯 줄을 그대로 검사한다:
    ///  1. 죽으면 그 자리에 독이 생긴다.
    ///  2. 독은 떨어지지 않고 고정된다.
    ///  3. 밟으면 움직일 때마다 1 피해(한 턴 동안).
    ///  4. 독은 두 턴 뒤 사라진다.
    ///  5. 몬스터도 밟는다.
    /// </summary>
    public sealed class PoisonTests
    {
        /// <summary>기획 확정값 그대로의 독 설정.</summary>
        private sealed class Poison : IPoisonConfig
        {
            public int FieldTurns { get; set; } = 2;

            public int StatusTurns { get; set; } = 1;

            public int StepDamage { get; set; } = 1;
        }

        /// <summary>죽으면 독을 남기는 적. LeavesPoison을 켠 것 말고는 평범한 잡몹이다.</summary>
        private static Trash PoisonMob(TrashType type, int maxHp = 1, int attack = 1)
            => new Trash(type, new TrashStats(maxHp, attack, 0, 1, true, 0, false, leavesPoison: true));

        private static PoisonResolver Resolver(BoardGrid grid, Player player, IPoisonConfig config = null)
            => new PoisonResolver(grid, player, config ?? new Poison());

        // ── 1. 죽은 자리에 독이 생긴다 ────────────────────────────────────

        [Test]
        public void DeadPoisonMob_LeavesPoisonOnItsCell()
        {
            var grid = new BoardGrid(3, 3);
            var player = Make.Player();
            grid.Place(player, new Vector2Int(0, 0));

            var poison = Resolver(grid, player);
            var cell = new Vector2Int(2, 2);

            poison.MaybeLeavePoison(PoisonMob(TrashType.Sludge).LeavesPoison, cell);

            Assert.IsTrue(poison.IsPoisoned(cell));
            Assert.AreEqual(1, poison.Cells.Count);
            Assert.AreEqual(cell, poison.Cells[0]);
        }

        [Test]
        public void OrdinaryMob_LeavesNothing()
        {
            var grid = new BoardGrid(3, 3);
            var player = Make.Player();
            grid.Place(player, new Vector2Int(0, 0));

            var poison = Resolver(grid, player);
            var cell = new Vector2Int(1, 1);

            poison.MaybeLeavePoison(Make.Trash(TrashType.Paper).LeavesPoison, cell);

            Assert.IsFalse(poison.IsPoisoned(cell));
            Assert.AreEqual(0, poison.Cells.Count);
        }

        [Test]
        public void KillingAPoisonMob_LeavesPoisonThroughTheWholeLoop()
        {
            // 공격으로 죽이는 실제 경로(CombatMoveResolver)를 탄다.
            var config = new FakeBoardConfig { RowsOnStart = 0, BlocksPerSpawn = 0, PlayerStart = new Vector2Int(1, 2) };
            var grid = new BoardGrid(config.Cols, config.Rows);
            var player = new Player(config.MaxHp, config.Attack, 0);
            var poison = Resolver(grid, player);

            grid.Place(player, new Vector2Int(1, 2));
            var mobCell = new Vector2Int(2, 2);
            grid.Place(PoisonMob(TrashType.Sludge), mobCell);

            var chain = new ChainFinder(grid, new SameTypeChainRule(), config.FirstPlayableRow);
            var move = new CombatMoveResolver(
                grid, player, config, config, chain, new MinRandom(), null, null, poison);

            move.Resolve(Direction.Right);

            Assert.IsTrue(grid.IsEmpty(mobCell), "죽은 몬스터는 걷힌다");
            Assert.IsTrue(poison.IsPoisoned(mobCell), "죽은 자리에 독이 깔려야 한다");
        }

        // ── 2. 독은 떨어지지 않는다 ───────────────────────────────────────

        [Test]
        public void Poison_StaysWhereItIsWhileTheBoardFalls()
        {
            var grid = new BoardGrid(1, 4);
            var player = Make.Player();
            var poison = Resolver(grid, player);

            // 독은 바닥에서 한 칸 위에 깔아 둔다. 블록이었다면 중력이 바닥으로 끌어내렸을 자리다.
            var cell = new Vector2Int(0, 2);
            poison.MaybeLeavePoison(true, cell);

            // 독 위로 블록을 하나 띄워 두고 중력을 돌린다.
            grid.Place(Make.Trash(TrashType.Paper, 3, 1), new Vector2Int(0, 0));

            var gravity = new GravityResolver(grid, null, poison);
            gravity.Step();
            gravity.Step();

            Assert.IsTrue(poison.IsPoisoned(cell), "중력이 돌아도 독은 제자리여야 한다");
            Assert.AreEqual(1, poison.Cells.Count);
            Assert.AreEqual(2, poison.RemainingAt(cell), "중력은 수명도 건드리지 않는다(턴은 Tick만 깎는다)");
        }

        // ── 3. 밟으면 움직일 때마다 1 피해 (한 턴 동안) ───────────────────

        [Test]
        public void SteppingIntoPoison_HurtsAndPoisons()
        {
            var grid = new BoardGrid(3, 3);
            var player = Make.Player(5, 1);
            grid.Place(player, new Vector2Int(0, 0));

            var poison = Resolver(grid, player);
            poison.MaybeLeavePoison(true, new Vector2Int(1, 0));

            grid.Move(new Vector2Int(0, 0), new Vector2Int(1, 0));
            int damage = poison.OnMoved(player);

            Assert.AreEqual(1, damage, "밟은 그 이동에 1 피해");
            Assert.AreEqual(4, player.Hp);
            Assert.Greater(player.PoisonTurns, 0, "중독이 걸려야 한다");
        }

        [Test]
        public void MovingWhilePoisoned_HurtsEvenOnACleanCell()
        {
            var grid = new BoardGrid(4, 1);
            var player = Make.Player(5, 1);
            grid.Place(player, new Vector2Int(0, 0));

            var poison = Resolver(grid, player);
            poison.MaybeLeavePoison(true, new Vector2Int(1, 0));

            // 독을 밟는다.
            grid.Move(new Vector2Int(0, 0), new Vector2Int(1, 0));
            poison.OnMoved(player);
            poison.Tick();                      // 한 턴이 지난다

            // 깨끗한 칸으로 빠져나와도 중독이 남아 있어 한 번 더 아프다.
            grid.Move(new Vector2Int(1, 0), new Vector2Int(2, 0));
            int damage = poison.OnMoved(player);

            Assert.AreEqual(1, damage);
            Assert.AreEqual(3, player.Hp, "밟을 때 1, 중독 상태로 움직여서 1");
        }

        [Test]
        public void PoisonWearsOff_AfterOneTurn()
        {
            var grid = new BoardGrid(4, 1);
            var player = Make.Player(5, 1);
            grid.Place(player, new Vector2Int(0, 0));

            var poison = Resolver(grid, player);
            poison.MaybeLeavePoison(true, new Vector2Int(1, 0));

            grid.Move(new Vector2Int(0, 0), new Vector2Int(1, 0));
            poison.OnMoved(player);             // 밟음: 1 피해
            poison.Tick();

            grid.Move(new Vector2Int(1, 0), new Vector2Int(2, 0));
            poison.OnMoved(player);             // 중독으로 1 피해, 여기서 중독이 끝난다
            poison.Tick();

            grid.Move(new Vector2Int(2, 0), new Vector2Int(3, 0));
            int damage = poison.OnMoved(player);

            Assert.AreEqual(0, damage, "한 턴이 지났으니 더는 아프지 않아야 한다");
            Assert.AreEqual(3, player.Hp);
            Assert.AreEqual(0, player.PoisonTurns);
        }

        [Test]
        public void CrossingTwoPoisonCells_CostsOnlyOnePerMove()
        {
            var grid = new BoardGrid(3, 1);
            var player = Make.Player(5, 1);
            grid.Place(player, new Vector2Int(0, 0));

            var poison = Resolver(grid, player);
            poison.MaybeLeavePoison(true, new Vector2Int(1, 0));
            poison.MaybeLeavePoison(true, new Vector2Int(2, 0));

            grid.Move(new Vector2Int(0, 0), new Vector2Int(1, 0));
            poison.OnMoved(player);
            poison.Tick();

            // 중독인 채로 또 다른 독 칸에 들어선다 — 그래도 한 번만 맞는다.
            grid.Move(new Vector2Int(1, 0), new Vector2Int(2, 0));
            int damage = poison.OnMoved(player);

            Assert.AreEqual(1, damage);
            Assert.AreEqual(3, player.Hp);
        }

        [Test]
        public void StandingStillOnPoison_DoesNotHurtAgain()
        {
            var grid = new BoardGrid(3, 1);
            var player = Make.Player(5, 1);
            grid.Place(player, new Vector2Int(0, 0));

            var poison = Resolver(grid, player);
            poison.MaybeLeavePoison(true, new Vector2Int(1, 0));

            grid.Move(new Vector2Int(0, 0), new Vector2Int(1, 0));
            poison.OnMoved(player);             // 밟음: 1 피해
            poison.Tick();                      // 가만히 있는 턴 — OnMoved가 불리지 않는다

            Assert.AreEqual(4, player.Hp, "움직이지 않으면 더 아프지 않다");
        }

        [Test]
        public void PlayerStep_TakesPoisonDamageThroughTheWholeLoop()
        {
            // 실제 한 턴(GameLoop.Step)을 돌려 플레이어가 독 칸으로 걸어 들어가게 한다.
            var config = new FakeBoardConfig { RowsOnStart = 0, BlocksPerSpawn = 0, PlayerStart = new Vector2Int(4, 5) };
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom(),
                null, null, null, new Poison());

            Vector2Int start = loop.Player.Position;
            loop.Poison.MaybeLeavePoison(true, start + Vector2Int.right);

            int before = loop.Player.Hp;
            loop.Step(Direction.Right);

            Assert.AreEqual(before - 1, loop.Player.Hp, "독 칸으로 걸어 들어가면 1 아프다");
            Assert.Greater(loop.Player.PoisonTurns, 0, "턴이 끝난 뒤에도 중독이 한 턴 남아 있어야 한다");
        }

        [Test]
        public void AttackingOrWaiting_DoesNotStepInPoison()
        {
            var config = new FakeBoardConfig { RowsOnStart = 0, BlocksPerSpawn = 0, PlayerStart = new Vector2Int(4, 5) };
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom(),
                null, null, null, new Poison());

            // 서 있는 칸 자체에 독을 깔아 둔다. 제자리 행동은 "움직임"이 아니므로 아프지 않아야 한다.
            loop.Poison.MaybeLeavePoison(true, loop.Player.Position);

            int before = loop.Player.Hp;
            loop.Wait();

            Assert.AreEqual(before, loop.Player.Hp);
            Assert.AreEqual(0, loop.Player.PoisonTurns);
        }

        // ── 4. 독은 두 턴 뒤 사라진다 ─────────────────────────────────────

        [Test]
        public void Poison_DisappearsAfterTwoTurns()
        {
            var grid = new BoardGrid(2, 2);
            var player = Make.Player();
            grid.Place(player, new Vector2Int(0, 0));

            var poison = Resolver(grid, player);
            var cell = new Vector2Int(1, 1);
            poison.MaybeLeavePoison(true, cell);
            Assert.AreEqual(2, poison.RemainingAt(cell));

            // 몬스터를 잡은 바로 그 턴의 끝. 갓 생긴 독은 여기서 늑지 않는다 —
            // 안 그러면 생기자마자 한 턴을 깎여 "2턴 뒤"가 실제로는 1턴이 된다.
            poison.Tick();
            Assert.AreEqual(2, poison.RemainingAt(cell), "깔린 턴에는 줄지 않는다");

            poison.Tick();
            Assert.IsTrue(poison.IsPoisoned(cell), "한 턴 뒤에는 아직 남아 있다");

            poison.Tick();
            Assert.IsFalse(poison.IsPoisoned(cell), "두 턴 뒤에는 사라진다");
            Assert.AreEqual(0, poison.Cells.Count, "목록에서도 빠져야 한다");
        }

        [Test]
        public void SteppingOnTheSameCell_RefreshesTheField()
        {
            var grid = new BoardGrid(2, 2);
            var player = Make.Player();
            grid.Place(player, new Vector2Int(0, 0));

            var poison = Resolver(grid, player);
            var cell = new Vector2Int(1, 1);

            poison.MaybeLeavePoison(true, cell);
            poison.Tick();                              // 깔린 턴
            poison.Tick();                              // 한 턴 지났다
            Assert.AreEqual(1, poison.RemainingAt(cell), "수명이 하나 줄어 있어야 한다");

            poison.MaybeLeavePoison(true, cell);        // 같은 칸에서 또 죽었다

            Assert.AreEqual(2, poison.RemainingAt(cell), "수명이 다시 찬다");
            Assert.AreEqual(1, poison.Cells.Count, "목록에 두 번 들어가면 안 된다");
        }

        // ── 5. 몬스터도 독을 밟는다 ───────────────────────────────────────

        [Test]
        public void FallingBlock_StepsInPoisonAndGetsHurt()
        {
            var grid = new BoardGrid(1, 3);
            var player = Make.Player();
            grid.Place(player, new Vector2Int(0, 0));   // 맨 위에 둬서 낙하와 무관하게 한다

            var poison = Resolver(grid, player);
            var mob = Make.Trash(TrashType.Paper, maxHp: 3, attack: 1);
            grid.Place(mob, new Vector2Int(0, 1));
            poison.MaybeLeavePoison(true, new Vector2Int(0, 2));

            var gravity = new GravityResolver(grid, null, poison);
            gravity.Step();                              // 독 칸으로 떨어진다

            Assert.AreEqual(new Vector2Int(0, 2), mob.Position);
            Assert.AreEqual(2, mob.Hp, "독을 밟은 블록도 1 아프다");
            Assert.Greater(mob.PoisonTurns, 0, "몬스터도 중독된다");
        }

        [Test]
        public void PoisonedBlock_DiesAndIsSweptAwayByTheLoop()
        {
            var config = new FakeBoardConfig { RowsOnStart = 0, BlocksPerSpawn = 0, PlayerStart = new Vector2Int(0, 8) };
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom(),
                null, null, null, new Poison());

            // 체력 1짜리 블록을 독 바로 위에 띄워 둔다. 떨어지면서 밟고 죽는다.
            var mob = Make.Trash(TrashType.Paper, maxHp: 1, attack: 1);
            var landing = new Vector2Int(4, 8);
            loop.Grid.Place(mob, new Vector2Int(4, 7));
            loop.Poison.MaybeLeavePoison(true, landing);

            loop.Wait();

            Assert.AreEqual(0, mob.Hp, "독을 밟아 죽는다");
            Assert.IsTrue(loop.Grid.IsEmpty(landing), "죽은 블록은 보드에서 걷혀야 한다");
        }

        // ── 설정이 없으면 기믹이 통째로 꺼진다 ────────────────────────────

        [Test]
        public void WithoutAPoisonConfig_NothingHappens()
        {
            var config = new FakeBoardConfig { RowsOnStart = 0, BlocksPerSpawn = 0 };
            GameLoop loop = Make.Staged(config, new MinRandom());
            loop.CompleteSetup();

            Assert.IsNull(loop.Poison, "독 설정을 안 꽂으면 기믹이 아예 없다");
        }
    }
}
