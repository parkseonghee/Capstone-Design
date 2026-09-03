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
            new TrashEntry { type = TrashType.A, color = new Color(0.36f, 0.72f, 0.94f) },
            new TrashEntry { type = TrashType.B, color = new Color(0.47f, 0.82f, 0.51f) },
            new TrashEntry { type = TrashType.C, color = new Color(0.94f, 0.52f, 0.42f) },
            new TrashEntry { type = TrashType.D, color = new Color(0.76f, 0.62f, 0.93f) },
        };

        [Header("미지정 종류 폴백")]
        [SerializeField] private Color fallbackColor = Color.gray;

        public void Resolve(Entity entity, out Color color, out Sprite sprite)
        {
            if (entity != null && entity.Kind == EntityKind.Player)
            {
                color = playerColor;
                sprite = playerSprite;
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
