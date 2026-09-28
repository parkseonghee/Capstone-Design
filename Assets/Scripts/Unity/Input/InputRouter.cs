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
    ///  - 화면 슬라이드 (모바일 기본 조작)
    ///  - 외부에서 Emit을 부르는 버튼(있다면)
    ///  - 키보드 방향키/WASD (에디터에서 빠르게 테스트하기 위함)
    ///
    /// <b>슬라이드는 "플로팅" 방식이다.</b>
    ///  - 손가락이 처음 닿은 자리가 원점이 된다. 조작 영역을 화면에 고정하지 않으므로
    ///    엄지가 어디에 있든 바로 조작할 수 있고, 판을 가리는 UI도 안 생긴다.
    ///  - 임계값은 픽셀이 아니라 <b>밀리미터</b>로 잡는다. 픽셀로 잡으면 고DPI 기기에서
    ///    임계값이 2mm도 안 돼 탭이 흔들리기만 해도 이동으로 새어 나간다.
    ///
    /// <b>기본은 한 번 터치에 한 칸이다</b>(<see cref="oneStepPerTouch"/>).
    /// 손을 떼지 않고 계속 밀면 연달아 나가게도 할 수 있지만(래칫), 그렇게 하면
    /// 판이 매 턴 바뀌는 이 게임에서는 의도한 것보다 훨씬 멀리 가 버린다.
    /// 한 칸 나간 즉시 입력을 닫고, 다시 움직이려면 손을 떼었다 대야 한다.
    ///
    /// 임계값·활성화 여부는 전부 인스펙터에서 조절한다(Hard Rule 1).
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        /// <summary>슬라이드가 어디서 시작해 어디를 밀고 있는지. 표시용 뷰가 읽는다.</summary>
        public struct SlideState
        {
            /// <summary>지금 슬라이드를 받고 있는지.</summary>
            public bool Active;

            /// <summary>손가락이 닿은(혹은 마지막으로 한 칸 나간) 기준점. 스크린 좌표.</summary>
            public Vector2 Origin;

            /// <summary>지금 손가락 위치. 스크린 좌표.</summary>
            public Vector2 Current;

            /// <summary>기울기가 데드존을 넘어 "어느 쪽을 밀고 있는지"가 정해졌는지.</summary>
            public bool HasDirection;

            /// <summary>밀고 있는 방향. <see cref="HasDirection"/>이 false면 의미 없다.</summary>
            public Direction Direction;

            /// <summary>임계값 대비 얼마나 밀었는지(0~1). 표시를 채우는 데 쓴다.</summary>
            public float Progress;
        }

        [Header("슬라이드 (모바일)")]
        [SerializeField, Tooltip("화면 슬라이드로 방향을 입력받을지.")]
        private bool swipeEnabled = true;

        [SerializeField, Min(1f), Tooltip("한 칸 나가는 데 필요한 이동 거리(밀리미터). " +
                                          "픽셀이 아니라 실제 길이라 기기 DPI가 달라도 손맛이 같다.")]
        private float slideThresholdMillimeters = 4.5f;

        [SerializeField, Range(0.05f, 0.9f), Tooltip("방향 표시가 뜨기 시작하는 지점(임계값 대비). " +
                                                     "이보다 덜 밀면 탭으로 본다.")]
        private float directionDeadzone = 0.25f;

        [SerializeField, Tooltip("한 번 터치에 한 칸만 나간다. 한 칸 움직이면 그 터치는 거기서 끝나고, " +
                                 "다시 움직이려면 손을 떼었다 대야 한다.\n" +
                                 "끄면 손을 댄 채로 계속 밀어 연달아 움직일 수 있다(래칫).")]
        private bool oneStepPerTouch = true;

        [SerializeField, Min(0f), Tooltip("연속 이동의 최소 간격(초). 래칫일 때만 쓰인다 — " +
                                          "빠르게 쓸어도 이 간격보다 자주는 안 나간다.")]
        private float minStepIntervalSeconds = 0.07f;

        [SerializeField, Tooltip("UI(버튼) 위에서 시작한 드래그는 슬라이드로 치지 않는다.")]
        private bool ignoreSwipeOverUI = true;

        [SerializeField, Min(1f), Tooltip("Screen.dpi를 못 읽는 기기에서 대신 쓸 값. " +
                                          "안드로이드 기본 밀도(mdpi)가 160이다.")]
        private float fallbackDpi = 160f;

        [Header("진동")]
        [SerializeField, Tooltip("한 칸 나갈 때마다 짧게 진동시킬지. 표시를 못 봐도 입력이 먹은 걸 알 수 있다.")]
        private bool hapticsEnabled = true;

        [SerializeField, Min(1), Tooltip("진동 길이(밀리초). 짧을수록 '틱' 하는 느낌이다.")]
        private int hapticMilliseconds = 12;

        [Header("키보드 (에디터 테스트용)")]
        [SerializeField, Tooltip("방향키와 WASD로도 입력받을지.")]
        private bool keyboardEnabled = true;

        /// <summary>방향 입력이 확정됐을 때. GameSession이 구독한다.</summary>
        public event Action<Direction> DirectionPressed;

        /// <summary>슬라이드 상태가 바뀌었을 때. 표시용 뷰가 구독해 <see cref="Slide"/>를 읽는다.</summary>
        public event Action SlideChanged;

        /// <summary>지금 슬라이드 상태. 매 프레임 구조체를 새로 만들지 않도록 필드로 들고 있다(Hard Rule 8).</summary>
        public SlideState Slide => _slide;

        /// <summary>
        /// 켜 두면 슬라이드를 무시한다. 폭탄 설치처럼 <b>보드를 탭해야 하는</b> 조작이
        /// 도는 동안 켠다 — 조준 탭이 손떨림 때문에 이동으로 새는 걸 막는다.
        /// </summary>
        public bool SwipeSuppressed
        {
            get => _swipeSuppressed;
            set
            {
                if (_swipeSuppressed == value)
                {
                    return;
                }

                _swipeSuppressed = value;
                if (_swipeSuppressed)
                {
                    EndSlide();
                }
            }
        }

        private SlideState _slide;
        private bool _swipeSuppressed;
        private bool _dragging;
        private float _nextStepTime;

        /// <summary>외부(버튼 등)에서 직접 방향을 넣는 통로.</summary>
        public void Emit(Direction direction)
        {
            DirectionPressed?.Invoke(direction);
        }

        /// <summary>한 칸 나가는 데 필요한 픽셀 거리. mm를 이 기기의 DPI로 환산한 값이다.</summary>
        public float ThresholdPixels
        {
            get
            {
                float dpi = Screen.dpi;
                if (dpi <= 1f)
                {
                    dpi = fallbackDpi;
                }

                // 8px 밑으로는 안 내린다 — 어떤 기기든 그 아래는 손떨림과 구분이 안 된다.
                return Mathf.Max(8f, slideThresholdMillimeters * dpi / 25.4f);
            }
        }

        private void OnDisable()
        {
            EndSlide();
        }

        private void Update()
        {
            if (keyboardEnabled)
            {
                ReadKeyboard();
            }

            if (swipeEnabled && !_swipeSuppressed)
            {
                ReadSlide();
            }
            else
            {
                // 도중에 꺼졌다면 진행 중이던 슬라이드도 접는다. 안 그러면 표시가 화면에 남는다.
                EndSlide();
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

        private void ReadSlide()
        {
            // Pointer.current는 터치스크린과 마우스를 함께 덮는다 — 에디터에서도 같은 코드로 테스트된다.
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                EndSlide();
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                BeginSlide(pointer.position.ReadValue());
            }

            // 손을 떼는 판정은 wasReleasedThisFrame이 아니라 isPressed로 본다.
            // 한 프레임 안에서 누르고 떼면(짧은 탭) 두 플래그가 같은 프레임에 함께 서는데,
            // 누름만 처리하고 빠지면 슬라이드가 안 닫혀 손을 뗀 뒤에도 포인터를 따라다닌다.
            if (_dragging && !pointer.press.isPressed)
            {
                EndSlide();
                return;
            }

            if (!_dragging)
            {
                return;
            }

            UpdateSlide(pointer.position.ReadValue());
        }

        private void BeginSlide(Vector2 position)
        {
            if (ignoreSwipeOverUI && IsPointerOverUI())
            {
                // UI 위에서 시작한 드래그는 버튼 몫이다. 슬라이드로 가로채지 않는다.
                _dragging = false;
                return;
            }

            _dragging = true;
            _slide.Active = true;
            _slide.Origin = position;
            _slide.Current = position;
            _slide.HasDirection = false;
            _slide.Progress = 0f;
            _nextStepTime = 0f;
            SlideChanged?.Invoke();
        }

        private void UpdateSlide(Vector2 position)
        {
            _slide.Current = position;

            Vector2 delta = position - _slide.Origin;
            float threshold = ThresholdPixels;
            float distance = delta.magnitude;

            _slide.Progress = Mathf.Clamp01(distance / threshold);
            _slide.HasDirection = _slide.Progress >= directionDeadzone;
            if (_slide.HasDirection)
            {
                _slide.Direction = ToDirection(delta);
            }

            if (distance < threshold)
            {
                SlideChanged?.Invoke();
                return;
            }

            if (!oneStepPerTouch && Time.unscaledTime < _nextStepTime)
            {
                SlideChanged?.Invoke();
                return;
            }

            Direction direction = ToDirection(delta);

            if (hapticsEnabled)
            {
                Haptics.Tick(hapticMilliseconds);
            }

            if (oneStepPerTouch)
            {
                // 한 칸 나갔으면 이 터치는 여기서 끝이다. 손을 떼었다 다시 대야 또 움직인다.
                // EndSlide가 _dragging을 내리므로 손가락이 붙어 있어도 더 이상 읽지 않는다.
                //
                // Emit보다 먼저 닫는다 — Emit이 부르는 쪽에서 표시를 다시 그릴 수 있는데,
                // 그때 슬라이드가 아직 살아 있으면 이미 소비된 입력이 화면에 남는다.
                EndSlide();
                Emit(direction);
                return;
            }

            // 래칫: 원점을 여기로 옮겨 계속 밀 수 있게 한다.
            // 남은 여분을 이월하지 않는 건, 빠르게 쓸었을 때 한 프레임에 여러 턴이
            // 몰아서 지나가는 것보다 "민 만큼만 간다"가 예측하기 쉽기 때문이다.
            _slide.Origin = position;
            _slide.Progress = 0f;
            _nextStepTime = Time.unscaledTime + minStepIntervalSeconds;

            Emit(direction);
            SlideChanged?.Invoke();
        }

        private void EndSlide()
        {
            _dragging = false;

            if (!_slide.Active)
            {
                return;
            }

            _slide.Active = false;
            _slide.HasDirection = false;
            _slide.Progress = 0f;
            SlideChanged?.Invoke();
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

        /// <summary>방향을 <b>스크린</b> 기준 단위 벡터로. 표시(화살표)를 돌릴 때 쓴다.</summary>
        public static Vector2 ToScreenVector(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return Vector2.up;
                case Direction.Down: return Vector2.down;
                case Direction.Left: return Vector2.left;
                default: return Vector2.right;
            }
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
