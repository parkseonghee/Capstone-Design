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
        private readonly TrapResolver _traps;
        private readonly MaterialDropResolver _drops;
        private readonly PoisonResolver _poison;

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
            WaveRunner waves = null,
            TrapResolver traps = null,
            MaterialDropResolver drops = null,
            PoisonResolver poison = null)
        {
            _traps = traps;
            _drops = drops;
            _poison = poison;
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
        /// 지금 상점 방에 있는지. 켜져 있으면 <b>중력·스폰·패배 판정이 돌지 않는다</b> —
        /// 상점은 시간이 흐르지 않는 방이라 구경하는 동안 블록이 쌓이면 안 된다.
        /// 폭탄 도화선도 멈춘다(들고 온 폭탄이 상점에서 터질 이유가 없다).
        /// </summary>
        public bool InShop { get; private set; }

        /// <summary>
        /// 보드를 비우고 상점 방으로 만든다. 상품 배치는 호출자(Unity 계층)가 한다 —
        /// 무엇을 파는지는 Core가 알 바가 아니기 때문이다.
        /// </summary>
        public void EnterShop()
        {
            for (int row = 0; row < Grid.Rows; row++)
            {
                for (int col = 0; col < Grid.Cols; col++)
                {
                    var cell = new Vector2Int(col, row);
                    Entity e = Grid[cell];
                    if (e != null && e.Kind != EntityKind.Player)
                    {
                        Grid.Remove(cell);
                    }
                }
            }

            InShop = true;
        }

        /// <summary>상점에서 나온다. 보드는 호출자가 새로 깐다(대개 다음 웨이브를 시작한다).</summary>
        public void ExitShop()
        {
            InShop = false;
        }

        /// <summary>상품을 상점 바닥에 놓는다.</summary>
        /// <returns>놓았으면 true. 칸이 비어 있지 않으면 false.</returns>
        public bool PlaceShopItem(string id, int price, Vector2Int cell)
        {
            if (!Grid.InBounds(cell) || cell.y < _config.FirstPlayableRow || !Grid.IsEmpty(cell))
            {
                return false;
            }

            Grid.Place(new ShopItem(id, price), cell);
            return true;
        }

        /// <summary>
        /// 웨이브 진행 상태. 뷰가 진행도 바를 그릴 때 읽는다.
        /// 웨이브를 안 쓰는 런에서도 null이 아니다(IsIdle이 true인 빈 러너가 들어간다).
        /// </summary>
        public WaveRunner Waves => _waves;

        /// <summary>
        /// 재료 드롭. 몬스터가 죽을 때 Dropped가 울리고, Unity 쪽 인벤토리가 그걸 받는다.
        /// 드롭 표를 안 꽂았으면 null이다(= 재료가 아예 안 나온다).
        /// </summary>
        public MaterialDropResolver MaterialDrops => _drops;

        /// <summary>
        /// 바닥에 깔린 독. 독이 깔린 칸을 뷰가 읽어 그린다.
        /// 독 설정을 안 꽂았으면 null이다(= 독 기믹이 통째로 꺼진다).
        /// </summary>
        public PoisonResolver Poison => _poison;

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
                           || move.Outcome == MoveOutcome.Teleported
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
        /// 맵의 모든 <b>적</b>에게 피해를 준다. 일회성 아이템 C01 정화의 물약.
        ///
        /// 벽과 포션은 건드리지 않는다 — "적에게 데미지"가 CSV 문구다.
        /// <b>턴을 쓰지 않는다.</b> 아이템 사용은 행동이 아니라는 전제이며,
        /// 확정되면 여기서 AdvanceBoard를 부르면 된다.
        /// </summary>
        /// <returns>피해를 입은 적 수.</returns>
        public int DamageAllEnemies(int damage)
        {
            if (damage <= 0 || !IsReady || IsOver)
            {
                return 0;
            }

            int hit = 0;
            for (int row = _config.FirstPlayableRow; row < Grid.Rows; row++)
            {
                for (int col = 0; col < Grid.Cols; col++)
                {
                    var cell = new Vector2Int(col, row);
                    var trash = Grid[cell] as Trash;
                    if (trash == null || !trash.IsEnemy)
                    {
                        continue;
                    }

                    trash.TakeDamage(damage);
                    hit++;

                    if (trash.IsDead)
                    {
                        bool leavesTrap = trash.LeavesTrap;
                        bool leavesPoison = trash.LeavesPoison;
                        Grid.Remove(cell);
                        Player.AddGold(trash.Gold);
                        _waves.Report(1, 0);
                        _traps?.MaybeLeaveTrap(leavesTrap, cell);
                        _poison?.MaybeLeavePoison(leavesPoison, cell);
                        _drops?.MaybeDrop(trash);
                    }
                }
            }

            return hit;
        }

        /// <summary>
        /// 맵의 일반 몬스터를 <b>즉시 처치</b>한다. 일회성 아이템 C02 대청소 물약.
        ///
        /// 누구를 고를지는 기획 미확정이라(CSV 비고: "5마리 선정 기준 미정")
        /// 지금은 <b>플레이어에게 가까운 순</b>으로 잡는다. 기준이 정해지면 이 정렬만 바꾸면 된다.
        /// </summary>
        /// <returns>실제로 처치한 수. 적이 그보다 적으면 그만큼만.</returns>
        public int KillEnemies(int count)
        {
            if (count <= 0 || !IsReady || IsOver)
            {
                return 0;
            }

            int killed = 0;
            for (int n = 0; n < count; n++)
            {
                Vector2Int best = new Vector2Int(-1, -1);
                int bestDistance = int.MaxValue;

                for (int row = _config.FirstPlayableRow; row < Grid.Rows; row++)
                {
                    for (int col = 0; col < Grid.Cols; col++)
                    {
                        var cell = new Vector2Int(col, row);
                        var trash = Grid[cell] as Trash;
                        if (trash == null || !trash.IsEnemy)
                        {
                            continue;
                        }

                        int distance = Mathf.Abs(cell.x - Player.Position.x)
                                       + Mathf.Abs(cell.y - Player.Position.y);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = cell;
                        }
                    }
                }

                if (best.x < 0)
                {
                    break;      // 남은 적이 없다
                }

                var target = (Trash)Grid[best];
                bool leavesTrap = target.LeavesTrap;
                bool leavesPoison = target.LeavesPoison;
                Grid.Remove(best);
                Player.AddGold(target.Gold);
                _waves.Report(1, 0);
                _traps?.MaybeLeaveTrap(leavesTrap, best);
                _poison?.MaybeLeavePoison(leavesPoison, best);
                _drops?.MaybeDrop(target);
                killed++;
            }

            return killed;
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
            // ── 페이즈 0: 독(플레이어) ──────────────────────────
            // 칸을 정말로 옮긴 행동만 독을 밟는다. 공격·대기·폭탄 설치는 제자리에서 끝나므로
            // 밟을 새 자체가 없다. 이동이 끝난 뒤에 보는 것도 그 때문이다 — 들어선 칸을 봐야 한다.
            if (_poison != null
                && (move.Outcome == MoveOutcome.Moved || move.Outcome == MoveOutcome.Teleported))
            {
                _poison.OnMoved(Player);
            }

            // 죽었어도 부활 유물(R04)이 남아 있으면 한 번 일어난다.
            Player.TryRevive();

            // 반격으로 죽었다면 여기서 끝난다. 이미 진 판에 블록을 더 떨어뜨릴 이유가 없다.
            if (Player.IsDead)
            {
                Reason = _gameOver.Evaluate();
                StepCount++;
                return new StepResult(move, true, 0, 0, false, Reason);
            }

            // 상점 방에서는 시간이 멈춘다. 걸어 다니고 사는 것만 된다.
            if (InShop)
            {
                StepCount++;
                return new StepResult(move, true, 0, 0, false, Reason);
            }

            // ── 페이즈 2: 폭탄 도화선 ────────────────────────────────────────
            // 중력보다 먼저 도는 이유: 터져서 생긴 빈 칸을 같은 턴의 중력이 메우게 하려는 것이다.
            // 공격으로 처치했을 때와 순서를 맞췄다.
            BlastResult blast = _bombs.Tick();

            Player.TryRevive();

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

            // ── 페이즈 3.5: 독 ──────────────────────────────────
            // 떨어지면서 독을 밟아 죽은 블록을 걷어내고, 장판 수명과 중독 시간을 한 턴씩 깎는다.
            // 스폰보다 먼저 도는 이유는 중력과 같다 — 죽어서 생긴 빈 칸이 이번 턴에 전부 정리돼야 한다.
            int poisonedEnemies = 0;
            int poisonedWalls = 0;
            if (_poison != null)
            {
                SweepPoisonDeaths(ref poisonedEnemies, ref poisonedWalls);
                _poison.Tick();
            }

            // ── 페이즈 4: 스폰 ───────────────────────────────────────────────
            // 중력 뒤에 도는 이유: 프리뷰 칸을 먼저 비워야 이번 턴 블록이 들어갈 자리가 생긴다.
            int spawned = _stepSpawner.Spawn();
            bool spawnBlocked = _stepSpawner.LastSpawnBlocked;

            // ── 페이즈 5: 웨이브 진행도 ──────────────────────────────────────
            // 스폰 뒤에 세는 이유: 목표를 채워 웨이브가 넘어가면 그 다음 스폰부터
            // 새 조합이 내려와야 하는데, 이번 턴 스폰은 아직 이전 웨이브의 것이 맞기 때문이다.
            _waves.Report(move.EnemiesKilled + blast.EnemiesKilled + poisonedEnemies,
                          move.WallsDestroyed + blast.WallsDestroyed + poisonedWalls);

            // ── 페이즈 6: 패배 판정 ──────────────────────────────────────────
            Reason = _gameOver.Evaluate(spawnBlocked);

            StepCount++;
            return new StepResult(move, blast, true, settled, spawned, spawnBlocked, Reason);
        }

        /// <summary>
        /// 독으로 죽은 블록을 걷어낸다. 중력 페이즈가 도는 도중에 죽은 것들이라
        /// 그 자리에서 바로 치우지 않고 여기서 한꺼번에 정리한다 — 처치 보상(골드·재료·덱·독)을
        /// 먹이는 처리가 그렇게 해야 한 군데에 모인다.
        ///
        /// 여기서 살아서 죽은 블록은 다른 경로가 없다 — 공격·폭발·아이템은 죽이자마자 걷어낸다.
        /// </summary>
        private void SweepPoisonDeaths(ref int enemiesKilled, ref int wallsDestroyed)
        {
            for (int row = 0; row < Grid.Rows; row++)
            {
                for (int col = 0; col < Grid.Cols; col++)
                {
                    var cell = new Vector2Int(col, row);
                    var trash = Grid[cell] as Trash;
                    if (trash == null || !trash.IsDead)
                    {
                        continue;
                    }

                    bool leavesTrap = trash.LeavesTrap;
                    bool leavesPoison = trash.LeavesPoison;
                    Grid.Remove(cell);
                    Player.AddGold(trash.Gold);

                    if (trash.IsEnemy)
                    {
                        enemiesKilled++;
                    }
                    else
                    {
                        wallsDestroyed++;
                    }

                    _traps?.MaybeLeaveTrap(leavesTrap, cell);
                    _poison?.MaybeLeavePoison(leavesPoison, cell);
                    _drops?.MaybeDrop(trash);
                }
            }
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
