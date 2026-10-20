using System.Collections.Generic;
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

        [SerializeField, Tooltip("정산 영수증. 데이터를 넘기면 채우고 등장 연출까지 한다.")]
        private ReceiptRevealAnimator receipt;

        [SerializeField, Tooltip("다음 스테이지로 넘어가는 버튼. 마지막 스테이지면 자동으로 꺼진다.")]
        private Button nextButton;

        [SerializeField, Tooltip("마을로 돌아가는 버튼.")]
        private Button villageButton;

        [Header("이동")]
        [SerializeField, Tooltip("마을 씬 이름. Build Settings에 등록돼 있어야 한다.")]
        private string villageSceneName = "Village";

        [SerializeField, Tooltip("화면이 떠 있는 동안 조작을 막을 라우터. 비워 두면 같은 오브젝트에서 찾는다.")]
        private InputRouter input;

        [SerializeField, Tooltip("마을로 갈 때 재료를 넘길 곳. 비워 두면 같은 오브젝트에서 찾는다.")]
        private ItemService items;

        /// <summary>이번 스테이지 골드를 이미 적립했는지. 패널이 여러 번 떠도 두 번 넣지 않는다.</summary>
        private bool _banked;

        // ---- 영수증에 찍을 스테이지 정산. 웨이브마다 루프가 새로 생기므로 여기서 모은다. ----

        private int _stageNumber = -1;
        private int _kills;
        private readonly List<string> _materialOrder = new List<string>();
        private readonly Dictionary<string, int> _materialCounts = new Dictionary<string, int>();

        /// <summary>웨이브를 실제로 싸운 시간의 합. 웨이브 사이 상점에 머문 시간은 빠진다.</summary>
        private float _elapsed;
        private float _waveStart;
        private bool _waveTimed;

        private MaterialDropResolver _drops;

        /// <summary>적립 직후 만든 정산. 패널이 다시 떠도 같은 숫자를 보여 준다.</summary>
        private StageReceiptData _receiptData;

        private void OnEnable()
        {
            // 기본 배치는 GameSession과 같은 오브젝트다. 안 꽂혀 있으면 거기서 집어 온다.
            if (input == null)
            {
                input = GetComponent<InputRouter>();
            }

            if (items == null)
            {
                items = GetComponent<ItemService>();
            }

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

            SubscribeDrops(null);

            // 꺼지면서 잠근 채로 두면 판이 멈춘다.
            SetInputBlocked(false);
        }

        /// <summary>
        /// 클리어 화면이 떠 있는 동안 방향 입력을 잠근다.
        ///
        /// 웨이브는 끝났지만 <b>루프는 아직 살아 있어서</b>, 막지 않으면 패널 뒤에서
        /// 플레이어가 계속 움직이고 보드도 같이 진행한다. 레이캐스트는 터치만 막고
        /// 키보드는 그대로 통하므로 라우터를 직접 잠근다.
        /// </summary>
        private void SetInputBlocked(bool blocked)
        {
            if (input != null)
            {
                input.SetInputBlocked(this, blocked);
            }
        }

        private void HandleRunStarted(GameLoop loop)
        {
            Hide();
            _banked = false;

            SubscribeDrops(loop);

            // 웨이브마다 불린다. 정산은 스테이지가 바뀌었거나 새 런일 때만 비운다.
            int stage = loop != null && loop.Waves != null && loop.Waves.Current != null
                ? loop.Waves.Current.StageNumber
                : -1;

            if (session.IsNewRun || stage != _stageNumber)
            {
                _stageNumber = stage;
                _kills = 0;
                _elapsed = 0f;
                _materialOrder.Clear();
                _materialCounts.Clear();
            }

            _waveStart = Time.unscaledTime;
            _waveTimed = false;
        }

        private void SubscribeDrops(GameLoop loop)
        {
            if (_drops != null)
            {
                _drops.Dropped -= HandleMaterialDropped;
            }

            _drops = loop != null ? loop.MaterialDrops : null;

            if (_drops != null)
            {
                _drops.Dropped += HandleMaterialDropped;
            }
        }

        private void HandleMaterialDropped(string id)
        {
            int count;
            if (!_materialCounts.TryGetValue(id, out count))
            {
                _materialOrder.Add(id);
            }

            _materialCounts[id] = count + 1;
        }

        private void HandleStepped(StepResult result)
        {
            if (session == null || session.Loop == null)
            {
                return;
            }

            if (!result.IsGameOver)
            {
                _kills += result.EnemiesKilled;

                WaveRunner current = session.Loop.Waves;
                if (current != null && current.IsWaveCleared && !_waveTimed)
                {
                    _waveTimed = true;
                    _elapsed += Time.unscaledTime - _waveStart;
                }
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

            if (!_banked)
            {
                // 이번 판에서 번 돈을 총액에 적립한다. 죽었을 때 어떻게 할지는 RunProgress의 설정이다.
                int earned = session.Loop.Player.Gold;
                int previous = runProgress != null ? runProgress.Gold : 0;

                if (runProgress != null)
                {
                    _banked = true;
                    runProgress.MarkCleared(index);
                    runProgress.AddGold(earned);

                    // 다음 스테이지부터도 체력·폭탄과 함께 지갑이 그대로 이어진다(로그라이크: 실패해야만 비워짐).
                    // 방금 적립한 돈을 지갑에 그대로 두면 다음 스테이지 클리어 때 또 적립돼 버린다.
                    session.Loop.Player.BankGold();
                }

                _receiptData = BuildReceipt(waves, earned, previous);
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(waves.HasNext);
            }

            if (panel != null && !panel.activeSelf)
            {
                panel.SetActive(true);
            }

            if (receipt != null)
            {
                receipt.Play(_receiptData);
            }

            SetInputBlocked(true);
        }

        private StageReceiptData BuildReceipt(WaveRunner waves, int earned, int previous)
        {
            // 깬 것은 웨이브가 아니라 <b>스테이지</b>다(1스테이지 = 3웨이브).
            string label = runProgress != null
                ? runProgress.StageLabelForWave(waves.Index)
                : (waves.Current != null ? waves.Current.StageNumber.ToString() : string.Empty);

            var materials = new List<StageReceiptData.Material>(_materialOrder.Count);
            for (int i = 0; i < _materialOrder.Count; i++)
            {
                string id = _materialOrder[i];
                ItemConfig.Entry entry = default(ItemConfig.Entry);
                bool known = items != null && items.Catalog != null && items.Catalog.TryGet(id, out entry);
                materials.Add(new StageReceiptData.Material
                {
                    Id = id,
                    DisplayName = known ? entry.displayName : id,
                    Count = _materialCounts[id],
                });
            }

            return new StageReceiptData
            {
                StageLabel = label,
                EnemiesKilled = _kills,
                Materials = materials,
                ClearSeconds = _elapsed,
                EarnedGold = earned,
                PreviousGold = previous,
            };
        }

        private void Hide()
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }

            SetInputBlocked(false);
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

            // 씬을 갈아타면 인벤토리는 사라진다. 넘어가기 <b>전에</b> 재료를 마을에 맡긴다.
            // 유물은 여기서 버려진다 — 런 한정이라는 전제 그대로다.
            if (items != null)
            {
                items.DepositToVillage();
            }

            SceneManager.LoadScene(villageSceneName);
        }
    }
}
