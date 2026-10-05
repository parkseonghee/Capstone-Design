using System;

namespace RecycleLife.Core
{
    /// <summary>
    /// 배선을 한곳에 모은다. Unity 계층과 테스트가 같은 배선을 쓰게 해서
    /// "에디터에서는 되는데 테스트에서는 다른 조합" 같은 어긋남을 막는다.
    ///
    /// 설정 4종이 전부 인터페이스로 들어온다 — 런타임에는 SO 4개,
    /// 테스트에는 페이크가 꽂힌다(Hard Rule 5).
    /// </summary>
    public static class GameLoopFactory
    {
        /// <summary>
        /// 초기 줄과 플레이어 배치까지 끝난 상태로 만들어 준다.
        /// 시작 연출 없이 바로 플레이 가능한 보드가 필요할 때(테스트 등) 쓴다.
        /// </summary>
        public static GameLoop CreateWeek1(
            IBoardConfig board,
            ISpawnConfig spawn,
            ITrashStatsProvider trashStats,
            ICharacterConfig character,
            IBombConfig bomb,
            IRandomSource random,
            WaveRunner waves = null,
            RunModifiers modifiers = null,
            IMaterialDropTable materialDrops = null,
            IPoisonConfig poisonConfig = null)
        {
            GameLoop loop = CreateStaged(
                board, spawn, trashStats, character, bomb, random, waves, modifiers, materialDrops,
                poisonConfig);
            loop.CompleteSetup();
            return loop;
        }

        /// <summary>
        /// 보드가 빈 상태로 만들어 준다. 초기 줄은 호출자가 SeedNextRow()로 한 줄씩 떨어뜨리고,
        /// 다 떨어진 뒤 PlacePlayer()를 부른다 — 그 간격이 시작 연출이 된다.
        /// </summary>
        public static GameLoop CreateStaged(
            IBoardConfig board,
            ISpawnConfig spawn,
            ITrashStatsProvider trashStats,
            ICharacterConfig character,
            IBombConfig bomb,
            IRandomSource random,
            WaveRunner waves = null,
            RunModifiers modifiers = null,
            IMaterialDropTable materialDrops = null,
            IPoisonConfig poisonConfig = null)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));
            if (trashStats == null) throw new ArgumentNullException(nameof(trashStats));
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (bomb == null) throw new ArgumentNullException(nameof(bomb));
            if (random == null) throw new ArgumentNullException(nameof(random));

            // 유물 효과는 설정을 한 겹 덮어서 적용한다 — 스포너·폭탄·전투는 자기가 유물을 쓰는지 모른다.
            if (modifiers != null)
            {
                bomb = new ModifiedBombConfig(bomb, modifiers);
                trashStats = new ModifiedTrashStats(trashStats, modifiers);
            }

            // 웨이브가 있으면 스폰 가중치만 현재 웨이브 값으로 갈아끼운다.
            // 스포너·피커는 이 교체를 모른 채 평소대로 ITrashStatsProvider를 읽는다.
            ITrashStatsProvider spawnStats = waves == null
                ? trashStats
                : new WaveSpawnWeights(trashStats, waves);

            var grid = new BoardGrid(board.Cols, board.Rows);

            // 덫 몬스터 기믹: 죽은 자리에 덫을 남기고, 닿으면 무작위 빈 칸으로 보낸다.
            // 여기 한 곳에서 만들어 GravityResolver·CombatMoveResolver·BombResolver·GameLoop가
            // 전부 같은 인스턴스를 물게 한다(Hard Rule 4 — 배선은 조립 지점에만 있다).
            var traps = new TrapResolver(grid, random);

            // 재료 드롭도 덫과 같은 자리다 — 적이 죽는 네 군데가 전부 이 인스턴스를 물어야
            // 어디서 죽든 같은 확률로 떨어진다. 표를 안 넘기면 null이라 아무 일도 안 일어난다.
            MaterialDropResolver drops = materialDrops != null
                ? new MaterialDropResolver(materialDrops, random)
                : null;

            // 최대 체력 유물(R01·R02)은 생성 시점에 이미 반영돼야 하트가 맞게 그려진다.
            int maxHp = character.MaxHp + (modifiers != null ? modifiers.BonusMaxHp : 0);
            var player = new Player(maxHp, character.Attack, bomb.StartingCount);
            player.AttachModifiers(modifiers);

            // 독 몬스터 기믹. 덱·드롭과 같은 자리에서 한 번만 만들어 중력·전투·폭발·루프가
            // 전부 같은 인스턴스를 물게 한다(Hard Rule 4). 설정을 안 넘기면 null이라 기믹이 통째로 꺼진다.
            PoisonResolver poison = poisonConfig != null
                ? new PoisonResolver(grid, player, poisonConfig)
                : null;

            if (!grid.InBounds(board.PlayerStart))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(board),
                    $"PlayerStart {board.PlayerStart} is outside the {board.Cols}x{board.Rows} board.");
            }

            // 진행 중 스폰: 언제나 프리뷰 줄로만 들어온다(WEEK1 §5 확정).
            // 캐이던스와 컬럼 잠금은 PreviewRowSpawner가 갖는다.
            ITrashSpawner stepSpawner = new PreviewRowSpawner(grid, board, spawn, spawnStats, random);

            // 시작 3줄은 줄 단위로 통째로 깔린다. GapsPerRow = 0이면 완전히 꽉 찬 줄이다.
            ISeedSpawner seedSpawner = new RowTrashSpawner(grid, board, spawnStats, random);

            // 연쇄 기준은 "같은 종류"로 확정됐다(CORE_COMBAT.md §8-1).
            // 재질 기준으로 바꾸려면 이 한 줄만 다른 IChainRule로 갈아끼우면 된다.
            var chain = new ChainFinder(grid, new SameTypeChainRule(), board.FirstPlayableRow);

            // 패배 판정이 이동 규칙을 알아야 한다 — 순서상 리졸버를 먼저 만든다.
            IMoveResolver move = new CombatMoveResolver(
                grid, player, board, character, chain, random, traps, drops, poison);

            return new GameLoop(
                grid,
                player,
                board,
                move,
                new GravityResolver(grid, traps, poison),
                stepSpawner,
                seedSpawner,
                new BombResolver(grid, player, board, bomb, traps, drops, poison),
                new GameOverChecker(grid, player, move),
                waves,
                traps,
                drops,
                poison);
        }
    }
}
