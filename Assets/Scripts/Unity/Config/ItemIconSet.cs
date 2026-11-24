using System;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 아이템 id별 아이콘 표. 씬에 묶이지 않는 에셋이라 마을과 본편이 같은 그림을 함께 쓴다.
    ///
    /// 본편의 <see cref="ItemFlyEffect"/>는 이 표를 먼저 보고, 없을 때만 자기 안의 옛 표를 본다.
    /// 그래서 새 아이콘은 <b>이 에셋에만</b> 넣으면 인벤토리·조합대·도감에 모두 뜬다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemIconSet", menuName = "RecycleLife/Item Icon Set", order = 17)]
    public sealed class ItemIconSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("ItemConfig의 id와 같아야 한다.")]
            public string id;

            public Sprite sprite;

            [Tooltip("곱할 색. 투명으로 두면 흰색으로 본다.")]
            public Color color;
        }

        [SerializeField, Tooltip("아이템별 아이콘.")]
        private Entry[] icons = new Entry[0];

        public Entry[] Icons => icons;

        /// <summary>id의 아이콘. 표에 없거나 그림이 비어 있으면 false.</summary>
        public bool TryGet(string id, out Sprite sprite, out Color color)
        {
            if (icons != null && !string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < icons.Length; i++)
                {
                    if (icons[i].id != id || icons[i].sprite == null)
                    {
                        continue;
                    }

                    sprite = icons[i].sprite;
                    color = icons[i].color.a > 0f ? icons[i].color : Color.white;
                    return true;
                }
            }

            sprite = null;
            color = Color.white;
            return false;
        }
    }
}
