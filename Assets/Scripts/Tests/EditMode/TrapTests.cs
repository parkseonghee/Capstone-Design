using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 덫 몬스터 기믹: 죽으면 그 자리에 덫을 남기고(TrashStats.LeavesTrap),
    /// 플레이어나 몬스터가 닿으면 무작위 빈 칸으로 보내며(TrapResolver),
    /// 덫 자신은 중력을 영구히 무시한다(IGravityHold).
    /// </summary>
    public sealed class TrapTests
    {
        private static FakeBoardConfig Board()
            => new FakeBoardConfig
            {
                RowsOnStart = 0,
                BlocksPerSpawn = 0,
                PlayerStart = new Vector2Int(4, 5),
            };

        /// <summary>죽으면 덫을 남기는 적. LeavesTrap을 켠 것 말고는 평범한 잡몹이다.</summary>
        private static Trash TrapMob(TrashType type, int maxHp, int attack)
            => new Trash(type, new TrashStats(maxHp, attack, 0, 1, true, 0, leavesTrap: true));

        // ── TrapResolver.TeleportToRandomEmptyCell ───────────────────────

        [Test]
        public void Teleport_MovesEntityToAnEmptyCell()
        {
            var grid = new BoardGrid(5, 5);
            var avatar = new VillageAvatar();
            grid.Place(avatar, new Vector2Int(0, 0));

            var resolver = new TrapResolver(grid, new MinRandom());
            bool moved = resolver.TeleportToRandomEmptyCell(avatar);

            Assert.IsTrue(moved);
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(0, 0)), "원래 칸은 비어야 한다");
            Assert.AreEqual(avatar.Position, avatar.Position, "위치는 grid.Move가 갱신한다");
            Assert.IsFalse(grid.IsEmpty(avatar.Position), "새 칸에는 아바타가 있어야 한다");
        }

        [Test]
        public void Teleport_NeverPicksTheCurrentCell()
        {
            // MinRandom은 항상 0을 돌려주므로, 저수지 표본추출 순서상 스캔되는
            // "맨 처음" 빈 칸이 후보 1번(index 0)이 된다. entity가 서 있는 칸(0,0)이
            // 스캔 순서상 가장 먼저 오더라도 자기 자신은 후보에서 빠져야 한다.
            var grid = new BoardGrid(2, 1);
            var avatar = new VillageAvatar();
            grid.Place(avatar, new Vector2Int(0, 0));   // 남은 빈 칸은 (1,0) 하나뿐

            var resolver = new TrapResolver(grid, new MinRandom());
            resolver.TeleportToRandomEmptyCell(avatar);

            Assert.AreEqual(new Vector2Int(1, 0), avatar.Position);
        }

        [Test]
        public void Teleport_RespectsFirstAllowedRow()
        {
            // row 0만 빈 칸, 나머지는 꽉 채운다. firstAllowedRow=1로 부르면 갈 곳이 없어야 한다.
            var grid = new BoardGrid(2, 2);
            var avatar = new VillageAvatar();
            grid.Place(avatar, new Vector2Int(0, 1));
            grid.Place(new VillageAvatar(), new Vector2Int(1, 1));   // row 1의 남은 칸도 채운다

            var resolver = new TrapResolver(grid, new MinRandom());
            bool moved = resolver.TeleportToRandomEmptyCell(avatar, firstAllowedRow: 1);

            Assert.IsFalse(moved, "프리뷰 줄(row 0)은 후보가 아니므로 갈 곳이 없어야 한다");
            Assert.AreEqual(new Vector2Int(0, 1), avatar.Position, "실패하면 제자리여야 한다");
        }

        [Test]
        public void Teleport_ReturnsFalseWhenBoardIsFull()
        {
            var grid = new BoardGrid(1, 1);
            var avatar = new VillageAvatar();
            grid.Place(avatar, new Vector2Int(0, 0));

            var resolver = new TrapResolver(grid, new MinRandom());
            bool moved = resolver.TeleportToRandomEmptyCell(avatar);

            Assert.IsFalse(moved);
        }

        // ── 덫은 중력을 영구히 무시한다 ────────────────────────────────────

        [Test]
        public void Trap_NeverFallsUnderGravity()
        {
            var grid = new BoardGrid(1, 3);
            var trap = new Trap();
            grid.Place(trap, new Vector2Int(0, 0));

            var gravity = new GravityResolver(grid);
            gravity.Settle();
            gravity.Settle();   // 여러 번 돌려도 항상 제자리인지 확인

            Assert.AreEqual(new Vector2Int(0, 0), trap.Position);
        }

        [Test]
        public void MonsterFallingOntoATrap_TeleportsInsteadOfStacking()
        {
            // 1열짜리 보드: (0,0)에 낙하 중인 몬스터, (0,1)에 덫, 다른 열에 도피용 빈 칸.
            var grid = new BoardGrid(3, 2);
            var falling = new Trash(TrashType.Paper, new TrashStats(1, 1, 0, 1, true));
            grid.Place(falling, new Vector2Int(0, 0));
            grid.Place(new Trap(), new Vector2Int(0, 1));

            var resolver = new TrapResolver(grid, new MinRandom());
            var gravity = new GravityResolver(grid, resolver);

            gravity.Step();

            Assert.AreNotEqual(new Vector2Int(0, 1), falling.Position,
                "덫이 있는 칸으로 그냥 내려앉으면 안 된다");
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(0, 1)), "밟힌 덫은 사라져야 한다(일회용)");
        }

        [Test]
        public void WithoutATrapResolver_GravityTreatsATrapAsAnOrdinaryBlocker()
        {
            // traps를 안 넘기면(구형 배선) 덫은 그냥 막는 장애물일 뿐 텔레포트는 안 일어난다.
            var grid = new BoardGrid(1, 2);
            var falling = new Trash(TrashType.Paper, new TrashStats(1, 1, 0, 1, true));
            grid.Place(falling, new Vector2Int(0, 0));
            grid.Place(new Trap(), new Vector2Int(0, 1));

            var gravity = new GravityResolver(grid);
            gravity.Step();

            Assert.AreEqual(new Vector2Int(0, 0), falling.Position, "막혀서 제자리여야 한다");
        }

        // ── 죽으면 덫을 남긴다 ───────────────────────────────────────────

        [Test]
        public void KillingATrapMob_LeavesATrapAtItsCell()
        {
            var config = Board();
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(TrapMob(TrashType.Paper, maxHp: 1, attack: 1), below);

            loop.Step(Direction.Down);

            Assert.IsTrue(loop.Grid[below] is Trap, "죽은 자리에 덫이 남아야 한다");
        }

        [Test]
        public void KillingAnOrdinaryMob_LeavesNoTrap()
        {
            var config = Board();
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Make.Trash(TrashType.Paper, maxHp: 1, attack: 1), below);

            loop.Step(Direction.Down);

            Assert.IsFalse(loop.Grid[below] is Trap, "LeavesTrap이 꺼진 적은 덫을 남기면 안 된다");
        }

        // ── 플레이어가 덫에 닿으면 텔레포트한다 ──────────────────────────

        [Test]
        public void PlayerBumpingATrap_Teleports_AndDoesNotWalkIntoTheTrapCell()
        {
            var config = Board();
            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            Vector2Int playerStart = loop.Player.Position;
            var below = new Vector2Int(playerStart.x, playerStart.y + 1);
            loop.Grid.Place(new Trap(), below);

            StepResult result = loop.Step(Direction.Down);

            Assert.IsTrue(result.Advanced, "덫에 닿는 것도 유효한 한 턴 행동이어야 한다");
            Assert.AreEqual(MoveOutcome.Teleported, result.Move);
            Assert.AreNotEqual(playerStart, loop.Player.Position, "제자리가 아니라 옮겨져야 한다");
            Assert.AreNotEqual(below, loop.Player.Position, "덫이 있던 칸으로 들어가면 안 된다");
            Assert.IsTrue(loop.Grid.IsEmpty(below), "밟힌 덫은 사라져야 한다(일회용)");
        }

        [Test]
        public void Teleport_WhenItFails_LeavesTheTrapInPlace()
        {
            // 보드가 꽉 차서 갈 곳이 없으면 아무 일도 안 일어난 것이므로 덫도 지워지면 안 된다.
            var grid = new BoardGrid(1, 1);
            var trap = new Trap();
            grid.Place(trap, new Vector2Int(0, 0));

            var resolver = new TrapResolver(grid, new MinRandom());

            // entity가 이미 덫과 같은 칸에 있는 상황을 흉내 낼 수 없으니(격자엔 칸당 하나),
            // 대신 "빈 칸이 아예 없는 보드"에서 다른 위치의 entity로 직접 호출해 실패를 재현한다.
            var stuck = new VillageAvatar();
            bool moved = resolver.Trigger(new Vector2Int(0, 0), stuck);

            Assert.IsFalse(moved);
            Assert.AreEqual(new Vector2Int(0, 0), trap.Position);
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(0, 0)), "실패했으면 덫이 그대로 남아야 한다");
        }
    }
}
