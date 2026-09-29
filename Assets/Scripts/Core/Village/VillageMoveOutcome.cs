namespace RecycleLife.Core
{
    /// <summary>마을 이동 한 칸의 결과. CORE_COMBAT.md의 CombatOutcome과 같은 역할을 마을판에서 한다.</summary>
    public enum VillageMoveOutcome
    {
        /// <summary>빈 칸으로 이동했다.</summary>
        Moved,

        /// <summary>보드 밖이라 무시됐다.</summary>
        OutOfBounds,

        /// <summary>뭔가에 막혀 이동하지 않았다(캐릭터 상이 아닌 다른 것).</summary>
        Blocked,

        /// <summary>캐릭터 상에 부딪혀 그 캐릭터로 바뀌었다. 어떤 캐릭터인지는 CharacterId를 본다.</summary>
        SwappedCharacter,
    }
}
