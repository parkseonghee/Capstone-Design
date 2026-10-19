using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 폭탄이 터질 때 그 칸에 폭발 이펙트(CFXR Explosion 1)를 띄운다.
    ///
    /// 보드는 메인 카메라가 그리는 월드 오브젝트라 이펙트도 같은 월드에 그대로 띄운다 —
    /// 인벤토리처럼 UI 위에 겹칠 필요가 없다. 끝나면 이펙트가 스스로 사라진다(CFXR_Effect의 Clear Behavior).
    ///
    /// 연쇄로 여러 개가 터지면 각 폭탄 자리에서 하나씩 터진다(GameLoop.BombExploded).
    /// </summary>
    public sealed class BombExplosionEffect : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("폭발 알림을 받을 세션. 웨이브마다 루프가 바뀌어 다시 구독한다.")]
        private GameSession session;

        [SerializeField, Tooltip("터진 칸의 월드 위치를 얻을 보드.")]
        private BoardView board;

        [Header("이펙트")]
        [SerializeField, Tooltip("폭탄이 터질 때 나올 이펙트. CFXR Explosion 1.")]
        private GameObject explosionPrefab;

        [SerializeField, Min(0.01f), Tooltip("폭발 반경 1(3x3칸)일 때의 크기 배율. 칸 크기에 곱해진다.")]
        private float scale = 1f;

        [SerializeField, Tooltip("반경이 커지면(유물 '대형 폭약') 이펙트도 그만큼 키울지.")]
        private bool scaleWithRadius = true;

        [SerializeField, Tooltip("이펙트의 정렬 순서에 더할 값. 보드 위 그림(0~4)보다 커야 블록에 가려지지 않는다.")]
        private int sortingOrderOffset = 10;

        [SerializeField, Tooltip("카메라 쪽으로 당겨 놓을 거리(월드). 블록과 같은 깊이면 파묻혀 보일 수 있다.")]
        private float towardCamera = 0.5f;

        private GameLoop _loop;

        private void OnEnable()
        {
            if (session != null)
            {
                session.RunStarted += HandleRunStarted;
                Subscribe(session.Loop);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.RunStarted -= HandleRunStarted;
            }

            Subscribe(null);
        }

        private void HandleRunStarted(GameLoop loop)
        {
            Subscribe(loop);
        }

        private void Subscribe(GameLoop loop)
        {
            if (_loop != null)
            {
                _loop.BombExploded -= HandleBombExploded;
            }

            _loop = loop;

            if (_loop != null)
            {
                _loop.BombExploded += HandleBombExploded;
            }
        }

        private void HandleBombExploded(Vector2Int cell, int radius)
        {
            if (explosionPrefab == null || board == null || board.CellRoot == null)
            {
                return;
            }

            Transform root = board.CellRoot;
            Vector3 world = root.TransformPoint(board.CellToLocalPosition(cell));

            Camera cam = Camera.main;
            if (cam != null && towardCamera > 0f)
            {
                world += (cam.transform.position - world).normalized * towardCamera;
            }

            GameObject instance = Instantiate(explosionPrefab, world, explosionPrefab.transform.rotation);

            // 반경 1이 3칸 폭이다. 반경이 2면 5칸 폭이니 5/3배로 키운다.
            float size = scale * board.CellSize * root.lossyScale.x;
            if (scaleWithRadius)
            {
                size *= (2f * Mathf.Max(1, radius) + 1f) / 3f;
            }

            instance.transform.localScale = explosionPrefab.transform.localScale * size;

            if (sortingOrderOffset != 0)
            {
                foreach (var r in instance.GetComponentsInChildren<ParticleSystemRenderer>())
                {
                    r.sortingOrder += sortingOrderOffset;
                }
            }
        }
    }
}
