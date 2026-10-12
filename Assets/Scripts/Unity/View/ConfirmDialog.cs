using System;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// "정말 할까요?" 확인 창. 문구를 받아 띄우고, 확인을 누르면 넘겨받은 일을 한다.
    ///
    /// 화면 전체를 덮는 어두운 바닥이 뒤쪽 UI의 터치를 막는다 — 창이 떠 있는 동안
    /// 인벤토리를 또 끌거나 버튼을 누르는 일이 없게 하려는 것이다.
    ///
    /// 무엇을 확인받을지는 부르는 쪽이 정한다. 여기는 띄우고 닫는 것만 한다.
    /// 창의 위치·크기는 씬이 정한다(Hard Rule 2).
    /// </summary>
    public sealed class ConfirmDialog : MonoBehaviour
    {
        [SerializeField, Tooltip("창 전체(어두운 바닥 포함). 평소에는 꺼져 있다.")]
        private GameObject panel;

        [SerializeField] private Text messageLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [SerializeField, Tooltip("창이 떠 있는 동안 이동을 잠글 입력 라우터. 어두운 바닥은 터치만 막고 " +
                                 "키보드·스와이프는 그대로 통하므로 라우터를 직접 잠근다(RecipeView와 같은 방식).")]
        private InputRouter input;

        private Action _onConfirm;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (confirmButton != null) { confirmButton.onClick.AddListener(Confirm); }
            if (cancelButton != null) { cancelButton.onClick.AddListener(Cancel); }

            Hide();
        }

        private void OnDisable()
        {
            // 꺼지면서 잠근 채로 두면 판이 멈춘다. 떠 있던 요청은 취소된 것으로 친다.
            Hide();
        }

        /// <summary>창을 띄운다. 이미 떠 있으면 앞의 요청은 취소된 것으로 친다.</summary>
        public void Show(string message, Action onConfirm)
        {
            _onConfirm = onConfirm;

            if (messageLabel != null)
            {
                messageLabel.text = message;
            }

            if (panel != null)
            {
                panel.transform.SetAsLastSibling();     // 다른 UI보다 위에
                panel.SetActive(true);
            }

            SetInputBlocked(true);
        }

        private void Confirm()
        {
            // 먼저 닫고 비운 뒤에 부른다 — 할 일 안에서 다시 Show를 불러도 꼬이지 않게.
            Action action = _onConfirm;
            Hide();
            action?.Invoke();
        }

        private void Cancel()
        {
            Hide();
        }

        private void Hide()
        {
            _onConfirm = null;

            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }

            SetInputBlocked(false);
        }

        private void SetInputBlocked(bool blocked)
        {
            if (input != null)
            {
                input.SetInputBlocked(this, blocked);
            }
        }
    }
}
