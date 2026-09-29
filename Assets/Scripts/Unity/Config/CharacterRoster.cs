using System;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 고를 수 있는 캐릭터 표. 마을의 캐릭터 상(像)과 <see cref="CharacterSelection"/>이
    /// 이 표를 통해 "char_a" 같은 꼬리표를 실제 CharacterConfig로 바꾼다.
    ///
    /// 캐릭터가 늘어나도 이 표에 항목만 추가하면 된다 — 코드에 목록을 박지 않는다(Hard Rule 1·3).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterRoster", menuName = "RecycleLife/Character Roster", order = 4)]
    public sealed class CharacterRoster : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("마을 캐릭터 상(像)에 적을 꼬리표. VillageCharacterBoard의 배치 목록이 이 값을 쓴다.")]
            public string id;

            public CharacterConfig config;

            [Tooltip("이 캐릭터를 대표하는 색. 아트가 나오기 전까지 마을 아바타·상(像)을 " +
                     "이 색으로 칠해 \"지금 이 캐릭터를 쓰고 있다\"를 한눈에 보여 준다.")]
            public Color tint;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>그 id에 대응하는 캐릭터 설정을 찾는다.</summary>
        public bool TryGet(string id, out CharacterConfig config)
        {
            config = null;
            if (string.IsNullOrEmpty(id) || entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].id == id && entries[i].config != null)
                {
                    config = entries[i].config;
                    return true;
                }
            }

            return false;
        }

        /// <summary>그 id를 대표하는 색을 찾는다. 표에 없으면 흰색(무염색)으로 대신한다.</summary>
        public bool TryGetTint(string id, out Color tint)
        {
            tint = Color.white;
            if (string.IsNullOrEmpty(id) || entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].id == id)
                {
                    tint = entries[i].tint;
                    return true;
                }
            }

            return false;
        }

        /// <summary>표의 첫 캐릭터. 저장된 선택이 없거나 표에서 빠졌을 때의 기본값이다.</summary>
        public CharacterConfig Default => entries != null && entries.Length > 0 ? entries[0].config : null;

        public string DefaultId => entries != null && entries.Length > 0 ? entries[0].id : null;
    }
}
