using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 한 스텝의 페이즈 1(WEEK1 §2). 구현을 갈아끼우면 게임의 성격이 바뀐다 —
    /// BlockingMoveResolver는 "부딪히면 막힘", CombatMoveResolver는 "부딪히면 공격"이다.
    /// GameLoop은 어느 쪽이 꽂혔는지 모른다(Hard Rule 3).
    /// </summary>
    public interface IMoveResolver
    {
        MoveResult Resolve(Direction direction);

        /// <summary>
        /// 이 칸으로의 입력이 <b>무언가 행동이 되는가</b>(이동이든 공격이든).
        /// 보드를 바꾸지 않는 순수 질의이며, GameOverChecker의 갇힘 판정이 쓴다.
        ///
        /// 갇힘 규칙을 따로 구현하지 않고 이쪽에 묻는 이유: 전투가 붙으면
        /// "쓰레기에 둘러싸임"은 더 이상 갇힌 게 아니라 때릴 게 많은 것이기 때문이다.
        /// 규칙이 두 군데로 갈라지지 않게 이동 규칙의 주인에게 물어본다.
        /// </summary>
        bool CanAct(Vector2Int target);
    }
}
