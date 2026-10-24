using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을을 돌아다니는 캐릭터. 웨이포인트 중 하나를 골라 천천히 걸어가고, 도착하면 잠깐 쉬었다가 다시 걷는다.
    ///
    /// 걷는 동안 몸(Visual)이 위아래로 통통 튀고, 가는 방향에 따라 좌우로 뒤집힌다.
    /// 콜라이더가 없어 터치 대상이 아니다 — 건물 위를 지나가도 건물 터치를 가리지 않는다.
    ///
    /// 계층: Wanderer(이 컴포넌트, 발 위치) → Visual(SpriteRenderer, 통통 튀는 부분).
    /// </summary>
    public sealed class Wanderer : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("통통 튀고 좌우 반전될 몸.")]
        private SpriteRenderer body;
        [SerializeField, Tooltip("걸어갈 수 있는 지점들(길 위의 빈 오브젝트).")]
        private List<Transform> waypoints = new List<Transform>();

        [Header("이동")]
        [SerializeField, Min(0.01f), Tooltip("초당 이동 거리(유닛).")]
        private float moveSpeed = 0.6f;
        [SerializeField, Min(0f)] private float restMinSeconds = 1f;
        [SerializeField, Min(0f)] private float restMaxSeconds = 3f;
        [SerializeField, Tooltip("스프라이트가 원래 오른쪽을 보고 있으면 체크. 왼쪽으로 갈 때 뒤집는다.")]
        private bool spriteFacesRight = true;

        [Header("통통")]
        [SerializeField, Min(0f)] private float hopHeight = 0.06f;
        [SerializeField, Min(0.01f), Tooltip("한 번 올라갔다 내려오는 시간의 절반(초).")]
        private float hopHalfDuration = 0.15f;

        [Header("정렬")]
        [SerializeField, Tooltip("1 유닛당 Sorting Order 차이. 건물(Building)과 같은 값을 쓴다.")]
        private float ordersPerUnit = 100f;
        [SerializeField] private int orderOffset;

        private Transform _bodyTransform;
        private float _bodyBaseY;
        private int _currentIndex = -1;
        private Tween _hop;

        private void Awake()
        {
            _bodyTransform = body.transform;
            _bodyBaseY = _bodyTransform.localPosition.y;
        }

        private void Start()
        {
            if (waypoints.Count < 2)
            {
                Debug.LogWarning($"{nameof(Wanderer)}: 웨이포인트가 2개 이상 있어야 걸어다닙니다.", this);
                return;
            }

            Rest();
        }

        private void LateUpdate()
        {
            body.sortingOrder = YSorting.OrderFor(transform.position.y, ordersPerUnit, orderOffset);
        }

        private void OnDestroy()
        {
            this.DOKill();
            transform.DOKill();
            _bodyTransform.DOKill();
        }

        /// <summary>걷던 것을 그 자리에서 멈춘다. <see cref="Release"/>로 이어서 걷는다.</summary>
        public void Hold()
        {
            DOTween.Pause(this);
            transform.DOPause();
            _bodyTransform.DOPause();
        }

        public void Release()
        {
            DOTween.Play(this);
            transform.DOPlay();
            _bodyTransform.DOPlay();
        }

        private void Rest()
        {
            DOVirtual.DelayedCall(Random.Range(restMinSeconds, restMaxSeconds), WalkToNext)
                .SetTarget(this);
        }

        private void WalkToNext()
        {
            int next;
            if (_currentIndex < 0)
            {
                next = Random.Range(0, waypoints.Count);
            }
            else
            {
                next = Random.Range(0, waypoints.Count - 1);
                if (next >= _currentIndex)
                {
                    next++; // 지금 서 있는 지점은 건너뛴다.
                }
            }

            _currentIndex = next;
            Vector3 target = waypoints[next].position;
            target.z = transform.position.z;

            float dx = target.x - transform.position.x;
            if (!Mathf.Approximately(dx, 0f))
            {
                body.flipX = spriteFacesRight ? dx < 0f : dx > 0f;
            }

            StartHop();
            float seconds = Vector3.Distance(transform.position, target) / moveSpeed;
            transform.DOMove(target, seconds)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    StopHop();
                    Rest();
                });
        }

        private void StartHop()
        {
            _hop?.Kill();
            _hop = _bodyTransform.DOLocalMoveY(_bodyBaseY + hopHeight, hopHalfDuration)
                .SetEase(Ease.OutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopHop()
        {
            _hop?.Kill();
            _hop = _bodyTransform.DOLocalMoveY(_bodyBaseY, hopHalfDuration).SetEase(Ease.InQuad);
        }
    }
}
