using System;
using System.Collections.Generic;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// ScriptableObject 없이 설정을 넣기 위한 페이크.
    /// Core가 인터페이스에만 의존하기 때문에 가능하다.
    ///
    /// 한 클래스가 네 인터페이스를 다 구현하는 건 테스트 편의일 뿐이다 —
    /// 런타임에서는 GridConfig / SpawnConfig / TrashStatsConfig / PlayerStatsConfig
    /// 네 에셋으로 갈라져 있다.
    /// </summary>
    internal sealed class FakeBoardConfig : IBoardConfig, ISpawnConfig, ITrashStatsProvider, ICharacterConfig
    {
        // ── 보드 (WEEK1 §1 확정값) ──────────────────────────────────────────
        public int Cols { get; set; } = 8;

        public int PlayableRows { get; set; } = 8;

        public int PreviewRows { get; set; } = 1;

        public int Rows => PlayableRows + PreviewRows;

        public int FirstPlayableRow => PreviewRows;

        public int RowsOnStart { get; set; }

        public int GapsPerRow { get; set; }

        public bool AdvanceOnBlocked { get; set; }

        public Vector2Int PlayerStart { get; set; } = new Vector2Int(4, 8);

        // ── 스폰 캐이던스 (WEEK1 §5 확정값) ─────────────────────────────────
        public int BlocksBeforeSlowdown { get; set; } = 15;

        public int TurnsPerSpawnEarly { get; set; } = 1;

        public int TurnsPerSpawnLate { get; set; } = 2;

        public int BlocksPerSpawn { get; set; } = 1;

        // ── 전투 (CORE_COMBAT.md §1) ────────────────────────────────────────
        // 종류를 구분하지 않는 균일 스탯이 기본이다. 종류별로 다르게 줘야 하는
        // 연쇄 테스트는 FakeTrashStats를 따로 꽂는다.
        public int TrashMaxHp { get; set; } = 1;

        public int TrashAttack { get; set; } = 1;

        /// <summary>
        /// 모든 종류가 같은 가중치 1이다 — ScriptedRandom에 넘긴 값이 곧 종류 인덱스가 되므로
        /// 가중치 도입 전과 똑같이 결정론적으로 읽힌다.
        /// </summary>
        public TrashStats For(TrashType type) => new TrashStats(TrashMaxHp, TrashAttack, 0, 1, true);

        // ── 캐릭터 (기본은 A형: 부딪힌 칸만) ────────────────────────────────
        public int MaxHp { get; set; } = 10;

        public int Attack { get; set; } = 1;

        public IReadOnlyList<Vector2Int> AttackOffsets { get; set; } = Offsets.BumpedOnly;
    }

    /// <summary>종류별로 다른 값을 주고 싶을 때 쓴다(연쇄·반격·포션·가중치 테스트).</summary>
    internal sealed class FakeTrashStats : ITrashStatsProvider
    {
        private readonly TrashStats[] _stats = new TrashStats[Enum.GetValues(typeof(TrashType)).Length];

        public FakeTrashStats(int maxHp = 1, int attack = 1, int spawnWeight = 1)
        {
            for (int i = 0; i < _stats.Length; i++)
            {
                _stats[i] = new TrashStats(maxHp, attack, 0, spawnWeight, true);
            }
        }

        /// <summary>적 한 종류를 정의한다.</summary>
        public FakeTrashStats Set(TrashType type, int maxHp, int attack)
        {
            TrashStats old = _stats[(int)type];
            _stats[(int)type] = new TrashStats(maxHp, attack, 0, old.SpawnWeight, true);
            return this;
        }

        /// <summary>아이템 한 종류를 정의한다. 체력·공격력은 0이 된다.</summary>
        public FakeTrashStats SetPotion(TrashType type, int heal)
        {
            TrashStats old = _stats[(int)type];
            _stats[(int)type] = new TrashStats(0, 0, heal, old.SpawnWeight, true);
            return this;
        }

        /// <summary>벽 한 종류를 정의한다. 공격력이 없을 뿐 연쇄는 다른 블록과 똑같이 한다.</summary>
        public FakeTrashStats SetWall(TrashType type, int maxHp)
        {
            TrashStats old = _stats[(int)type];
            _stats[(int)type] = new TrashStats(maxHp, 0, 0, old.SpawnWeight, true);
            return this;
        }

        public FakeTrashStats SetWeight(TrashType type, int weight)
        {
            TrashStats old = _stats[(int)type];
            _stats[(int)type] = new TrashStats(
                old.MaxHp, old.Attack, old.Heal, weight, old.ChainsWithSameType);
            return this;
        }

        public TrashStats For(TrashType type) => _stats[(int)type];
    }

    /// <summary>
    /// 배선을 한곳에 모아 테스트가 팩토리 시그니처 변경에 흔들리지 않게 한다.
    /// 페이크가 설정 네 종류를 겸하므로 같은 객체를 여러 번 넘긴다.
    /// </summary>
    internal static class Make
    {
        public static GameLoop Week1(FakeBoardConfig config, IRandomSource random)
            => GameLoopFactory.CreateWeek1(config, config, config, config, random);

        public static GameLoop Week1(FakeBoardConfig config, IRandomSource random, ITrashStatsProvider stats)
            => GameLoopFactory.CreateWeek1(config, config, stats, config, random);

        public static GameLoop Staged(FakeBoardConfig config, IRandomSource random)
            => GameLoopFactory.CreateStaged(config, config, config, config, random);

        /// <summary>
        /// 전투 <b>없이</b> "쓰레기에 막힘"이 살아 있는 보드.
        /// 런타임 배선(GameLoopFactory)은 언제나 CombatMoveResolver를 꽂으므로,
        /// AdvanceOnBlocked·갇힘처럼 '막힘'을 전제한 규칙을 검사할 때만 쓴다.
        /// </summary>
        public static GameLoop Blocking(FakeBoardConfig config, IRandomSource random)
        {
            var grid = new BoardGrid(config.Cols, config.Rows);
            var player = new RecycleLife.Core.Player(config.MaxHp, config.Attack);
            IMoveResolver move = new BlockingMoveResolver(grid, player, config);

            var loop = new GameLoop(
                grid,
                player,
                config,
                move,
                new GravityResolver(grid),
                new PreviewRowSpawner(grid, config, config, config, random),
                new RowTrashSpawner(grid, config, config, random),
                new GameOverChecker(grid, player, move));

            loop.CompleteSetup();
            return loop;
        }

        /// <summary>스탯을 신경 쓰지 않는 테스트용 쓰레기(체력 1, 공격력 1).</summary>
        public static RecycleLife.Core.Trash Trash(TrashType type)
            => new RecycleLife.Core.Trash(type, new TrashStats(1, 1, 0, 1, true));

        public static RecycleLife.Core.Trash Trash(TrashType type, int maxHp, int attack)
            => new RecycleLife.Core.Trash(type, new TrashStats(maxHp, attack, 0, 1, true));

        /// <summary>먹으면 heal만큼 회복하는 아이템. 체력·공격력은 없다.</summary>
        public static RecycleLife.Core.Trash Potion(TrashType type, int heal)
            => new RecycleLife.Core.Trash(type, new TrashStats(0, 0, heal, 1, true));

        /// <summary>벽. 공격력이 없어 반격하지 않을 뿐, 연쇄는 다른 블록과 똑같이 한다.</summary>
        public static RecycleLife.Core.Trash Wall(TrashType type, int maxHp)
            => new RecycleLife.Core.Trash(type, new TrashStats(maxHp, 0, 0, 1, true));

        /// <summary>전투를 보지 않는 테스트용 플레이어.</summary>
        public static RecycleLife.Core.Player Player()
            => new RecycleLife.Core.Player(10, 1);

        public static RecycleLife.Core.Player Player(int maxHp, int attack)
            => new RecycleLife.Core.Player(maxHp, attack);
    }

    /// <summary>기획서가 정의한 캐릭터별 공격 범위.</summary>
    internal static class Offsets
    {
        /// <summary>캐릭터 A(기본형) — 부딪힌 칸만.</summary>
        public static readonly Vector2Int[] BumpedOnly = { Vector2Int.zero };

        /// <summary>캐릭터 B(사이드 어택형) — 부딪힌 칸 + 좌우.</summary>
        public static readonly Vector2Int[] BumpedAndSides =
        {
            Vector2Int.zero,
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
        };
    }

    /// <summary>공격 범위가 다른 캐릭터를 꽂아 볼 때 쓴다.</summary>
    internal sealed class FakeCharacter : ICharacterConfig
    {
        public FakeCharacter(int maxHp, int attack, IReadOnlyList<Vector2Int> offsets)
        {
            MaxHp = maxHp;
            Attack = attack;
            AttackOffsets = offsets;
        }

        public int MaxHp { get; }

        public int Attack { get; }

        public IReadOnlyList<Vector2Int> AttackOffsets { get; }
    }

    /// <summary>항상 최솟값을 돌려준다 — 스폰이 가장 왼쪽 빈 칸 / TrashType.Paper로 고정된다.</summary>
    internal sealed class MinRandom : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }

    /// <summary>정해진 값을 순서대로 돌려주고, 다 쓰면 처음부터 반복한다.</summary>
    internal sealed class ScriptedRandom : IRandomSource
    {
        private readonly int[] _values;
        private int _cursor;

        public ScriptedRandom(params int[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("At least one value is required.", nameof(values));
            }

            _values = values;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            int value = _values[_cursor % _values.Length];
            _cursor++;

            if (value < minInclusive || value >= maxExclusive)
            {
                throw new InvalidOperationException(
                    $"Scripted value {value} is outside the requested range [{minInclusive}, {maxExclusive}).");
            }

            return value;
        }
    }
}
