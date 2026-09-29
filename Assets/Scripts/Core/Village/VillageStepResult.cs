namespace RecycleLife.Core
{
    /// <summary>
    /// 마을 이동 한 번의 결과. Core는 "어느 캐릭터 꼬리표에 부딪혔는지"만 넘긴다 —
    /// 실제로 캐릭터를 바꾸는 효과는 Unity 계층(ShopItem 구매와 같은 패턴)이 적용한다.
    /// </summary>
    public readonly struct VillageStepResult
    {
        public VillageStepResult(VillageMoveOutcome outcome, string characterId)
        {
            Outcome = outcome;
            CharacterId = characterId;
        }

        public VillageMoveOutcome Outcome { get; }

        /// <summary>Outcome이 SwappedCharacter일 때만 값이 있다.</summary>
        public string CharacterId { get; }

        public static VillageStepResult Moved => new VillageStepResult(VillageMoveOutcome.Moved, null);

        public static VillageStepResult OutOfBounds => new VillageStepResult(VillageMoveOutcome.OutOfBounds, null);

        public static VillageStepResult Blocked => new VillageStepResult(VillageMoveOutcome.Blocked, null);

        public static VillageStepResult SwappedCharacter(string characterId)
            => new VillageStepResult(VillageMoveOutcome.SwappedCharacter, characterId);
    }
}
