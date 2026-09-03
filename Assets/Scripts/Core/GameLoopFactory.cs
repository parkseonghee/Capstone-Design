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
        public static GameLoop CreateWeek1(IBoardConfig config, IRandomSource random)
        {
            GameLoop loop = CreateStaged(config, random);
            loop.CompleteSetup();
            return loop;
        }

        /// <summary>
        /// 보드가 빈 상태로 만들어 준다. 초기 줄은 호출자가 SeedNextRow()로 한 줄씩 떨어뜨리고,
        /// 다 떨어진 뒤 PlacePlayer()를 부른다 — 그 간격이 시작 연출이 된다.
        /// </summary>
        public static GameLoop CreateStaged(IBoardConfig config, IRandomSource random)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var grid = new BoardGrid(config.Cols, config.Rows);
            var player = new Player();

            if (!grid.InBounds(config.PlayerStart))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(config),
                    $"PlayerStart {config.PlayerStart} is outside the {config.Cols}x{config.Rows} board.");
            }

            // 진행 중 스폰 전략은 설정으로 갈아끼운다(Hard Rule 3).
            ITrashSpawner stepSpawner = config.Mode == SpawnMode.FullRow
                ? (ITrashSpawner)new RowTrashSpawner(grid, config, random)
                : new RandomTopRowSpawner(grid, config, random);

            // 시작 연출은 진행 중 스폰 방식과 무관하게 언제나 줄 단위다.
            ITrashSpawner seedSpawner = new RowTrashSpawner(grid, config, random);

            return new GameLoop(
                grid,
                player,
                config,
                new BlockingMoveResolver(grid, player),
                new GravityResolver(grid),
                stepSpawner,
                seedSpawner,
                new GameOverChecker(grid, player));
        }
    }
}
