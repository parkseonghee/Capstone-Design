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
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterSelection", menuName = "RecycleLife/Character Selection", order = 13)]
    public sealed class CharacterSelection : ScriptableObject
    {
        private const string SelectedIdKey = "RecycleLife.SelectedCharacterId";

        [SerializeField, Tooltip("고를 수 있는 캐릭터 표.")]
        private CharacterRoster roster;

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
                return _selectedId;
            }
        }

        /// <summary>지금 고른 캐릭터의 실제 설정. 표에 없으면 표의 첫 캐릭터로 대신한다.</summary>
        public CharacterConfig SelectedConfig
        {
            get
            {
                EnsureLoaded();

                CharacterConfig config;
                if (roster != null && roster.TryGet(_selectedId, out config))
                {
                    return config;
                }

                return roster != null ? roster.Default : null;
            }
        }

        /// <summary>마을에서 캐릭터 상에 부딪혔을 때 부른다. 표에 없는 id는 무시한다.</summary>
        public void Select(string characterId)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(characterId) || characterId == _selectedId)
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
