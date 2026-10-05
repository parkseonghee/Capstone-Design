using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 세로 보드가 기기 화면 비율에 상관없이 화면 안에 들어오게 카메라를 맞춘다.
    /// 크기는 BoardView가 알려주는 <b>보이는 영역</b>(플레이 8줄 + 프리뷰 1/6)을 쓴다.
    ///
    /// 건드리는 것은 <b>카메라의 orthographicSize와 위치뿐</b>이다.
    /// Canvas·RectTransform·앵커·해상도는 절대 손대지 않는다(Hard Rule 2).
    /// 직접 화각을 잡고 싶으면 인스펙터에서 fitOnStart를 끄면 이 컴포넌트는 아무 일도 하지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraBoardFitter : MonoBehaviour
    {
        [SerializeField, Tooltip("크기를 읽어올 보드 뷰.")]
        private BoardView boardView;

        [SerializeField, Tooltip("끄면 이 컴포넌트는 카메라를 전혀 건드리지 않는다.")]
        private bool fitOnStart = true;

        [SerializeField, Tooltip("화면 회전·해상도 변경 시 다시 맞출지.")]
        private bool refitOnAspectChange = true;

        [SerializeField, Tooltip("보드를 카메라 중앙에 오도록 카메라를 옮길지.")]
        private bool centerOnBoard = true;

        [SerializeField, Min(0f), Tooltip("보드 가장자리와 화면 사이 여백(월드 단위).")]
        private float padding = 0.5f;

        [Header("보드가 쓸 화면 구간")]
        [SerializeField, Range(0f, 0.8f), Tooltip("위쪽 HUD에 내주는 몫(화면 높이 비율).")]
        private float topMargin = 0.129f;

        [SerializeField, Range(0f, 0.8f), Tooltip("아래쪽 조작·인벤토리에 내주는 몫(화면 높이 비율).")]
        private float bottomMargin = 0.275f;

        [SerializeField, Tooltip("구간 안에서 보드를 한 번 더 밀고 싶을 때 쓰는 미세 조정(월드 단위).")]
        private float verticalOffset;

        private Camera _camera;
        private float _lastAspect;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (!fitOnStart || boardView == null || _camera == null || !_camera.orthographic)
            {
                return;
            }

            if (!refitOnAspectChange && !Mathf.Approximately(_lastAspect, 0f))
            {
                return;
            }

            if (Mathf.Approximately(_lastAspect, _camera.aspect) && _lastAspect != 0f)
            {
                return;
            }

            Fit();
        }

        public void Fit()
        {
            Vector2 board = boardView.BoardWorldSize;
            if (board.x <= 0f || board.y <= 0f || _camera == null || _camera.aspect <= 0f)
            {
                return;
            }

            // 보드는 화면 전체가 아니라 위아래 여백을 뺀 <b>구간</b> 안에 들어가야 한다.
            // 구간이 화면의 band(예: 0.596)만 차지하므로, 같은 보드를 담으려면
            // 카메라가 그만큼 더 넓게 봐야 한다 — 그래서 필요한 높이를 band로 나눈다.
            float band = Mathf.Clamp(1f - topMargin - bottomMargin, 0.05f, 1f);

            float halfHeightNeeded = (board.y * 0.5f + padding) / band;
            float halfWidthNeeded = (board.x * 0.5f + padding) / _camera.aspect;

            _camera.orthographicSize = Mathf.Max(halfHeightNeeded, halfWidthNeeded);

            if (centerOnBoard)
            {
                Vector3 center = boardView.BoardWorldCenter;
                Vector3 position = _camera.transform.position;

                // 구간의 한가운데가 화면 중심에서 얼마나 떨어져 있는지(화면 높이 비율).
                // 보드 중심이 거기 오도록 카메라를 반대로 민다.
                float bandCenter = bottomMargin + band * 0.5f;
                float shift = (bandCenter - 0.5f) * 2f * _camera.orthographicSize;

                _camera.transform.position = new Vector3(center.x, center.y - shift + verticalOffset, position.z);
            }

            _lastAspect = _camera.aspect;
        }
    }
}
