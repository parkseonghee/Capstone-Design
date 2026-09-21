using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// "빨리 내리기" 버튼. 누르고 있으면 플레이어가 제자리에 선 채로 턴이 연달아 넘어가,
    /// 블록이 줄줄이 떨어져 자리를 잡는다.
    ///
    /// 한 번 탭하면 한 턴, 계속 누르고 있으면 <see cref="repeatInterval"/>마다 한 턴씩.
    /// 처음 한 턴과 반복 시작 사이에 <see cref="firstRepeatDelay"/>를 두는 건,
    /// 한 칸만 넘기고 싶어 짧게 눌렀을 때 여러 턴이 우수수 넘어가지 않게 하기 위함이다.
    ///
    /// <b>이건 공짜 행동이 아니다.</b> 한 번에 한 턴이 그대로 소비되므로
    /// 중력뿐 아니라 스폰과 폭탄 도화선도 같이 돈다 — 오래 누르면 그만큼 블록도 더 쏟아진다.
    /// 판단은 Core(GameLoop.Wait)가 하고 여기서는 타이밍만 잰다(Hard Rule 5).
    ///
    /// 속도는 전부 인스펙터 값이다(Hard Rule 1). 버튼의 위치·크기는 씬에서 정한다(Hard Rule 2).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class FastDropButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("연결")]
        [SerializeField, Tooltip("턴을 넘길 세션. 씬에서 연결한다.")]
        private GameSession session;

        [Header("속도")]
        [SerializeField, Min(0f), Tooltip("누른 직후 한 턴이 넘어간 뒤, 연속 낙하가 시작되기까지의 뜸(초). " +
                                          "짧게 탭했을 때 여러 턴이 넘어가지 않게 하는 여유다.")]
        private float firstRepeatDelay = 0.25f;

        [SerializeField, Min(0.01f), Tooltip("누르고 있는 동안 한 턴이 넘어가는 간격(초). " +
                                             "작을수록 블록이 빨리 떨어진다.")]
        private float repeatInterval = 0.08f;

        [SerializeField, Min(0), Tooltip("한 번 누르는 동안 넘길 수 있는 최대 턴 수. " +
                                         "0이면 제한 없음. 실수로 계속 눌러 판이 묻히는 걸 막고 싶을 때 쓴다.")]
        private int maxTurnsPerHold;

        /// <summary>지금 누르고 있는지. HUD가 표시를 바꾸고 싶을 때 읽어도 되게 열어 둔다.</summary>
        public bool IsHeld { get; private set; }

        private float _nextStepTime;
        private int _turnsThisHold;

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
            _turnsThisHold = 0;

            // 누르는 순간 한 턴은 바로 넘긴다 — 탭이 곧바로 반응해야 조작감이 산다.
            StepOnce();

            _nextStepTime = Time.unscaledTime + firstRepeatDelay;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsHeld = false;
        }

        private void OnDisable()
        {
            // 버튼이 꺼지거나 씬이 바뀌는 사이에 눌림 상태가 남으면 계속 턴이 넘어간다.
            IsHeld = false;
        }

        private void Update()
        {
            if (!IsHeld)
            {
                return;
            }

            if (Time.unscaledTime < _nextStepTime)
            {
                return;
            }

            if (!StepOnce())
            {
                // 판이 끝났거나 인트로 중이다. 계속 두드려 봐야 소용없으니 손을 뗀 것으로 친다.
                IsHeld = false;
                return;
            }

            _nextStepTime = Time.unscaledTime + repeatInterval;
        }

        /// <returns>턴이 실제로 진행됐으면 true.</returns>
        private bool StepOnce()
        {
            if (session == null)
            {
                Debug.LogWarning($"{nameof(FastDropButton)}: GameSession이 연결되지 않았습니다.", this);
                return false;
            }

            if (maxTurnsPerHold > 0 && _turnsThisHold >= maxTurnsPerHold)
            {
                return false;
            }

            if (!session.Wait())
            {
                return false;
            }

            _turnsThisHold++;
            return true;
        }
    }
}
