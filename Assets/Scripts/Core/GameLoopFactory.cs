using System;

namespace RecycleLife.Core
{
    /// <summary>
    /// WEEK1 구성의 배선을 한곳에 모은다. Unity 계층과 테스트가 같은 배선을 쓰게 해서
    /// "에디터에서는 되는데 테스트에서는 다른 조합" 같은 어긋남을 막는다.
    /// </summary>
    public static class GameLoopFactory
    {
        /// <summary>
        /// 초기 줄과 플레이어 배치까지 끝난 상태로 만들어 준다.
        /// 시작 연출 없이 바로 플레이 가능한 보드가 필요할 때(테스트 등) 쓴다.
        /// </summary>
        public static GameLoop CreateWeek1(IBoardConfig board, ISpawnConfig spawn, IRandomSource random)
        {
            GameLoop loop = CreateStaged(board, spawn, random);
            loop.CompleteSetup();
            return loop;
        }

        /// <summary>
        /// 보드가 빈 상태로 만들어 준다. 초기 줄은 호출자가 SeedNextRow()로 한 줄씩 떨어뜨리고,
        /// 다 떨어진 뒤 PlacePlayer()를 부른다 — 그 간격이 시작 연출이 된다.
        /// </summary>
        public static GameLoop CreateStaged(IBoardConfig board, ISpawnConfig spawn, IRandomSource random)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var grid = new BoardGrid(board.Cols, board.Rows);
            var player = new Player();

            if (!grid.InBounds(board.PlayerStart))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(board),
                    $"PlayerStart {board.PlayerStart} is outside the {board.Cols}x{board.Rows} board.");
            }

            // 진행 중 스폰: 언제나 프리뷰 줄로만 들어온다(WEEK1 §5 확정).
            // 캐이던스와 컬럼 잠금은 PreviewRowSpawner가 갖는다.
            ITrashSpawner stepSpawner = new PreviewRowSpawner(grid, board, spawn, random);

            // 시작 3줄은 줄 단위로 통째로 깔린다. GapsPerRow = 0이면 완전히 꽉 찬 줄이다.
            ISeedSpawner seedSpawner = new RowTrashSpawner(grid, board, random);

            return new GameLoop(
                grid,
                player,
                board,
                new BlockingMoveResolver(grid, player, board),
                new GravityResolver(grid),
                stepSpawner,
                seedSpawner,
                new GameOverChecker(grid, player, board));
        }
    }
}
