namespace RecycleLife.Core
{
    /// <summary>
    /// 보드에 놓이는 블록 한 종류의 데이터. 값은 전부 TrashStatsConfig 에셋에서 온다(Hard Rule 1).
    ///
    /// 이 구조체 하나가 <b>적·아이템·벽</b>을 모두 표현한다 —
    /// 무엇인지는 별도의 종류 플래그가 아니라 값이 정한다:
    ///  · Heal &gt; 0     → 부딪히면 먹는 아이템
    ///  · Attack == 0   → 벽 (때릴 수는 있지만 반격이 없다)
    ///  · 그 외         → 때려서 없애는 적
    ///
    /// enum으로 역할을 나누지 않은 이유는 Hard Rule 3이다. 새 블록을 넣을 때
    /// 코드에 분기를 더하는 게 아니라 에셋에 줄을 하나 더하게 하려는 것이다.
    ///
    /// struct라 종류별 조회가 힙을 건드리지 않는다(Hard Rule 8).
    /// </summary>
    public readonly struct TrashStats
    {
        public TrashStats(int maxHp, int attack, int heal, int spawnWeight, bool chainsWithSameType)
            : this(maxHp, attack, heal, spawnWeight, chainsWithSameType, 0)
        {
        }

        public TrashStats(
            int maxHp, int attack, int heal, int spawnWeight, bool chainsWithSameType, int gold)
        {
            MaxHp = maxHp;
            Attack = attack;
            Heal = heal;
            SpawnWeight = spawnWeight;
            ChainsWithSameType = chainsWithSameType;
            Gold = gold;
        }

        /// <summary>체력. 아이템은 0이며 하트도 그리지 않는다.</summary>
        public int MaxHp { get; }

        /// <summary>살아남았을 때 플레이어에게 돌려주는 반격 피해량. 아이템과 벽은 0.</summary>
        public int Attack { get; }

        /// <summary>
        /// 하나를 먹었을 때 플레이어가 회복하는 체력. 0이면 아이템이 아니다.
        /// 연쇄로 여러 개를 한 번에 먹으면 개수만큼 합산된다.
        /// </summary>
        public int Heal { get; }

        /// <summary>
        /// 스폰 추첨에서의 가중치. 클수록 자주 나오고, 0이면 랜덤 스폰에 아예 안 낀다.
        /// 종류를 늘릴 때 다른 종류의 출현율이 멋대로 깎이지 않게 하려고 뒀다.
        /// </summary>
        public int SpawnWeight { get; }

        /// <summary>
        /// 같은 종류끼리 연쇄로 묶이는지. <b>벽을 포함해 기본은 전부 켜져 있다</b>(기획 확인 완료).
        ///
        /// 밸런싱 v1 §3의 "이동·연쇄 차단"은 벽이 <b>길을 막아</b> 다른 블록의 연쇄를 끊는다는
        /// 뜻이지, 벽 자신이 연쇄에서 빠진다는 뜻이 아니다 — 종류가 다르면 자연히 끊기므로
        /// 그 효과는 이 값과 무관하게 이미 성립한다.
        ///
        /// 값으로 남겨 둔 이유는 "연쇄에 안 끼는 블록"이 나중에 필요할 수 있어서다.
        /// </summary>
        public bool ChainsWithSameType { get; }

        /// <summary>
        /// 처치했을 때 떨어뜨리는 골드. 종류마다 다르며 전부 인스펙터 값이다(Hard Rule 1).
        ///
        /// 역할을 따로 묻지 않는 것도 같은 이유다 — 벽에 돈을 주고 싶으면 표에서 값만 올리면 되고,
        /// 0이면 아무것도 안 떨어진다. 코드에 "벽은 제외" 같은 분기를 두지 않았다(Hard Rule 3).
        /// </summary>
        public int Gold { get; }

        /// <summary>부딪히면 때리는 게 아니라 먹는 대상인지.</summary>
        public bool IsConsumable => Heal > 0;

        /// <summary>
        /// 스폰 가중치만 바꾼 사본. 웨이브가 전역 표를 덮어쓸 때 쓴다.
        ///
        /// 생성자를 직접 부르지 말고 이걸 쓸 것. 필드를 손으로 다시 나열하면
        /// 새 필드가 생겼을 때 조용히 0으로 떨어진다 —
        /// 실제로 골드를 추가했을 때 이 경로에서 그 사고가 났다.
        /// </summary>
        public TrashStats WithSpawnWeight(int spawnWeight)
            => new TrashStats(MaxHp, Attack, Heal, spawnWeight, ChainsWithSameType, Gold);
    }
}
