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
    ///
    /// <b>패배</b>했을 때는 같은 영수증을 빨갛게 칠한 실패 화면(FAIL 도장)을 띄운다.
    /// 정산(처치 수·재료·시간)은 여기서 모으고 있으므로 실패 화면도 여기서 함께 다룬다.
    /// 실패 화면의 '다시 시작'은 1스테이지로 되돌아가고(RestartAfterDefeat), '마을로'는 마을로 돌아간다.
    ///
    /// <b>런 종료 정산</b>(재화 문서 §1·§2): 남은 재료를 스크랩으로 녹이고, 스크랩 전체를 1:1로 골드에 이관한다.
    /// 정산은 <b>런이 실제로 끝날 때</b>만 한다 — 죽었을 때, 또는 클리어 후 '마을로'를 눌렀을 때.
    /// 클리어 후 '다음 스테이지'를 누르면 예전처럼 런이 이어진다(팀 요청) — 체력·폭탄·아이템·스크랩·재료가
    /// 그대로 넘어가고, 정산은 나중에 마을로 가거나 죽을 때 한 번에 한다.
    /// 그래서 클리어 영수증의 골드는 "지금 마을로 가면 받을 골드"를 미리 보여 주는 값이다.
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

        [SerializeField, Tooltip("다음 스테이지로 이어 가는 버튼. 정산하지 않고 런을 계속한다. 마지막 스테이지면 자동으로 꺼진다.")]
        private Button nextButton;

        [SerializeField, Tooltip("정산하고 마을로 돌아가는 버튼.")]
        private Button villageButton;

        [Header("실패 화면 (씬에서 배치)")]
        [SerializeField, Tooltip("패배했을 때 뜨는 화면. 클리어 화면을 복제해 빨갛게 칠한 것. 평소에는 꺼져 있어야 한다.")]
        private GameObject failPanel;

        [SerializeField, Tooltip("실패 화면의 정산 영수증.")]
        private ReceiptRevealAnimator failReceipt;

        [SerializeField, Tooltip("1스테이지부터 다시 시작하는 버튼.")]
        private Button restartButton;

        [SerializeField, Tooltip("실패 화면에서 마을로 돌아가는 버튼.")]
        private Button failVillageButton;

        [Header("이동")]
        [SerializeField, Tooltip("마을 씬 이름. Build Settings에 등록돼 있어야 한다.")]
        private string villageSceneName = "Village";

        [SerializeField, Tooltip("화면이 떠 있는 동안 조작을 막을 라우터. 비워 두면 같은 오브젝트에서 찾는다.")]
        private InputRouter input;

        [SerializeField, Tooltip("정산할 때 재료를 녹일 인벤토리. 비워 두면 같은 오브젝트에서 찾는다.")]
        private ItemService items;

        /// <summary>이번 런을 이미 정산했는지. 패널이 여러 번 떠도 두 번 넣지 않는다.</summary>
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

        /// <summary>영수증에 찍을 정산. 패널이 다시 떠도 같은 숫자를 보여 준다. 클리어 때는 미리보기 값이다.</summary>
        private StageReceiptData _receiptData;

        /// <summary>이번 클리어의 영수증을 이미 만들었는지. 새 웨이브가 시작되면 지운다.</summary>
        private bool _receiptReady;

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
            if (restartButton != null) { restartButton.onClick.AddListener(Restart); }
            if (failVillageButton != null) { failVillageButton.onClick.AddListener(GoToVillage); }

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
            if (restartButton != null) { restartButton.onClick.RemoveListener(Restart); }
            if (failVillageButton != null) { failVillageButton.onClick.RemoveListener(GoToVillage); }

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
            _receiptReady = false;

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

            // 죽어도 런 종료다 — 클리어와 똑같이 정산한다(재화 문서 §1).
            if (result.IsGameOver)
            {
                ShowFail(session.Loop.Waves);
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

            if (!_receiptReady)
            {
                _receiptReady = true;
                if (runProgress != null)
                {
                    runProgress.MarkCleared(index);
                }

                // 아직 정산하지 않는다 — 다음 스테이지로 이어 갈 수도 있다. 마을로 갈 때 받을 값만 미리 찍는다.
                _receiptData = Preview(waves);
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

        /// <summary>
        /// 패배 화면. 클리어 영수증과 같은 정산을 FAIL 도장으로 찍는다.
        /// 죽어도 런 종료라 클리어와 똑같이 정산한다(재화 문서 §1).
        /// </summary>
        private void ShowFail(WaveRunner waves)
        {
            if (!_banked)
            {
                // 지금 싸우던 웨이브의 시간도 넣는다 — 깨지 못했으니 위에서 더해지지 않았다.
                if (!_waveTimed)
                {
                    _waveTimed = true;
                    _elapsed += Time.unscaledTime - _waveStart;
                }

                _receiptData = Settle(waves);
            }

            if (failPanel == null)
            {
                return;
            }

            if (!failPanel.activeSelf)
            {
                failPanel.SetActive(true);
            }

            if (failReceipt != null)
            {
                failReceipt.Play(_receiptData);
            }

            SetInputBlocked(true);
        }

        /// <summary>실패 화면의 '다시 시작'. 어느 스테이지에서 죽었든 1스테이지로 되돌아간다.</summary>
        private void Restart()
        {
            if (session == null)
            {
                return;
            }

            Hide();
            session.RestartAfterDefeat();
        }

        /// <summary>
        /// 런 종료 정산. 남은 재료 → 스크랩 → 골드(재화 문서 §1·§2).
        ///
        /// 녹인 직후 마을 보관함도 덮어쓴다. 보관함은 판에 들어갈 때 <b>복사</b>해 들고 들어오는데
        /// (ItemService.LoadStash), 그걸 이미 녹였으니 보관함에 남겨 두면 다음 런에 같은 재료가 또 나온다.
        /// '다시 시작'처럼 마을을 거치지 않는 경로도 있어서 씬을 떠날 때가 아니라 여기서 한다.
        /// </summary>
        private StageReceiptData Settle(WaveRunner waves)
        {
            _banked = true;

            int previous = runProgress != null ? runProgress.Gold : 0;
            int materialScrap = 0;
            if (items != null)
            {
                materialScrap = items.MeltAllMaterials();
                items.DepositToVillage();
            }

            RunSettlement settlement = RunSettlement.Settle(session.Loop.Player, materialScrap);
            if (runProgress != null)
            {
                runProgress.AddGold(settlement.Gold);
            }

            StageReceiptData data = waves != null
                ? BuildReceipt(waves, settlement.Gold, previous)
                : new StageReceiptData
                {
                    Materials = new List<StageReceiptData.Material>(),
                    EarnedGold = settlement.Gold,
                    PreviousGold = previous,
                };

            data.LeftoverScrap = settlement.LeftoverScrap;
            data.MaterialScrap = settlement.MaterialScrap;
            return data;
        }

        /// <summary>
        /// 지금 런을 끝내면 받을 정산을 지갑·인벤토리를 건드리지 않고 계산한다(클리어 영수증용).
        /// 실제 정산(<see cref="Settle"/>)과 같은 규칙이다 — 남은 스크랩 + 재료 변환 가치, 1:1.
        /// </summary>
        private StageReceiptData Preview(WaveRunner waves)
        {
            int previous = runProgress != null ? runProgress.Gold : 0;
            int leftover = Mathf.Max(0, session.Loop.Player.Scrap);
            int materialScrap = items != null ? items.MaterialScrapValue() : 0;
            int gold = (leftover + materialScrap) * RunSettlement.GoldPerScrap;

            StageReceiptData data = BuildReceipt(waves, gold, previous);
            data.LeftoverScrap = leftover;
            data.MaterialScrap = materialScrap;
            return data;
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

            if (failPanel != null && failPanel.activeSelf)
            {
                failPanel.SetActive(false);
            }

            SetInputBlocked(false);
        }

        /// <summary>
        /// 다음 스테이지로. 보드는 새로 깔리지만(WaveRunner가 다음 웨이브로 넘어간다),
        /// <b>플레이어는 그대로다</b> — 체력·폭탄·아이템·스크랩이 스테이지를 넘어 이어진다.
        /// 상점 나갈 때와 같은 경로인 StartNextWave를 그대로 쓴다. 정산은 하지 않는다 — 런이 안 끝났다.
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
            // 클리어 화면에서 오면 여기서 런이 끝난다 — 미리 보여 준 값 그대로 실제 정산한다.
            // (실패 화면에서 오면 이미 정산했다.)
            if (!_banked && session != null && session.Loop != null)
            {
                Settle(session.Loop.Waves);
            }

            if (string.IsNullOrEmpty(villageSceneName))
            {
                Debug.LogWarning($"{nameof(StageClearView)}: villageSceneName이 비어 있습니다.", this);
                return;
            }

            // 재료는 정산(Settle)에서 이미 녹였고 보관함도 그때 정리했다.
            // 유물·일회성은 씬을 갈아타며 사라진다 — 런 한정이라는 전제 그대로다.
            SceneManager.LoadScene(villageSceneName);
        }
    }
}
