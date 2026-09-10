using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// CORE_COMBAT.md §3·§4·§5 + 기획 확정 사항.
    ///
    /// 확정된 규칙 세 가지를 그대로 검사한다:
    ///  1. 부딪히면 제자리에서 공격하고, 같은 <b>종류</b>로 이어진 덩어리가 전부 맞는다.
    ///  2. 인접은 4방향 — 대각선은 안 이어진다.
    ///  3. 반격은 <b>부딪힌 한 마리</b>만, 그 마리가 살아남았을 때만 한다.
    ///
    /// 데미지 규칙은 리졸버를 직접 불러 검사한다. GameLoop을 거치면 중력·스폰이 같이 돌아
    /// 무엇 때문에 보드가 바뀌었는지 흐려지기 때문이다. 턴 진행·패배는 아래쪽 루프 테스트가 본다.
    /// </summary>
    public sealed class CombatTests
    {
        /// <summary>캐릭터 A(부딪힌 칸만) 기준. 공격 범위가 다른 캐릭터는 아래 오버로드를 쓴다.</summary>
        private static CombatMoveResolver Build(BoardGrid grid, IBoardConfig config, Player player)
            => Build(grid, config, player, Offsets.BumpedOnly);

        private static CombatMoveResolver Build(
            BoardGrid grid, IBoardConfig config, Player player, System.Collections.Generic.IReadOnlyList<Vector2Int> offsets)
            => new CombatMoveResolver(
                grid,
                player,
                config,
                new FakeCharacter(player.MaxHp, player.Attack, offsets),
                new ChainFinder(grid, new SameTypeChainRule(), config.FirstPlayableRow));

        /// <summary>플레이어만 놓인 8x9 빈 보드.</summary>
        private static BoardGrid EmptyBoard(FakeBoardConfig config, Player player, Vector2Int at)
        {
            var grid = new BoardGrid(config.Cols, config.Rows);
            grid.Place(player, at);
            return grid;
        }

        // ── §3 범프 ────────────────────────────────────────────────────────

        [Test]
        public void BumpingAnEnemy_AttacksInPlaceInsteadOfMoving()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            Trash enemy = Make.Trash(TrashType.Plastic, 5, 3);
            grid.Place(enemy, new Vector2Int(4, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(MoveOutcome.Attacked, result.Outcome);
            Assert.AreEqual(new Vector2Int(4, 5), player.Position, "공격은 제자리에서 한다(§3).");
            Assert.AreEqual(3, enemy.Hp, "5 - 플레이어 공격력 2");
            Assert.AreEqual(1, result.ChainSize);
            Assert.AreEqual(0, result.Killed);
        }

        [Test]
        public void SurvivingEnemy_CountersForItsOwnAttack()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            grid.Place(Make.Trash(TrashType.Plastic, 5, 3), new Vector2Int(4, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(3, result.DamageTaken);
            Assert.AreEqual(7, player.Hp, "10 - 반격 3");
        }

        [Test]
        public void KillingTheBumpedEnemy_TakesNoCounter()
        {
            // 원작 핵심 규칙: 처치하면 그 적의 반격은 받지 않는다(§3).
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            grid.Place(Make.Trash(TrashType.Plastic, 2, 9), new Vector2Int(4, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(1, result.Killed);
            Assert.AreEqual(0, result.DamageTaken, "죽은 적은 반격하지 않는다.");
            Assert.AreEqual(10, player.Hp);
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(4, 6)), "처치된 적은 보드에서 사라진다.");
            Assert.AreEqual(new Vector2Int(4, 5), player.Position,
                "처치해서 칸이 비어도 그 자리로 들어가지 않는다(기획 확정).");
        }

        [Test]
        public void PreviewRow_CannotBeAttacked()
        {
            // 프리뷰 줄은 플레이어에게 보드 밖과 같다 — 이동도 공격도 안 된다(WEEK1 §1).
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 1));
            Trash waiting = Make.Trash(TrashType.Paper, 3, 1);
            grid.Place(waiting, new Vector2Int(4, 0));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Up);

            Assert.AreEqual(MoveOutcome.OutOfBounds, result.Outcome);
            Assert.AreEqual(3, waiting.Hp, "대기 중인 블록은 손댈 수 없다.");
        }

        [Test]
        public void Chain_NeverReachesIntoThePreviewRow()
        {
            // 프리뷰 줄은 아직 판에 들어오지 않은 대기 블록이라 어떤 방식으로도 손댈 수 없다(WEEK1 §1).
            // 첫 행에서 같은 종류를 치면 연쇄가 위로 타고 올라갈 수 있어 경계를 따로 막는다.
            //   row 0  . . . . G     <- 프리뷰 (같은 종류!)
            //   row 1  . . . . G     <- 여기를 친다
            //   row 2  . . . . @
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 2));

            Trash target = Make.Trash(TrashType.Glass, 3, 0);
            Trash waiting = Make.Trash(TrashType.Glass, 3, 0);
            grid.Place(target, new Vector2Int(4, 1));
            grid.Place(waiting, new Vector2Int(4, 0));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Up);

            Assert.AreEqual(1, result.ChainSize, "연쇄가 프리뷰 줄로 번지면 안 된다.");
            Assert.AreEqual(2, target.Hp);
            Assert.AreEqual(3, waiting.Hp, "대기 중인 블록은 멀쩡해야 한다.");
        }

        // ── §4 연쇄 ────────────────────────────────────────────────────────

        [Test]
        public void ConnectedSameType_AllTakeTheFullDamage()
        {
            // 보내준 스샷 그대로의 배치: 아래 한 칸을 치면 붙어 있는 같은 색이 같이 맞는다.
            //   row 5  . . . . @ . .
            //   row 6  . . . . C C .     <- (4,6)을 친다
            //   row 7  . . . . C A .
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            Trash bumped = Make.Trash(TrashType.Glass, 3, 1);
            Trash right = Make.Trash(TrashType.Glass, 3, 1);
            Trash below = Make.Trash(TrashType.Glass, 3, 1);
            Trash other = Make.Trash(TrashType.Paper, 3, 1);

            grid.Place(bumped, new Vector2Int(4, 6));
            grid.Place(right, new Vector2Int(5, 6));
            grid.Place(below, new Vector2Int(4, 7));
            grid.Place(other, new Vector2Int(5, 7));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(3, result.ChainSize, "같은 종류 3칸이 한 덩어리다.");
            Assert.AreEqual(2, bumped.Hp);
            Assert.AreEqual(2, right.Hp, "직접 부딪히지 않아도 같은 피해를 받는다(§5-2).");
            Assert.AreEqual(2, below.Hp);
            Assert.AreEqual(3, other.Hp, "종류가 다르면 붙어 있어도 안 맞는다.");
        }

        [Test]
        public void Chain_DoesNotTravelDiagonally()
        {
            // 확정: 인접은 4방향(§8-3).
            //   row 6  . . . . C .
            //   row 7  . . . . . C   <- 대각선으로만 닿아 있다
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            Trash bumped = Make.Trash(TrashType.Glass, 3, 1);
            Trash diagonal = Make.Trash(TrashType.Glass, 3, 1);
            grid.Place(bumped, new Vector2Int(4, 6));
            grid.Place(diagonal, new Vector2Int(5, 7));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(1, result.ChainSize);
            Assert.AreEqual(3, diagonal.Hp, "대각선은 이어지지 않는다.");
        }

        [Test]
        public void SplitGroupOfTheSameType_OnlyTheTouchingHalfIsHit()
        {
            // 같은 종류라도 떨어져 있으면 다른 덩어리다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            Trash bumped = Make.Trash(TrashType.Glass, 3, 1);
            Trash faraway = Make.Trash(TrashType.Glass, 3, 1);
            grid.Place(bumped, new Vector2Int(4, 6));
            grid.Place(faraway, new Vector2Int(0, 8));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(1, result.ChainSize);
            Assert.AreEqual(3, faraway.Hp);
        }

        [Test]
        public void LongChain_IsFoundThroughEveryConnectedCell()
        {
            // ㄱ자로 길게 이어진 덩어리도 한 번에 다 묶인다(BFS 검증).
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(0, 5));

            //   row 6  C . . .
            //   row 7  C C C .
            //   row 8  . . C .
            var cells = new[]
            {
                new Vector2Int(0, 6),
                new Vector2Int(0, 7),
                new Vector2Int(1, 7),
                new Vector2Int(2, 7),
                new Vector2Int(2, 8),
            };

            for (int i = 0; i < cells.Length; i++)
            {
                grid.Place(Make.Trash(TrashType.Glass, 2, 0), cells[i]);
            }

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(5, result.ChainSize);
            Assert.AreEqual(0, result.Killed, "체력 2에 공격력 1이라 한 방에는 안 죽는다.");
        }

        // ── §5 데미지 해결 ─────────────────────────────────────────────────

        [Test]
        public void WipingTheWholeChain_LeavesThePlayerUnharmed()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Trash(TrashType.Glass, 1, 5), new Vector2Int(4, 6));
            grid.Place(Make.Trash(TrashType.Glass, 1, 5), new Vector2Int(5, 6));
            grid.Place(Make.Trash(TrashType.Glass, 1, 5), new Vector2Int(4, 7));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(3, result.Killed, "한 방에 덩어리 전체가 사라진다.");
            Assert.AreEqual(0, result.DamageTaken);
            Assert.AreEqual(10, player.Hp);
            Assert.AreEqual(config.Cols * config.Rows - 1, grid.CountEmpty(), "남은 건 플레이어뿐이다.");
        }

        [Test]
        public void SurvivingChainMates_DoNotCounter_OnlyTheBumpedOneDoes()
        {
            // 확정(§8-2): 반격은 부딪힌 한 마리만 한다.
            // 부딪힌 놈은 죽고 옆의 같은 종류는 멀쩡히 살아남는 배치로 검사한다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Trash(TrashType.Glass, 1, 2), new Vector2Int(4, 6));   // 부딪힌 놈: 죽는다
            Trash survivor = Make.Trash(TrashType.Glass, 9, 7);                    // 옆: 살아남는다
            grid.Place(survivor, new Vector2Int(5, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(2, result.ChainSize);
            Assert.AreEqual(1, result.Killed);
            Assert.AreEqual(8, survivor.Hp, "살아남은 옆 놈도 피해는 받는다.");
            Assert.AreEqual(0, result.DamageTaken, "반격은 안 한다 — 부딪힌 놈이 죽었으므로 무피해.");
            Assert.AreEqual(10, player.Hp);
        }

        [Test]
        public void PartialKill_CountersWithTheBumpedEnemyOnly()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Trash(TrashType.Glass, 4, 2), new Vector2Int(4, 6));   // 부딪힌 놈: 살아남는다
            grid.Place(Make.Trash(TrashType.Glass, 1, 6), new Vector2Int(5, 6));   // 옆: 죽는다

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(1, result.Killed);
            Assert.AreEqual(2, result.DamageTaken, "부딪힌 놈의 공격력 2만 들어온다 — 죽은 옆 놈의 6은 무시.");
            Assert.AreEqual(8, player.Hp);
        }

        // ── 루프 통합 ──────────────────────────────────────────────────────

        [Test]
        public void Attack_CountsAsATurnAndAdvancesTheBoard()
        {
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(4, 7) };
            GameLoop loop = Make.Week1(config, new MinRandom());
            loop.Grid.Place(Make.Trash(TrashType.Paper, 9, 1), new Vector2Int(4, 8));

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(MoveOutcome.Attacked, result.Move);
            Assert.IsTrue(result.Advanced, "공격도 한 턴을 쓴다.");
            Assert.AreEqual(1, loop.StepCount);
            Assert.AreEqual(1, result.Spawned, "스폰 페이즈까지 정상적으로 돈다.");
            Assert.AreEqual(new Vector2Int(4, 7), loop.Player.Position);
        }

        [Test]
        public void KilledEnemies_LeaveHolesThatGravityFillsInTheSameStep()
        {
            // 공격 -> 중력 순서(§2)라 처치한 자리로 위 블록이 그 스텝에 한 칸 내려온다.
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(0, 8), BlocksPerSpawn = 0 };
            GameLoop loop = Make.Week1(config, new MinRandom());

            loop.Grid.Place(Make.Trash(TrashType.Paper, 1, 0), new Vector2Int(1, 8));
            Trash above = Make.Trash(TrashType.Plastic, 5, 0);
            loop.Grid.Place(above, new Vector2Int(1, 7));

            StepResult result = loop.Step(Direction.Right);

            Assert.AreEqual(1, result.Killed);
            Assert.AreEqual(new Vector2Int(1, 8), above.Position, "빈자리로 한 칸 내려온다.");
        }

        [Test]
        public void SurroundedPlayer_IsNotTrappedBecauseEveryNeighbourIsAttackable()
        {
            // 전투 전에는 이 배치가 "빠져나갈 곳이 없습니다"였다.
            // 이제는 사방이 전부 때릴 수 있는 대상이라 갇힘이 아니다.
            var config = new FakeBoardConfig
            {
                Cols = 3,
                PlayableRows = 2,
                PlayerStart = new Vector2Int(1, 1),
                BlocksPerSpawn = 0,
            };
            GameLoop loop = Make.Week1(config, new MinRandom());

            loop.Grid.Place(Make.Trash(TrashType.Paper, 9, 0), new Vector2Int(0, 1));
            loop.Grid.Place(Make.Trash(TrashType.Paper, 9, 0), new Vector2Int(2, 1));
            loop.Grid.Place(Make.Trash(TrashType.Paper, 9, 0), new Vector2Int(1, 2));

            StepResult result = loop.Step(Direction.Right);

            Assert.AreEqual(MoveOutcome.Attacked, result.Move);
            Assert.AreEqual(GameOverReason.None, result.GameOver, "때릴 수 있으면 갇힌 게 아니다.");
            Assert.IsFalse(loop.IsOver);
        }

        [Test]
        public void PlayerHittingZeroHp_EndsTheRun()
        {
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(4, 7), MaxHp = 3, Attack = 1 };
            GameLoop loop = Make.Week1(config, new MinRandom());
            loop.Grid.Place(Make.Trash(TrashType.Paper, 99, 3), new Vector2Int(4, 8));

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(3, result.DamageTaken);
            Assert.AreEqual(0, loop.Player.Hp);
            Assert.AreEqual(GameOverReason.PlayerDead, result.GameOver);
            Assert.IsTrue(loop.IsOver);

            // 진 뒤의 입력은 아무것도 하지 않는다.
            int stepsBefore = loop.StepCount;
            Assert.IsFalse(loop.Step(Direction.Up).Advanced);
            Assert.AreEqual(stepsBefore, loop.StepCount);
        }

        [Test]
        public void CounterDamage_AccumulatesAcrossTurns()
        {
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(4, 7), MaxHp = 10, Attack = 1 };
            GameLoop loop = Make.Week1(config, new MinRandom());
            loop.Grid.Place(Make.Trash(TrashType.Paper, 99, 2), new Vector2Int(4, 8));

            loop.Step(Direction.Down);
            Assert.AreEqual(8, loop.Player.Hp);

            loop.Step(Direction.Down);
            Assert.AreEqual(6, loop.Player.Hp, "때릴 때마다 반격이 쌓인다.");
            Assert.IsFalse(loop.IsOver);
        }

        [Test]
        public void SpawnedTrash_UsesTheStatsFromTheConfig()
        {
            // Hard Rule 1: 스탯은 코드가 아니라 설정에서 온다.
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(4, 8) };
            var stats = new FakeTrashStats().Set(TrashType.Paper, 7, 4);
            GameLoop loop = Make.Week1(config, new MinRandom(), stats);

            loop.Step(Direction.Up);

            var spawned = loop.Grid[new Vector2Int(0, 0)] as Trash;
            Assert.IsNotNull(spawned, "MinRandom은 첫 컬럼에 TrashType.Paper를 낸다.");
            Assert.AreEqual(7, spawned.MaxHp);
            Assert.AreEqual(4, spawned.Attack);
        }
    }
}
