using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// TrashType -> 보이는 모습 매핑. 뷰에 enum switch를 박지 않기 위한 데이터 에셋(Hard Rule 3).
    /// 쓰레기 종류가 늘어나면 이 에셋에 줄만 추가하면 되고 코드는 그대로다.
    ///
    /// 스프라이트를 비워두면 단색 사각형 플레이스홀더가 쓰인다.
    /// WEEK1 §0이 "연출·아트 없이 순수 로직 먼저"라 아트는 나중에 이 슬롯에 꽂으면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "EntityVisualSet", menuName = "RecycleLife/Entity Visual Set", order = 1)]
    public sealed class EntityVisualSet : ScriptableObject
    {
        [Serializable]
        public struct TrashEntry
        {
            public TrashType type;
            public Color color;
            public Sprite sprite;
        }

        [Header("플레이어")]
        [SerializeField] private Color playerColor = new Color(0.98f, 0.85f, 0.28f);
        [SerializeField] private Sprite playerSprite;

        [Header("쓰레기 종류별")]
        [SerializeField]
        private TrashEntry[] trash =
        {
            // 스프라이트가 들어오기 전까지 쓰는 임시 색. 재질이 구분되게 골랐다.
            new TrashEntry { type = TrashType.Paper, color = new Color(0.87f, 0.85f, 0.78f) },
            new TrashEntry { type = TrashType.Plastic, color = new Color(0.36f, 0.72f, 0.94f) },
            new TrashEntry { type = TrashType.Glass, color = new Color(0.45f, 0.85f, 0.80f) },
            new TrashEntry { type = TrashType.Potion, color = new Color(0.98f, 0.62f, 0.68f) },
            new TrashEntry { type = TrashType.Wood, color = new Color(0.60f, 0.44f, 0.28f) },
            new TrashEntry { type = TrashType.Concrete, color = new Color(0.55f, 0.56f, 0.58f) },
            new TrashEntry { type = TrashType.Steel, color = new Color(0.33f, 0.36f, 0.42f) },

            // 나중에 추가된 잡몹 6종. 기존 7종과 섞이지 않게 색상환에서 떨어뜨렸다.
            new TrashEntry { type = TrashType.Can, color = new Color(0.78f, 0.79f, 0.52f) },
            new TrashEntry { type = TrashType.Vinyl, color = new Color(0.72f, 0.66f, 0.88f) },
            new TrashEntry { type = TrashType.Styrofoam, color = new Color(0.94f, 0.94f, 0.90f) },
            new TrashEntry { type = TrashType.Tire, color = new Color(0.22f, 0.22f, 0.24f) },
            new TrashEntry { type = TrashType.Net, color = new Color(0.30f, 0.62f, 0.55f) },
            new TrashEntry { type = TrashType.Sludge, color = new Color(0.44f, 0.55f, 0.20f) },
        };

        [Header("미지정 종류 폴백")]
        [SerializeField] private Color fallbackColor = Color.gray;

        [Header("폭탄")]
        [SerializeField, Tooltip("설치만 하고 아직 불이 안 붙은 폭탄.")]
        private Color bombColor = new Color(0.25f, 0.25f, 0.30f);

        [SerializeField, Tooltip("불이 붙어 카운트다운 중인 폭탄. 위험 신호라 눈에 띄게.")]
        private Color bombArmedColor = new Color(1f, 0.42f, 0.12f);

        [SerializeField, Tooltip("터지기 직전의 폭탄. 남은 턴이 줄수록 이 색으로 물든다.")]
        private Color bombImminentColor = new Color(0.95f, 0.15f, 0.15f);

        [SerializeField, Tooltip("(선택) 폭탄 스프라이트. 비우면 단색 사각형.")]
        private Sprite bombSprite;

        [Header("체력 하트 (CORE_COMBAT.md §1)")]
        [SerializeField, Tooltip("남아 있는 체력 한 칸. 비워 두면 하트를 아예 그리지 않는다.")]
        private Sprite heartFull;

        [SerializeField, Tooltip("잃은 체력 한 칸.")]
        private Sprite heartEmpty;

        public Sprite HeartFull => heartFull;

        public Sprite HeartEmpty => heartEmpty;

        /// <summary>둘 다 꽂혀 있어야 하트를 그린다. 하나만 있으면 표시가 반쪽이라 아예 끈다.</summary>
        public bool HasHeartSprites => heartFull != null && heartEmpty != null;

        public void Resolve(Entity entity, out Color color, out Sprite sprite)
        {
            if (entity != null && entity.Kind == EntityKind.Player)
            {
                color = playerColor;
                sprite = playerSprite;
                return;
            }

            var bomb = entity as Bomb;
            if (bomb != null)
            {
                // 불이 붙었는지, 그리고 얼마나 급한지가 한눈에 보여야 한다.
                // 숫자를 띄우려면 월드 텍스트가 필요해서, 우선 색으로 남은 턴을 읽게 했다.
                if (!bomb.IsArmed)
                {
                    color = bombColor;
                }
                else
                {
                    float t = bomb.FuseTurns <= 1
                        ? 1f
                        : 1f - ((bomb.FuseRemaining - 1f) / (bomb.FuseTurns - 1f));
                    color = Color.Lerp(bombArmedColor, bombImminentColor, Mathf.Clamp01(t));
                }

                sprite = bombSprite;
                return;
            }

            var trashEntity = entity as Trash;
            if (trashEntity != null)
            {
                for (int i = 0; i < trash.Length; i++)
                {
                    if (trash[i].type == trashEntity.Type)
                    {
                        color = trash[i].color;
                        sprite = trash[i].sprite;
                        return;
                    }
                }
            }

            color = fallbackColor;
            sprite = null;
        }
    }
}
