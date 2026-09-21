using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 폭탄. 기획서(폭탄·아이템·기믹 v1) §1.
    ///
    /// 확정된 규칙:
    ///  1. 설치는 <b>한 칸 거리의 빈 칸</b>에만 된다.
    ///  2. 설치만으로는 <b>터지지 않는다</b> — 플레이어가 때려야 카운트다운이 시작된다.
    ///  3. 점화 후 3턴 뒤 3×3 범위에 피해 5.
    ///  4. 폭탄도 다른 블록처럼 중력을 받는다.
    ///
    /// 미확정이라 설정값으로 빼 둔 것: 플레이어 피해 여부, 연쇄 폭발 여부.
    /// 두 경우를 모두 검사해 어느 쪽으로 확정되든 동작이 고정되게 한다.
    /// </summary>
    public sealed class BombTests
    {
        private static BombResolver Bombs(BoardGrid grid, Player player, FakeBoardConfig config)
            => new BombResolver(grid, player, config, config);

        private static CombatMoveResolver Combat(BoardGrid grid, Player player, FakeBoardConfig config)
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

        private static bool Has(System.Collections.Generic.IReadOnlyList<Vector2Int> cells, Vector2Int cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }

        // ── 설치 ────────────────────────────────────────────────────────────

        [Test]
        public void Placements_AreTheFourEmptyNeighbours()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            BombResolver bombs = Bombs(grid, player, config);

            Assert.AreEqual(4, bombs.CollectPlacements(), "사방이 비어 있으면 후보 4칸.");
        }

        [Test]
        public void OccupiedNeighbours_AreNotPlaceable()
        {
            // 몬스터·블록·벽이 있는 칸에는 못 놓는다(기획 확정).
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            grid.Place(Make.Trash(TrashType.Glass, 3, 1), new Vector2Int(4, 6));
            grid.Place(Make.Wall(TrashType.Concrete, 5), new Vector2Int(5, 5));

            BombResolver bombs = Bombs(grid, player, config);

            Assert.AreEqual(2, bombs.CollectPlacements(), "찬 두 칸은 빠진다.");
            Assert.IsFalse(bombs.CanPlace(new Vector2Int(4, 6)), "블록이 있는 칸.");
            Assert.IsFalse(bombs.CanPlace(new Vector2Int(5, 5)), "벽이 있는 칸.");
            Assert.IsTrue(bombs.CanPlace(new Vector2Int(3, 5)));
        }

        [Test]
        public void PreviewRow_IsNeverPlaceable()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 1));

            Assert.IsFalse(Bombs(grid, player, config).CanPlace(new Vector2Int(4, 0)));
        }

        [Test]
        public void TwoCellsAway_IsNotPlaceable()
        {
            // "한 칸 거리"가 규칙이다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            BombResolver bombs = Bombs(grid, player, config);
            bombs.CollectPlacements();

            Assert.IsFalse(Has(bombs.Placements, new Vector2Int(4, 7)), "두 칸 아래는 후보가 아니다.");
            Assert.IsFalse(Has(bombs.Placements, new Vector2Int(5, 6)), "대각선도 아니다.");
            Assert.IsTrue(Has(bombs.Placements, new Vector2Int(4, 6)), "바로 아래는 후보다.");
        }

        [Test]
        public void WithNoBombsLeft_NothingIsPlaceable()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 0);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            Assert.AreEqual(0, Bombs(grid, player, config).CollectPlacements());
        }

        [Test]
        public void Placing_SpendsOneBombAndLeavesItUnarmed()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            Assert.IsTrue(Bombs(grid, player, config).Place(new Vector2Int(4, 6)));

            Assert.AreEqual(2, player.Bombs, "보유가 하나 줄어든다.");
            var bomb = grid[new Vector2Int(4, 6)] as Bomb;
            Assert.IsNotNull(bomb);
            Assert.IsFalse(bomb.IsArmed, "설치만으로는 불이 붙지 않는다(기획 확정).");
        }

        // ── 점화와 카운트다운 ───────────────────────────────────────────────

        [Test]
        public void UnarmedBomb_NeverTicksDown()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            BombResolver bombs = Bombs(grid, player, config);
            bombs.Place(new Vector2Int(4, 6));

            for (int i = 0; i < 10; i++)
            {
                Assert.IsFalse(bombs.Tick().Happened, "불이 안 붙었으면 영원히 안 터진다.");
            }

            Assert.IsNotNull(grid[new Vector2Int(4, 6)] as Bomb);
        }

        [Test]
        public void HittingABomb_ArmsItWithoutDamagingIt()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            Bombs(grid, player, config).Place(new Vector2Int(4, 6));

            MoveResult result = Combat(grid, player, config).Resolve(Direction.Down);

            Assert.AreEqual(MoveOutcome.Armed, result.Outcome);
            Assert.AreEqual(new Vector2Int(4, 5), player.Position, "때려도 제자리다.");
            Assert.AreEqual(0, result.DamageTaken, "폭탄은 반격하지 않는다.");

            var bomb = grid[new Vector2Int(4, 6)] as Bomb;
            Assert.IsNotNull(bomb, "폭탄은 맞아서 사라지지 않는다.");
            Assert.IsTrue(bomb.IsArmed);
            Assert.AreEqual(3, bomb.FuseRemaining, "확정값 3턴.");
        }

        [Test]
        public void ArmedBomb_ExplodesOnTheThirdTick()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(0, 8));
            BombResolver bombs = Bombs(grid, player, config);
            bombs.Place(new Vector2Int(1, 8));
            ((Bomb)grid[new Vector2Int(1, 8)]).Arm();

            Assert.IsFalse(bombs.Tick().Happened, "점화한 턴은 세지 않는다.");
            Assert.IsFalse(bombs.Tick().Happened, "1턴째");
            Assert.IsFalse(bombs.Tick().Happened, "2턴째");

            BlastResult blast = bombs.Tick();

            Assert.IsTrue(blast.Happened, "3턴째에 터진다.");
            Assert.AreEqual(1, blast.Exploded);
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(1, 8)), "폭탄 자신도 사라진다.");
        }

        [Test]
        public void HittingAnAlreadyArmedBomb_DoesNotResetTheFuse()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            BombResolver bombs = Bombs(grid, player, config);
            bombs.Place(new Vector2Int(4, 6));

            CombatMoveResolver combat = Combat(grid, player, config);
            combat.Resolve(Direction.Down);
            bombs.Tick();                                   // 점화 턴 — 안 깎인다
            bombs.Tick();                                   // 3 -> 2
            combat.Resolve(Direction.Down);                 // 다시 때려도

            Assert.AreEqual(2, ((Bomb)grid[new Vector2Int(4, 6)]).FuseRemaining, "되감기지 않는다.");
        }

        // ── 폭발 ────────────────────────────────────────────────────────────

        [Test]
        public void Explosion_HitsEveryBlockInThreeByThree()
        {
            //   row 6  E E E
            //   row 7  E B E      B=폭탄
            //   row 8  E E E
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(0, 5));

            for (int y = 6; y <= 8; y++)
            {
                for (int x = 3; x <= 5; x++)
                {
                    if (x == 4 && y == 7) continue;
                    grid.Place(Make.Trash(TrashType.Glass, 3, 0), new Vector2Int(x, y));
                }
            }

            grid.Place(new Bomb(config.FuseTurns, config.Damage, config.BlastRadius), new Vector2Int(4, 7));
            ((Bomb)grid[new Vector2Int(4, 7)]).ForceDetonate();

            BlastResult blast = Bombs(grid, player, config).Tick();

            Assert.AreEqual(8, blast.Destroyed, "주변 8칸이 전부 날아간다(피해 5 > 체력 3).");
            for (int y = 6; y <= 8; y++)
            {
                for (int x = 3; x <= 5; x++)
                {
                    Assert.IsTrue(grid.IsEmpty(new Vector2Int(x, y)), "(" + x + "," + y + ") 가 남았다.");
                }
            }
        }

        [Test]
        public void Explosion_LeavesToughBlocksAliveWithDamage()
        {
            // 철제(6)는 피해 5를 맞고도 1이 남는다 — 밸런싱 문서 수치 그대로다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(0, 5));

            Trash steel = Make.Wall(TrashType.Steel, 6);
            grid.Place(steel, new Vector2Int(4, 8));
            grid.Place(new Bomb(config.FuseTurns, config.Damage, config.BlastRadius), new Vector2Int(4, 7));
            ((Bomb)grid[new Vector2Int(4, 7)]).ForceDetonate();

            Bombs(grid, player, config).Tick();

            Assert.AreEqual(1, steel.Hp, "6 - 5");
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(4, 8)), "살아남는다.");
        }

        [Test]
        public void Explosion_NeverTouchesThePreviewRow()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(0, 5));

            Trash waiting = Make.Trash(TrashType.Glass, 3, 0);
            grid.Place(waiting, new Vector2Int(4, 0));
            grid.Place(new Bomb(config.FuseTurns, config.Damage, config.BlastRadius), new Vector2Int(4, 1));
            ((Bomb)grid[new Vector2Int(4, 1)]).ForceDetonate();

            Bombs(grid, player, config).Tick();

            Assert.AreEqual(3, waiting.Hp, "프리뷰 줄의 대기 블록은 폭발에도 안 맞는다.");
        }

        // ── 미확정 항목 — 설정값에 따라 갈린다 ──────────────────────────────

        [Test]
        public void SelfDamage_FollowsTheConfigFlag()
        {
            Assert.AreEqual(5, PlayerDamageFromAdjacentBlast(damagesPlayer: true), "켜면 맞는다(현재 임시값).");
            Assert.AreEqual(0, PlayerDamageFromAdjacentBlast(damagesPlayer: false), "끄면 안 맞는다.");
        }

        [Test]
        public void ChainDetonation_FollowsTheConfigFlag()
        {
            Assert.AreEqual(2, ExplodedCountForBombPair(chainDetonates: true), "켜면 옆 폭탄도 같이 터진다.");
            Assert.AreEqual(1, ExplodedCountForBombPair(chainDetonates: false), "끄면 자기 카운트대로만.");
        }

        [Test]
        public void PlayerCaughtByTwoBlastsAtOnce_TakesDamageOnlyOnce()
        {
            // 체력이 3~4라 중첩되면 설명 없이 즉사한다. 한 번만 맞게 해 둔다.
            var config = new FakeBoardConfig { MaxHp = 20, ChainDetonates = true };
            Player player = Make.Player(20, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 8));

            grid.Place(new Bomb(config.FuseTurns, config.Damage, config.BlastRadius), new Vector2Int(3, 8));
            grid.Place(new Bomb(config.FuseTurns, config.Damage, config.BlastRadius), new Vector2Int(5, 8));
            ((Bomb)grid[new Vector2Int(3, 8)]).ForceDetonate();
            ((Bomb)grid[new Vector2Int(5, 8)]).ForceDetonate();

            BlastResult blast = Bombs(grid, player, config).Tick();

            Assert.AreEqual(2, blast.Exploded);
            Assert.AreEqual(5, blast.PlayerDamage, "두 폭발에 겹쳐도 5 한 번이다.");
            Assert.AreEqual(15, player.Hp);
        }

        private static int PlayerDamageFromAdjacentBlast(bool damagesPlayer)
        {
            var config = new FakeBoardConfig { MaxHp = 20, DamagesPlayer = damagesPlayer };
            Player player = Make.Player(20, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 8));

            grid.Place(new Bomb(config.FuseTurns, config.Damage, config.BlastRadius), new Vector2Int(4, 7));
            ((Bomb)grid[new Vector2Int(4, 7)]).ForceDetonate();

            return Bombs(grid, player, config).Tick().PlayerDamage;
        }

        private static int ExplodedCountForBombPair(bool chainDetonates)
        {
            var config = new FakeBoardConfig { MaxHp = 20, ChainDetonates = chainDetonates, DamagesPlayer = false };
            Player player = Make.Player(20, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(0, 5));

            grid.Place(new Bomb(config.FuseTurns, config.Damage, config.BlastRadius), new Vector2Int(4, 8));
            grid.Place(new Bomb(config.FuseTurns, config.Damage, config.BlastRadius), new Vector2Int(5, 8));
            ((Bomb)grid[new Vector2Int(4, 8)]).ForceDetonate();

            return Bombs(grid, player, config).Tick().Exploded;
        }

        // ── 루프 통합 ───────────────────────────────────────────────────────

        [Test]
        public void BombFalls_LikeAnyOtherBlock()
        {
            // 기획 확정: 폭탄도 밑으로 떨어진다.
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(0, 8), BlocksPerSpawn = 0 };
            GameLoop loop = Make.Week1(config, new MinRandom());

            var bomb = new Bomb(config.FuseTurns, config.Damage, config.BlastRadius);
            loop.Grid.Place(bomb, new Vector2Int(4, 4));

            loop.Step(Direction.Up);

            Assert.AreEqual(new Vector2Int(4, 5), bomb.Position, "한 스텝에 한 칸 내려온다.");
        }

        [Test]
        public void PlacedBomb_StaysPutOnTheTurnItWasPlaced()
        {
            // 놓자마자 발밑으로 흘러내리면 "여기에 놓는다"는 조작이 성립하지 않는다.
            // 설치한 칸에 머물렀다가, 플레이어가 다음 행동을 하면 그때부터 떨어진다.
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(4, 4), BlocksPerSpawn = 0 };
            GameLoop loop = Make.Week1(config, new MinRandom());

            var spot = new Vector2Int(4, 5);            // 아래는 전부 빈 보드다
            Assert.IsTrue(loop.PlaceBomb(spot).Advanced);

            var bomb = loop.Grid[spot] as Bomb;
            Assert.IsNotNull(bomb, "설치한 턴에는 그 자리에 있어야 한다.");
            Assert.AreEqual(spot, bomb.Position);

            // 다음 행동부터는 다른 블록과 똑같이 한 칸씩 내려간다.
            loop.Step(Direction.Up);
            Assert.AreEqual(new Vector2Int(4, 6), bomb.Position, "이제 한 칸 내려온다.");

            loop.Step(Direction.Up);
            Assert.AreEqual(new Vector2Int(4, 7), bomb.Position, "계속 내려온다.");
        }

        [Test]
        public void TheHoldLastsExactlyOneTurn()
        {
            // 유예가 한 턴을 넘기면 폭탄이 공중에 떠 버린다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(4, 1, 3);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 4));
            var gravity = new GravityResolver(grid);

            Bombs(grid, player, config).Place(new Vector2Int(4, 5));
            var bomb = grid[new Vector2Int(4, 5)] as Bomb;

            Assert.IsTrue(bomb.HoldsPosition, "설치 직후에는 유예가 걸려 있다.");

            Assert.AreEqual(0, gravity.Step(), "설치 턴 중력은 건너뛴다.");
            Assert.IsFalse(bomb.HoldsPosition, "건너뛰면서 유예가 풀린다.");

            Assert.AreEqual(1, gravity.Step(), "그다음 중력부터는 떨어진다.");
            Assert.AreEqual(new Vector2Int(4, 6), bomb.Position);
        }

        [Test]
        public void PlacingABomb_CountsAsATurn()
        {
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(4, 8) };
            GameLoop loop = Make.Week1(config, new MinRandom());

            StepResult result = loop.PlaceBomb(new Vector2Int(4, 7));

            Assert.AreEqual(MoveOutcome.BombPlaced, result.Move);
            Assert.IsTrue(result.Advanced, "설치도 한 턴을 쓴다(임시 확정).");
            Assert.AreEqual(1, loop.StepCount);
            Assert.AreEqual(config.StartingCount - 1, loop.Player.Bombs);
        }

        [Test]
        public void PlacingOnAnInvalidCell_DoesNotAdvanceTheBoard()
        {
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(4, 8) };
            GameLoop loop = Make.Week1(config, new MinRandom());

            StepResult result = loop.PlaceBomb(new Vector2Int(0, 0));

            Assert.IsFalse(result.Advanced, "무효 입력이므로 보드가 진행되지 않는다.");
            Assert.AreEqual(0, loop.StepCount);
            Assert.AreEqual(config.StartingCount, loop.Player.Bombs, "보유도 안 깎인다.");
        }

        [Test]
        public void FullBombCycle_PlaceThenHitThenWait()
        {
            // 설치 -> 때려서 점화 -> 3턴 뒤 폭발. 기획서의 흐름 그대로다.
            var config = new FakeBoardConfig
            {
                PlayerStart = new Vector2Int(4, 8),
                BlocksPerSpawn = 0,
                DamagesPlayer = false,
            };
            GameLoop loop = Make.Week1(config, new MinRandom());

            Trash victim = Make.Trash(TrashType.Glass, 3, 0);
            loop.Grid.Place(victim, new Vector2Int(5, 7));

            Assert.IsTrue(loop.PlaceBomb(new Vector2Int(4, 7)).Advanced, "① 설치");

            StepResult armed = loop.Step(Direction.Up);
            Assert.AreEqual(MoveOutcome.Armed, armed.Move, "② 때려서 점화");

            // 점화한 턴은 세지 않으므로, 그다음 세 턴이 카운트다운이다.
            Assert.AreEqual(0, loop.Step(Direction.Left).BombsExploded, "카운트 3 -> 2");
            Assert.AreEqual(0, loop.Step(Direction.Right).BombsExploded, "2 -> 1");

            StepResult boom = loop.Step(Direction.Left);

            Assert.AreEqual(1, boom.BombsExploded, "③ 3턴 뒤 폭발");
            Assert.AreEqual(1, boom.BlastDestroyed, "옆 블록이 날아간다.");
        }
    }
}
