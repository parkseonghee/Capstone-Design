using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// WEEK1_MOVEMENT_FALLING.md §5 — 확정된 스폰 규칙.
    /// 새 쓰레기는 프리뷰 줄로만 들어오고, 캐이던스와 컬럼 잠금이 겹쳐 돈다.
    /// </summary>
    public sealed class SpawnTests
    {
        private static PreviewRowSpawner Build(BoardGrid grid, FakeBoardConfig config, IRandomSource random)
            => new PreviewRowSpawner(grid, config, config, config, random);

        // ── 스폰 위치 ───────────────────────────────────────────────────────

        [Test]
        public void SpawnsIntoThePreviewRowOnly()
        {
            var config = new FakeBoardConfig();
            var grid = new BoardGrid(config.Cols, config.Rows);
            PreviewRowSpawner spawner = Build(grid, config, new MinRandom());

            Assert.AreEqual(1, spawner.Spawn());
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(0, 0)), "MinRandom은 가장 왼쪽 후보를 고른다.");
            Assert.AreEqual(TrashType.Paper, ((Trash)grid[new Vector2Int(0, 0)]).Type);
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(0, 1)), "낙하는 다음 스텝의 중력이 시킨다.");
        }

        [Test]
        public void ScriptedRandom_MakesSpawnFullyDeterministic()
        {
            var config = new FakeBoardConfig();
            var grid = new BoardGrid(config.Cols, config.Rows);
            // 첫 값 = 후보 컬럼 인덱스, 둘째 값 = TrashType 인덱스
            PreviewRowSpawner spawner = Build(grid, config, new ScriptedRandom(5, 2));

            spawner.Spawn();

            Assert.IsTrue(grid.IsEmpty(new Vector2Int(0, 0)));
            Assert.AreEqual(TrashType.Glass, ((Trash)grid[new Vector2Int(5, 0)]).Type);
        }

        // ── 캐이던스 ────────────────────────────────────────────────────────

        [Test]
        public void EveryTurnBeforeTheThreshold()
        {
            var config = new FakeBoardConfig { BlocksBeforeSlowdown = 3 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            PreviewRowSpawner spawner = Build(grid, config, new MinRandom());

            Assert.AreEqual(1, spawner.Spawn(), "1턴차");
            Assert.AreEqual(1, spawner.Spawn(), "2턴차");
            Assert.AreEqual(1, spawner.Spawn(), "3턴차");
            Assert.AreEqual(3, spawner.SpawnedTotal);
        }

        [Test]
        public void EveryOtherTurnAfterTheThreshold()
        {
            var config = new FakeBoardConfig { BlocksBeforeSlowdown = 2, TurnsPerSpawnLate = 2 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            PreviewRowSpawner spawner = Build(grid, config, new MinRandom());

            Assert.AreEqual(1, spawner.Spawn());
            Assert.AreEqual(1, spawner.Spawn(), "여기서 누적 2개 — 다음 턴부터 느려진다.");
            Assert.AreEqual(2, spawner.CurrentCadence);

            Assert.AreEqual(0, spawner.Spawn(), "쉬는 턴");
            Assert.IsFalse(spawner.WasDue, "쉬는 턴은 '차례 아님'이지 '막힘'이 아니다.");
            Assert.IsFalse(spawner.LastSpawnBlocked);

            Assert.AreEqual(1, spawner.Spawn(), "2턴 뒤 다시 1개");
        }

        [Test]
        public void CadenceNeverSpeedsBackUp()
        {
            var config = new FakeBoardConfig { BlocksBeforeSlowdown = 1, TurnsPerSpawnLate = 2 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            PreviewRowSpawner spawner = Build(grid, config, new MinRandom());

            spawner.Spawn();

            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(2, spawner.CurrentCadence, "한 번 느려지면 되돌아오지 않는다.");
                spawner.Spawn();
            }
        }

        // ── 컬럼 잠금 & 오버플로 ────────────────────────────────────────────

        [Test]
        public void FullColumn_IsSkipped()
        {
            // 세로 8칸(여기선 2칸)이 찬 열에는 더 이상 쌓지 않는다.
            var config = new FakeBoardConfig { Cols = 2, PlayableRows = 2 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            grid.Place(Make.Trash(TrashType.Paper), new Vector2Int(0, 1));
            grid.Place(Make.Trash(TrashType.Paper), new Vector2Int(0, 2));

            PreviewRowSpawner spawner = Build(grid, config, new MinRandom());

            Assert.AreEqual(1, spawner.Spawn());
            Assert.IsTrue(grid.IsEmpty(new Vector2Int(0, 0)), "꽉 찬 열의 프리뷰 칸은 비워 둔다.");
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(1, 0)), "안 찬 열로 간다.");
        }

        [Test]
        public void PlayAreaFull_UnlocksThePreviewRow()
        {
            // 플로우차트의 예외: 플레이 영역이 전부 차면 그때부터 9번째 칸에 쌓인다.
            var config = new FakeBoardConfig { Cols = 2, PlayableRows = 2 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            for (int col = 0; col < config.Cols; col++)
            {
                for (int row = config.FirstPlayableRow; row < config.Rows; row++)
                {
                    grid.Place(Make.Trash(TrashType.Paper), new Vector2Int(col, row));
                }
            }

            PreviewRowSpawner spawner = Build(grid, config, new MinRandom());

            Assert.AreEqual(1, spawner.Spawn(), "잠금이 풀려 프리뷰 줄에 들어간다.");
            Assert.IsFalse(grid.IsEmpty(new Vector2Int(0, 0)));
            Assert.IsFalse(spawner.LastSpawnBlocked);
        }

        [Test]
        public void NoRoomAnywhere_ReportsBlocked()
        {
            var config = new FakeBoardConfig { Cols = 2, PlayableRows = 2 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            for (int col = 0; col < config.Cols; col++)
            {
                for (int row = 0; row < config.Rows; row++)
                {
                    grid.Place(Make.Trash(TrashType.Paper), new Vector2Int(col, row));
                }
            }

            PreviewRowSpawner spawner = Build(grid, config, new MinRandom());

            Assert.AreEqual(0, spawner.Spawn());
            Assert.IsTrue(spawner.WasDue, "차례이긴 했다.");
            Assert.IsTrue(spawner.LastSpawnBlocked, "차례인데 놓을 칸이 없었다 — §7의 패배 조건.");
        }

        [Test]
        public void RestingTurn_IsNotBlocked()
        {
            // "아직 차례 아님"(0개)과 "차례인데 자리 없음"(0개)을 섞으면 안 된다.
            var config = new FakeBoardConfig { BlocksBeforeSlowdown = 0, TurnsPerSpawnLate = 2 };
            var grid = new BoardGrid(config.Cols, config.Rows);
            PreviewRowSpawner spawner = Build(grid, config, new MinRandom());

            Assert.AreEqual(0, spawner.Spawn());
            Assert.IsFalse(spawner.WasDue);
            Assert.IsFalse(spawner.LastSpawnBlocked);
        }

        // ── 루프와 합쳤을 때 ────────────────────────────────────────────────

        [Test]
        public void SeedRowsDoNotCountTowardTheSlowdownThreshold()
        {
            // 기획 확인 완료: 15는 "게임 시작 후 스폰된 개수"다. 시작 3줄(24개)은 세지 않는다.
            var config = new FakeBoardConfig
            {
                RowsOnStart = 3,
                BlocksBeforeSlowdown = 15,
                TurnsPerSpawnLate = 2,
                PlayerStart = new Vector2Int(4, 5),
            };

            GameLoop loop = Make.Week1(config, new MinRandom());

            // 플레이어는 col 4에서 위아래로만 움직인다. MinRandom은 왼쪽 열부터 채우므로
            // 15턴 안에는 플레이어를 막지 않는다.
            int spawned = 0;
            for (int turn = 0; turn < 15; turn++)
            {
                StepResult step = loop.Step(turn % 2 == 0 ? Direction.Up : Direction.Down);
                Assert.IsTrue(step.Advanced, "턴 " + turn + "이 진행돼야 한다.");
                spawned += step.Spawned;
            }

            Assert.AreEqual(15, spawned, "15턴 동안 매 턴 1개.");

            Assert.AreEqual(0, loop.Step(Direction.Up).Spawned, "16턴차는 쉬는 턴이다.");
            Assert.AreEqual(1, loop.Step(Direction.Down).Spawned, "17턴차에 다시 1개.");
        }
    }
}
