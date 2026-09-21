using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 지도 위 스테이지 노드 하나. 참고 이미지의 "2-1, 2-2 …" 배지에 해당한다.
    ///
    /// <b>위치는 씬에서 손으로 잡는다.</b> 경로를 따라 구불구불 놓아야 하는 배치라
    /// 코드가 좌표를 계산하지 않는다 — 이 컴포넌트는 번호·잠금·클리어 상태만 칠한다(Hard Rule 2).
    /// 지도 그림이 들어오면 노드를 그 위 원하는 자리로 끌어다 놓기만 하면 된다.
    /// </summary>
    public sealed class StageMapNode : MonoBehaviour
    {
        [Header("구성 (씬에서 연결)")]
        [SerializeField, Tooltip("노드를 누르는 버튼.")]
        private Button button;

        [SerializeField, Tooltip("'2-1' 같은 번호 라벨.")]
        private Text label;

        [SerializeField, Tooltip("노드 배경. 잠금·클리어에 따라 색이 바뀐다.")]
        private Image background;

        [SerializeField, Tooltip("(선택) 클리어 표시(별 등). 깬 스테이지에만 켠다.")]
        private GameObject clearedMark;

        [SerializeField, Tooltip("(선택) 잠금 표시(자물쇠 등). 못 들어가는 스테이지에만 켠다.")]
        private GameObject lockedMark;

        public Button Button => button;

        /// <summary>이 노드가 지금 가리키는 스테이지 인덱스. 비어 있으면 -1.</summary>
        public int StageIndex { get; private set; } = -1;

        /// <summary>노드를 그 스테이지로 칠한다.</summary>
        public void Bind(int stageIndex, string text, bool unlocked, bool cleared)
        {
            StageIndex = stageIndex;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (label != null)
            {
                label.text = text;
            }

            if (button != null)
            {
                button.interactable = unlocked;
            }

            if (clearedMark != null && clearedMark.activeSelf != cleared)
            {
                clearedMark.SetActive(cleared);
            }

            bool showLock = !unlocked;
            if (lockedMark != null && lockedMark.activeSelf != showLock)
            {
                lockedMark.SetActive(showLock);
            }
        }

        /// <summary>배경색을 바꾼다. 색 자체는 지도 뷰가 인스펙터에서 들고 있다.</summary>
        public void Tint(Color color)
        {
            if (background != null)
            {
                background.color = color;
            }
        }

        /// <summary>이 챕터에 그 자리가 없을 때. 노드를 통째로 감춘다.</summary>
        public void Hide()
        {
            StageIndex = -1;

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
