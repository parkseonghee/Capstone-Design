using System;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 마을 화면의 아주 단순한 이동판. 본편(GameLoop)의 낙하·스폰·전투는 전혀 쓰지 않는다 —
    /// 마을은 걸어 다니다가 캐릭터 상에 부딪히면 그 캐릭터로 바뀌는 것뿐이라
    /// 새 책임을 GameLoop에 욱여넣지 않고 별도 클래스로 뒀다(Hard Rule 4).
    ///
    /// 시간도 MonoBehaviour도 모르는 순수 로직이다 — 연출은 Unity 계층이 맡는다(Hard Rule 5).
    /// </summary>
    public sealed class VillageMap
    {
        private readonly BoardGrid _grid;
        private readonly VillageAvatar _avatar;

        public VillageMap(int cols, int rows, Vector2Int avatarStart)
        {
            _grid = new BoardGrid(cols, rows);

            if (!_grid.InBounds(avatarStart))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(avatarStart), $"{avatarStart} is outside the {cols}x{rows} board.");
            }

            _avatar = new VillageAvatar();
            _grid.Place(_avatar, avatarStart);
        }

        public BoardGrid Grid => _grid;

        public VillageAvatar Avatar => _avatar;

        /// <summary>
        /// 캐릭터 상을 그 칸에 세운다. 씬에 배치한 좌표를 그대로 읽어 초기화 시점에만 부른다.
        /// </summary>
        public void PlaceCharacterStand(string characterId, Vector2Int position)
        {
            _grid.Place(new CharacterStand(characterId), position);
        }

        /// <summary>
        /// 한 칸 이동을 시도한다.
        ///
        /// 목표 칸이 캐릭터 상이면 <b>그 칸으로 들어가지 않고</b> 바로 그 자리에서 부딪힌 것으로 친다 —
        /// 확인창 없이 즉시 전환하는 기획(2026-09-29)이라 굳이 상 위로 걸어 들어갈 필요가 없다.
        /// </summary>
        public VillageStepResult TryMove(Direction direction)
        {
            Vector2Int target = _avatar.Position + direction.ToOffset();

            if (!_grid.InBounds(target))
            {
                return VillageStepResult.OutOfBounds;
            }

            Entity occupant = _grid[target];
            if (occupant == null)
            {
                _grid.Move(_avatar.Position, target);
                return VillageStepResult.Moved;
            }

            if (occupant is CharacterStand stand)
            {
                return VillageStepResult.SwappedCharacter(stand.CharacterId);
            }

            return VillageStepResult.Blocked;
        }
    }
}
