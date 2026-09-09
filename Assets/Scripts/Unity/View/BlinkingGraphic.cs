using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// Graphic(예: Text, Image)의 알파를 사인파로 반복시켜 페이드 인/아웃 깜빡임을 연출한다.
    /// "TOUCH TO START" 같은 안내 문구에 쓴다.
    ///
    /// 주기·알파 범위는 인스펙터에서 조절한다(Hard Rule 1).
    /// 매 프레임 새 오브젝트를 만들지 않는다(Hard Rule 8).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class BlinkingGraphic : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("한 번 깜빡이는 데 걸리는 시간(초).")]
        private float cycleSeconds = 1.2f;

        [SerializeField, Range(0f, 1f), Tooltip("가장 흐려질 때의 알파값.")]
        private float minAlpha = 0.2f;

        [SerializeField, Range(0f, 1f), Tooltip("가장 또렷할 때의 알파값.")]
        private float maxAlpha = 1f;

        private Graphic _graphic;

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
        }

        private void Update()
        {
            float wave = (Mathf.Sin(Time.time * (2f * Mathf.PI / cycleSeconds)) + 1f) * 0.5f;
            Color color = _graphic.color;
            color.a = Mathf.Lerp(minAlpha, maxAlpha, wave);
            _graphic.color = color;
        }
    }
}
