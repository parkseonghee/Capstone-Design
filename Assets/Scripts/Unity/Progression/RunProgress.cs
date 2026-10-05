using System;
using System.Collections.Generic;
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

        /// <summary>마을에서 산 조합법 id들을 담는 PlayerPrefs 키.</summary>
        private const string RecipesKey = "RecycleLife.KnownRecipes";

        /// <summary>마을에 보관 중인 재료를 담는 PlayerPrefs 키.</summary>
        private const string StashKey = "RecycleLife.Stash";

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

        /// <summary>
        /// 마을에서 샀거나 직접 만들어 본 조합법 id들.
        ///
        /// 인덱스가 아니라 <b>id</b>로 저장한다 — 인덱스로 두면 조합법 목록의 순서만 바뀌어도
        /// 플레이어가 산 기록이 엉뚱한 조합법을 가리킨다.
        /// </summary>
        private readonly HashSet<string> _knownRecipes = new HashSet<string>();

        /// <summary>
        /// 마을에 놓고 가는 재료. 다음 판에 그대로 들고 들어가거나, 쓰레기통에 넣어 골드로 바꾼다.
        ///
        /// 순서를 유지해야 목록이 매번 뒤바뀌지 않아서 id 목록과 개수를 따로 둔다.
        /// </summary>
        private readonly List<string> _stashOrder = new List<string>(16);
        private readonly Dictionary<string, int> _stash = new Dictionary<string, int>(16);

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

        /// <summary>그 조합법을 알고 있는지(샀거나, 직접 맞춰 봤거나).</summary>
        public bool IsRecipeKnown(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return false;
            }

            EnsureLoaded();
            return _knownRecipes.Contains(recipeId);
        }

        /// <summary>
        /// 조합법을 알게 된다. 이미 알고 있으면 아무 일도 없다.
        /// <b>값은 깎지 않는다</b> — 돈 내는 쪽은 상점이고 여기는 기록만 맡는다.
        /// </summary>
        /// <returns>이번에 새로 알게 됐으면 true.</returns>
        public bool LearnRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return false;
            }

            EnsureLoaded();

            if (!_knownRecipes.Add(recipeId))
            {
                return false;
            }

            Save();
            Changed?.Invoke();
            return true;
        }

        // ── 재료 보관함 ──────────────────────────────────────────────────

        /// <summary>보관 중인 재료의 id. 넣은 순서대로다.</summary>
        public IReadOnlyList<string> StashIds
        {
            get
            {
                EnsureLoaded();
                return _stashOrder;
            }
        }

        /// <summary>그 재료를 몇 개 보관하고 있는지.</summary>
        public int StashCount(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return 0;
            }

            EnsureLoaded();
            int n;
            return _stash.TryGetValue(id, out n) ? n : 0;
        }

        /// <summary>재료를 보관함에 넣는다.</summary>
        public void AddToStash(string id, int count)
        {
            if (string.IsNullOrEmpty(id) || count <= 0)
            {
                return;
            }

            EnsureLoaded();

            int n;
            if (_stash.TryGetValue(id, out n))
            {
                _stash[id] = n + count;
            }
            else
            {
                _stash[id] = count;
                _stashOrder.Add(id);
            }

            Save();
            Changed?.Invoke();
        }

        /// <summary>재료를 보관함에서 뺀다. 가진 것보다 많이 빼려 하면 아무것도 안 하고 false.</summary>
        public bool RemoveFromStash(string id, int count)
        {
            if (string.IsNullOrEmpty(id) || count <= 0)
            {
                return false;
            }

            EnsureLoaded();

            int n;
            if (!_stash.TryGetValue(id, out n) || n < count)
            {
                return false;
            }

            if (n == count)
            {
                _stash.Remove(id);
                _stashOrder.Remove(id);
            }
            else
            {
                _stash[id] = n - count;
            }

            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 보관함을 통째로 갈아 끼운다. 판을 마치고 마을로 돌아올 때, 남은 인벤토리를 그대로 옮긴다.
        ///
        /// 더하는 게 아니라 <b>덮어쓰는</b> 이유: 판에 들어갈 때 보관함을 복사해 갔으므로,
        /// 돌아올 때 더하면 같은 재료가 두 배로 늘어난다.
        /// </summary>
        public void ReplaceStash(IReadOnlyList<string> ids, IReadOnlyList<int> counts)
        {
            EnsureLoaded();

            _stash.Clear();
            _stashOrder.Clear();

            if (ids != null && counts != null)
            {
                for (int i = 0; i < ids.Count && i < counts.Count; i++)
                {
                    if (string.IsNullOrEmpty(ids[i]) || counts[i] <= 0 || _stash.ContainsKey(ids[i]))
                    {
                        continue;
                    }

                    _stash[ids[i]] = counts[i];
                    _stashOrder.Add(ids[i]);
                }
            }

            Save();
            Changed?.Invoke();
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
        [ContextMenu("클리어 기록·골드·조합법·보관함 초기화")]
        public void ClearAll()
        {
            _cleared = 0;
            _gold = 0;
            _knownRecipes.Clear();
            _stash.Clear();
            _stashOrder.Clear();
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

        /// <summary>그 챕터(=스테이지)를 깼는지. <b>마지막 웨이브</b>를 깨야 스테이지 클리어다.</summary>
        public bool IsChapterCleared(int chapterIndex)
        {
            int count = StagesInChapter(chapterIndex);
            if (count <= 0)
            {
                return false;
            }

            return IsCleared(StageIndex(chapterIndex, count - 1));
        }

        /// <summary>그 챕터에 들어갈 수 있는지. 첫 챕터는 항상 열려 있고, 그 뒤는 앞 챕터를 깨야 열린다.</summary>
        public bool IsChapterUnlocked(int chapterIndex)
        {
            if (chapterIndex < 0 || chapterIndex >= ChapterCount)
            {
                return false;
            }

            return unlockAll || chapterIndex == 0 || IsChapterCleared(chapterIndex - 1);
        }

        /// <summary>
        /// 그 챕터를 고른다. 스테이지는 <b>첫 웨이브부터</b> 시작한다 —
        /// 1스테이지 = 3웨이브라 중간부터 들어갈 수는 없다(팀 확정 2026-09-22).
        /// </summary>
        public void SelectChapter(int chapterIndex)
        {
            int first = StageIndex(Mathf.Clamp(chapterIndex, 0, Mathf.Max(0, ChapterCount - 1)), 0);
            Select(first < 0 ? 0 : first);
        }

        // ── 스테이지 건너뛰기(유료) ──────────────────────────────────────
        //
        // 예전에는 "바로 앞 챕터를 깨야 다음이 열린다"는 순서 잠금이었다.
        // 팀 요청(임시값, 확정 전)으로 그 잠금을 골드 지불로 바꿨다:
        // 1챕터는 항상 무료, 이미 깬 챕터는 다시 무료, 그 외에는 chapterEntryCost만큼 낸다.

        [Header("스테이지 건너뛰기 비용 (임시값)")]
        [SerializeField, Tooltip("챕터 인덱스(0부터)별로 건너뛸 때 내는 골드. " +
                                 "0번(1스테이지)은 계산에서 항상 0 취급된다. " +
                                 "챕터 수보다 배열이 짧으면 남는 챕터는 배열의 마지막 값을 그대로 쓴다. " +
                                 "기본값: 2스테이지 100골드, 3스테이지 300골드(임시, 밸런스 확정 전).")]
        private int[] chapterEntryCost = { 0, 100, 300 };

        /// <summary>
        /// 그 챕터로 바로 건너뛸 때 내야 하는 골드. 첫 챕터거나 이미 깬 챕터면 0(무료).
        /// </summary>
        public int EntryCost(int chapterIndex)
        {
            if (unlockAll || chapterIndex <= 0 || IsChapterCleared(chapterIndex))
            {
                return 0;
            }

            if (chapterEntryCost == null || chapterEntryCost.Length == 0)
            {
                return 0;
            }

            int i = Mathf.Clamp(chapterIndex, 0, chapterEntryCost.Length - 1);
            return Mathf.Max(0, chapterEntryCost[i]);
        }

        /// <summary>지금 가진 골드로 그 챕터에 들어갈 수 있는지(무료 조건 포함).</summary>
        public bool CanAffordChapter(int chapterIndex)
        {
            return chapterIndex == 0 || IsChapterCleared(chapterIndex) || Gold >= EntryCost(chapterIndex);
        }

        /// <summary>
        /// 지도에서 챕터를 고르면서, 필요하면 그 자리에서 골드를 낸다.
        /// 무료 조건이 아니면 <see cref="EntryCost"/>만큼 <see cref="SpendGold"/>를 시도한다.
        /// </summary>
        /// <returns>실제로 그 챕터를 골랐으면 true. 돈이 모자라면 아무것도 안 하고 false.</returns>
        public bool TryEnterChapter(int chapterIndex)
        {
            if (chapterIndex < 0 || chapterIndex >= ChapterCount)
            {
                return false;
            }

            int cost = EntryCost(chapterIndex);
            if (cost > 0 && !SpendGold(cost))
            {
                return false;
            }

            SelectChapter(chapterIndex);
            return true;
        }

        /// <summary>그 웨이브가 속한 스테이지의 표시 이름. 클리어 화면이 쓴다.</summary>
        public string StageLabelForWave(int waveIndex)
            => ChapterNumber(ChapterIndexOfStage(waveIndex)).ToString();

        /// <summary>아직 안 깬 챕터 중 가장 앞선 것. 지도를 열면 여기를 보여 준다.</summary>
        public int FurthestUnlockedChapter
        {
            get
            {
                for (int c = 0; c < ChapterCount; c++)
                {
                    if (!IsChapterCleared(c)) { return c; }
                }

                return Mathf.Max(0, ChapterCount - 1);
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

            _knownRecipes.Clear();
            _stash.Clear();
            _stashOrder.Clear();

            if (!ignoreSavedProgress)
            {
                string saved = PlayerPrefs.GetString(RecipesKey, string.Empty);
                if (!string.IsNullOrEmpty(saved))
                {
                    foreach (string id in saved.Split(','))
                    {
                        if (!string.IsNullOrEmpty(id)) { _knownRecipes.Add(id); }
                    }
                }

                // "M01:3,M02:1" 꼴. 망가진 줄은 통째로 버린다 — 세이브가 깨졌다고
                // 마을이 안 열리는 것보다 재료를 잃는 쪽이 낫다.
                string stashed = PlayerPrefs.GetString(StashKey, string.Empty);
                if (!string.IsNullOrEmpty(stashed))
                {
                    foreach (string pair in stashed.Split(','))
                    {
                        if (string.IsNullOrEmpty(pair)) { continue; }

                        int colon = pair.IndexOf(':');
                        if (colon <= 0) { continue; }

                        string id = pair.Substring(0, colon);
                        int count;
                        if (!int.TryParse(pair.Substring(colon + 1), out count) || count <= 0) { continue; }
                        if (_stash.ContainsKey(id)) { continue; }

                        _stash[id] = count;
                        _stashOrder.Add(id);
                    }
                }
            }
        }

        private void Save()
        {
            if (ignoreSavedProgress)
            {
                return;
            }

            PlayerPrefs.SetInt(ClearedKey, _cleared);
            PlayerPrefs.SetInt(GoldKey, _gold);
            PlayerPrefs.SetString(RecipesKey, string.Join(",", _knownRecipes));

            var stash = new string[_stashOrder.Count];
            for (int i = 0; i < _stashOrder.Count; i++)
            {
                stash[i] = _stashOrder[i] + ":" + _stash[_stashOrder[i]];
            }

            PlayerPrefs.SetString(StashKey, string.Join(",", stash));
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
