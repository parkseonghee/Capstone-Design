using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 보드에 놓이는 모든 것의 베이스. WEEK1_MOVEMENT_FALLING.md §1.
    /// Position은 BoardGrid만 갱신한다(internal set) — 격자와 엔티티 좌표가 어긋나지 않게 하기 위함.
    /// </summary>
    public abstract class Entity
    {
        public abstract EntityKind Kind { get; }

        public Vector2Int Position { get; internal set; }

        /// <summary>
        /// 남은 중독 턴. 0이면 중독이 아니다. 독 몬스터가 남긴 장판을 밟으면 걸리고,
        /// 중독인 채로 움직일 때마다 피해를 받는다(<see cref="PoisonResolver"/>).
        ///
        /// 플레이어와 몬스터가 <b>같은 규칙</b>을 타야 해서(기획 5: "몬스터도 독을 밟을 수 있다")
        /// Player·Trash에 각각 두지 않고 여기 한 번만 뒀다. 밟을 일이 없는 것들(덫·상품)은
        /// 아무도 이 값을 건드리지 않으므로 늘 0이다.
        /// </summary>
        public int PoisonTurns { get; private set; }

        /// <summary>중독을 건다. 이미 더 길게 걸려 있으면 그대로 둔다(겹쳐 밟아도 줄지 않게).</summary>
        internal void ApplyPoison(int turns)
        {
            if (turns > PoisonTurns)
            {
                PoisonTurns = turns;
            }
        }

        /// <summary>한 턴이 지났다. 중독이 걸려 있으면 하나 깎는다.</summary>
        internal void AgePoison()
        {
            if (PoisonTurns > 0)
            {
                PoisonTurns--;
            }
        }
    }
}
