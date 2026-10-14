using System;
using DG.Tweening;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 플레이어의 "느낌"만 담당한다(포켓던전류 범프 연출). 로직 위치는 전혀 모른다 —
    /// 부모(Player 루트)의 localPosition만 건드리지 않으면 실제 좌표는 절대 어긋나지 않는다.
    ///
    /// 이 컴포넌트가 바꾸는 건 자기 자신(Player/Visual)의 localPosition / localRotation /
    /// localScale뿐이다. 연출이 끝나면 반드시 (0,0,0) / identity / (1,1,1)로 돌아온다.
    ///
    /// 연출 도중 새 입력이 들어오면 이전 트윈을 죽이고 <b>항상 기본값에서</b> 다시 시작한다
    /// (ResetToRest) — 안 그러면 값이 누적돼 캐릭터가 점점 비뚤어지거나 밀려난다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerVisualFeedback : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("이 오브젝트(Visual)에 달린 스프라이트. 비우면 같은 오브젝트에서 찾는다.")]
        private SpriteRenderer spriteRenderer;

        [Header("1. 좌우 바라보기")]
        [SerializeField, Tooltip("원본 스프라이트가 왼쪽을 보고 있는지. 켜져 있으면 오른쪽으로 " +
                                 "움직이거나 칠 때만 flipX를 켠다.")]
        private bool artFacesLeft = true;

        [Header("2. 좌우 이동 기울기 (Z 회전)")]
        [SerializeField, Tooltip("기울어지는 각도(도). 부호를 바꾸면 기울어지는 방향이 반대가 된다 — " +
                                 "실제로 돌려 보고 반대로 보이면 이 값의 부호를 뒤집는다.")]
        private float tiltAngle = 10f;

        [SerializeField, Min(0f)] private float tiltOutDuration = 0.05f;
        [SerializeField, Min(0f)] private float tiltBackDuration = 0.1f;
        [SerializeField] private Ease tiltOutEase = Ease.OutQuad;
        [SerializeField] private Ease tiltBackEase = Ease.OutBack;

        [Header("3. 위아래 이동 (살짝 튀어나감 + 스쿼시&스트레치)")]
        [SerializeField, Min(0f), Tooltip("이동 방향으로 먼저 나가는 거리(월드 유닛, 대략).")]
        private float bobDistance = 0.1f;

        [SerializeField, Min(0f)] private float bobOutDuration = 0.05f;
        [SerializeField, Min(0f)] private float bobBackDuration = 0.1f;
        [SerializeField] private Ease bobOutEase = Ease.OutQuad;
        [SerializeField] private Ease bobBackEase = Ease.OutQuad;

        [SerializeField, Min(0.01f), Tooltip("이동 축 방향 스케일(늘어남).")]
        private float bobStretchAlongAxis = 1.1f;

        [SerializeField, Min(0.01f), Tooltip("이동 축의 반대(가로질러) 스케일(줄어듦).")]
        private float bobSquashAcrossAxis = 0.9f;

        [Header("4. 공격(범프) 돌진")]
        [SerializeField, Min(0f), Tooltip("적 방향으로 돌진하는 거리(월드 유닛, 대략).")]
        private float attackLungeDistance = 0.3f;

        [SerializeField, Min(0f)] private float attackOutDuration = 0.06f;
        [SerializeField, Min(0f)] private float attackBackDuration = 0.12f;
        [SerializeField] private Ease attackOutEase = Ease.OutQuad;
        [SerializeField] private Ease attackBackEase = Ease.OutQuad;

        /// <summary>다른 코드가 지금 쓰고 있는 스프라이트를 읽거나(색 비교 등) 바꿀 수 있게 연다.</summary>
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        private Sequence _sequence;
        private bool _facingRight;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void OnDisable()
        {
            // 풀로 돌아가기 전엔 BoardView가 SnapIdle을 직접 부르지만, 그 경로를 안 타고
            // 꺼지는 경우(씬 전환 등)에도 DOTween이 죽은 Transform을 붙들고 있지 않게 한다.
            KillSequence();
        }

        /// <summary>
        /// 좌우로 한 칸 이동했다(진짜 이동만 — 공격은 PlayAttack). direction은 월드 기준
        /// 단위 벡터 성분(각 -1/0/1)이다.
        /// </summary>
        public void PlayMove(Vector2Int direction)
        {
            if (direction.x != 0)
            {
                UpdateFacing(direction.x > 0);
                PlayTilt(direction.x);
            }
            else if (direction.y != 0)
            {
                PlayBob(direction.y);
            }
        }

        /// <summary>
        /// 적이 있는 칸으로 범프 공격했다(실제 이동은 없음). <paramref name="onHit"/>은
        /// 돌진이 끝에 닿는 순간(= 복귀를 시작하기 직전) 호출된다 — 피격 이펙트·사운드 등을
        /// 때리는 타이밍에 맞춰 걸고 싶을 때 쓰라고 열어 둔 훅이다. 비워도 된다.
        /// </summary>
        public void PlayAttack(Vector2Int direction, Action onHit)
        {
            if (direction.x != 0)
            {
                UpdateFacing(direction.x > 0);
            }

            KillSequence();
            ResetToRest();

            Vector3 lungePos = new Vector3(direction.x * attackLungeDistance, direction.y * attackLungeDistance, 0f);

            _sequence = DOTween.Sequence();
            _sequence.Append(transform.DOLocalMove(lungePos, attackOutDuration).SetEase(attackOutEase));
            _sequence.AppendCallback(() => onHit?.Invoke());
            _sequence.Append(transform.DOLocalMove(Vector3.zero, attackBackDuration).SetEase(attackBackEase));
        }

        /// <summary>진행 중인 연출을 즉시 끊고 기본 상태로 되돌린다. 풀에 반납되기 전에 부른다.</summary>
        public void SnapIdle()
        {
            KillSequence();
            ResetToRest();
        }

        private void PlayTilt(int xSign)
        {
            KillSequence();
            ResetToRest();

            // 진행 방향으로 윗부분이 기울어지게. 반대로 보이면 tiltAngle의 부호를 뒤집는다.
            float targetZ = -xSign * tiltAngle;

            _sequence = DOTween.Sequence();
            _sequence.Append(transform.DOLocalRotate(new Vector3(0f, 0f, targetZ), tiltOutDuration).SetEase(tiltOutEase));
            _sequence.Append(transform.DOLocalRotate(Vector3.zero, tiltBackDuration).SetEase(tiltBackEase));
        }

        private void PlayBob(int ySign)
        {
            KillSequence();
            ResetToRest();

            Vector3 targetPos = new Vector3(0f, ySign * bobDistance, 0f);
            // 이동 축(y)으로 늘어나고, 가로지르는 축(x)으로는 줄어든다.
            Vector3 targetScale = new Vector3(bobSquashAcrossAxis, bobStretchAlongAxis, 1f);

            _sequence = DOTween.Sequence();
            _sequence.Append(transform.DOLocalMove(targetPos, bobOutDuration).SetEase(bobOutEase));
            _sequence.Join(transform.DOScale(targetScale, bobOutDuration).SetEase(bobOutEase));
            _sequence.Append(transform.DOLocalMove(Vector3.zero, bobBackDuration).SetEase(bobBackEase));
            _sequence.Join(transform.DOScale(Vector3.one, bobBackDuration).SetEase(bobBackEase));
        }

        private void UpdateFacing(bool right)
        {
            if (_facingRight == right && spriteRenderer != null && spriteRenderer.flipX == (right != artFacesLeft))
            {
                return;
            }

            _facingRight = right;
            if (spriteRenderer != null)
            {
                // 원본이 왼쪽을 본다면: 오른쪽을 바라볼 때만 뒤집는다.
                spriteRenderer.flipX = artFacesLeft ? right : !right;
            }
        }

        private void KillSequence()
        {
            if (_sequence != null && _sequence.IsActive())
            {
                _sequence.Kill(false);
            }

            _sequence = null;
        }

        /// <summary>트윈이 뭘 건드리고 있었든, 세 값을 전부 기본값으로 되돌린다 — 값 누적 방지의 핵심.</summary>
        private void ResetToRest()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }
}
