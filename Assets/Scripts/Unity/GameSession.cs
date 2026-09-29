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

        [SerializeField, Tooltip("플레이할 캐릭터(기본값). 체력·공격력·공격 범위를 담는다. " +
                                 "characterSelection을 안 꽂으면 이 값을 그대로 쓴다.")]
        private CharacterConfig character;

        [SerializeField, Tooltip("마을에서 고른 캐릭터를 읽어 올 곳. 꽂아 두면 매 런 시작마다 " +
                                 "여기서 고른 캐릭터로 시작한다 — 비워 두면 위 character 필드를 그대로 쓴다.")]
        private CharacterSelection characterSelection;

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

        /// <summary>
        /// 방금 <see cref="RunStarted"/>가 진짜 새 런(웨이브·스테이지 인계가 아님)이었는지.
        /// RunStarted 구독자가 "이번에 상태를 비워야 하는지"를 판단할 유일하게 믿을 수 있는 값이다
        /// — Modifiers·인벤토리가 비어 있는지로 추측하면 안 된다(유물을 안 샀을 뿐인 경우와 구분이 안 됨).
        /// </summary>
        public bool IsNewRun { get; private set; }

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

        /// <summary>
        /// 다음 웨이브로 넘어간다. 상점에서 "다음 웨이브"를 눌렀을 때 부른다.
        ///
        /// 보드는 새로 깔리지만 <b>플레이어는 그대로다</b> — 체력·폭탄·골드가 이어진다.
        /// 스테이지 안에서 상태가 이어져야 상점이 의미를 갖기 때문이다
        /// (팀 확정 구조: 1스테이지 = 3웨이브 + 상점 2회).
        /// </summary>
        /// <returns>넘어갔으면 true. 스테이지의 마지막 웨이브였으면 false.</returns>
        public bool StartNextWave()
        {
            if (Loop == null || Loop.Waves == null || !Loop.Waves.AdvanceToNext())
            {
                return false;
            }

            _carryOver = Loop.Player;
            StartNewRun();
            return true;
        }

        /// <summary>다음 런에 물려줄 직전 플레이어. StartNewRun이 한 번 쓰고 비운다.</summary>
        private Player _carryOver;

        /// <summary>
        /// 이번 <b>스테이지</b>에 걸린 유물 효과. 웨이브 사이에는 유지되고,
        /// 마을에서 새 스테이지로 들어올 때 비워진다(유물은 런 한정이다).
        /// </summary>
        public RunModifiers Modifiers { get; private set; } = new RunModifiers();

        /// <summary>
        /// 게임오버 화면의 재시작 버튼이 부른다. 로그라이크 방식(팀 요청)이라
        /// 어느 스테이지에서 죽었든 <b>1스테이지로 되돌아간다</b>.
        ///
        /// 죽은 직후라 _carryOver가 비어 있으므로 StartNewRun이 Modifiers를 새로 만들고,
        /// 그걸 본 ItemService가 인벤토리를 비운다 — 이번 런에서 든 아이템은 여기서 사라진다.
        /// 클리어 기록·누적 골드(RunProgress)는 건드리지 않는다 — 실패해도 남는 메타 진행이다.
        /// </summary>
        public void RestartAfterDefeat()
        {
            if (runProgress != null)
            {
                runProgress.Select(0);
            }

            StartNewRun();
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

            // 마을에서 캐릭터를 골랐으면 그걸 쓰고, 아니면 인스펙터의 기본 캐릭터를 쓴다.
            ICharacterConfig resolvedCharacter = characterSelection != null && characterSelection.SelectedConfig != null
                ? characterSelection.SelectedConfig
                : character;

            if (resolvedCharacter == null)
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
            //
            // 웨이브 사이 인계 중이면 지도에서 고른 스테이지가 아니라
            // <b>직전 러너가 가리키던 웨이브</b>에서 이어야 한다.
            int startIndex = runProgress != null ? runProgress.SelectedIndex : 0;
            if (_carryOver != null && Loop != null && Loop.Waves != null)
            {
                startIndex = Loop.Waves.Index;
            }

            WaveRunner waves = null;
            if (waveSet != null)
            {
                waves = runProgress != null
                    ? waveSet.CreateRunner(startIndex, stopAfterEachWave: true)
                    : waveSet.CreateRunner();
            }

            // 웨이브 인계가 아니면 진짜 새 런이다 — 유물 초기화.
            //
            // RunStarted 구독자(ItemService 등)가 "이번이 새 런인지"를 직접 물을 수 있게
            // 여기서 확정해 둔다. 예전에는 Modifiers의 값이 전부 기본값인지로 추측했는데,
            // 유물을 하나도 안 사고 일회용 아이템만 산 상태에서는 그 값이 웨이브를 넘어가도
            // 항상 기본값이라 "새 런"으로 오판해 매 웨이브 인벤토리를 비워 버리는 버그가 있었다.
            IsNewRun = _carryOver == null;

            if (IsNewRun)
            {
                Modifiers = new RunModifiers();
            }

            Loop = GameLoopFactory.CreateStaged(
                config, spawnConfig, trashStats, resolvedCharacter, bombConfig,
                new SystemRandomSource(CurrentSeed), waves, Modifiers);

            // 웨이브 사이 인계: 체력·폭탄·골드를 그대로 물려받는다.
            if (_carryOver != null)
            {
                Loop.Player.CarryOver(_carryOver);
                _carryOver = null;
            }

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
