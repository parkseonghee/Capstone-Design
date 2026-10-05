using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 모아 둔 총 골드를 비춘다. 마을 어디서나 보이는 표시라 상점을 안 열어도 잔액을 안다.
    ///
    /// 게임 씬의 골드 표시(<see cref="HudView"/>)와는 다른 값이다 — 저쪽은 <b>이번 판에서 번</b>
    /// 골드이고, 이쪽은 스테이지를 넘어 쌓여 조합법을 사는 데 쓰는 <b>총액</b>이다.
    ///
    /// <see cref="RunProgress.Changed"/>를 구독해 값이 바뀔 때만 다시 그린다 —
    /// 매 프레임 문자열을 만들면 쓸데없는 쓰레기가 쌓인다(Hard Rule 8).
    /// </summary>
    public sealed class GoldLabelView : MonoBehaviour
    {
        [SerializeField, Tooltip("골드를 읽어 올 곳.")]
        private RunProgress progress;

        [SerializeField, Tooltip("값을 찍을 칸. 비우면 같은 오브젝트에서 찾는다.")]
        private Text label;

        [SerializeField, Tooltip("표시 형식. {0}이 골드다.")]
        private string format = "G {0}";

        /// <summary>마지막으로 찍은 값. 같으면 문자열을 새로 만들지 않는다.</summary>
        private int _shown = int.MinValue;

        private void OnEnable()
        {
            if (label == null)
            {
                label = GetComponent<Text>();
            }

            if (progress != null)
            {
                progress.Changed += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }
        }

        private void Refresh()
        {
            if (label == null || progress == null)
            {
                return;
            }

            int gold = progress.Gold;
            if (_shown == gold)
            {
                return;
            }

            _shown = gold;
            label.text = string.Format(format, gold);
        }
    }
}
