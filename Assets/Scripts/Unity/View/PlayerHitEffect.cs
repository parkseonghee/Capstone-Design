using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 플레이어 공격이 들어간 칸마다 피격 이펙트를 띄운다(<see cref="GameLoop.PlayerStruck"/>).
    ///
    ///  - <b>맞기만 했을 때</b>: 노란 만화풍 타격 별(CFXR Hit D 3D (Yellow)).
    ///    빨강은 플레이어가 맞을 때 쓰는 색이라 피했고, 파란 물 배경 위에서 잘 보이는 노랑으로 골랐다.
    ///  - <b>처치했을 때</b>: 타격 별 + 하얀 연기 "펑"(CFXR3 Hit Misc F Smoke) — 쓰레기가 치워져 사라지는 느낌.
    ///
    /// 범위 공격(캐릭터 B)·연쇄로 같이 맞은 칸도 하나씩 터진다. 이펙트는 끝나면 스스로 사라진다
    /// (CFXR_Effect의 Clear Behavior = Destroy). 배치·크기 규칙은 <see cref="BombExplosionEffect"/>와 같다.
    /// </summary>
    public sealed class PlayerHitEffect : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("공격 알림을 받을 세션. 웨이브마다 루프가 바뀌어 다시 구독한다.")]
        private GameSession session;

        [SerializeField, Tooltip("맞은 칸의 월드 위치를 얻을 보드.")]
        private BoardView board;

        [Header("이펙트")]
        [SerializeField, Tooltip("공격이 들어간 칸마다 나올 이펙트. CFXR Hit D 3D (Yellow).")]
        private GameObject hitPrefab;

        [SerializeField, Tooltip("그 공격으로 죽었을 때 추가로 나올 이펙트. CFXR3 Hit Misc F Smoke. 비우면 타격 이펙트만.")]
        private GameObject killPrefab;

        [SerializeField, Min(0.01f), Tooltip("타격 이펙트 크기. 칸 크기에 곱해진다.")]
        private float hitScale = 0.8f;

        [SerializeField, Min(0.01f), Tooltip("처치 이펙트 크기. 칸 크기에 곱해진다.")]
        private float killScale = 0.9f;

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
                _loop.PlayerStruck -= HandleStruck;
            }

            _loop = loop;

            if (_loop != null)
            {
                _loop.PlayerStruck += HandleStruck;
            }
        }

        private void HandleStruck(Vector2Int cell, bool killed)
        {
            if (board == null || board.CellRoot == null)
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

            float cell01 = board.CellSize * root.lossyScale.x;
            Spawn(hitPrefab, world, hitScale * cell01);

            if (killed)
            {
                Spawn(killPrefab, world, killScale * cell01);
            }
        }

        private void Spawn(GameObject prefab, Vector3 world, float size)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(prefab, world, prefab.transform.rotation);
            instance.transform.localScale = prefab.transform.localScale * size;

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
