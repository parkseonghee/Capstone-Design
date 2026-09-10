using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 벽(장애물) 3종. 밸런싱 v1 §3.
    ///
    /// 벽은 데미지를 주지 않는 대신 <b>시간과 동선을 빼앗는</b> 존재다.
    ///  · 공격력 0 — 때려도 반격이 없다.
    ///  · 체력이 잡몹보다 높다 (나무 3 / 콘크리트 5 / 철제 6).
    ///  · 서로 연쇄되는지는 <b>종류마다 따로</b> 정한다(TrashStatsConfig의 Chains With Same Type).
    ///
    /// <b>어느 벽이 연쇄하는지는 아직 기획 미확정</b>이다(나무만? 전부?).
    /// 그래서 이 테스트들은 에셋 설정을 고정하지 않는다 — 페이크로 직접 만든 벽을 써서
    /// "끄면 안 묶이고 켜면 묶인다"는 <b>동작</b>만 검사한다.
    /// 기획이 어느 쪽으로 정해지든 이 테스트들은 그대로 통과한다.
    /// </summary>
    public sealed class WallTests
    {
        private static CombatMoveResolver Build(BoardGrid grid, IBoardConfig config, Player player)
            => new CombatMoveResolver(
                grid,
                player,
                config,
                new FakeCharacter(player.MaxHp, player.Attack, Offsets.BumpedOnly),
                new ChainFinder(grid, new SameTypeChainRule(), config.FirstPlayableRow));

        private static BoardGrid EmptyBoard(FakeBoardConfig config, Player player, Vector2Int at)
        {
            var grid = new BoardGrid(config.Cols, config.Rows);
            grid.Place(player, at);
            return grid;
        }

        [Test]
        public void Wall_TakesDamageButNeverCounters()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(3, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            Trash wall = Make.Wall(TrashType.Concrete, 5);
            grid.Place(wall, new Vector2Int(4, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(MoveOutcome.Attacked, result.Outcome);
            Assert.AreEqual(3, wall.Hp, "5 - 공격력 2");
            Assert.AreEqual(0, result.DamageTaken, "벽은 공격력이 없어 반격하지 않는다.");
            Assert.AreEqual(3, player.Hp);
            Assert.AreEqual(new Vector2Int(4, 5), player.Position, "때려도 제자리다.");
        }

        [Test]
        public void AdjacentWallsOfTheSameKind_DoNotChain()
        {
            // 이게 깨지면 콘크리트 벽 세 개를 한 방에 같이 깎을 수 있어
            // "우회 유도"라는 설계 의도가 사라진다.
            //   row 5  . . . @ .
            //   row 6  . . W W .     둘 다 콘크리트
            var config = new FakeBoardConfig();
            Player player = Make.Player(3, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            Trash bumped = Make.Wall(TrashType.Concrete, 5);
            Trash neighbour = Make.Wall(TrashType.Concrete, 5);
            grid.Place(bumped, new Vector2Int(4, 6));
            grid.Place(neighbour, new Vector2Int(5, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(1, result.ChainSize, "벽은 자기 혼자만 맞는다.");
            Assert.AreEqual(3, bumped.Hp);
            Assert.AreEqual(5, neighbour.Hp, "옆 벽은 멀쩡하다.");
        }

        [Test]
        public void OrdinaryEnemiesStillChain_SoTheFlagIsPerType()
        {
            // 벽만 연쇄가 꺼져 있다는 걸 대조로 확인한다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(3, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Trash(TrashType.Plastic, 5, 0), new Vector2Int(4, 6));
            Trash neighbour = Make.Trash(TrashType.Plastic, 5, 0);
            grid.Place(neighbour, new Vector2Int(5, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(2, result.ChainSize, "적은 여전히 연쇄된다.");
            Assert.AreEqual(3, neighbour.Hp);
        }

        [Test]
        public void Wall_IsNotConsumable()
        {
            Trash wall = Make.Wall(TrashType.Wood, 3);

            Assert.IsFalse(wall.IsConsumable, "벽은 먹는 게 아니라 때리는 것이다.");
            Assert.AreEqual(0, wall.Heal);
            Assert.AreEqual(0, wall.Attack);
            Assert.AreEqual(3, wall.MaxHp);
        }

        [Test]
        public void Wall_BlocksTheCellUntilItIsDestroyed()
        {
            // 벽이 서 있는 동안은 그 칸으로 못 간다. 부수고 나서야 지나갈 수 있다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(3, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            grid.Place(Make.Wall(TrashType.Wood, 3), new Vector2Int(4, 6));

            CombatMoveResolver resolver = Build(grid, config, player);

            Assert.AreEqual(MoveOutcome.Attacked, resolver.Resolve(Direction.Down).Outcome);
            Assert.AreEqual(new Vector2Int(4, 5), player.Position, "아직 못 간다.");

            Assert.AreEqual(MoveOutcome.Attacked, resolver.Resolve(Direction.Down).Outcome, "체력 3, 공격력 2 -> 두 대");
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(4, 6)), "부서졌다.");

            Assert.AreEqual(MoveOutcome.Moved, resolver.Resolve(Direction.Down).Outcome);
            Assert.AreEqual(new Vector2Int(4, 6), player.Position, "이제 지나간다.");
        }

        // ── 연쇄 여부는 종류마다 따로 (기획 미확정 대비) ────────────────────

        [Test]
        public void ChainingIsDecidedPerType_NotForWallsAsAWhole()
        {
            // "나무만 연쇄 금지"와 "모든 벽 연쇄 금지" 중 무엇으로 정해지든
            // 코드 수정 없이 설정만으로 갈린다는 걸 고정한다.
            //   row 5  . . @ . . @ .       왼쪽은 나무(연쇄 끔), 오른쪽은 콘크리트(연쇄 켬)
            //   row 6  . . W W . C C .
            var config = new FakeBoardConfig();

            Assert.AreEqual(1, ChainSizeForPair(config, TrashType.Wood, 3, chains: false),
                "연쇄를 끈 종류는 혼자만 맞는다.");
            Assert.AreEqual(2, ChainSizeForPair(config, TrashType.Concrete, 5, chains: true),
                "같은 벽이라도 연쇄를 켜면 옆까지 같이 맞는다.");
        }

        [Test]
        public void ChainingChangesHowLongAWallClusterTakes()
        {
            // 결정의 무게를 숫자로 남겨 둔다. 캐릭터 A(공격력 2)로 콘크리트(체력 5) 두 개가
            // 세로로 붙어 있는 걸 치울 때, 계속 아래만 누른다고 하면:
            var config = new FakeBoardConfig();

            Assert.AreEqual(3, TurnsToClearStack(config, chains: true),
                "연쇄 있음: 둘이 같이 맞아 3턴 — 벽이 뭉칠수록 오히려 싸진다.");
            Assert.AreEqual(7, TurnsToClearStack(config, chains: false),
                "연쇄 없음: 앞 벽 3턴 + 빈 칸으로 한 턴 이동 + 뒤 벽 3턴 = 7턴");
        }

        /// <summary>
        /// 세로로 붙은 콘크리트 두 개를 전부 없애는 데 걸리는 턴 수.
        /// 아래만 계속 누르는 상황이라 이동 처리를 따로 하지 않아도 된다.
        /// </summary>
        private static int TurnsToClearStack(FakeBoardConfig config, bool chains)
        {
            Player player = Make.Player(3, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            var near = new Trash(TrashType.Concrete, new TrashStats(5, 0, 0, 1, chains));
            var far = new Trash(TrashType.Concrete, new TrashStats(5, 0, 0, 1, chains));
            grid.Place(near, new Vector2Int(4, 6));
            grid.Place(far, new Vector2Int(4, 7));

            CombatMoveResolver resolver = Build(grid, config, player);

            int turns = 0;
            while ((!near.IsDead || !far.IsDead) && turns < 30)
            {
                resolver.Resolve(Direction.Down);
                turns++;
            }

            return turns;
        }

        /// <summary>나란한 벽 두 개를 치고 몇 칸이 맞았는지.</summary>
        private static int ChainSizeForPair(FakeBoardConfig config, TrashType type, int hp, bool chains)
        {
            Player player = Make.Player(3, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(new Trash(type, new TrashStats(hp, 0, 0, 1, chains)), new Vector2Int(4, 6));
            grid.Place(new Trash(type, new TrashStats(hp, 0, 0, 1, chains)), new Vector2Int(5, 6));

            return Build(grid, config, player).Resolve(Direction.Down).ChainSize;
        }

        /// <summary>나란한 콘크리트 두 개를 전부 없애는 데 걸리는 턴 수.</summary>
        private static int TurnsToClearPair(FakeBoardConfig config, bool chains)
        {
            Player player = Make.Player(3, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            var left = new Vector2Int(4, 6);
            var right = new Vector2Int(5, 6);
            grid.Place(new Trash(TrashType.Concrete, new TrashStats(5, 0, 0, 1, chains)), left);
            grid.Place(new Trash(TrashType.Concrete, new TrashStats(5, 0, 0, 1, chains)), right);

            CombatMoveResolver resolver = Build(grid, config, player);

            int turns = 0;
            while ((!grid.IsEmpty(left) || !grid.IsEmpty(right)) && turns < 30)
            {
                // 앞이 비면 옆으로 돌아가 마저 친다(플레이어는 제자리이므로 직접 옮긴다).
                if (grid.IsEmpty(left) && !grid.IsEmpty(right))
                {
                    resolver.Resolve(Direction.Down);   // 빈 칸으로 이동
                    resolver.Resolve(Direction.Right);  // 옆 벽을 친다
                    turns += 2;
                    continue;
                }

                resolver.Resolve(Direction.Down);
                turns++;
            }

            return turns;
        }

        // ── 밸런싱 문서의 타격 횟수 확인 ────────────────────────────────────

        [Test]
        public void ConcreteWall_TakesThreeHitsFromA_AndFiveFromB()
        {
            // 밸런싱 v1 §3: "체력 5~6은 A 기준 3대, B 기준 5대 -> B에게 특히 불리".
            var config = new FakeBoardConfig();

            Assert.AreEqual(3, HitsToBreak(config, Make.Player(3, 2), 5), "캐릭터 A (공격력 2)");
            Assert.AreEqual(5, HitsToBreak(config, Make.Player(4, 1), 5), "캐릭터 B (공격력 1)");
        }

        [Test]
        public void SteelWall_IsDeliberatelyInefficientToBreak()
        {
            // 철제(6)는 "파괴 비효율. 지형으로 취급"이다.
            var config = new FakeBoardConfig();

            Assert.AreEqual(3, HitsToBreak(config, Make.Player(3, 2), 6), "A라도 세 턴을 버린다.");
            Assert.AreEqual(6, HitsToBreak(config, Make.Player(4, 1), 6), "B는 여섯 턴 — 사실상 우회해야 한다.");
        }

        private static int HitsToBreak(FakeBoardConfig config, Player player, int wallHp)
        {
            var grid = new BoardGrid(config.Cols, config.Rows);
            grid.Place(player, new Vector2Int(4, 5));
            grid.Place(Make.Wall(TrashType.Concrete, wallHp), new Vector2Int(4, 6));

            CombatMoveResolver resolver = Build(grid, config, player);

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
