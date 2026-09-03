using UnityEngine;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 화면 D-Pad 버튼 하나를 방향 입력으로 연결한다.
    ///
    /// 버튼의 위치·크기·앵커는 전부 씬에서 정한다. 이 스크립트는 RectTransform을
    /// 절대 건드리지 않고 onClick 배선만 한다(Hard Rule 2).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class DirectionButton : MonoBehaviour
    {
        [SerializeField, Tooltip("이 버튼이 보낼 방향.")]
        private Direction direction = Direction.Up;

        [SerializeField, Tooltip("입력을 받을 라우터. 씬에서 연결한다.")]
        private InputRouter router;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(Press);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(Press);
        }

        private void Press()
        {
            if (router == null)
            {
                Debug.LogWarning($"{nameof(DirectionButton)}: InputRouter가 연결되지 않았습니다.", this);
                return;
            }

            router.Emit(direction);
        }
    }
}
