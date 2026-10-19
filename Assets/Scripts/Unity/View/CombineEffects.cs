using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 인벤토리 조합의 이펙트.
    ///
    ///  - 조합 중(재료를 다른 재료 위로 끌어 올려 미리보기 창이 떠 있는 동안): 반복 이펙트가 계속 나온다.
    ///  - 조합 완료(놓아서 조합이 성공한 순간): 한 번 터지는 이펙트가 나온다.
    ///
    /// 이펙트는 <see cref="UIEffectLayer"/>를 통해 UI 위에 그린다. 언제 켜고 끌지는 InventoryView가 정한다.
    /// </summary>
    public sealed class CombineEffects : MonoBehaviour
    {
        [SerializeField, Tooltip("이펙트를 UI 위에 그려 줄 층.")]
        private UIEffectLayer layer;

        [Header("조합 중 (반복)")]
        [SerializeField, Tooltip("끌어 올린 칸 위에서 계속 나오는 이펙트. CFXR Electrified 3.")]
        private GameObject chargingPrefab;

        [SerializeField, Min(0.01f), Tooltip("조합 중 이펙트 크기 배율. 0.6이면 인벤토리 칸 1.5~2개 넓이로 퍼진다.")]
        private float chargingScale = 0.6f;

        [Header("조합 완료 (한 번)")]
        [SerializeField, Tooltip("완성된 아이템 칸에서 한 번 터지는 이펙트. CFXR3 Hit Electric C (Air).")]
        private GameObject completePrefab;

        [SerializeField, Min(0.01f), Tooltip("조합 완료 이펙트 크기 배율. 0.65면 칸 2~3개쯤 크기로 번쩍인다. " +
                                             "2를 넘기면 빛이 화면을 통째로 덮는다.")]
        private float completeScale = 0.65f;

        private GameObject _charging;
        private RectTransform _chargingTarget;

        private void OnDisable()
        {
            EndCharging();
        }

        /// <summary>그 칸 위에서 조합 중 이펙트를 켠다. 같은 칸이면 이어서 둔다(처음부터 다시 틀지 않는다).</summary>
        public void BeginCharging(RectTransform target)
        {
            if (target == _chargingTarget && _charging != null)
            {
                return;
            }

            EndCharging();

            if (layer == null || chargingPrefab == null || target == null)
            {
                return;
            }

            _chargingTarget = target;
            _charging = layer.Spawn(chargingPrefab, ScreenCenterOf(target), chargingScale);
        }

        /// <summary>조합 중 이펙트를 끈다. 칸에서 벗어났거나 끌기가 끝났을 때.</summary>
        public void EndCharging()
        {
            if (_charging != null && layer != null)
            {
                layer.Stop(_charging);
            }

            _charging = null;
            _chargingTarget = null;
        }

        /// <summary>조합이 끝난 칸에서 완료 이펙트를 터뜨린다.</summary>
        public void PlayComplete(Vector2 screenPoint)
        {
            EndCharging();

            if (layer != null && completePrefab != null)
            {
                layer.Spawn(completePrefab, screenPoint, completeScale);
            }
        }

        /// <summary>UI 칸의 가운데를 화면 좌표로. Overlay 캔버스는 카메라 없이(null) 변환해야 맞다.</summary>
        public static Vector2 ScreenCenterOf(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera cam = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.rootCanvas.worldCamera;

            return RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
        }
    }
}
