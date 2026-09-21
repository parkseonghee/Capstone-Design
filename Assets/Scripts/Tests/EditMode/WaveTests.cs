using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 웨이브 진행 규칙. 밸런싱 v1 §2-1-4 확정:
    /// <b>진행도는 적 처치 수로만 찬다. 다 차면 다음 웨이브로 넘어간다.</b>
    /// </summary>
    public sealed class WaveTests
    {
        /// <summary>목표 수와 등장 종류만 들고 있는 최소 구현.</summary>
        private sealed class FakeWave : IWaveConfig
        {
            private readonly Dictionary<TrashType, int> _weights = new Dictionary<TrashType, int>();

            public FakeWave(int stage, int wave, int killGoal)
            {
                StageNumber = stage;
                WaveNumber = wave;
                KillGoal = killGoal;
            }

            public int StageNumber { get; }

            public int WaveNumber { get; }

            public int KillGoal { get; }

            public FakeWave With(TrashType type, int weight)
            {
                _weights[type] = weight;
                return this;
            }

            public int SpawnWeightFor(TrashType type)
                => _weights.TryGetValue(type, out int w) ? w : 0;
        }

        private static WaveRunner Runner(bool countWalls, params IWaveConfig[] waves)
            => new WaveRunner(waves, countWalls);

        // ── 진행도 ───────────────────────────────────────────────────────

        [Test]
        public void EnemyKills_FillTheBar()
        {
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 3));

            runner.Report(1, 0);
            Assert.AreEqual(1, runner.Progress);
            Assert.AreEqual(3, runner.Goal);
            Assert.AreEqual(1f / 3f, runner.Fill, 0.0001f);
        }

        [Test]
        public void MovingWithoutKilling_DoesNotFillTheBar()
        {
            // 확정 사항의 핵심: 도망만 다니는 플레이로는 클리어할 수 없어야 한다.
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 3));

            for (int i = 0; i < 20; i++)
            {
                runner.Report(0, 0);
            }

            Assert.AreEqual(0, runner.Progress);
            Assert.AreEqual(0f, runner.Fill);
        }

        [Test]
        public void WallsDoNotCount_ByDefault()
        {
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 3));

            runner.Report(0, 5);

            Assert.AreEqual(0, runner.Progress);
        }

        [Test]
        public void WallsCount_WhenTheOptionIsOn()
        {
            WaveRunner runner = Runner(true, new FakeWave(1, 1, 10));

            runner.Report(1, 2);

            Assert.AreEqual(3, runner.Progress);
        }

        // ── 웨이브 전환 ──────────────────────────────────────────────────

        [Test]
        public void ReachingTheGoal_AdvancesToTheNextWave()
        {
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 2), new FakeWave(1, 2, 2));

            Assert.IsFalse(runner.Report(1, 0), "목표에 못 미쳤는데 넘어갔다");
            Assert.IsTrue(runner.Report(1, 0), "목표를 채웠는데 안 넘어갔다");

            Assert.AreEqual(2, runner.Current.WaveNumber);
            Assert.AreEqual(0, runner.Progress, "새 웨이브는 0부터 시작해야 한다");
        }

        [Test]
        public void OverkillDoesNotSkipAWave()
        {
            // 연쇄로 목표를 훌쩍 넘겨도 한 웨이브만 넘어간다 —
            // 두 칸을 건너뛰면 등장 조합이 통째로 생략된다(§2-1 "새 적은 웨이브마다 1종씩").
            WaveRunner runner = Runner(false,
                new FakeWave(1, 1, 2), new FakeWave(1, 2, 2), new FakeWave(1, 3, 2));

            runner.Report(99, 0);

            Assert.AreEqual(2, runner.Current.WaveNumber);
            Assert.AreEqual(0, runner.Progress, "넘친 분은 다음 웨이브로 이월되지 않는다");
        }

        [Test]
        public void StageChange_IsReported()
        {
            WaveRunner runner = Runner(false, new FakeWave(1, 3, 1), new FakeWave(2, 4, 1));

            runner.Report(1, 0);

            Assert.IsTrue(runner.JustAdvanced);
            Assert.IsTrue(runner.JustChangedStage, "스테이지가 1에서 2로 바뀌었다");
        }

        [Test]
        public void StageStaysTheSame_WithinAStage()
        {
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 1), new FakeWave(1, 2, 1));

            runner.Report(1, 0);

            Assert.IsTrue(runner.JustAdvanced);
            Assert.IsFalse(runner.JustChangedStage);
        }

        [Test]
        public void ClearingTheLastWave_EndsTheRun()
        {
            WaveRunner runner = Runner(false, new FakeWave(3, 9, 1));

            runner.Report(1, 0);

            Assert.IsTrue(runner.IsRunCleared);
            Assert.IsNull(runner.Current);
            Assert.AreEqual(1f, runner.Fill);
        }

        [Test]
        public void ReportAfterTheRunIsCleared_IsIgnored()
        {
            WaveRunner runner = Runner(false, new FakeWave(3, 9, 1));
            runner.Report(1, 0);

            Assert.IsFalse(runner.Report(5, 5), "다 끝난 런에서 또 넘어가면 안 된다");
            Assert.IsTrue(runner.IsRunCleared);
        }

        [Test]
        public void AZeroGoalWaveIsSkipped_RatherThanStalling()
        {
            // 설정 실수로 목표가 0이어도 판이 멈추면 안 된다.
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 0), new FakeWave(1, 2, 5));

            runner.Report(1, 0);

            Assert.AreEqual(2, runner.Current.WaveNumber);
        }

        [Test]
        public void AnEmptyWaveList_JustIdles()
        {
            WaveRunner runner = Runner(false);

            Assert.IsTrue(runner.IsIdle);
            Assert.IsNull(runner.Current);
            Assert.IsFalse(runner.Report(10, 10));
            Assert.IsFalse(runner.IsRunCleared, "웨이브가 없는 런은 '클리어'가 아니다");
        }

        [Test]
        public void Reset_GoesBackToTheFirstWave()
        {
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 1), new FakeWave(1, 2, 1));
            runner.Report(1, 0);

            runner.Reset();

            Assert.AreEqual(1, runner.Current.WaveNumber);
            Assert.AreEqual(0, runner.Progress);
        }

        // ── 스테이지 선택 방식 (마을 지도) ───────────────────────────────

        private static WaveRunner Selected(int startIndex, params IWaveConfig[] waves)
            => new WaveRunner(waves, false, startIndex, stopAfterEachWave: true);

        [Test]
        public void StartIndex_PicksTheChosenStage()
        {
            WaveRunner runner = Selected(2,
                new FakeWave(1, 1, 5), new FakeWave(1, 2, 5), new FakeWave(1, 3, 5));

            Assert.AreEqual(3, runner.Current.WaveNumber, "1-3을 골랐으면 거기서 시작해야 한다");
        }

        [Test]
        public void StartIndexOutOfRange_ClampsInsteadOfThrowing()
        {
            WaveRunner runner = Selected(99, new FakeWave(1, 1, 5), new FakeWave(1, 2, 5));

            Assert.AreEqual(2, runner.Current.WaveNumber);
        }

        [Test]
        public void ClearingAStage_StopsInsteadOfRunningOn()
        {
            // 핵심: 1-1을 깨면 거기서 멈춰야 클리어 화면을 띄우고 물어볼 수 있다.
            WaveRunner runner = Selected(0, new FakeWave(1, 1, 2), new FakeWave(1, 2, 2));

            runner.Report(2, 0);

            Assert.IsTrue(runner.IsWaveCleared);
            Assert.AreEqual(1, runner.Current.WaveNumber, "제멋대로 다음 웨이브로 넘어가면 안 된다");
            Assert.AreEqual(1f, runner.Fill, "바는 가득 찬 채로 보여야 한다");
        }

        [Test]
        public void KillsAfterClearing_AreIgnored()
        {
            // 클리어 화면이 떠 있는 동안 들어오는 마지막 연쇄가 진행도를 넘치게 하면 안 된다.
            WaveRunner runner = Selected(0, new FakeWave(1, 1, 2), new FakeWave(1, 2, 2));
            runner.Report(2, 0);

            Assert.IsFalse(runner.Report(5, 0));
            Assert.AreEqual(2, runner.Progress);
            Assert.AreEqual(1, runner.Current.WaveNumber);
        }

        [Test]
        public void AdvanceToNext_MovesToTheFollowingStage()
        {
            WaveRunner runner = Selected(0, new FakeWave(1, 1, 2), new FakeWave(1, 2, 2));
            runner.Report(2, 0);

            Assert.IsTrue(runner.AdvanceToNext());

            Assert.AreEqual(2, runner.Current.WaveNumber);
            Assert.AreEqual(0, runner.Progress);
            Assert.IsFalse(runner.IsWaveCleared, "새 스테이지는 클리어 상태가 아니어야 한다");
        }

        [Test]
        public void AdvanceToNext_OnTheLastStage_DoesNothing()
        {
            WaveRunner runner = Selected(1, new FakeWave(1, 1, 2), new FakeWave(1, 2, 2));
            runner.Report(2, 0);

            Assert.IsFalse(runner.HasNext);
            Assert.IsFalse(runner.AdvanceToNext(), "마지막 스테이지 뒤로는 못 간다");
            Assert.AreEqual(2, runner.Current.WaveNumber);
        }

        [Test]
        public void ResetGoesBackToTheSelectedStage_NotTheFirst()
        {
            // 1-3을 골라 들어왔다가 죽어서 다시 시작하면 1-1이 아니라 1-3이어야 한다.
            WaveRunner runner = Selected(2,
                new FakeWave(1, 1, 5), new FakeWave(1, 2, 5), new FakeWave(1, 3, 5));
            runner.Report(5, 0);

            runner.Reset();

            Assert.AreEqual(3, runner.Current.WaveNumber);
            Assert.IsFalse(runner.IsWaveCleared);
        }

        // ── 스폰 가중치 교체 ─────────────────────────────────────────────

        [Test]
        public void TheCurrentWaveOverridesGlobalSpawnWeights()
        {
            var global = new FakeTrashStats(maxHp: 2, attack: 1, spawnWeight: 10);
            WaveRunner runner = Runner(false,
                new FakeWave(1, 1, 5).With(TrashType.Paper, 7));

            var weights = new WaveSpawnWeights(global, runner);

            Assert.AreEqual(7, weights.For(TrashType.Paper).SpawnWeight, "웨이브 값이 이겨야 한다");
            Assert.AreEqual(0, weights.For(TrashType.Plastic).SpawnWeight,
                "웨이브 목록에 없는 종류는 이 웨이브에 안 나온다");
        }

        [Test]
        public void SwappingWeightsDoesNotTouchCombatStats()
        {
            var global = new FakeTrashStats(maxHp: 4, attack: 2, spawnWeight: 10);
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 5).With(TrashType.Paper, 1));

            TrashStats stats = new WaveSpawnWeights(global, runner).For(TrashType.Paper);

            Assert.AreEqual(4, stats.MaxHp);
            Assert.AreEqual(2, stats.Attack);
        }

        [Test]
        public void WhenTheRunIsCleared_GlobalWeightsComeBack()
        {
            var global = new FakeTrashStats(maxHp: 1, attack: 1, spawnWeight: 9);
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 1).With(TrashType.Paper, 1));
            runner.Report(1, 0);

            // 웨이브가 다 끝나면 덮어쓸 값이 없다. 판이 멈추지 않게 전역 표로 돌아간다.
            Assert.AreEqual(9, new WaveSpawnWeights(global, runner).For(TrashType.Paper).SpawnWeight);
        }

        // ── GameLoop 연동 ────────────────────────────────────────────────

        [Test]
        public void KillingAnEnemyThroughTheLoop_FillsTheBar()
        {
            var config = new FakeBoardConfig
            {
                RowsOnStart = 0,
                BlocksPerSpawn = 0,
                // 기본 시작칸은 맨 아랫줄이라 그 아래에 적을 둘 수 없다.
                PlayerStart = new Vector2Int(4, 5),
            };
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 2));

            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom(), runner);

            // 플레이어 바로 아래에 체력 1짜리 적을 놓고 내려친다.
            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Make.Trash(TrashType.Paper, maxHp: 1, attack: 1), below);

            loop.Step(Direction.Down);

            Assert.AreEqual(1, runner.Progress, "적을 잡았으면 진행도가 올라야 한다");
        }

        [Test]
        public void BreakingAWallThroughTheLoop_DoesNotFillTheBar()
        {
            var config = new FakeBoardConfig
            {
                RowsOnStart = 0,
                BlocksPerSpawn = 0,
                // 기본 시작칸은 맨 아랫줄이라 그 아래에 적을 둘 수 없다.
                PlayerStart = new Vector2Int(4, 5),
            };
            WaveRunner runner = Runner(false, new FakeWave(1, 1, 2));

            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom(), runner);

            // 공격력 0 = 벽. 체력 1이라 한 대에 부서진다.
            var below = new Vector2Int(loop.Player.Position.x, loop.Player.Position.y + 1);
            loop.Grid.Place(Make.Trash(TrashType.Wood, maxHp: 1, attack: 0), below);

            StepResult result = loop.Step(Direction.Down);

            Assert.AreEqual(1, result.Killed, "벽은 부서졌어야 한다");
            Assert.AreEqual(0, result.EnemiesKilled, "벽은 적으로 세면 안 된다");
            Assert.AreEqual(1, result.WallsDestroyed);
            Assert.AreEqual(0, runner.Progress, "벽은 기본 설정에서 진행도에 안 들어간다");
        }

        [Test]
        public void ALoopWithoutWaves_StillWorks()
        {
            var config = new FakeBoardConfig
            {
                RowsOnStart = 0,
                BlocksPerSpawn = 0,
                // 기본 시작칸은 맨 아랫줄이라 그 아래에 적을 둘 수 없다.
                PlayerStart = new Vector2Int(4, 5),
            };

            GameLoop loop = GameLoopFactory.CreateWeek1(
                config, config, config, config, config, new MinRandom());

            Assert.IsNotNull(loop.Waves, "웨이브를 안 넘겨도 null이면 뷰가 터진다");
            Assert.IsTrue(loop.Waves.IsIdle);
        }
    }
}
