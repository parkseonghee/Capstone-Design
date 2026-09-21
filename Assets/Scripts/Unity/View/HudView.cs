using UnityEngine;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 플레이어 HP · 골드 · 폭탄 수 · 게임오버 상태를 표시한다.
    ///
    /// 라벨·패널·버튼의 위치와 앵커는 전부 씬에서 정한다.
    /// 이 스크립트는 문자열과 활성 상태만 바꾼다 — RectTransform은 읽지도 쓰지도 않는다(Hard Rule 2).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameSession session;

        [Header("표시 대상 (씬에서 배치)")]
        [SerializeField, Tooltip("비워 두면 HP를 표시하지 않는다.")] private Text hpLabel;

        [SerializeField, Tooltip("이번 판에서 번 골드. 비워 두면 표시하지 않는다.")]
        private Text goldLabel;

        [SerializeField, Tooltip("들고 있는 폭탄 수. 비워 두면 표시하지 않는다.")]
        private Text bombLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Text gameOverLabel;
        [SerializeField] private Button restartButton;

        [Header("문구")]
        [SerializeField] private string hpFormat = "HP {0}/{1}";
        [SerializeField] private string goldFormat = "G {0}";
        [SerializeField] private string bombFormat = "{0}";
        [SerializeField] private string boardFullText = "보드가 가득 찼습니다";
        [SerializeField] private string trappedText = "빠져나갈 곳이 없습니다";
        [SerializeField] private string playerDeadText = "체력이 바닥났습니다";
        [SerializeField] private string blockedHint = "막혔습니다";
        [SerializeField] private string outOfBoundsHint = "보드 밖입니다";

        private int _shownHp = -1;
        private int _shownGold = -1;
        private int _shownBombs = -1;

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
            _shownHp = -1;
            _shownGold = -1;
            _shownBombs = -1;
            SetHp(loop.Player);
            SetGold(loop.Player);
            SetBombs(loop.Player);
            SetStatus(string.Empty);
            ShowGameOver(GameOverReason.None);
        }

        private void HandleStepped(StepResult result)
        {
            SetHp(session.Loop.Player);
            SetGold(session.Loop.Player);
            SetBombs(session.Loop.Player);

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

        private void SetGold(Player player)
        {
            // 값이 그대로면 문자열을 새로 만들지 않는다(Hard Rule 8).
            if (goldLabel == null || player == null || _shownGold == player.Gold)
            {
                return;
            }

            _shownGold = player.Gold;
            goldLabel.text = string.Format(goldFormat, player.Gold);
        }

        private void SetBombs(Player player)
        {
            // 값이 그대로면 문자열을 새로 만들지 않는다(Hard Rule 8).
            if (bombLabel == null || player == null || _shownBombs == player.Bombs)
            {
                return;
            }

            _shownBombs = player.Bombs;
            bombLabel.text = string.Format(bombFormat, player.Bombs);
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
