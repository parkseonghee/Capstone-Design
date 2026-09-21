using System;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 스테이지 선택 상태와 클리어 기록. 마을 ↔ 게임 씬을 오갈 때 살아남아야 하는 값들이다.
    ///
    /// ScriptableObject로 둔 이유: 씬을 갈아타도 에셋은 그대로 살아 있어서
    /// static 전역 변수를 두지 않고도 값을 넘길 수 있다(Hard Rule 7).
    /// 마을 씬의 지도와 게임 씬의 세션이 <b>같은 에셋을 인스펙터에서 물고</b> 데이터를 주고받는다.
    ///
    /// 클리어 기록은 PlayerPrefs에 저장한다 — 앱을 껐다 켜도 지도에 표시가 남아야 하기 때문이다.
    /// 저장 위치를 한 군데로 모아 둬서, 나중에 세이브 파일 방식으로 바꿔도 여기만 고치면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "RunProgress", menuName = "RecycleLife/Run Progress", order = 12)]
    public sealed class RunProgress : ScriptableObject
    {
        /// <summary>클리어 비트마스크를 담는 PlayerPrefs 키.</summary>
        private const string ClearedKey = "RecycleLife.ClearedWaves";

        /// <summary>모아 둔 골드를 담는 PlayerPrefs 키.</summary>
        private const string GoldKey = "RecycleLife.Gold";

        [SerializeField, Tooltip("스테이지 목록. 지도가 이 순서대로 노드를 만든다.")]
        private WaveSet waveSet;

        [Header("개발용")]
        [SerializeField, Tooltip("켜면 잠금을 무시하고 모든 스테이지를 고를 수 있다. 밸런스 확인용.")]
        private bool unlockAll;

        [SerializeField, Tooltip("켜면 플레이할 때마다 클리어 기록을 불러오지 않는다(항상 처음부터).")]
        private bool ignoreSavedProgress;

        [Header("골드")]
        [SerializeField, Tooltip("스테이지에서 죽었을 때 그 판에서 번 골드를 그대로 가질지. " +
                                 "끄면 클리어했을 때만 적립된다(로그라이트 기본값). " +
                                 "기획 미확정이라 값으로 열어 뒀다.")]
        private bool keepGoldOnDefeat;

        /// <summary>클리어한 웨이브 비트마스크. 0번 비트 = 첫 스테이지.</summary>
        private int _cleared;

        /// <summary>스테이지를 넘어 쌓이는 총 골드.</summary>
        private int _gold;

        private bool _loaded;

        /// <summary>지금 고른 스테이지의 인덱스(0부터). 게임 씬이 이 값으로 시작 웨이브를 정한다.</summary>
        public int SelectedIndex { get; private set; }

        /// <summary>클리어 기록이 바뀌었을 때. 지도가 표시를 갱신한다.</summary>
        public event Action Changed;

        public WaveSet Waves => waveSet;

        public int StageCount => waveSet != null ? waveSet.Waves.Count : 0;

        public bool UnlockAll => unlockAll;

        public bool KeepGoldOnDefeat => keepGoldOnDefeat;

        /// <summary>지금까지 모은 총 골드. 상점이 생기면 여기서 깎는다.</summary>
        public int Gold
        {
            get
            {
                EnsureLoaded();
                return _gold;
            }
        }

        /// <summary>스테이지에서 번 돈을 총액에 적립한다.</summary>
        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            EnsureLoaded();
            _gold += amount;
            Save();
            Changed?.Invoke();
        }

        /// <summary>골드를 쓴다(상점). 모자라면 아무것도 안 하고 false.</summary>
        public bool SpendGold(int amount)
        {
            EnsureLoaded();

            if (amount <= 0 || _gold < amount)
            {
                return false;
            }

            _gold -= amount;
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>그 스테이지를 깼는지.</summary>
        public bool IsCleared(int index)
        {
            EnsureLoaded();
            return index >= 0 && index < 32 && (_cleared & (1 << index)) != 0;
        }

        /// <summary>
        /// 그 스테이지를 지금 고를 수 있는지.
        /// 첫 스테이지는 항상 열려 있고, 그 뒤는 <b>바로 앞을 깨야</b> 열린다.
        /// </summary>
        public bool IsUnlocked(int index)
        {
            if (index < 0 || index >= StageCount)
            {
                return false;
            }

            return unlockAll || index == 0 || IsCleared(index - 1);
        }

        /// <summary>지도에서 스테이지를 골랐을 때. 게임 씬을 띄우기 직전에 부른다.</summary>
        public void Select(int index)
        {
            SelectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, StageCount - 1));
        }

        /// <summary>스테이지를 깼을 때. 기록에 남기고 저장한다.</summary>
        public void MarkCleared(int index)
        {
            EnsureLoaded();

            if (index < 0 || index >= 32 || (_cleared & (1 << index)) != 0)
            {
                return;
            }

            _cleared |= 1 << index;
            Save();
            Changed?.Invoke();
        }

        /// <summary>기록을 전부 지운다. 인스펙터 우클릭 메뉴에서도 부를 수 있다.</summary>
        [ContextMenu("클리어 기록·골드 초기화")]
        public void ClearAll()
        {
            _cleared = 0;
            _gold = 0;
            SelectedIndex = 0;
            _loaded = true;
            Save();
            Changed?.Invoke();
        }

        // ── 챕터 ─────────────────────────────────────────────────────────
        //
        // 챕터 = 웨이브의 StageNumber. 지도가 한 번에 한 챕터씩 보여 준다.
        // 챕터당 스테이지 수를 코드에 박지 않으려고 목록을 훑어 센다(Hard Rule 1) —
        // 나중에 챕터당 5스테이지로 바뀌어도 웨이브 에셋만 고치면 된다.

        /// <summary>챕터 수. 서로 다른 StageNumber의 개수다.</summary>
        public int ChapterCount
        {
            get
            {
                if (waveSet == null) { return 0; }

                var waves = waveSet.Waves;
                int count = 0;
                int last = int.MinValue;
                for (int i = 0; i < waves.Count; i++)
                {
                    // 같은 챕터의 웨이브는 붙어 있다는 전제다(§2-1 표 순서).
                    if (waves[i].StageNumber != last)
                    {
                        last = waves[i].StageNumber;
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>chapterIndex(0부터)에 해당하는 챕터 번호. 화면에 "챕터 2"로 찍는 값이다.</summary>
        public int ChapterNumber(int chapterIndex)
        {
            int first = FirstIndexOfChapter(chapterIndex);
            return first < 0 ? 0 : waveSet.Waves[first].StageNumber;
        }

        /// <summary>그 챕터에 든 스테이지 수.</summary>
        public int StagesInChapter(int chapterIndex)
        {
            int first = FirstIndexOfChapter(chapterIndex);
            if (first < 0) { return 0; }

            var waves = waveSet.Waves;
            int chapter = waves[first].StageNumber;
            int n = 0;
            for (int i = first; i < waves.Count && waves[i].StageNumber == chapter; i++)
            {
                n++;
            }

            return n;
        }

        /// <summary>챕터 안 slot번째 스테이지의 전체 인덱스. 없으면 -1.</summary>
        public int StageIndex(int chapterIndex, int slot)
        {
            int first = FirstIndexOfChapter(chapterIndex);
            if (first < 0 || slot < 0 || slot >= StagesInChapter(chapterIndex))
            {
                return -1;
            }

            return first + slot;
        }

        /// <summary>그 스테이지가 몇 번째 챕터에 속하는지(0부터). 지도를 열 때 어디를 보여 줄지 정한다.</summary>
        public int ChapterIndexOfStage(int stageIndex)
        {
            if (waveSet == null) { return 0; }

            var waves = waveSet.Waves;
            if (stageIndex < 0 || stageIndex >= waves.Count) { return 0; }

            int chapter = -1;
            int last = int.MinValue;
            for (int i = 0; i <= stageIndex; i++)
            {
                if (waves[i].StageNumber != last)
                {
                    last = waves[i].StageNumber;
                    chapter++;
                }
            }

            return Mathf.Max(0, chapter);
        }

        /// <summary>아직 안 깬 스테이지 중 가장 앞선 것. 지도를 열면 여기가 있는 챕터를 보여 준다.</summary>
        public int FurthestUnlockedIndex
        {
            get
            {
                for (int i = 0; i < StageCount; i++)
                {
                    if (!IsCleared(i)) { return i; }
                }

                return Mathf.Max(0, StageCount - 1);
            }
        }

        private int FirstIndexOfChapter(int chapterIndex)
        {
            if (waveSet == null || chapterIndex < 0) { return -1; }

            var waves = waveSet.Waves;
            int chapter = -1;
            int last = int.MinValue;
            for (int i = 0; i < waves.Count; i++)
            {
                if (waves[i].StageNumber != last)
                {
                    last = waves[i].StageNumber;
                    chapter++;
                    if (chapter == chapterIndex) { return i; }
                }
            }

            return -1;
        }

        /// <summary>
        /// "1-1" 같은 표시 이름. 스테이지 번호와, 그 스테이지 안에서 몇 번째인지를 조합한다.
        /// 스테이지당 웨이브 수를 코드에 박지 않으려고 목록을 훑어 센다(Hard Rule 1).
        /// </summary>
        public string LabelFor(int index)
        {
            if (waveSet == null)
            {
                return string.Empty;
            }

            var waves = waveSet.Waves;
            if (index < 0 || index >= waves.Count)
            {
                return string.Empty;
            }

            int stage = waves[index].StageNumber;
            int within = 1;
            for (int i = 0; i < index; i++)
            {
                if (waves[i].StageNumber == stage) { within++; }
            }

            return stage + "-" + within;
        }

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _cleared = ignoreSavedProgress ? 0 : PlayerPrefs.GetInt(ClearedKey, 0);
            _gold = ignoreSavedProgress ? 0 : PlayerPrefs.GetInt(GoldKey, 0);
        }

        private void Save()
        {
            if (ignoreSavedProgress)
            {
                return;
            }

            PlayerPrefs.SetInt(ClearedKey, _cleared);
            PlayerPrefs.SetInt(GoldKey, _gold);
            PlayerPrefs.Save();
        }

        private void OnDisable()
        {
            // 플레이 모드를 나가면 다음 진입 때 다시 읽게 한다.
            // 에디터에서 SO의 런타임 값이 남아 "지웠는데 그대로"처럼 보이는 걸 막는다.
            _loaded = false;
        }
    }
}
