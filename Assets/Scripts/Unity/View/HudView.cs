using UnityEngine;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 스텝 수 · 플레이어 HP · 게임오버 상태를 표시한다.
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
        [SerializeField, Tooltip("비워 두면 HP를 표시하지 않는다.")] private Text hpLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Text gameOverLabel;
        [SerializeField] private Button restartButton;

        [Header("문구")]
        [SerializeField] private string stepFormat = "STEP {0}";
        [SerializeField] private string hpFormat = "HP {0}/{1}";
        [SerializeField] private string boardFullText = "보드가 가득 찼습니다";
        [SerializeField] private string trappedText = "빠져나갈 곳이 없습니다";
        [SerializeField] private string playerDeadText = "체력이 바닥났습니다";
        [SerializeField] private string blockedHint = "막혔습니다";
        [SerializeField] private string outOfBoundsHint = "보드 밖입니다";

        private int _shownStep = -1;
        private int _shownHp = -1;

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
            _shownHp = -1;
            SetStep(loop.StepCount);
            SetHp(loop.Player);
            SetStatus(string.Empty);
            ShowGameOver(GameOverReason.None);
        }

        private void HandleStepped(StepResult result)
        {
            SetStep(session.Loop.StepCount);
            SetHp(session.Loop.Player);

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

        private void SetHp(Player player)
        {
            // 값이 그대로면 문자열을 새로 만들지 않는다(Hard Rule 8).
            if (hpLabel == null || player == null || _shownHp == player.Hp)
            {
                return;
            }

            _shownHp = player.Hp;
            hpLabel.text = string.Format(hpFormat, player.Hp, player.MaxHp);
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
                gameOverLabel.text = TextFor(reason);
            }
        }

        /// <summary>패배 사유 -> 문구. 로직이 아니라 표시 문자열 매핑이라 여기 둔다.</summary>
        private string TextFor(GameOverReason reason)
        {
            switch (reason)
            {
                case GameOverReason.BoardFull: return boardFullText;
                case GameOverReason.PlayerDead: return playerDeadText;
                default: return trappedText;
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
