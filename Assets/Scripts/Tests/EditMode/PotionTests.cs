using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 체력 포션 — 부딪히면 먹는 소비 아이템.
    ///
    /// 확정된 규칙:
    ///  1. 체력·공격력이 없다. 하트도 안 뜨고 반격도 안 한다.
    ///  2. 하나당 정해진 양만큼 플레이어를 회복시킨다(기본 2).
    ///  3. 적과 <b>같은 연쇄 규칙</b>을 탄다 — 붙어 있는 같은 포션까지 한 번에 먹고 합산 회복한다.
    ///  4. 먹어도 <b>플레이어는 제자리</b>다. 공격과 규칙을 맞췄다(기획 확정).
    ///
    /// 적이냐 아이템이냐는 코드 분기가 아니라 설정값(Heal)이 정한다 — 그래서 이 테스트들은
    /// TrashType.Potion을 쓰지만, 아무 종류에나 Heal을 주면 똑같이 동작한다.
    /// </summary>
    public sealed class PotionTests
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

        private static BoardGrid EmptyBoard(FakeBoardConfig config, Player player, Vector2Int at)
        {
            var grid = new BoardGrid(config.Cols, config.Rows);
            grid.Place(player, at);
            return grid;
        }

        // ── 기본 ────────────────────────────────────────────────────────────

        [Test]
        public void BumpingAPotion_EatsItAndHealsThePlayer()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            player.TakeDamage(5);                       // 5/10 에서 시작
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(4, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(MoveOutcome.Consumed, result.Outcome);
            Assert.AreEqual(2, result.Healed);
            Assert.AreEqual(7, player.Hp, "5 + 2");
            Assert.AreEqual(1, result.ChainSize);
            Assert.AreEqual(0, result.Killed, "먹은 건 처치가 아니다.");
            Assert.AreEqual(0, result.DamageTaken, "포션은 반격하지 않는다.");
        }

        [Test]
        public void EatingAPotion_LeavesThePlayerWhereItWas()
        {
            // 버튼 한 번은 "먹는다"이지 "먹고 들어간다"가 아니다.
            // 한 칸 전진할 생각이 없을 때 실수로 밀려 들어가면 안 되기 때문이다(기획 확정).
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            player.TakeDamage(4);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(4, 6));

            Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(new Vector2Int(4, 5), player.Position, "제자리에 있어야 한다.");
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(4, 6)), "포션이 있던 칸은 비고, 들어가지는 않는다.");
        }

        [Test]
        public void Potion_HasNoHealthAndNoAttack()
        {
            // 하트를 그릴지 말지를 뷰가 MaxHp로 판단하므로, 0이어야 한다.
            Trash potion = Make.Potion(TrashType.Potion, 2);

            Assert.AreEqual(0, potion.MaxHp);
            Assert.AreEqual(0, potion.Attack);
            Assert.AreEqual(2, potion.Heal);
            Assert.IsTrue(potion.IsConsumable);
        }

        [Test]
        public void OrdinaryTrash_IsNotConsumable()
        {
            Assert.IsFalse(Make.Trash(TrashType.Paper, 3, 1).IsConsumable);
        }

        // ── 연쇄 ────────────────────────────────────────────────────────────

        [Test]
        public void ConnectedPotions_AreAllEatenAndTheHealAddsUp()
        {
            //   row 5  . . . . @ .
            //   row 6  . . . . P P     <- (4,6)을 친다
            //   row 7  . . . . P .
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            player.TakeDamage(8);                       // 2/10
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(4, 6));
            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(5, 6));
            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(4, 7));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(3, result.ChainSize, "붙어 있는 포션 셋이 한 덩어리다.");
            Assert.AreEqual(6, result.Healed, "2 x 3");
            Assert.AreEqual(8, player.Hp, "2 + 6");
            Assert.AreEqual(config.Cols * config.Rows - 1, grid.CountEmpty(), "셋 다 사라지고 플레이어만 남는다.");
        }

        [Test]
        public void PotionChain_DoesNotTravelDiagonally()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            player.TakeDamage(8);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(4, 6));
            Trash diagonal = Make.Potion(TrashType.Potion, 2);
            grid.Place(diagonal, new Vector2Int(5, 7));   // 대각선으로만 닿는다

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(1, result.ChainSize);
            Assert.AreEqual(2, result.Healed);
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(5, 7)), "대각선 포션은 남는다.");
        }

        [Test]
        public void PotionsAndEnemies_NeverShareAChain()
        {
            // 종류가 다르므로 서로 묶이지 않는다. 적을 쳐도 옆 포션은 안 없어진다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Trash(TrashType.Paper, 5, 1), new Vector2Int(4, 6));
            Trash potion = Make.Potion(TrashType.Potion, 2);
            grid.Place(potion, new Vector2Int(5, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(MoveOutcome.Attacked, result.Outcome, "부딪힌 건 적이므로 공격이다.");
            Assert.AreEqual(1, result.ChainSize);
            Assert.AreEqual(0, result.Healed);
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(5, 6)), "옆 포션은 그대로 남는다.");
            Assert.AreEqual(2, potion.Heal);
        }

        // ── 회복량 한계 ─────────────────────────────────────────────────────

        [Test]
        public void Overheal_IsCappedAtMaxHp_AndTheRestIsWasted()
        {
            // 9/10 에서 2회복 포션 셋(6)을 한 번에 먹어도 1만 찬다.
            // "언제 먹을지"가 판단거리가 되는 지점이라 일부러 이렇게 둔다.
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            player.TakeDamage(1);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));

            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(4, 6));
            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(5, 6));
            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(4, 7));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(3, result.ChainSize, "셋 다 사라진다 — 넘친다고 남겨 두지 않는다.");
            Assert.AreEqual(1, result.Healed, "실제로 찬 건 1뿐이다.");
            Assert.AreEqual(10, player.Hp);
        }

        [Test]
        public void EatingAtFullHealth_HealsNothingButStillConsumes()
        {
            var config = new FakeBoardConfig();
            Player player = Make.Player(10, 2);
            BoardGrid grid = EmptyBoard(config, player, new Vector2Int(4, 5));
            grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(4, 6));

            MoveResult result = Build(grid, config, player).Resolve(Direction.Down);

            Assert.AreEqual(0, result.Healed);
            Assert.AreEqual(10, player.Hp);
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(4, 6)), "회복이 0이어도 포션은 사라진다.");
            Assert.AreEqual(new Vector2Int(4, 5), player.Position, "제자리다.");
        }

        // ── 루프 통합 ───────────────────────────────────────────────────────

        [Test]
        public void EatingAPotion_CountsAsATurn()
        {
            var config = new FakeBoardConfig { PlayerStart = new Vector2Int(4, 7) };
            var stats = new FakeTrashStats().SetPotion(TrashType.Potion, 2);
            GameLoop loop = Make.Week1(config, new MinRandom(), stats);
            loop.Player.TakeDamage(5);

            loop.Grid.Place(Make.Potion(TrashType.Potion, 2), new Vector2Int(4, 8));

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(MoveOutcome.Consumed, result.Move);
            Assert.IsTrue(result.Advanced, "먹는 것도 한 턴을 쓴다.");
            Assert.AreEqual(1, loop.StepCount);
            Assert.AreEqual(2, result.Healed);
            Assert.AreEqual(7, loop.Player.Hp);
            Assert.AreEqual(new Vector2Int(4, 7), loop.Player.Position, "턴이 돌아도 제자리다.");
        }

        // ── 스폰 가중치 ─────────────────────────────────────────────────────

        [Test]
        public void SpawnWeightZero_KeepsThatTypeOutOfTheDraw()
        {
            // 포션 가중치를 0으로 두면 랜덤 스폰에 절대 안 나온다.
            var stats = new FakeTrashStats(spawnWeight: 0)
                .SetWeight(TrashType.Glass, 1);

            var picker = new TrashTypePicker(stats, new SystemRandomSource(1234));

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(TrashType.Glass, picker.Next(), "가중치가 있는 종류만 나와야 한다.");
            }
        }

        [Test]
        public void SpawnWeight_DecidesHowOftenATypeAppears()
        {
            // A는 가중치 9, 포션은 1 -> 포션이 10% 근처로 나와야 한다.
            var stats = new FakeTrashStats(spawnWeight: 0)
                .SetWeight(TrashType.Paper, 9)
                .SetWeight(TrashType.Potion, 1);

            var picker = new TrashTypePicker(stats, new SystemRandomSource(20260910));

            int potions = 0;
            const int Draws = 4000;
            for (int i = 0; i < Draws; i++)
            {
                if (picker.Next() == TrashType.Potion)
                {
                    potions++;
                }
            }

            // 4000번이면 10%에서 크게 벗어나지 않는다. 시드를 고정했으므로 값도 재현된다.
            Assert.That(potions, Is.InRange(Draws * 0.08f, Draws * 0.12f),
                "가중치 1/10이면 대략 10%다. 실제=" + potions);
        }

        [Test]
        public void AllWeightsZero_FallsBackToUniformInsteadOfBreaking()
        {
            // 설정 실수로 전부 0이어도 스폰이 멈추면 안 된다.
            var stats = new FakeTrashStats(spawnWeight: 0);
            var picker = new TrashTypePicker(stats, new MinRandom());

            Assert.AreEqual(TrashType.Paper, picker.Next());
        }
    }
}
