using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 방향 입력을 한곳으로 모으는 얇은 라우터. 게임 규칙은 모른다.
    ///
    /// 세 가지 경로를 모두 받는다:
    ///  - 화면 스와이프 (모바일 기본 조작)
    ///  - 화면 위 D-Pad 버튼 (DirectionButton이 Emit을 부른다)
    ///  - 키보드 방향키/WASD (에디터에서 빠르게 테스트하기 위함)
    ///
    /// 임계값·활성화 여부는 전부 인스펙터에서 조절한다(Hard Rule 1).
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        [Header("스와이프 (모바일)")]
        [SerializeField, Tooltip("드래그로 방향을 입력받을지.")]
        private bool swipeEnabled = true;

        [SerializeField, Min(1f), Tooltip("스와이프로 인정할 최소 이동 거리(픽셀). 기기 DPI에 따라 조절.")]
        private float swipeThresholdPixels = 40f;

        [SerializeField, Tooltip("UI(D-Pad·버튼) 위에서 시작한 드래그는 스와이프로 치지 않는다.")]
        private bool ignoreSwipeOverUI = true;

        [Header("키보드 (에디터 테스트용)")]
        [SerializeField, Tooltip("방향키와 WASD로도 입력받을지.")]
        private bool keyboardEnabled = true;

        /// <summary>방향 입력이 확정됐을 때. GameSession이 구독한다.</summary>
        public event Action<Direction> DirectionPressed;

        private bool _dragging;
        private bool _dragConsumed;
        private Vector2 _dragStart;

        /// <summary>D-Pad 버튼 등 외부에서 직접 방향을 넣는 통로.</summary>
        public void Emit(Direction direction)
        {
            DirectionPressed?.Invoke(direction);
        }

        private void Update()
        {
            if (keyboardEnabled)
            {
                ReadKeyboard();
            }

            if (swipeEnabled)
            {
                ReadSwipe();
            }
        }

        private void ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) Emit(Direction.Up);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) Emit(Direction.Down);
            else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) Emit(Direction.Left);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) Emit(Direction.Right);
        }

        private void ReadSwipe()
        {
            // Pointer.current는 터치스크린과 마우스를 함께 덮는다 — 에디터에서도 같은 코드로 테스트된다.
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                _dragging = !(ignoreSwipeOverUI && IsPointerOverUI());
                _dragConsumed = false;
                _dragStart = pointer.position.ReadValue();
                return;
            }

            if (!_dragging || _dragConsumed)
            {
                if (pointer.press.wasReleasedThisFrame)
                {
                    _dragging = false;
                }

                return;
            }

            Vector2 delta = pointer.position.ReadValue() - _dragStart;

            // 임계값을 넘는 순간 방향을 확정한다. 손가락을 떼기 전에 반응해야 조작감이 산다.
            if (delta.sqrMagnitude < swipeThresholdPixels * swipeThresholdPixels)
            {
                if (pointer.press.wasReleasedThisFrame)
                {
                    _dragging = false;
                }

                return;
            }

            Emit(ToDirection(delta));
            _dragConsumed = true;

            if (pointer.press.wasReleasedThisFrame)
            {
                _dragging = false;
            }
        }

        /// <summary>화면 좌표는 위쪽이 +y지만 보드는 위쪽이 row 감소다 — 그 변환을 여기서 한다.</summary>
        private static Direction ToDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x > 0f ? Direction.Right : Direction.Left;
            }

            return delta.y > 0f ? Direction.Up : Direction.Down;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
