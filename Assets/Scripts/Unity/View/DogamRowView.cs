using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 도감 목록 한 줄. <see cref="DogamView"/>가 이 줄을 틀로 삼아 필요한 만큼 복제해 스크롤 목록에 쌓는다.
    /// 글자·아이콘 위치는 프리팹(씬의 틀)이 정하고, 여기서는 칸만 들고 있다.
    /// </summary>
    public sealed class DogamRowView : MonoBehaviour
    {
        public Image icon;
        public Text nameLabel;

        [Tooltip("오른쪽 위 작은 글자. 조합은 분류, 몬스터는 나오는 곳.")]
        public Text tagLabel;

        [Tooltip("둘째 줄. 조합은 재료, 몬스터는 특징·수치.")]
        public Text subLabel;

        [Tooltip("셋째 줄. 설명.")]
        public Text descriptionLabel;

        public void Set(string displayName, string tag, string sub, string description, Sprite sprite, Color color)
        {
            SetText(nameLabel, displayName);
            SetText(tagLabel, tag);
            SetText(subLabel, sub);
            SetText(descriptionLabel, description);

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = color;
                icon.enabled = sprite != null;
            }
        }

        private static void SetText(Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }
    }
}
