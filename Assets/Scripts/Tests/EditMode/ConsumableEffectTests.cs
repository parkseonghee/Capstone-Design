using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 조합레시피 문서 3단계의 소모품 효과와 드랍 규칙 중 Core가 맡는 부분.
    ///  - C11 방호복: 다음 피격 1회 무효
    ///  - C06 방독면: N턴 동안 피해 무효
    ///  - C12 완전 회복 키트: 체력 전부 회복
    ///  - C04 제거 스프레이: 보드의 덫 전부 제거
    ///  - C07 재배치 물약: 적끼리 자리 섞기
    ///  - 드랍: N마리마다 재료 1개 보장
    /// </summary>
    public sealed class ConsumableEffectTests
    {
        private static FakeBoardConfig QuietBoard()
            => new FakeBoardConfig { RowsOnStart = 0, BlocksPerSpawn = 0, PlayerStart = new Vector2Int(0, 8) };

        // ── 방호복 · 방독면 · 완전 회복 ───────────────────────────────────

        [Test]
        public void Shield_BlocksExactlyOneHit()
        {
            var player = new Player(10, 1);
            player.AddShield(1);

            player.TakeDamage(3);
            Assert.AreEqual(10, player.Hp, "첫 피격은 막힌다");
            Assert.AreEqual(0, player.Shields);

            player.TakeDamage(3);
            Assert.AreEqual(7, player.Hp, "두 번째부터는 그대로 맞는다");
        }

        [Test]
        public void Invulnerability_LastsTheGivenTurns()
        {
            var player = new Player(10, 1);
            player.GrantInvulnerability(3);

            for (int turn = 0; turn < 3; turn++)
            {
                player.TakeDamage(2);
                player.TickInvulnerability();
            }

            Assert.AreEqual(10, player.Hp, "3턴 동안은 무엇에 맞아도 멀쩡하다");

            player.TakeDamage(2);
            Assert.AreEqual(8, player.Hp, "그 뒤에는 다시 아프다");
        }

        [Test]
        public void Invulnerability_DoesNotStackIntoLongerTime()
        {
            var player = new Player(10, 1);
            player.GrantInvulnerability(3);
            player.GrantInvulnerability(2);

            Assert.AreEqual(3, player.InvulnerableTurns, "긴 쪽이 남고 더해지지 않는다");
        }

        [Test]
        public void LoopTicksInvulnerability_AtTheEndOfEachTurn()
        {
            var config = QuietBoard();
            GameLoop loop = Make.Week1(config, new MinRandom());
            loop.Player.GrantInvulnerability(2);

            loop.Wait();
            loop.Wait();

            Assert.AreEqual(0, loop.Player.InvulnerableTurns);
        }

        [Test]
        public void HealFull_FillsToMax()
        {
            var player = new Player(10, 1);
            player.TakeDamage(7);

            Assert.AreEqual(7, player.HealFull());
            Assert.AreEqual(10, player.Hp);
        }

        [Test]
        public void ShieldAndInvulnerability_CarryOverToTheNextWave()
        {
            var previous = new Player(10, 1);
            previous.AddShield(1);
            previous.GrantInvulnerability(2);

            var next = new Player(10, 1);
            next.CarryOver(previous);

            Assert.AreEqual(1, next.Shields);
            Assert.AreEqual(2, next.InvulnerableTurns);
        }

        // ── 제거 스프레이 · 재배치 물약 ───────────────────────────────────

        [Test]
        public void RemoveAllTraps_ClearsEveryTrapAndNothingElse()
        {
            var config = QuietBoard();
            GameLoop loop = Make.Week1(config, new MinRandom());
            loop.Grid.Place(new Trap(), new Vector2Int(2, 8));
            loop.Grid.Place(new Trap(), new Vector2Int(5, 6));
            Trash enemy = Make.Trash(TrashType.Paper);
            loop.Grid.Place(enemy, new Vector2Int(6, 8));

            Assert.AreEqual(2, loop.RemoveAllTraps());
            Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(2, 8)));
            Assert.IsTrue(loop.Grid.IsEmpty(new Vector2Int(5, 6)));
            Assert.AreSame(enemy, loop.Grid[new Vector2Int(6, 8)], "덫이 아닌 것은 그대로다");
        }

        [Test]
        public void ShuffleEnemies_KeepsTheSameCells_AndLeavesWallsAlone()
        {
            var config = QuietBoard();
            GameLoop loop = Make.Week1(config, new MinRandom());

            var cells = new[] { new Vector2Int(2, 8), new Vector2Int(3, 8), new Vector2Int(4, 8) };
            for (int i = 0; i < cells.Length; i++)
            {
                loop.Grid.Place(Make.Trash(TrashType.Paper), cells[i]);
            }

            Trash wall = Make.Wall(TrashType.Wood, 3);
            loop.Grid.Place(wall, new Vector2Int(6, 8));

            Assert.AreEqual(3, loop.ShuffleEnemies(new SystemRandomSource(7)));

            for (int i = 0; i < cells.Length; i++)
            {
                var trash = loop.Grid[cells[i]] as Trash;
                Assert.IsNotNull(trash, "적이 있던 칸에는 여전히 적이 있다");
                Assert.IsTrue(trash.IsEnemy);
            }

            Assert.AreSame(wall, loop.Grid[new Vector2Int(6, 8)], "벽은 섞이지 않는다");
        }

        // ── 드랍: N마리마다 보장 ─────────────────────────────────────────

        private sealed class NeverDropsTable : IMaterialDropTable
        {
            public bool TryRoll(TrashType type, IRandomSource random, int bonusChance, out string materialId)
            {
                materialId = null;
                return false;
            }

            public int GuaranteeEvery => 3;

            public bool TryPickGuaranteed(IRandomSource random, out string materialId)
            {
                materialId = "M05";
                return true;
            }
        }

        [Test]
        public void EveryNthKill_GuaranteesAMaterial_EvenIfTheRollsMiss()
        {
            var mods = new RunModifiers();
            var drops = new MaterialDropResolver(new NeverDropsTable(), new MinRandom(), mods);
            int dropped = 0;
            drops.Dropped += id => dropped++;

            for (int i = 0; i < 7; i++)
            {
                drops.MaybeDrop(Make.Trash(TrashType.Paper));
            }

            Assert.AreEqual(2, dropped, "3번째와 6번째 처치에 하나씩");
            Assert.AreEqual(1, mods.KillsSinceGuaranteedDrop, "7번째는 다음 보장까지의 첫 처치다");
        }

        [Test]
        public void GuaranteeCounter_SurvivesANewLoop()
        {
            // 웨이브마다 루프(=드랍 리졸버)가 새로 만들어져도 카운터는 런과 함께 이어진다.
            var mods = new RunModifiers();
            int dropped = 0;

            var first = new MaterialDropResolver(new NeverDropsTable(), new MinRandom(), mods);
            first.Dropped += id => dropped++;
            first.MaybeDrop(Make.Trash(TrashType.Paper));
            first.MaybeDrop(Make.Trash(TrashType.Paper));

            var second = new MaterialDropResolver(new NeverDropsTable(), new MinRandom(), mods);
            second.Dropped += id => dropped++;
            second.MaybeDrop(Make.Trash(TrashType.Paper));

            Assert.AreEqual(1, dropped);
        }

        [Test]
        public void StageDropChance_AddsToTheRelicBonus()
        {
            var mods = new RunModifiers();
            mods.AddDropChance(5);
            mods.AddStageDropChance(10);

            Assert.AreEqual(15, mods.TotalDropChance);
        }
    }
}
