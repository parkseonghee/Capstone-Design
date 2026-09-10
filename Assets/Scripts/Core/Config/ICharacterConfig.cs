using System.Collections.Generic;
using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 플레이어 캐릭터 한 명의 정의. 기획서(캐릭터 &amp; 적 기믹) §1.
    ///
    /// 캐릭터의 개성은 체력·공격력 숫자가 아니라 <b>"공격이 어디에 들어가는가"</b>다.
    /// 그래서 공격 범위를 코드에 박지 않고 <see cref="AttackOffsets"/> 좌표 리스트로 데이터화한다 —
    /// 기획서가 지정한 구현 방식이며, 캐릭터가 늘어도 에셋만 추가하면 된다(Hard Rule 1·3).
    ///
    /// 런타임 구현은 Unity 계층의 CharacterConfig(SO), 테스트 구현은 페이크.
    /// Core는 ScriptableObject를 모른다(Hard Rule 5).
    /// </summary>
    public interface ICharacterConfig
    {
        int MaxHp { get; }

        /// <summary>공격 범위 <b>각 칸</b>에 들어가는 피해량.</summary>
        int Attack { get; }

        /// <summary>
        /// 공격이 닿는 칸들. <b>부딪힌 칸을 (0,0)으로 하는 상대 좌표</b>이며,
        /// <b>아래로 공격할 때를 기준</b>으로 적는다 — +y가 공격 방향, +x가 그 오른쪽이다.
        ///  · 캐릭터 A(기본형) = [(0,0)]                — 부딪힌 칸만
        ///  · 캐릭터 B(사이드) = [(0,0), (-1,0), (1,0)]  — 부딪힌 칸 + 양옆
        ///
        /// 실제 공격 시에는 <b>공격한 방향에 맞춰 회전</b>해서 적용된다.
        /// 그래서 B의 "양옆"은 위로 치면 좌우, 좌우로 치면 위아래가 된다(기획 확정).
        ///
        /// 각 칸은 <b>독립적으로</b> 연쇄를 시작한다. 빈 칸이나 보드 밖은 그냥 무시된다(공허 타격).
        /// </summary>
        IReadOnlyList<Vector2Int> AttackOffsets { get; }
    }
}
