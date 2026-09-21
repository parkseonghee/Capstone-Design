using System;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// Core 배선을 씬에 연결하는 조립 지점. 게임 규칙은 한 줄도 여기 없다(Hard Rule 5).
    ///
    /// 시간이 개입하는 유일한 부분이 시작 연출이다. Core는 "초기 줄을 한 줄 떨어뜨린다"는
    /// 동작만 제공하고, 그걸 <b>몇 초 간격으로 부를지</b>는 여기서 정한다.
    ///
    /// 뷰를 참조하지 않는다 — 뷰가 이 컴포넌트의 이벤트를 구독한다(Hard Rule 4).
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField, Tooltip("보드 규격(8x9, 프리뷰 1줄)과 시작 상태. 값은 전부 이 에셋에서 읽는다.")]
        private GridConfig config;

        [SerializeField, Tooltip("스폰 캐이던스(15개까지 매 턴 → 이후 2턴당 1개). 밸런스 전용 에셋.")]
        private SpawnConfig spawnConfig;

        [SerializeField, Tooltip("쓰레기 종류별 HP·공격력. 전투 밸런스는 전부 이 에셋에서 잡는다.")]
        private TrashStatsConfig trashStats;

        [SerializeField, Tooltip("플레이할 캐릭터. 체력·공격력·공격 범위를 담는다. " +
                                 "캐릭터를 바꾸려면 다른 CharacterConfig 에셋을 꽂으면 된다.")]
        private CharacterConfig character;

        [SerializeField, Tooltip("폭탄 설정 — 폭발 범위·피해·기폭 턴·시작 보유 개수.")]
        private BombConfig bombConfig;

        [SerializeField, Tooltip("웨이브 목록(3스테이지 × 3웨이브). 비워 두면 웨이브 없이 무한히 돈다.")]
        private WaveSet waveSet;

        [SerializeField, Tooltip("마을 지도에서 고른 스테이지를 읽어 올 곳. " +
                                 "비워 두면 항상 첫 웨이브부터 연속으로 달린다(지도 없이 바로 플레이할 때).")]
        private RunProgress runProgress;

        [Header("입력")]
        [SerializeField, Tooltip("방향 입력을 흘려보내는 라우터. 씬에서 연결한다.")]
        private InputRouter input;

        [Header("시작 연출")]
        [SerializeField, Min(0f), Tooltip("초기 줄이 한 칸 내려오는 간격(초). 0이면 연출 없이 즉시 시작한다.")]
        private float introFallInterval = 0.06f;

        [SerializeField, Min(0f), Tooltip("한 줄이 다 정착한 뒤 다음 줄이 나오기까지의 여유(초).")]
        private float introRowInterval = 0.3f;

        [SerializeField, Min(0f), Tooltip("마지막 줄이 정착한 뒤 플레이어가 등장하기까지의 여유(초).")]
        private float introPlayerDelay = 0.35f;

        [SerializeField, Tooltip("시작 연출이 도는 동안 입력을 무시할지.")]
        private bool blockInputDuringIntro = true;

        [Header("난수 (WEEK1 §0: 결정론 확인용)")]
        [SerializeField, Tooltip("켜면 항상 같은 시드로 시작해 같은 판이 재현된다. 버그 재현에 쓴다.")]
        private bool useFixedSeed;

        [SerializeField, Tooltip("useFixedSeed가 켜졌을 때 쓰는 시드.")]
        private int fixedSeed = 20260903;

        /// <summary>새 런이 시작될 때. 뷰는 여기서 보드를 처음부터 다시 그린다.</summary>
        public event Action<GameLoop> RunStarted;

        /// <summary>시작 연출로 보드가 바뀌었을 때(줄 하나 낙하, 플레이어 등장).</summary>
        public event Action BoardChanged;

        /// <summary>시작 연출이 끝나 입력을 받기 시작할 때.</summary>
        public event Action IntroFinished;

        /// <summary>한 번의 입력 처리가 끝났을 때. 거부된 입력도 포함된다(Advanced로 구분).</summary>
        public event Action<StepResult> Stepped;

        public GameLoop Loop { get; private set; }

        /// <summary>이번 런에 실제로 쓰인 시드. 재현이 필요할 때 이 값을 남긴다.</summary>
        public int CurrentSeed { get; private set; }

        public bool IsIntroPlaying => Loop != null && !Loop.IsReady;

        private float _introTimer;

        /// <summary>줄 하나가 정착한 직후의 한 박자 쉼을 이미 줬는지.</summary>
        private bool _restedAfterRow;

        private void OnEnable()
        {
            if (input != null)
            {
                input.DirectionPressed += HandleDirection;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.DirectionPressed -= HandleDirection;
            }
        }

        private void Start()
        {
            // 모든 뷰의 OnEnable(구독)이 끝난 뒤에 첫 런을 시작해야 RunStarted를 놓치지 않는다.
            StartNewRun();
        }

        private void Update()
        {
            if (Loop == null || Loop.IsReady)
            {
                return;
            }

            _introTimer -= Time.deltaTime;
            if (_introTimer > 0f)
            {
                return;
            }

            AdvanceIntro();
        }

        /// <summary>재시작 버튼이 호출한다.</summary>
        public void StartNewRun()
        {
            if (config == null)
            {
                Debug.LogError($"{nameof(GameSession)}: GridConfig가 비어 있습니다. 인스펙터에서 연결해 주세요.", this);
                return;
            }

            if (spawnConfig == null)
            {
                Debug.LogError($"{nameof(GameSession)}: SpawnConfig가 비어 있습니다. 인스펙터에서 연결해 주세요.", this);
                return;
            }

            if (trashStats == null)
            {
                Debug.LogError($"{nameof(GameSession)}: TrashStatsConfig가 비어 있습니다. 인스펙터에서 연결해 주세요.", this);
                return;
            }

            if (character == null)
            {
                Debug.LogError($"{nameof(GameSession)}: CharacterConfig가 비어 있습니다. 인스펙터에서 연결해 주세요.", this);
                return;
            }

            if (bombConfig == null)
            {
                Debug.LogError($"{nameof(GameSession)}: BombConfig가 비어 있습니다. 인스펙터에서 연결해 주세요.", this);
                return;
            }

            CurrentSeed = useFixedSeed ? fixedSeed : Environment.TickCount;

            // 웨이브는 선택이다. 안 꽂으면 기존처럼 전역 스폰 표로 무한히 돈다.
            //
            // RunProgress가 꽂혀 있으면 마을 지도에서 고른 스테이지 하나만 플레이한다.
            // 깨면 거기서 멈추고(StageClearView가 받는다), 다음으로 넘어갈지는 플레이어가 고른다.
            WaveRunner waves = null;
            if (waveSet != null)
            {
                waves = runProgress != null
                    ? waveSet.CreateRunner(runProgress.SelectedIndex, stopAfterEachWave: true)
                    : waveSet.CreateRunner();
            }

            Loop = GameLoopFactory.CreateStaged(
                config, spawnConfig, trashStats, character, bombConfig,
                new SystemRandomSource(CurrentSeed), waves);

            // 첫 줄은 아래의 introRowInterval 대기만 거치고 바로 나오게 한다
            // (빈 보드에서 쉬는 박자를 한 번 더 먹지 않도록).
            _restedAfterRow = true;
            RunStarted?.Invoke(Loop);

            if (introFallInterval <= 0f)
            {
                Loop.CompleteSetup();
                BoardChanged?.Invoke();
                IntroFinished?.Invoke();
                return;
            }

            // 첫 줄은 조금 뜸을 들였다가 내보낸다.
            _introTimer = introRowInterval;
        }

        /// <summary>
        /// 시작 연출을 한 칸 진행시킨다.
        /// 우선순위: 떨어지는 중이면 한 칸 낙하 → 다 정착했고 남은 줄이 있으면 다음 줄 투입 →
        /// 전부 끝났으면 플레이어 등장.
        /// </summary>
        private void AdvanceIntro()
        {
            if (Loop.TickGravity() > 0)
            {
                BoardChanged?.Invoke();
                _introTimer = introFallInterval;
                return;
            }

            // 여기 왔다는 건 보드가 완전히 정착했다는 뜻이다. 한 박자 쉬어 준다.
            if (!_restedAfterRow)
            {
                _restedAfterRow = true;
                _introTimer = Loop.PendingSeedRows > 0 ? introRowInterval : introPlayerDelay;
                return;
            }

            _restedAfterRow = false;

            if (Loop.PendingSeedRows > 0)
            {
                Loop.SeedNextRow();
                BoardChanged?.Invoke();
                _introTimer = introFallInterval;
                return;
            }

            Loop.PlacePlayer();
            Loop.EvaluateInitialState();
            BoardChanged?.Invoke();
            IntroFinished?.Invoke();
        }

        /// <summary>
        /// 폭탄을 설치한다. 방향 입력과 같은 경로로 결과를 알리므로 뷰들이 똑같이 갱신된다.
        /// </summary>
        /// <returns>설치돼서 보드가 진행했으면 true.</returns>
        public bool TryPlaceBomb(UnityEngine.Vector2Int cell)
        {
            if (Loop == null || (blockInputDuringIntro && IsIntroPlaying))
            {
                return false;
            }

            StepResult result = Loop.PlaceBomb(cell);
            Stepped?.Invoke(result);
            return result.Advanced;
        }

        /// <summary>
        /// 제자리에서 한 턴을 넘긴다. 대기(빨리 내리기) 버튼이 부른다.
        /// 방향 입력과 똑같이 한 턴을 쓰고 Stepped를 띄운다.
        /// </summary>
        /// <returns>실제로 턴이 진행됐으면 true. 인트로 중이거나 끝난 판이면 false.</returns>
        public bool Wait()
        {
            if (Loop == null || !Loop.IsReady || Loop.IsOver)
            {
                return false;
            }

            if (blockInputDuringIntro && IsIntroPlaying)
            {
                return false;
            }

            StepResult result = Loop.Wait();
            Stepped?.Invoke(result);
            return result.Advanced;
        }

        private void HandleDirection(Direction direction)
        {
            if (Loop == null)
            {
                return;
            }

            if (blockInputDuringIntro && IsIntroPlaying)
            {
                return;
            }

            StepResult result = Loop.Step(direction);
            Stepped?.Invoke(result);
        }
    }
}
