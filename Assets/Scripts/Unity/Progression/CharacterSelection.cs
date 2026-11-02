using System;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 지금 고른 캐릭터. 마을 ↔ 게임 씬을 오갈 때 살아남아야 하는 값이라
    /// <see cref="RunProgress"/>와 같은 이유로 ScriptableObject + PlayerPrefs로 둔다(Hard Rule 7) —
    /// 마을과 게임 씬이 같은 에셋을 인스펙터에서 물고 값을 주고받는다.
    ///
    /// 마을에서 캐릭터 상에 부딪히면 확인창 없이 바로 <see cref="Select"/>가 불린다(기획 2026-09-29).
    ///
    /// 표에 가격이 붙은 캐릭터는 마을 골드로 한 번 사야 고를 수 있다(재화 문서 §5 — B 75 · C 525).
    /// 산 기록은 골드와 같은 곳(<see cref="RunProgress"/>)에 둔다 — 진행 초기화 때 함께 지워진다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterSelection", menuName = "RecycleLife/Character Selection", order = 13)]
    public sealed class CharacterSelection : ScriptableObject
    {
        private const string SelectedIdKey = "RecycleLife.SelectedCharacterId";

        [SerializeField, Tooltip("고를 수 있는 캐릭터 표.")]
        private CharacterRoster roster;

        [SerializeField, Tooltip("골드를 내고 산 캐릭터 기록. 비워 두면 가격과 상관없이 모든 캐릭터가 열린다.")]
        private RunProgress progress;

        [SerializeField, Tooltip("켜면 저장된 선택을 불러오지 않는다(항상 표의 첫 캐릭터로 시작). 개발용.")]
        private bool ignoreSavedSelection;

        private string _selectedId;
        private bool _loaded;

        /// <summary>선택이 바뀌었을 때. 마을 UI(선택 표시 등)가 구독한다.</summary>
        public event Action Changed;

        public string SelectedId
        {
            get
            {
                EnsureLoaded();

                // 저장된 선택이 잠긴 캐릭터면(진행 초기화 등) 기본 캐릭터로 되돌린다.
                return IsUnlocked(_selectedId) ? _selectedId : DefaultId();
            }
        }

        /// <summary>캐릭터를 살 때 쓰는 마을 골드. 기록이 안 꽂혀 있으면 0.</summary>
        public int Gold => progress != null ? progress.Gold : 0;

        /// <summary>그 캐릭터의 해금 골드. 0이면 기본 제공.</summary>
        public int PriceOf(string characterId) => roster != null ? roster.PriceOf(characterId) : 0;

        /// <summary>그 캐릭터를 지금 고를 수 있는지. 기본 제공이거나 이미 샀으면 true.</summary>
        public bool IsUnlocked(string characterId)
        {
            return progress == null || PriceOf(characterId) <= 0 || progress.IsCharacterOwned(characterId);
        }

        /// <summary>지금 가진 골드로 그 캐릭터를 살 수 있는지. 이미 열려 있으면 true.</summary>
        public bool CanAfford(string characterId)
        {
            return IsUnlocked(characterId) || progress.Gold >= PriceOf(characterId);
        }

        /// <summary>
        /// 마을 골드를 내고 캐릭터를 산 뒤 바로 고른다. 이미 열려 있으면 돈 없이 고르기만 한다.
        /// 골드가 모자라면 아무것도 안 하고 false.
        /// </summary>
        public bool TryBuy(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                return false;
            }

            if (!IsUnlocked(characterId) && !progress.TryBuyCharacter(characterId, PriceOf(characterId)))
            {
                return false;
            }

            Select(characterId);
            return true;
        }

        /// <summary>지금 고른 캐릭터의 실제 설정. 표에 없으면 표의 첫 캐릭터로 대신한다.</summary>
        public CharacterConfig SelectedConfig
        {
            get
            {
                EnsureLoaded();

                CharacterConfig config;
                if (roster != null && roster.TryGet(SelectedId, out config))
                {
                    return config;
                }

                return roster != null ? roster.Default : null;
            }
        }

        /// <summary>마을에서 캐릭터 상에 부딪혔을 때 부른다. 표에 없거나 아직 안 산 캐릭터는 무시한다.</summary>
        public void Select(string characterId)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(characterId) || characterId == SelectedId || !IsUnlocked(characterId))
            {
                return;
            }

            CharacterConfig config;
            if (roster != null && !roster.TryGet(characterId, out config))
            {
                Debug.LogWarning($"{nameof(CharacterSelection)}: 표에 없는 캐릭터 id '{characterId}'입니다.", this);
                return;
            }

            _selectedId = characterId;
            Save();
            Changed?.Invoke();
        }

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _selectedId = ignoreSavedSelection
                ? DefaultId()
                : PlayerPrefs.GetString(SelectedIdKey, DefaultId());
        }

        private string DefaultId() => roster != null ? roster.DefaultId : null;

        private void Save()
        {
            if (ignoreSavedSelection)
            {
                return;
            }

            PlayerPrefs.SetString(SelectedIdKey, _selectedId ?? string.Empty);
            PlayerPrefs.Save();
        }

        private void OnDisable()
        {
            // 플레이 모드를 나가면 다음 진입 때 다시 읽게 한다(RunProgress와 같은 이유).
            _loaded = false;
        }
    }
}
