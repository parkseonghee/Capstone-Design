using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을 화면의 이동판을 조립하는 지점. 방향 입력을 받아 VillageMap에 넘기고,
    /// 캐릭터 상에 부딪히면 그 자리에서 CharacterSelection을 바꾼다 — 확인창 없음(기획 2026-09-29).
    ///
    /// 본편 GameSession과 같은 "조립 지점" 역할이지만 훨씬 얇다 — 마을에는
    /// 낙하·스폰·전투가 없어서 그 배선이 필요 없다(Hard Rule 4: 새 책임을 GameSession에 얹지 않는다).
    /// </summary>
    public sealed class VillageCharacterBoard : MonoBehaviour
    {
        [Serializable]
        public struct StandPlacement
        {
            [Tooltip("CharacterRoster의 id와 정확히 일치해야 한다.")]
            public string characterId;

            public Vector2Int cell;
        }

        [Header("보드 규격 (씬에서 조절)")]
        [SerializeField, Min(1), Tooltip("마을판 가로 칸 수.")]
        private int cols = 5;

        [SerializeField, Min(1), Tooltip("마을판 세로 칸 수.")]
        private int rows = 5;

        [SerializeField, Tooltip("아바타 시작 칸.")]
        private Vector2Int avatarStart = new Vector2Int(2, 4);

        [SerializeField, Tooltip("캐릭터 상을 세울 칸들. id는 CharacterRoster와 맞춰야 한다.")]
        private StandPlacement[] stands =
        {
            new StandPlacement { characterId = "char_a", cell = new Vector2Int(1, 1) },
            new StandPlacement { characterId = "char_b", cell = new Vector2Int(3, 1) },
        };

        [Header("연결")]
        [SerializeField, Tooltip("방향 입력을 흘려보내는 라우터. 씬에서 연결한다.")]
        private InputRouter input;

        [SerializeField, Tooltip("선택된 캐릭터를 저장할 곳. 상에 부딪히면 여기로 즉시 반영된다.")]
        private CharacterSelection characterSelection;

        /// <summary>보드가 새로 만들어졌을 때. 뷰가 여기서 처음부터 그린다.</summary>
        public event Action<VillageMap> MapReady;

        /// <summary>한 칸 이동/상호작용마다. 뷰가 아바타 위치를 갱신한다.</summary>
        public event Action<VillageStepResult> Stepped;

        public VillageMap Map { get; private set; }

        private void OnEnable()
        {
            if (input != null)
            {
                input.DirectionPressed += HandleDirection;
            }

            BuildMap();
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.DirectionPressed -= HandleDirection;
            }
        }

        private void BuildMap()
        {
            Map = new VillageMap(cols, rows, avatarStart);

            if (stands != null)
            {
                for (int i = 0; i < stands.Length; i++)
                {
                    if (!string.IsNullOrEmpty(stands[i].characterId))
                    {
                        Map.PlaceCharacterStand(stands[i].characterId, stands[i].cell);
                    }
                }
            }

            MapReady?.Invoke(Map);
        }

        private void HandleDirection(Direction direction)
        {
            if (Map == null)
            {
                return;
            }

            VillageStepResult result = Map.TryMove(direction);

            if (result.Outcome == VillageMoveOutcome.SwappedCharacter && characterSelection != null)
            {
                characterSelection.Select(result.CharacterId);
            }

            Stepped?.Invoke(result);
        }
    }
}
