using UnityEngine;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 스텝 수와 게임오버 상태만 표시한다.
    ///
    /// 라벨·패널·버튼의 위치와 앵커는 전부 씬에서 정한다.
    /// 이 스크립트는 문자열과 활성 상태만 바꾼다 — RectTransform은 읽지도 쓰지도 않는다(Hard Rule 2).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameSession session;

        [Header("표시 대상 (씬에서 배치)")]
        [SerializeField] private Text stepLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Text gameOverLabel;
        [SerializeField] private Button restartButton;

        [Header("문구")]
        [SerializeField] private string stepFormat = "STEP {0}";
        [SerializeField] private string boardFullText = "보드가 가득 찼습니다";
        [SerializeField] private string trappedText = "빠져나갈 곳이 없습니다";
        [SerializeField] private string blockedHint = "막혔습니다";
        [SerializeField] private string outOfBoundsHint = "보드 밖입니다";

        private int _shownStep = -1;

        private void OnEnable()
        {
            if (session != null)
            {
                session.RunStarted += HandleRunStarted;
                session.Stepped += HandleStepped;
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.RunStarted -= HandleRunStarted;
                session.Stepped -= HandleStepped;
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(Restart);
            }
        }

        private void HandleRunStarted(GameLoop loop)
        {
            _shownStep = -1;
            SetStep(loop.StepCount);
            SetStatus(string.Empty);
            ShowGameOver(GameOverReason.None);
        }

        private void HandleStepped(StepResult result)
        {
            SetStep(session.Loop.StepCount);

            if (!result.Advanced && !result.IsGameOver)
            {
                SetStatus(result.Move == MoveOutcome.OutOfBounds ? outOfBoundsHint : blockedHint);
            }
            else
            {
                SetStatus(string.Empty);
            }

            ShowGameOver(result.GameOver);
        }

        private void SetStep(int step)
        {
            // 값이 그대로면 문자열을 새로 만들지 않는다(Hard Rule 8).
            if (stepLabel == null || _shownStep == step)
            {
                return;
            }

            _shownStep = step;
            stepLabel.text = string.Format(stepFormat, step);
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null)
            {
                statusLabel.text = text;
            }
        }

        private void ShowGameOver(GameOverReason reason)
        {
            bool over = reason != GameOverReason.None;

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(over);
            }

            if (over && gameOverLabel != null)
            {
                gameOverLabel.text = reason == GameOverReason.BoardFull ? boardFullText : trappedText;
            }
        }

        private void Restart()
        {
            if (session != null)
            {
                session.StartNewRun();
            }
        }
    }
}
