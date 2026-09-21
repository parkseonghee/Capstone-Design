using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// WEEK1_MOVEMENT_FALLING.md §2의 4페이즈를 조립하는 유일한 지점.
    /// MonoBehaviour도 코루틴도 아니며 시간 개념이 없다 — 입력 1회 = Step 1회.
    ///
    /// 런 준비는 두 단계로 나뉜다:
    ///  1. SeedNextRow()를 RowsOnStart번 — 초기 줄을 <b>한 줄씩</b> 떨어뜨린다.
    ///  2. PlacePlayer() — 줄이 다 깔린 뒤에 플레이어를 놓는다.
    ///
    /// 한 줄씩 나눠 놓은 이유는 연출 때문이다. 이 계층은 여전히 시간을 모르고,
    /// "몇 초 간격으로 부를지"는 Unity 계층(GameSession)이 정한다.
    /// 한 번에 끝내고 싶으면 CompleteSetup()을 부르면 된다.
    ///
    /// 전투가 붙을 때 바뀌는 것은 생성자에 들어오는 IMoveResolver 구현뿐이고
    /// 이 클래스는 그대로다(CORE_COMBAT.md §6).
    /// </summary>
    public sealed class GameLoop
    {
        private readonly IBoardConfig _config;
        private readonly IMoveResolver _move;
        private readonly GravityResolver _gravity;
        private readonly ITrashSpawner _stepSpawner;
        private readonly ISeedSpawner _seedSpawner;
        private readonly BombResolver _bombs;
        private readonly GameOverChecker _gameOver;
        private readonly WaveRunner _waves;

        public GameLoop(
            BoardGrid grid,
            Player player,
            IBoardConfig config,
            IMoveResolver move,
            GravityResolver gravity,
            ITrashSpawner stepSpawner,
            ISeedSpawner seedSpawner,
            BombResolver bombs,
            GameOverChecker gameOver,
            WaveRunner waves = null)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _move = move ?? throw new ArgumentNullException(nameof(move));
            _gravity = gravity ?? throw new ArgumentNullException(nameof(gravity));
            _stepSpawner = stepSpawner ?? throw new ArgumentNullException(nameof(stepSpawner));
            _seedSpawner = seedSpawner ?? throw new ArgumentNullException(nameof(seedSpawner));
            _bombs = bombs ?? throw new ArgumentNullException(nameof(bombs));
            _gameOver = gameOver ?? throw new ArgumentNullException(nameof(gameOver));

            // 웨이브는 선택이다. 안 넘기면 웨이브 없이 무한히 도는 기존 동작 그대로다.
            _waves = waves ?? new WaveRunner(Array.Empty<IWaveConfig>());

            PendingSeedRows = Mathf.Max(0, config.RowsOnStart);
        }

        public BoardGrid Grid { get; }

        public Player Player { get; }

        /// <summary>이번 런에서 보드가 실제로 진행한 횟수(거부된 입력은 세지 않는다).</summary>
        public int StepCount { get; private set; }

        /// <summary>
        /// 웨이브 진행 상태. 뷰가 진행도 바를 그릴 때 읽는다.
        /// 웨이브를 안 쓰는 런에서도 null이 아니다(IsIdle이 true인 빈 러너가 들어간다).
        /// </summary>
        public WaveRunner Waves => _waves;

        /// <summary>
        /// 프리뷰 줄 바로 아래, 플레이 가능한 첫 행. 뷰가 프리뷰 줄을 다르게 그릴 때 읽는다 —
        /// 뷰가 설정 에셋을 따로 참조하지 않게 하려고 여기서 흘려준다.
        /// </summary>
        public int FirstPlayableRow => _config.FirstPlayableRow;

        public GameOverReason Reason { get; private set; }

        public bool IsOver => Reason != GameOverReason.None;

        /// <summary>아직 떨어뜨려야 할 초기 줄 수.</summary>
        public int PendingSeedRows { get; private set; }

        public bool IsPlayerPlaced { get; private set; }

        /// <summary>초기 줄이 다 깔리고 플레이어까지 놓였는지. false면 Step은 거부된다.</summary>
        public bool IsReady => PendingSeedRows == 0 && IsPlayerPlaced;

        /// <summary>
        /// 초기 줄 하나를 상단에 놓는다. 낙하는 시키지 않는다 —
        /// 호출자가 TickGravity()를 반복해 한 칸씩 내려오는 모습을 만든다.
        /// </summary>
        /// <returns>이번 줄에 놓인 쓰레기 수. 남은 줄이 없으면 0.</returns>
        public int SeedNextRow()
        {
            if (PendingSeedRows <= 0)
            {
                return 0;
            }

            int placed = _seedSpawner.SpawnRow();
            PendingSeedRows--;

            return placed;
        }

        /// <summary>
        /// 떠 있는 쓰레기를 한 칸씩 내린다. 시작 연출이 낙하를 눈에 보이게 만들 때 쓴다.
        /// </summary>
        /// <returns>이번에 움직인 쓰레기 수. 0이면 전부 정착했다는 뜻이다.</returns>
        public int TickGravity() => _gravity.Step();

        /// <summary>
        /// 플레이어를 보드에 올린다. 초기 줄을 다 깐 뒤에 불러야 한다 —
        /// 먼저 놓으면 §7-1의 벽 규칙 때문에 플레이어 컬럼만 바닥까지 비고 머리 위로 탑이 쌓인다.
        /// </summary>
        public Vector2Int PlacePlayer()
        {
            if (IsPlayerPlaced)
            {
                return Player.Position;
            }

            Vector2Int at = ResolveStart(Grid, _config.PlayerStart, _config.FirstPlayableRow);
            Grid.Place(Player, at);
            IsPlayerPlaced = true;

            return at;
        }

        /// <summary>남은 초기 줄을 전부 떨어뜨리고 플레이어까지 놓아 런을 즉시 시작 가능 상태로 만든다.</summary>
        public void CompleteSetup()
        {
            while (PendingSeedRows > 0)
            {
                SeedNextRow();
                _gravity.Settle();   // 연출을 건너뛰므로 여기서는 끝까지 내린다
            }

            PlacePlayer();
            EvaluateInitialState();
        }

        /// <summary>
        /// 시작 보드가 이미 패배 상태인지 확인한다.
        /// 페이즈 4는 스텝을 한 번 돌아야 실행되므로 초기 상태는 여기서 따로 본다.
        /// </summary>
        public GameOverReason EvaluateInitialState()
        {
            Reason = _gameOver.Evaluate();
            return Reason;
        }

        public StepResult Step(Direction direction)
        {
            // 준비가 끝나기 전(시작 연출 중)이거나 이미 끝난 런이면 아무 일도 하지 않는다.
            if (!IsReady || IsOver)
            {
                return new StepResult(MoveResult.Simple(MoveOutcome.BlockedByEntity), false, 0, 0, false, Reason);
            }

            // ── 페이즈 1: 이동 또는 공격 ──────────────────────────────────────
            MoveResult move = _move.Resolve(direction);

            // §3: 무효 입력(경계 밖 · 프리뷰 줄)은 언제나 무시한다.
            // 공격은 이동과 똑같이 한 턴을 쓴다(CORE_COMBAT.md §3).
            // 쓰레기에 막힌 경우만 AdvanceOnBlocked 설정을 따른다.
            bool advance = move.Outcome == MoveOutcome.Moved
                           || move.Outcome == MoveOutcome.Attacked
                           || move.Outcome == MoveOutcome.Consumed
                           || move.Outcome == MoveOutcome.Armed
                           || (move.Outcome == MoveOutcome.BlockedByEntity && _config.AdvanceOnBlocked);

            if (!advance)
            {
                // 보드가 진행하지 않으므로 페이즈 4에 도달하지 못한다.
                // 갇힘 판정만 여기서 따로 봐서 소프트락을 막는다(GameOverChecker 주석 참조).
                if (_gameOver.IsPlayerTrapped())
                {
                    Reason = GameOverReason.PlayerTrapped;
                }

                return new StepResult(move, false, 0, 0, false, Reason);
            }

            return AdvanceBoard(move);
        }

        /// <summary>
        /// 폭탄을 설치한다. 방향 입력이 아닌 <b>설치 버튼</b>으로 들어오는 행동이며,
        /// 성공하면 다른 행동과 똑같이 한 턴을 쓴다(폭탄 기획 §1-3, 임시 확정).
        ///
        /// 놓기만 할 뿐 <b>불은 붙지 않는다</b>. 카운트다운은 플레이어가 때려야 시작된다.
        /// </summary>
        public StepResult PlaceBomb(Vector2Int cell)
        {
            if (!IsReady || IsOver)
            {
                return new StepResult(MoveResult.Simple(MoveOutcome.BlockedByEntity), false, 0, 0, false, Reason);
            }

            if (!_bombs.Place(cell))
            {
                // 보유가 없거나 놓을 수 없는 칸 — 무효 입력이라 보드를 진행시키지 않는다.
                return new StepResult(MoveResult.Simple(MoveOutcome.OutOfBounds), false, 0, 0, false, Reason);
            }

            return AdvanceBoard(MoveResult.Simple(MoveOutcome.BombPlaced));
        }

        /// <summary>
        /// 제자리에서 한 턴을 넘긴다. 플레이어는 움직이지 않지만 보드는 평소대로 진행한다 —
        /// 중력이 한 칸 내려가고, 스폰이 돌고, 폭탄 도화선이 탄다.
        ///
        /// 이동과 똑같이 <b>한 턴을 소비하는 정식 행동</b>이다. 공짜로 중력만 돌리면
        /// 스폰 압박 없이 판을 정리할 수 있게 돼 밸런스가 무너지기 때문이다.
        /// </summary>
        public StepResult Wait()
        {
            if (!IsReady || IsOver)
            {
                return new StepResult(
                    MoveResult.Simple(MoveOutcome.OutOfBounds), false, 0, 0, false, Reason);
            }

            return AdvanceBoard(MoveResult.Simple(MoveOutcome.Waited));
        }

        /// <summary>
        /// 플레이어 한 칸 거리의 설치 가능 칸을 다시 계산한다.
        /// 뷰가 "설치 버튼을 꾹 눌렀을 때 표시할 자리"를 이걸로 가져간다.
        /// </summary>
        /// <returns>후보 칸 수.</returns>
        public int CollectBombPlacements() => _bombs.CollectPlacements();

        /// <summary>직전 <see cref="CollectBombPlacements"/>의 결과. 다음 호출 전까지만 유효하다.</summary>
        public IReadOnlyList<Vector2Int> BombPlacements => _bombs.Placements;

        public bool CanPlaceBombAt(Vector2Int cell) => _bombs.CanPlace(cell);

        /// <summary>
        /// 행동이 끝난 뒤의 공통 페이즈. 이동·공격·획득·폭탄 설치가 전부 여기로 모인다.
        /// </summary>
        private StepResult AdvanceBoard(MoveResult move)
        {
            // 반격으로 죽었다면 여기서 끝난다. 이미 진 판에 블록을 더 떨어뜨릴 이유가 없다.
            if (Player.IsDead)
            {
                Reason = _gameOver.Evaluate();
                StepCount++;
                return new StepResult(move, true, 0, 0, false, Reason);
            }

            // ── 페이즈 2: 폭탄 도화선 ────────────────────────────────────────
            // 중력보다 먼저 도는 이유: 터져서 생긴 빈 칸을 같은 턴의 중력이 메우게 하려는 것이다.
            // 공격으로 처치했을 때와 순서를 맞췄다.
            BlastResult blast = _bombs.Tick();

            if (Player.IsDead)
            {
                Reason = _gameOver.Evaluate();
                StepCount++;
                return new StepResult(move, blast, true, 0, 0, false, Reason);
            }

            // ── 페이즈 3: 중력 ───────────────────────────────────────────────
            // 스텝당 한 칸씩만 내린다(기획 확정). 프리뷰 줄에 대기하던 블록도
            // 여기서 한 칸 내려와 플레이 영역으로 들어간다.
            int settled = _gravity.Step();

            // ── 페이즈 4: 스폰 ───────────────────────────────────────────────
            // 중력 뒤에 도는 이유: 프리뷰 칸을 먼저 비워야 이번 턴 블록이 들어갈 자리가 생긴다.
            int spawned = _stepSpawner.Spawn();
            bool spawnBlocked = _stepSpawner.LastSpawnBlocked;

            // ── 페이즈 5: 웨이브 진행도 ──────────────────────────────────────
            // 스폰 뒤에 세는 이유: 목표를 채워 웨이브가 넘어가면 그 다음 스폰부터
            // 새 조합이 내려와야 하는데, 이번 턴 스폰은 아직 이전 웨이브의 것이 맞기 때문이다.
            _waves.Report(move.EnemiesKilled + blast.EnemiesKilled,
                          move.WallsDestroyed + blast.WallsDestroyed);

            // ── 페이즈 6: 패배 판정 ──────────────────────────────────────────
            Reason = _gameOver.Evaluate(spawnBlocked);

            StepCount++;
            return new StepResult(move, blast, true, settled, spawned, spawnBlocked, Reason);
        }

        /// <summary>
        /// 설정된 시작 칸이 이미 차 있으면(초기 줄이 높게 쌓인 경우) 같은 컬럼에서 위로 올라가며
        /// 첫 빈 칸을 찾는다. 그것도 없으면 보드 전체에서 아무 빈 칸이나 쓴다.
        /// 프리뷰 줄은 어느 경우에도 후보가 아니다 — 플레이어가 들어갈 수 없는 영역이다(§3).
        /// </summary>
        private static Vector2Int ResolveStart(BoardGrid grid, Vector2Int preferred, int firstPlayableRow)
        {
            if (preferred.y >= firstPlayableRow && grid.IsEmpty(preferred))
            {
                return preferred;
            }

            for (int row = preferred.y - 1; row >= firstPlayableRow; row--)
            {
                var candidate = new Vector2Int(preferred.x, row);
                if (grid.IsEmpty(candidate))
                {
                    return candidate;
                }
            }

            for (int row = firstPlayableRow; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Cols; col++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (grid.IsEmpty(candidate))
                    {
                        return candidate;
                    }
                }
            }

            throw new InvalidOperationException("보드에 플레이어를 놓을 빈 칸이 없습니다. RowsOnStart를 줄이세요.");
        }
    }
}
