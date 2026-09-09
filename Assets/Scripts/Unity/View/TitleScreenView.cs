using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 시작화면의 상태 전환만 담당한다: 설정 팝업 열기/닫기.
    ///
    /// 화면을 탭해 마을로 이동하는 동작은 전체 화면을 덮는 탭 캐처(TapCatcher)의
    /// SceneTransitionButton이 처리한다. 설정 버튼이 계층상 탭 캐처보다 위(나중 자식)에 있어
    /// UI 레이캐스트가 자연히 설정 버튼을 먼저 판정한다 — 그 우선순위를 코드로 만들지 않는다(Hard Rule 2).
    ///
    /// 라벨·패널의 위치와 앵커는 전부 씬에서 정한다. 이 스크립트는 활성 상태만 바꾼다.
    /// </summary>
    public sealed class TitleScreenView : MonoBehaviour
    {
        [Header("연결 (씬에서 배치)")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button settingsCloseButton;

        private void Awake()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OpenSettings);
            }

            if (settingsCloseButton != null)
            {
                settingsCloseButton.onClick.AddListener(CloseSettings);
            }
        }

        private void OnDisable()
        {
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OpenSettings);
            }

            if (settingsCloseButton != null)
            {
                settingsCloseButton.onClick.RemoveListener(CloseSettings);
            }
        }

        private void OpenSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
        }

        private void CloseSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }
    }
}
