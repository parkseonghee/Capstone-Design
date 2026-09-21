using UnityEngine;

namespace RecycleLife.Core
{
    /// <summary>
    /// 플레이어가 설치하는 폭탄. 기획서(폭탄·아이템·기믹 v1) §1.
    ///
    /// Trash와 따로 둔 이유는 성격이 근본적으로 다르기 때문이다 —
    /// 체력이 없어서 때려도 깎이지 않고, 대신 <b>도화선 상태</b>를 갖는다.
    ///
    /// 핵심 규칙: <b>설치만으로는 터지지 않는다.</b> 플레이어가 때려야 점화되고,
    /// 그때부터 카운트다운이 돈다. "놓는 턴"과 "불붙이는 턴"이 갈라져 있는 게 설계 의도다.
    ///
    /// 중력은 다른 블록과 똑같이 받는다(기획 확정). 다만 <b>설치한 턴 한 번만</b> 예외로
    /// 제자리에 머문다 — 놓자마자 흘러내리면 "여기에 놓는다"는 조작이 성립하지 않기 때문이다.
    /// 수치는 전부 주입받는다 — 여기에 리터럴이 없다(Hard Rule 1).
    /// </summary>
    public sealed class Bomb : Entity, IGravityHold
    {
        /// <summary>점화 전 상태를 나타내는 값. 0 이상이면 카운트다운이 돌고 있다.</summary>
        private const int NotArmed = -1;

        /// <summary>점화한 바로 그 턴은 카운트를 세지 않기 위한 표시.</summary>
        private bool _justArmed;

        public Bomb(int fuseTurns, int damage, int blastRadius)
        {
            FuseTurns = Mathf.Max(1, fuseTurns);
            Damage = Mathf.Max(0, damage);
            BlastRadius = Mathf.Max(0, blastRadius);
            FuseRemaining = NotArmed;
        }

        /// <summary>점화 후 몇 턴 뒤에 터지는지. 확정값 3.</summary>
        public int FuseTurns { get; }

        /// <summary>폭발이 한 칸에 주는 피해. 확정값 5.</summary>
        public int Damage { get; }

        /// <summary>
        /// 폭심에서 몇 칸까지 미치는지. 1이면 3×3, 2면 5×5다.
        /// 유물 "폭발 범위 증가"가 이 값을 올린다.
        /// </summary>
        public int BlastRadius { get; }

        /// <summary>남은 카운트다운. 점화 전에는 -1이다.</summary>
        public int FuseRemaining { get; private set; }

        public bool IsArmed => FuseRemaining >= 0;

        /// <summary>설치한 턴이라 이번 중력을 건너뛸지.</summary>
        public bool HoldsPosition { get; private set; }

        /// <summary>설치 직후에 불린다. 그 턴의 중력 한 번을 건너뛴다.</summary>
        public void HoldForOneTurn()
        {
            HoldsPosition = true;
        }

        /// <summary>중력이 한 번 건너뛴 뒤 풀어 준다.</summary>
        public void ReleaseHold()
        {
            HoldsPosition = false;
        }

        public override EntityKind Kind => EntityKind.Bomb;

        /// <summary>
        /// 불을 붙인다. 플레이어가 때렸을 때 호출된다.
        /// 이미 타고 있는 폭탄을 또 때려도 카운트가 되감기지 않는다(기획 미확정 — 현재는 무시).
        /// </summary>
        /// <returns>이번 호출로 점화됐으면 true.</returns>
        public bool Arm()
        {
            if (IsArmed)
            {
                return false;
            }

            FuseRemaining = FuseTurns;

            // 불을 붙인 턴에도 도화선 페이즈가 돌기 때문에, 표시해 두지 않으면
            // 그 턴에 바로 1이 깎여 "3턴 뒤"가 2턴 뒤가 된다.
            _justArmed = true;
            return true;
        }

        /// <summary>
        /// 카운트다운을 한 턴 줄인다. 점화되지 않은 폭탄은 아무 일도 없다.
        /// </summary>
        /// <returns>이번 턴에 터져야 하면 true.</returns>
        public bool TickFuse()
        {
            if (!IsArmed)
            {
                return false;
            }

            // 점화한 턴은 세지 않는다. 다음 턴부터 카운트가 돈다.
            if (_justArmed)
            {
                _justArmed = false;
                return false;
            }

            FuseRemaining--;
            return FuseRemaining <= 0;
        }

        /// <summary>범위 안에 다른 폭탄이 걸렸을 때 즉시 터뜨리기 위해 쓴다(연쇄 폭발).</summary>
        public void ForceDetonate()
        {
            FuseRemaining = 0;
            _justArmed = false;
        }
    }
}
