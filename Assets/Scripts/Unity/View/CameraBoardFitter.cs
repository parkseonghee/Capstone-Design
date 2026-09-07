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

        [SerializeField, Tooltip("HUD 자리를 비우려고 보드를 위아래로 밀 때 쓰는 오프셋(월드 단위).")]
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

            float halfHeightNeeded = board.y * 0.5f + padding;
            float halfWidthNeeded = (board.x * 0.5f + padding) / _camera.aspect;

            _camera.orthographicSize = Mathf.Max(halfHeightNeeded, halfWidthNeeded);

            if (centerOnBoard)
            {
                Vector3 center = boardView.BoardWorldCenter;
                Vector3 position = _camera.transform.position;
                _camera.transform.position = new Vector3(center.x, center.y + verticalOffset, position.z);
            }

            _lastAspect = _camera.aspect;
        }
    }
}
