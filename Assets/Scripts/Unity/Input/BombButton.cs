using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 폭탄 설치 버튼. <b>누르고 있는 동안</b> 설치 가능 칸이 표시되고,
    /// 떼면 사라진다(기획서 폭탄 §1-2).
    ///
    /// Button.onClick이 아니라 포인터 눌림/뗌을 직접 받는 이유가 그것이다 —
    /// onClick은 "뗐을 때" 한 번만 오므로 꾹 누른 상태를 알 수 없다.
    ///
    /// 버튼의 위치·크기·앵커는 전부 씬에서 정한다. 이 스크립트는 RectTransform을
    /// 절대 건드리지 않고 상태만 알린다(Hard Rule 2).
    /// </summary>
    public sealed class BombButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>버튼을 누르기 시작했을 때.</summary>
        public event Action HoldStarted;

        /// <summary>버튼에서 손을 뗐을 때.</summary>
        public event Action HoldEnded;

        /// <summary>지금 눌려 있는지.</summary>
        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHeld)
            {
                return;
            }

            IsHeld = true;
            HoldStarted?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        private void OnDisable()
        {
            // 버튼이 꺼지면서 눌린 채로 남으면 표시가 지워지지 않는다.
            Release();
        }

        private void Release()
        {
            if (!IsHeld)
            {
                return;
            }

            IsHeld = false;
            HoldEnded?.Invoke();
        }
    }
}
