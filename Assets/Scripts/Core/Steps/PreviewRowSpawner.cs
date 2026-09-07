using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// WEEK1 §5 확정 스폰 규칙. 새 쓰레기는 언제나 <b>프리뷰 줄(row 0)</b>로만 들어오고,
    /// 다음 스텝부터 중력이 한 칸씩 플레이 영역으로 내려보낸다.
    ///
    /// 두 가지 규칙이 겹쳐 있다.
    ///
    /// 1) 캐이던스 (ISpawnConfig)
    ///    누적 15개 전 = 매 턴 1개, 15개부터 = 2턴당 1개(이후 영구).
    ///    누적 개수는 이 스포너가 낸 것만 센다 — 시작 3줄은 seedSpawner가 따로 깔므로
    ///    자연히 제외된다(기획 확인 완료).
    ///
    /// 2) 컬럼 잠금 (기획 플로우차트)
    ///    어떤 열의 플레이 8칸이 전부 차면 그 열에는 더 이상 스폰하지 않는다.
    ///    <b>예외</b>: 플레이 영역 64칸이 전부 차면 그때부터 프리뷰 줄에 쌓기 시작한다.
    ///    프리뷰까지 꽉 찬 뒤 다음 스폰 차례가 오면 LastSpawnBlocked가 서고,
    ///    GameOverChecker가 그걸 보고 패배를 선언한다(§7).
    /// </summary>
    public sealed class PreviewRowSpawner : ITrashSpawner
    {
        /// <summary>Enum.GetValues는 배열을 할당하므로 최초 1회만 세어 캐시한다(Hard Rule 8).</summary>
        private static readonly int TrashTypeCount = Enum.GetValues(typeof(TrashType)).Length;

        private readonly BoardGrid _grid;
        private readonly IBoardConfig _board;
        private readonly ISpawnConfig _spawn;
        private readonly IRandomSource _random;

        /// <summary>매 스텝 재사용하는 후보 컬럼 버퍼(Hard Rule 8).</summary>
        private readonly List<int> _candidates;

        private int _turnsSinceSpawn;

        public PreviewRowSpawner(BoardGrid grid, IBoardConfig board, ISpawnConfig spawn, IRandomSource random)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _spawn = spawn ?? throw new ArgumentNullException(nameof(spawn));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _candidates = new List<int>(grid.Cols);
        }

        /// <summary>이 스포너가 지금까지 투입한 쓰레기 총 개수. 캐이던스 전환의 기준이다.</summary>
        public int SpawnedTotal { get; private set; }

        /// <summary>이번 턴이 스폰 차례였는지. 차례가 아니면 Spawn()이 0을 돌려줘도 정상이다.</summary>
        public bool WasDue { get; private set; }

        public bool LastSpawnBlocked { get; private set; }

        /// <summary>현재 적용 중인 주기(턴). 디버그 HUD가 읽는다.</summary>
        public int CurrentCadence =>
            SpawnedTotal < _spawn.BlocksBeforeSlowdown
                ? Mathf.Max(1, _spawn.TurnsPerSpawnEarly)
                : Mathf.Max(1, _spawn.TurnsPerSpawnLate);

        public int Spawn()
        {
            WasDue = false;
            LastSpawnBlocked = false;

            _turnsSinceSpawn++;
            if (_turnsSinceSpawn < CurrentCadence)
            {
                return 0;
            }

            _turnsSinceSpawn = 0;
            WasDue = true;

            int wanted = Mathf.Max(0, _spawn.BlocksPerSpawn);
            int placed = 0;

            for (int i = 0; i < wanted; i++)
            {
                CollectCandidates();

                if (_candidates.Count == 0)
                {
                    // 놓을 칸이 없다 — §7의 게임오버 경로. 판정은 GameOverChecker가 한다.
                    LastSpawnBlocked = true;
                    break;
                }

                int column = _candidates[_random.NextInt(0, _candidates.Count)];
                var type = (TrashType)_random.NextInt(0, TrashTypeCount);

                // 스텝당 최대 BlocksPerSpawn번의 불가피한 할당.
                // 전투 단계에서 제거가 생기면 그때 풀링을 검토한다.
                _grid.Place(new Trash(type), new Vector2Int(column, SpawnRow));
                placed++;
                SpawnedTotal++;
            }

            return placed;
        }

        /// <summary>새 쓰레기가 들어오는 행. 프리뷰가 여러 줄이어도 언제나 최상단이다.</summary>
        private int SpawnRow => 0;

        private void CollectCandidates()
        {
            _candidates.Clear();

            // 플레이 영역이 이미 꽉 찼다면 컬럼 잠금을 풀고 프리뷰 줄에 쌓기 시작한다.
            bool overflow = IsPlayAreaFull();

            for (int col = 0; col < _grid.Cols; col++)
            {
                if (!_grid.IsEmpty(col, SpawnRow))
                {
                    continue; // 그 열의 프리뷰 칸에 이미 블록이 대기 중이다.
                }

                if (!overflow && IsColumnFull(col))
                {
                    continue; // 세로 8칸이 찬 열 — 오버플로 전에는 스폰하지 않는다.
                }

                _candidates.Add(col);
            }
        }

        /// <summary>해당 열의 플레이 칸이 전부 점유됐는지(플레이어도 점유로 센다).</summary>
        private bool IsColumnFull(int col)
        {
            for (int row = _board.FirstPlayableRow; row < _grid.Rows; row++)
            {
                if (_grid.IsEmpty(col, row))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>플레이 64칸이 전부 점유됐는지. 8x8이라 최악 64회 — 할당은 없다.</summary>
        private bool IsPlayAreaFull()
        {
            for (int row = _board.FirstPlayableRow; row < _grid.Rows; row++)
            {
                for (int col = 0; col < _grid.Cols; col++)
                {
                    if (_grid.IsEmpty(col, row))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
