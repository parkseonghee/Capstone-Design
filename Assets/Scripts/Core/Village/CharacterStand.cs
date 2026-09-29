using System;

namespace RecycleLife.Core
{
    /// <summary>
    /// 마을에 세워진 캐릭터 상(像). 부딪히면 그 캐릭터로 바로 바뀐다.
    ///
    /// Core는 "어떤 캐릭터 꼬리표를 들고 있는가"만 안다 — 실제로 어느 CharacterConfig 에셋에
    /// 대응하는지, 전환을 어떻게 적용하는지는 Unity 계층의 몫이다(Hard Rule 5, ShopItem과 같은 패턴).
    /// </summary>
    public sealed class CharacterStand : Entity
    {
        public CharacterStand(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                throw new ArgumentException("characterId is required.", nameof(characterId));
            }

            CharacterId = characterId;
        }

        public override EntityKind Kind => EntityKind.CharacterStand;

        public string CharacterId { get; }
    }
}
