using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을을 돌아다니는 캐릭터 중 고를 수 있는 캐릭터. 눌리면 <see cref="Clicked"/>만 알린다 —
    /// 선택 확인창을 띄우는 것은 받는 쪽(<see cref="VillageManager"/>)이 정한다.
    ///
    /// 터치는 건물과 같은 경로(EventSystem + 카메라의 Physics2DRaycaster)로 받는다.
    /// 건물과 겹쳐 있을 때 캐릭터가 먼저 눌리도록 캐릭터의 z를 건물보다 카메라 쪽에 둔다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class VillageCharacter : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField, Tooltip("CharacterRoster의 id와 정확히 일치해야 한다.")]
        private string characterId;

        [SerializeField, Tooltip("확인창이 떠 있는 동안 멈춰 세울 Wanderer. 비워도 된다.")]
        private Wanderer wanderer;

        /// <summary>캐릭터가 눌렸을 때.</summary>
        public event Action<VillageCharacter> Clicked;

        public string CharacterId => characterId;

        /// <summary>false면 눌러도 반응하지 않는다(창이 열려 있는 동안 등).</summary>
        public bool Interactable { get; set; } = true;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable)
            {
                Clicked?.Invoke(this);
            }
        }

        /// <summary>제자리에 멈춰 세운다(확인창을 보는 동안).</summary>
        public void Hold()
        {
            if (wanderer != null)
            {
                wanderer.Hold();
            }
        }

        public void Release()
        {
            if (wanderer != null)
            {
                wanderer.Release();
            }
        }
    }
}
