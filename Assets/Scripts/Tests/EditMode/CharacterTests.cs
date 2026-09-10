using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 캐릭터별 공격 범위. 기획서(캐릭터 &amp; 적 기믹) §1 + 밸런싱 v1 §1.
    ///
    /// 캐릭터의 개성은 숫자가 아니라 <b>공격이 어디에 들어가는가</b>다.
    /// 범위는 코드가 아니라 좌표 오프셋 데이터로 오므로, 이 테스트들은
    /// 오프셋만 바꿔 끼워 서로 다른 캐릭터를 만든다 — 구현도 정확히 그 방식이다.
    ///
    ///  · A(기본형)  = [(0,0)]                 공격력 2 / 체력 3
    ///  · B(사이드형) = [(0,0), (-1,0), (1,0)]  공격력 1 / 체력 4
    /// </summary>
    public sealed class CharacterTests
    {
        private static CombatMoveResolver Build(
            BoardGrid grid,
            IBoardConfig config,
            Player player,
            System.Collections.Generic.IReadOnlyList<Vector2Int> offsets)
            => new CombatMoveResolver(
                grid,
                player,
                config,
                new FakeCharacter(player.MaxHp, player.Attack, offsets),
                new ChainFinder(grid, new SameTypeChainRule(), config.FirstPlayableRow));

        private static BoardGrid EmptyBoard(FakeBoardConfig config, Player player, Vector2Int at)
        {
            var grid = new BoardGrid(config.Cols, config.Rows);
            grid.Place(player, at);
            return grid;
        }

        // ── 캐릭터 A ────────────────────────────────────────────────────────

        [Test]
        public void CharacterA_HitsOnlyTheBumpedCell()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(3, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Trash(TrashType.Glass, 3, 1), new Vector2Int(4, 6));   // 부딪히는 칸
            Trash left = Make.Trash(TrashType.Plastic, 3, 1);
            Trash right = Make.Trash(TrashType.Plastic, 3, 1);
            grid.Place(left, new Vector2Int(3, 6));
            grid.Place(right, new Vector2Int(5, 6));

            MoveResult result = Build(grid, config, player, Offsets.BumpedOnly).Resolve(Direction.Down);

            Assert.AreEqual(1, result.ChainSize, "A는 부딪힌 칸 하나만 때린다.");
            Assert.AreEqual(3, left.Hp, "좌우는 건드리지 않는다.");
            Assert.AreEqual(3, right.Hp);
        }

        // ── 캐릭터 B — 범위 ─────────────────────────────────────────────────

        [Test]
        public void CharacterB_HitsTheBumpedCellAndBothSides()
        {
            //   row 5  . . . @ . .
            //   row 6  . . P G P .     G=부딪히는 칸, 좌우 P도 같이 맞는다
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            Trash bumped = Make.Trash(TrashType.Glass, 3, 1);
            Trash left = Make.Trash(TrashType.Plastic, 3, 1);
            Trash right = Make.Trash(TrashType.Plastic, 3, 1);
            grid.Place(bumped, new Vector2Int(4, 6));
            grid.Place(left, new Vector2Int(3, 6));
            grid.Place(right, new Vector2Int(5, 6));

            MoveResult result = Build(grid, config, player, Offsets.BumpedAndSides).Resolve(Direction.Down);

            Assert.AreEqual(3, result.ChainSize, "세 칸이 동시에 맞는다.");
            Assert.AreEqual(2, bumped.Hp, "3 - 공격력 1");
            Assert.AreEqual(2, left.Hp);
            Assert.AreEqual(2, right.Hp);
        }

        [Test]
        public void CharacterB_EachHitCellStartsItsOwnChain()
        {
            // 좌우 칸이 각각 자기 종류로 연쇄를 따로 시작한다.
            //   row 5  . . . @ . .
            //   row 6  . . P G P .
            //   row 7  . . P . P .     <- 좌우의 P가 각각 아래로 이어진다
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Trash(TrashType.Glass, 3, 1), new Vector2Int(4, 6));
            grid.Place(Make.Trash(TrashType.Plastic, 3, 1), new Vector2Int(3, 6));
            grid.Place(Make.Trash(TrashType.Plastic, 3, 1), new Vector2Int(5, 6));
            Trash leftTail = Make.Trash(TrashType.Plastic, 3, 1);
            Trash rightTail = Make.Trash(TrashType.Plastic, 3, 1);
            grid.Place(leftTail, new Vector2Int(3, 7));
            grid.Place(rightTail, new Vector2Int(5, 7));

            MoveResult result = Build(grid, config, player, Offsets.BumpedAndSides).Resolve(Direction.Down);

            Assert.AreEqual(5, result.ChainSize, "중앙 1 + 좌 2 + 우 2");
            Assert.AreEqual(2, leftTail.Hp, "연쇄로 이어진 칸도 맞는다.");
            Assert.AreEqual(2, rightTail.Hp);
        }

        [Test]
        public void CharacterB_ClusterSpanningTwoHitCells_TakesDamageOnlyOnce()
        {
            // 중앙과 왼쪽이 <b>같은 종류로 이어져</b> 있다. 두 시작점이 같은 덩어리를 가리키므로
            // 중복을 걸러내지 않으면 그 덩어리가 두 번 맞는다.
            // 두 번 맞으면 "B는 체력 2 이상을 원샷 못 한다"는 밸런싱 전제가 깨진다(v1 §1-2).
            //   row 5  . . . @ .
            //   row 6  . . P P .     둘 다 P — 한 덩어리다
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            Trash bumped = Make.Trash(TrashType.Plastic, 2, 1);
            Trash left = Make.Trash(TrashType.Plastic, 2, 1);
            grid.Place(bumped, new Vector2Int(4, 6));
            grid.Place(left, new Vector2Int(3, 6));

            MoveResult result = Build(grid, config, player, Offsets.BumpedAndSides).Resolve(Direction.Down);

            Assert.AreEqual(2, result.ChainSize, "두 칸뿐이다 — 중복으로 세지 않는다.");
            Assert.AreEqual(1, bumped.Hp, "2 - 1. 두 번 맞았다면 0이 됐을 것이다.");
            Assert.AreEqual(1, left.Hp);
            Assert.AreEqual(0, result.Killed, "체력 2는 공격력 1로 한 방에 안 죽는다.");
        }

        // ── 캐릭터 B — 빈 곳 처리 ───────────────────────────────────────────

        [Test]
        public void CharacterB_EmptySideCells_AreSimplyIgnored()
        {
            // 기획서의 "공허 타격" — 옆이 비어 있으면 그냥 무시된다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            grid.Place(Make.Trash(TrashType.Glass, 3, 1), new Vector2Int(4, 6));

            MoveResult result = Build(grid, config, player, Offsets.BumpedAndSides).Resolve(Direction.Down);

            Assert.AreEqual(MoveOutcome.Attacked, result.Outcome);
            Assert.AreEqual(1, result.ChainSize, "맞은 건 중앙 하나뿐이다.");
        }

        [Test]
        public void CharacterB_SideCellOutsideTheBoard_IsIgnored()
        {
            // 맨 왼쪽 열에서 아래를 치면 왼쪽 오프셋이 보드 밖이다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(0, 5));

            grid.Place(Make.Trash(TrashType.Glass, 3, 1), new Vector2Int(0, 6));
            Trash right = Make.Trash(TrashType.Glass, 3, 1);
            grid.Place(right, new Vector2Int(1, 6));

            MoveResult result = Build(grid, config, player, Offsets.BumpedAndSides).Resolve(Direction.Down);

            Assert.AreEqual(2, result.ChainSize, "왼쪽은 보드 밖이라 빠지고, 중앙과 오른쪽만 맞는다.");
            Assert.AreEqual(2, right.Hp);
        }

        [Test]
        public void CharacterB_SideCellInThePreviewRow_IsIgnored()
        {
            // 프리뷰 줄은 어떤 방식으로도 손댈 수 없다(WEEK1 §1).
            // 회전 규칙 때문에 옆칸이 프리뷰 줄에 걸리려면 <b>가로</b>로 쳐야 한다.
            //   row 0  . . . . . P     <- 프리뷰 (위쪽 옆칸)
            //   row 1  . . . . @ G     <- 오른쪽으로 친다
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 1));

            grid.Place(Make.Trash(TrashType.Glass, 3, 1), new Vector2Int(5, 1));
            Trash inPreview = Make.Trash(TrashType.Glass, 3, 1);
            grid.Place(inPreview, new Vector2Int(5, 0));   // 프리뷰 줄 = 위쪽 옆칸

            MoveResult result = Build(grid, config, player, Offsets.BumpedAndSides).Resolve(Direction.Right);

            Assert.AreEqual(1, result.ChainSize, "프리뷰 줄 옆칸은 빠진다.");
            Assert.AreEqual(3, inPreview.Hp, "프리뷰 줄의 블록은 안 맞는다.");
        }

        // ── 캐릭터 B — 방향 ─────────────────────────────────────────────────

        [Test]
        public void CharacterB_SideAttackWorksInEveryDirection()
        {
            // 공격 범위는 공격한 방향을 따라 돈다. "양옆"은 언제나 공격 축의 좌우다.
            // 회전이 없으면 좌우로 칠 때 옆칸이 공격 축과 겹쳐 옆공격이 사라진다.
            AssertHitsThree(Direction.Down, new Vector2Int(4, 5), new Vector2Int(3, 5), new Vector2Int(5, 5));
            AssertHitsThree(Direction.Up, new Vector2Int(4, 3), new Vector2Int(3, 3), new Vector2Int(5, 3));
            AssertHitsThree(Direction.Left, new Vector2Int(3, 4), new Vector2Int(3, 3), new Vector2Int(3, 5));
            AssertHitsThree(Direction.Right, new Vector2Int(5, 4), new Vector2Int(5, 3), new Vector2Int(5, 5));
        }

        [Test]
        public void CharacterA_IsUnaffectedByRotation()
        {
            // 오프셋이 (0,0) 하나뿐이라 어느 방향으로 돌려도 부딪힌 칸 그대로다.
            var config = new FakeBoardConfig();
            Direction[] all = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

            for (int i = 0; i < all.Length; i++)
            {
                Player player = Make.Player(3, 2);
                BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 4));
                Vector2Int target = new Vector2Int(4, 4) + all[i].ToOffset();
                grid.Place(Make.Trash(TrashType.Glass, 3, 0), target);

                MoveResult result = Build(grid, config, player, Offsets.BumpedOnly).Resolve(all[i]);

                Assert.AreEqual(1, result.ChainSize, all[i] + " 방향에서도 한 칸이다.");
            }
        }

        /// <summary>세 칸(부딪힌 칸 + 양옆)에 적을 놓고 셋 다 맞는지 본다.</summary>
        private static void AssertHitsThree(Direction direction, Vector2Int target, Vector2Int a, Vector2Int b)
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 4));

            // 종류를 다르게 둬서 연쇄가 아니라 '오프셋'으로 맞았음을 분명히 한다.
            grid.Place(Make.Trash(TrashType.Glass, 3, 0), target);
            Trash sideA = Make.Trash(TrashType.Plastic, 3, 0);
            Trash sideB = Make.Trash(TrashType.Paper, 3, 0);
            grid.Place(sideA, a);
            grid.Place(sideB, b);

            MoveResult result = Build(grid, config, player, Offsets.BumpedAndSides).Resolve(direction);

            Assert.AreEqual(3, result.ChainSize, direction + " 방향: 세 칸이 맞아야 한다.");
            Assert.AreEqual(2, sideA.Hp, direction + " 방향: 옆칸 " + a + " 가 안 맞았다.");
            Assert.AreEqual(2, sideB.Hp, direction + " 방향: 옆칸 " + b + " 가 안 맞았다.");
        }

        // ── 캐릭터 B — 반격과 아이템 ────────────────────────────────────────

        [Test]
        public void CharacterB_OnlyTheBumpedEnemyCounters()
        {
            // 확정 규칙(§8-2)은 "부딪힌 한 마리만 반격"이다. 옆으로 같이 맞은 적은 반격하지 않는다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Trash(TrashType.Glass, 3, 1), new Vector2Int(4, 6));    // 부딪힘: 공격력 1
            grid.Place(Make.Trash(TrashType.Plastic, 3, 2), new Vector2Int(3, 6));  // 옆: 공격력 2
            grid.Place(Make.Trash(TrashType.Paper, 3, 2), new Vector2Int(5, 6));    // 옆: 공격력 2

            MoveResult result = Build(grid, config, player, Offsets.BumpedAndSides).Resolve(Direction.Down);

            Assert.AreEqual(1, result.DamageTaken, "부딪힌 놈의 1만 들어온다 — 옆의 2+2는 무시.");
            Assert.AreEqual(3, player.Hp, "4 - 1");
        }

        [Test]
        public void CharacterB_DoesNotEatPotionsInSideCells()
        {
            // 포션은 때리는 대상이 아니다. 부딪혀서 먹는 것만 소비된다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1);
            player.TakeDamage(2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Trash(TrashType.Glass, 3, 1), new Vector2Int(4, 6));
            Trash potion = Make.Potion(TrashType.Potion, 2);
            grid.Place(potion, new Vector2Int(3, 6));

            MoveResult result = Build(grid, config, player, Offsets.BumpedAndSides).Resolve(Direction.Down);

            Assert.AreEqual(1, result.ChainSize, "포션은 타격 대상에서 빠진다.");
            Assert.AreEqual(0, result.Healed, "회복되지 않는다.");
            Assert.AreEqual(1, result.DamageTaken, "부딪힌 유리조각의 반격만 들어온다.");
            Assert.AreEqual(1, player.Hp, "2 - 반격 1. 포션을 먹었다면 올라갔을 것이다.");
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(3, 6)), "포션은 그대로 남는다.");
        }

        // ── 밸런싱 문서의 전제 확인 ─────────────────────────────────────────

        [Test]
        public void CharacterA_OneShotsTheEarlyEnemies_ButNotGlass()
        {
            // 밸런싱 v1 §1-2: "공격력 2 = 잡몹 대부분(체력 1~2)을 원샷".
            var config = new FakeBoardConfig();

            Assert.AreEqual(1, KillCount(config, Make.Player(3, 2), TrashType.Paper, 1), "종잇조각(1)");
            Assert.AreEqual(1, KillCount(config, Make.Player(3, 2), TrashType.Plastic, 2), "페트병(2)");
            Assert.AreEqual(2, KillCount(config, Make.Player(3, 2), TrashType.Glass, 3), "유리조각(3)은 두 대");
        }

        [Test]
        public void CharacterB_NeedsTwoHitsForAnythingTougherThanOne()
        {
            // 밸런싱 v1 §1-2: "한 대상 기준 1데미지라 체력 2 이상 적은 원샷 불가".
            var config = new FakeBoardConfig();

            Assert.AreEqual(1, KillCount(config, Make.Player(4, 1), TrashType.Paper, 1));
            Assert.AreEqual(2, KillCount(config, Make.Player(4, 1), TrashType.Plastic, 2));
            Assert.AreEqual(3, KillCount(config, Make.Player(4, 1), TrashType.Glass, 3));
        }

        /// <summary>적 하나를 죽이는 데 필요한 타격 횟수. 반격은 무시하려고 체력을 넉넉히 준다.</summary>
        private static int KillCount(FakeBoardConfig config, Player player, TrashType type, int enemyHp)
        {
            var grid = new BoardGrid(config.Cols, config.Rows);
            grid.Place(player, new Vector2Int(4, 5));
            grid.Place(Make.Trash(type, enemyHp, 0), new Vector2Int(4, 6));

            CombatMoveResolver resolver = Build(grid, config, player, Offsets.BumpedOnly);

            int hits = 0;
            while (!grid.IsEmpty(new Vector2Int(4, 6)) && hits < 20)
            {
                resolver.Resolve(Direction.Down);
                hits++;
            }

            return hits;
        }
    }
}
