using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 재료 고르기 창. 재료 전환기(C10)에 재료를 떨궜을 때 "무엇으로 바꿀까?"를 묻는다.
    ///
    ///   '고철'을 무엇으로 바꿀까요?
    ///   [펠릿] [펄프] [필름] [잔재]
    ///             [취소]
    ///
    /// 고르면 넘겨받은 일을 하고 닫힌다. 취소하면 아무 일도 없다(전환기도 재료도 그대로).
    /// 떠 있는 동안 화면 바닥이 터치를 막고, 입력 라우터를 잠가 이동도 막는다(ConfirmDialog와 같은 방식).
    /// </summary>
    public sealed class MaterialPickerView : MonoBehaviour
    {
        /// <summary>고를 수 있는 버튼 하나.</summary>
        [Serializable]
        public struct Option
        {
            public Button button;
            public Text label;
        }

        [SerializeField, Tooltip("창 전체(어두운 바닥 포함). 평소에는 꺼져 있다.")]
        private GameObject panel;

        [SerializeField] private Text titleLabel;
        [SerializeField] private Option[] options = new Option[0];
        [SerializeField] private Button cancelButton;

        [SerializeField, Tooltip("떠 있는 동안 이동을 잠글 입력 라우터.")]
        private InputRouter input;

        private readonly List<string> _ids = new List<string>(8);
        private Action<string> _onPick;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].button == null) { continue; }

                int index = i;
                options[i].button.onClick.AddListener(delegate { Pick(index); });
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(Hide);
            }

            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        /// <summary>창을 띄운다. 후보가 버튼보다 많으면 앞에서부터만 보인다.</summary>
        public void Show(string title, IReadOnlyList<ItemConfig.Entry> choices, Action<string> onPick)
        {
            _onPick = onPick;
            _ids.Clear();

            if (titleLabel != null)
            {
                titleLabel.text = title;
            }

            for (int i = 0; i < options.Length; i++)
            {
                bool used = choices != null && i < choices.Count;
                if (options[i].button != null && options[i].button.gameObject.activeSelf != used)
                {
                    options[i].button.gameObject.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                _ids.Add(choices[i].id);
                if (options[i].label != null)
                {
                    options[i].label.text = choices[i].displayName;
                }
            }

            if (panel != null)
            {
                panel.transform.SetAsLastSibling();
                panel.SetActive(true);
            }

            if (input != null)
            {
                input.SetInputBlocked(this, true);
            }
        }

        private void Pick(int index)
        {
            if (index < 0 || index >= _ids.Count)
            {
                return;
            }

            // 먼저 닫고 비운 뒤에 부른다 — 할 일 안에서 다시 Show를 불러도 꼬이지 않게.
            Action<string> action = _onPick;
            string id = _ids[index];
            Hide();
            action?.Invoke(id);
        }

        private void Hide()
        {
            _onPick = null;

            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }

            if (input != null)
            {
                input.SetInputBlocked(this, false);
            }
        }
    }
}
