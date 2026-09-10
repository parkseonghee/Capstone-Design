using System.Collections.Generic;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 플레이어 캐릭터 한 명. 기획서(캐릭터 &amp; 적 기믹) §1 + 밸런싱 v1 §1-1.
    ///
    /// 캐릭터마다 이 에셋을 하나씩 만든다. 개성은 숫자가 아니라
    /// <b>공격이 어디에 들어가는가</b>에서 나오므로, 공격 범위를 코드가 아니라
    /// 좌표 오프셋 리스트로 여기에 넣는다 — 기획서가 지정한 구현 방식이다(Hard Rule 1·3).
    ///
    /// 수치는 플레이테스트 후 재조정 전제의 초안이다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "RecycleLife/Character Config", order = 3)]
    public sealed class CharacterConfig : ScriptableObject, ICharacterConfig
    {
        /// <summary>오프셋이 비어 있을 때 쓰는 최소 동작 — 부딪힌 칸만 때린다.</summary>
        private static readonly Vector2Int[] BumpedCellOnly = { Vector2Int.zero };

        [Header("정보")]
        [SerializeField, Tooltip("인스펙터에서 알아보기 위한 이름. 로직에는 쓰이지 않는다.")]
        private string displayName = "캐릭터";

        [Header("수치 (밸런싱 v1 §1-1)")]
        [SerializeField, Min(1), Tooltip("런 시작 체력. 반격을 맞아 0이 되면 패배한다.")]
        private int maxHp = 3;

        [SerializeField, Min(0), Tooltip("공격 범위 <b>각 칸</b>에 들어가는 피해량. " +
                                         "연쇄로 묶인 블록도 각각 이만큼 맞는다.")]
        private int attack = 2;

        [Header("공격 범위")]
        [SerializeField, Tooltip("부딪힌 칸을 (0,0)으로 하는 상대 좌표. " +
                                 "아래로 공격할 때를 기준으로 적는다 - +y가 공격 방향, +x가 그 오른쪽. " +
                                 "실제로는 공격한 방향에 맞춰 회전해 적용되므로 상하좌우 모두 같은 모양이 된다. " +
                                 "A(기본형)은 (0,0) 하나, B(사이드)는 (0,0)/(-1,0)/(1,0). " +
                                 "각 칸이 독립적으로 연쇄를 시작하고, 빈 칸이나 보드 밖은 무시된다(공허 타격).")]
        private Vector2Int[] attackOffsets = { Vector2Int.zero };

        public string DisplayName => displayName;

        public int MaxHp => maxHp;

        public int Attack => attack;

        public IReadOnlyList<Vector2Int> AttackOffsets =>
            attackOffsets != null && attackOffsets.Length > 0 ? attackOffsets : BumpedCellOnly;
    }
}
