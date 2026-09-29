using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// <b>스테이지</b>를 깼을 때 뜨는 클리어 화면.
    ///
    /// 팀 확정 구조(2026-09-22): 1스테이지 = 3웨이브다. 웨이브를 깰 때마다 뜨는 게 아니라
    /// 스테이지의 마지막 웨이브를 깼을 때만 뜬다 — 중간 웨이브 뒤에는 상점(ShopView)이 뜬다.
    ///
    /// 클리어 판정 자체는 Core의 WaveRunner가 한다. 여기는 그 상태를 보고 패널을 켜고,
    /// 버튼 입력을 다시 세션에 돌려줄 뿐이다(Hard Rule 5).
    ///
    /// 패널의 위치·크기는 씬에서 정한다 — 여기서는 활성 상태와 문자열만 만진다(Hard Rule 2).
    /// </summary>
    public sealed class StageClearView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("구독할 세션. 씬에서 연결한다.")]
        private GameSession session;

        [SerializeField, Tooltip("클리어 기록을 남길 곳. 지도의 clear 표시가 여기서 나온다.")]
        private RunProgress runProgress;

        [Header("표시 대상 (씬에서 배치)")]
        [SerializeField, Tooltip("클리어 화면 전체. 평소에는 꺼져 있어야 한다.")]
        private GameObject panel;

        [SerializeField, Tooltip("'1-1 클리어!' 같은 제목.")]
        private Text titleLabel;

        [SerializeField, Tooltip("'다음 스테이지로 이동하시겠습니까?' 같은 안내.")]
        private Text messageLabel;

        [SerializeField, Tooltip("(선택) '획득 골드 24' 같은 보상 표시. 비우면 표시하지 않는다.")]
        private Text rewardLabel;

        [SerializeField, Tooltip("다음 스테이지로 넘어가는 버튼. 마지막 스테이지면 자동으로 꺼진다.")]
        private Button nextButton;

        [SerializeField, Tooltip("마을로 돌아가는 버튼.")]
        private Button villageButton;

        [Header("문구")]
        [SerializeField] private string titleFormat = "스테이지 {0} 클리어!";
        [SerializeField] private string rewardFormat = "획득 골드 {0}   (보유 {1})";
        [SerializeField] private string nextMessage = "다음 스테이지로 이동하시겠습니까?";
        [SerializeField, Tooltip("마지막 스테이지를 깼을 때의 안내.")]
        private string lastMessage = "모든 스테이지를 클리어했습니다!";

        [Header("이동")]
        [SerializeField, Tooltip("마을 씬 이름. Build Settings에 등록돼 있어야 한다.")]
        private string villageSceneName = "Village";

        /// <summary>이번 스테이지 골드를 이미 적립했는지. 패널이 여러 번 떠도 두 번 넣지 않는다.</summary>
        private bool _banked;

        private void OnEnable()
        {
            if (session != null)
            {
                session.RunStarted += HandleRunStarted;
                session.Stepped += HandleStepped;
            }

            if (nextButton != null) { nextButton.onClick.AddListener(GoToNextStage); }
            if (villageButton != null) { villageButton.onClick.AddListener(GoToVillage); }

            Hide();
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.RunStarted -= HandleRunStarted;
                session.Stepped -= HandleStepped;
            }

            if (nextButton != null) { nextButton.onClick.RemoveListener(GoToNextStage); }
            if (villageButton != null) { villageButton.onClick.RemoveListener(GoToVillage); }
        }

        private void HandleRunStarted(GameLoop loop)
        {
            Hide();
            _banked = false;
        }

        private void HandleStepped(StepResult result)
        {
            if (session == null || session.Loop == null)
            {
                return;
            }

            // 죽었을 때 번 돈을 살릴지는 설정이다. 기본은 잃는다(로그라이트 관례).
            if (result.IsGameOver)
            {
                if (!_banked && runProgress != null && runProgress.KeepGoldOnDefeat)
                {
                    _banked = true;
                    runProgress.AddGold(session.Loop.Player.Gold);
                }

                return;
            }

            // 웨이브만 깬 것이면 상점 차례다(ShopView가 받는다).
            // 스테이지의 <b>마지막</b> 웨이브를 깼을 때만 여기서 클리어 화면을 띄운다.
            WaveRunner waves = session.Loop.Waves;
            if (waves == null || !waves.IsStageCleared)
            {
                return;
            }

            Show(waves);
        }

        private void Show(WaveRunner waves)
        {
            int index = waves.Index;

            // 이번 판에서 번 돈을 총액에 적립한다. 죽었을 때 어떻게 할지는 RunProgress의 설정이다.
            int earned = session.Loop.Player.Gold;
            if (runProgress != null && !_banked)
            {
                _banked = true;
                runProgress.MarkCleared(index);
                runProgress.AddGold(earned);

                // 다음 스테이지부터도 체력·폭탄과 함께 지갑이 그대로 이어진다(로그라이크: 실패해야만 비워짐).
                // 방금 적립한 돈을 지갑에 그대로 두면 다음 스테이지 클리어 때 또 적립돼 버린다.
                session.Loop.Player.BankGold();
            }

            if (rewardLabel != null)
            {
                rewardLabel.text = string.Format(
                    rewardFormat, earned, runProgress != null ? runProgress.Gold : earned);
            }

            // 깬 것은 웨이브가 아니라 <b>스테이지</b>다(1스테이지 = 3웨이브).
            string label = runProgress != null
                ? runProgress.StageLabelForWave(index)
                : (waves.Current != null ? waves.Current.StageNumber.ToString() : string.Empty);

            if (titleLabel != null)
            {
                titleLabel.text = string.Format(titleFormat, label);
            }

            bool hasNext = waves.HasNext;

            if (messageLabel != null)
            {
                messageLabel.text = hasNext ? nextMessage : lastMessage;
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(hasNext);
            }

            if (panel != null && !panel.activeSelf)
            {
                panel.SetActive(true);
            }
        }

        private void Hide()
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
        }

        /// <summary>
        /// 다음 스테이지로. 보드는 새로 깔리지만(WaveRunner가 다음 웨이브로 넘어간다),
        /// <b>플레이어는 그대로다</b> — 체력·폭탄·아이템이 스테이지를 넘어 이어진다
        /// (로그라이크 방식, 팀 요청: 실패해야만 비워진다). 그래서 상점 나갈 때와 같은 경로인
        /// StartNextWave를 그대로 쓴다 — 여기서 따로 런을 새로 시작하지 않는다.
        /// </summary>
        private void GoToNextStage()
        {
            if (session == null || session.Loop == null)
            {
                return;
            }

            if (!session.StartNextWave())
            {
                GoToVillage();
                return;
            }

            if (runProgress != null)
            {
                runProgress.Select(session.Loop.Waves.Index);
            }

            Hide();
        }

        private void GoToVillage()
        {
            if (string.IsNullOrEmpty(villageSceneName))
            {
                Debug.LogWarning($"{nameof(StageClearView)}: villageSceneName이 비어 있습니다.", this);
                return;
            }

            SceneManager.LoadScene(villageSceneName);
        }
    }
}
