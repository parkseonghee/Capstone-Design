namespace RecycleLife.Core
{
    /// <summary>
    /// <b>이번 한 턴만</b> 중력을 건너뛰는 것.
    ///
    /// 폭탄이 이걸 쓴다 — 놓자마자 발밑으로 흘러내리면 "여기에 놓는다"는 조작이 성립하지 않는다.
    /// 설치한 자리에 일단 머물고, 플레이어가 다음 행동을 하면 그때부터 다른 블록처럼 떨어진다
    /// (폭탄 기획 §1-2).
    ///
    /// GravityResolver가 특정 타입을 알지 않도록 인터페이스로 뺐다(Hard Rule 3).
    /// 나중에 "한 턴 떠 있는" 다른 것이 생겨도 중력 쪽은 손대지 않는다.
    /// </summary>
    public interface IGravityHold
    {
        /// <summary>이번 중력 페이즈를 건너뛸지.</summary>
        bool HoldsPosition { get; }

        /// <summary>건너뛴 뒤 풀어 준다. 다음 턴부터는 떨어진다.</summary>
        void ReleaseHold();
    }
}
