using System.Collections.Generic;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을 보드를 스프라이트로 비추기만 한다. 규칙 판단은 하나도 하지 않는다.
    ///
    /// 캐릭터 상은 자리를 바꾸지 않으니 맵이 만들어질 때 한 번만 놓고, 아바타만 매 스텝
    /// 위치를 보간해 옮긴다 — 본편 BoardView처럼 매 프레임 보드 전체를 훑지 않는다(Hard Rule 8).
    /// </summary>
    public sealed class VillageBoardView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("구독할 보드. 씬에서 연결한다.")]
        private VillageCharacterBoard board;

        [SerializeField, Tooltip("칸 스프라이트가 들어갈 부모. 이 오브젝트의 위치가 보드 중심이 된다.")]
        private Transform cellRoot;

        [SerializeField, Tooltip("아바타를 그리는 SpriteRenderer. 씬에 미리 놓아 둔다.")]
        private SpriteRenderer avatarRenderer;

        [SerializeField, Tooltip("캐릭터 상 하나를 그리는 프리팹(SpriteRenderer).")]
        private SpriteRenderer standViewPrefab;

        [SerializeField, Tooltip("id별 대표 색을 읽어 올 표. 상과 아바타 색이 전부 여기서 나온다 — " +
                                 "그래야 \"바뀐 아바타 색 = 부딪힌 상 색\"이 항상 맞는다.")]
        private CharacterRoster roster;

        [SerializeField, Tooltip("지금 고른 캐릭터. 바뀌는 순간 아바타 색을 그 캐릭터 색으로 다시 칠한다 — " +
                                 "확인창 없이 바뀌는 대신 색으로 \"바뀌었다\"를 보여 준다.")]
        private CharacterSelection characterSelection;

        [SerializeField, Tooltip("(선택) 마을 배경. 연결하면 보드 크기에 맞춰 스케일만 조정한다.")]
        private SpriteRenderer boardBackground;

        [Header("레이아웃")]
        [SerializeField, Min(0.05f), Tooltip("한 칸의 월드 크기.")]
        private float cellSize = 1f;

        [SerializeField, Range(0.1f, 1f), Tooltip("칸 안에서 스프라이트가 차지하는 비율.")]
        private float cellFill = 0.9f;

        [Header("연출")]
        [SerializeField, Min(0f), Tooltip("아바타 이동 보간 속도. 0이면 즉시 이동한다.")]
        private float moveSmoothing = 16f;

        [SerializeField, Min(0f)]
        private float snapDistance = 0.002f;

        private readonly List<SpriteRenderer> _standViews = new List<SpriteRenderer>(8);

        private Vector3 _avatarTarget;
        private Vector3 _avatarPosition;
        private bool _settled = true;

        /// <summary>보드 전체가 화면에 들어오도록 카메라를 맞출 때 읽는 크기(CameraBoardFitter와 같은 역할).</summary>
        public Vector2 BoardWorldSize => board == null || board.Map == null
            ? Vector2.zero
            : new Vector2(board.Map.Grid.Cols * cellSize, board.Map.Grid.Rows * cellSize);

        public Vector3 BoardWorldCenter => cellRoot != null ? cellRoot.position : transform.position;

        private void OnEnable()
        {
            if (board == null)
            {
                Debug.LogError($"{nameof(VillageBoardView)}: VillageCharacterBoard가 연결되지 않았습니다.", this);
                return;
            }

            board.MapReady += HandleMapReady;
            board.Stepped += HandleStepped;

            if (characterSelection != null)
            {
                characterSelection.Changed += HandleSelectionChanged;
            }

            if (board.Map != null)
            {
                HandleMapReady(board.Map);
            }

            ApplyAvatarTint();
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.MapReady -= HandleMapReady;
                board.Stepped -= HandleStepped;
            }

            if (characterSelection != null)
            {
                characterSelection.Changed -= HandleSelectionChanged;
            }
        }

        private void LateUpdate()
        {
            if (avatarRenderer == null || _settled)
            {
                return;
            }

            float t = moveSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-moveSmoothing * Time.deltaTime);
            _avatarPosition = Vector3.Lerp(_avatarPosition, _avatarTarget, t);

            if ((_avatarPosition - _avatarTarget).sqrMagnitude <= snapDistance * snapDistance)
            {
                _avatarPosition = _avatarTarget;
                _settled = true;
            }

            avatarRenderer.transform.localPosition = _avatarPosition;
        }

        private void HandleMapReady(VillageMap map)
        {
            ReleaseStands();
            PlaceAvatar(map, snap: true);
            PlaceStands(map);
            ResizeBackground(map);
        }

        private void HandleStepped(VillageStepResult result)
        {
            if (board == null || board.Map == null)
            {
                return;
            }

            PlaceAvatar(board.Map, snap: false);
        }

        /// <summary>
        /// CharacterSelection이 바뀌는 순간(=상에 부딪힌 순간) 불린다.
        /// 확인창 대신 아바타 색이 바로 바뀌는 것으로 "전환됐다"를 알려 준다(기획: 2026-09-29 보완).
        /// </summary>
        private void HandleSelectionChanged()
        {
            ApplyAvatarTint();
        }

        private void ApplyAvatarTint()
        {
            if (avatarRenderer == null || roster == null || characterSelection == null)
            {
                return;
            }

            Color tint;
            if (roster.TryGetTint(characterSelection.SelectedId, out tint))
            {
                avatarRenderer.color = tint;
            }
        }

        private void PlaceAvatar(VillageMap map, bool snap)
        {
            if (avatarRenderer == null)
            {
                return;
            }

            if (avatarRenderer.sprite == null)
            {
                avatarRenderer.sprite = PlaceholderSprite.Square;
            }

            avatarRenderer.transform.localScale = new Vector3(cellSize * cellFill, cellSize * cellFill, 1f);

            _avatarTarget = CellToLocal(map, map.Avatar.Position);

            if (snap)
            {
                _avatarPosition = _avatarTarget;
                avatarRenderer.transform.localPosition = _avatarPosition;
                _settled = true;
            }
            else
            {
                _settled = false;
            }
        }

        private void PlaceStands(VillageMap map)
        {
            if (standViewPrefab == null || cellRoot == null)
            {
                return;
            }

            BoardGrid grid = map.Grid;
            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Cols; col++)
                {
                    var stand = grid[col, row] as CharacterStand;
                    if (stand == null)
                    {
                        continue;
                    }

                    SpriteRenderer view = Instantiate(standViewPrefab, cellRoot);
                    view.transform.localPosition = CellToLocal(map, stand.Position);
                    view.transform.localScale = new Vector3(cellSize * cellFill, cellSize * cellFill, 1f);

                    if (view.sprite == null)
                    {
                        view.sprite = PlaceholderSprite.Square;
                    }

                    Color tint;
                    if (roster != null && roster.TryGetTint(stand.CharacterId, out tint))
                    {
                        view.color = tint;
                    }

#if UNITY_EDITOR
                    view.name = "CharacterStand_" + stand.CharacterId;
#endif

                    _standViews.Add(view);
                }
            }
        }

        private void ReleaseStands()
        {
            for (int i = 0; i < _standViews.Count; i++)
            {
                if (_standViews[i] != null)
                {
                    Destroy(_standViews[i].gameObject);
                }
            }

            _standViews.Clear();
        }

        private void ResizeBackground(VillageMap map)
        {
            if (boardBackground == null)
            {
                return;
            }

            if (boardBackground.sprite == null)
            {
                boardBackground.sprite = PlaceholderSprite.Square;
            }

            BoardGrid grid = map.Grid;
            boardBackground.transform.localPosition = Vector3.zero;
            boardBackground.transform.localScale = new Vector3(grid.Cols * cellSize, grid.Rows * cellSize, 1f);
        }

        private Vector3 CellToLocal(VillageMap map, Vector2Int cell)
        {
            BoardGrid grid = map.Grid;
            float x = (cell.x - (grid.Cols - 1) * 0.5f) * cellSize;
            float y = ((grid.Rows - 1) * 0.5f - cell.y) * cellSize;
            return new Vector3(x, y, 0f);
        }
    }
}
