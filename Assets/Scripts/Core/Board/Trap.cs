namespace RecycleLife.Core
{
    /// <summary>
    /// 덫 몬스터가 죽은 자리에 남는 덫. Bomb·ShopItem처럼 Trash와 따로 둔 이유는
    /// 성격이 근본적으로 다르기 때문이다 — 체력이 없어 공격 대상이 아니고,
    /// 부딪히면 싸우는 게 아니라 무작위 빈 칸으로 날아간다(TrapResolver).
    ///
    /// 중력을 <b>영구히</b> 무시한다. 한 턴만 버티는 폭탄(IGravityHold)과 달리
    /// ReleaseHold를 불러도 실제로는 풀리지 않는다 — GravityResolver는 이 사실을 몰라도 된다
    /// (Hard Rule 3).
    /// </summary>
    public sealed class Trap : Entity, IGravityHold
    {
        public override EntityKind Kind => EntityKind.Trap;

        public bool HoldsPosition => true;

        /// <summary>아무것도 하지 않는다 — 덫은 절대 풀리지 않는다(기획 확정: 위치 영구 고정).</summary>
        public void ReleaseHold()
        {
        }
    }
}
