using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 파티클 이펙트를 <b>UI 위에</b> 그리는 층.
    ///
    /// 게임 UI 캔버스는 Screen Space - Overlay라서 카메라가 그리는 파티클은 항상 UI 밑에 깔린다.
    /// 인벤토리 칸 위에서 터지는 이펙트가 패널에 가려진다는 뜻이다. 캔버스를 Camera 모드로
    /// 바꾸면 HUD 전체의 정렬·후처리가 달라지므로, 대신 이렇게 한다:
    ///
    ///   1. 이펙트만 찍는 전용 카메라(UIEffect 레이어만 본다)가 투명 렌더 텍스처에 그린다.
    ///   2. 그 텍스처를 맨 위 Overlay 캔버스의 RawImage로 화면 전체에 겹친다.
    ///
    /// 렌더 텍스처 한 픽셀이 화면 한 픽셀이라, 화면 좌표만 주면 그 자리에 이펙트가 뜬다.
    /// 살아 있는 이펙트가 없으면 카메라와 겹침 이미지를 꺼서 비용이 들지 않는다.
    /// </summary>
    public sealed class UIEffectLayer : MonoBehaviour
    {
        [SerializeField, Tooltip("이펙트를 넣을 레이어. 메인 카메라에서는 자동으로 빠진다.")]
        private string layerName = "UIEffect";

        [SerializeField, Tooltip("겹침 캔버스의 정렬 순서. 게임 UI보다 크고, 획득 연출(1000)보다는 작게.")]
        private int sortingOrder = 900;

        [SerializeField, Tooltip("겹칠 때 쓰는 머티리얼(RecycleLife/UI Premultiplied Composite). " +
                                 "비우면 기본 UI 셰이더로 그려져 가산 이펙트가 흐려진다.")]
        private Material compositeMaterial;

        [SerializeField, Min(0.1f), Tooltip("이펙트 카메라의 Orthographic Size. 화면 세로가 이 값의 두 배(월드 단위)다. " +
                                            "이펙트 크기의 기준이 된다.")]
        private float orthographicSize = 5f;

        [SerializeField, Tooltip("UIEffect 레이어를 빼 줄 메인 카메라. 비우면 Camera.main.")]
        private Camera mainCamera;

        /// <summary>월드 어디와도 겹치지 않게 멀리 떨어진 곳에 둔다.</summary>
        private static readonly Vector3 StagePosition = new Vector3(0f, -10000f, 0f);

        private const float EffectDepth = 10f;

        private int _layer = -1;
        private Camera _camera;
        private RenderTexture _texture;
        private RawImage _overlay;
        private Transform _stage;
        private readonly List<GameObject> _alive = new List<GameObject>(8);

        public bool IsReady => _camera != null && _layer >= 0;

        private void Awake()
        {
            _layer = LayerMask.NameToLayer(layerName);
            if (_layer < 0)
            {
                Debug.LogWarning($"{nameof(UIEffectLayer)}: '{layerName}' 레이어가 없습니다. Project Settings > Tags and Layers에서 추가하세요.", this);
                enabled = false;
                return;
            }

            Camera main = mainCamera != null ? mainCamera : Camera.main;
            if (main != null)
            {
                main.cullingMask &= ~(1 << _layer);
            }

            BuildCamera();
            BuildOverlay();
            SetRendering(false);
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        private void LateUpdate()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null) { _alive.RemoveAt(i); }
            }

            SetRendering(_alive.Count > 0);
        }

        /// <summary>
        /// 화면 좌표에 이펙트를 띄운다. 끝나면 이펙트가 스스로 사라진다(CFXR_Effect의 Clear Behavior).
        /// </summary>
        /// <param name="scale">프리팹 원래 크기에 곱할 배율.</param>
        /// <returns>띄운 인스턴스. 반복 이펙트는 <see cref="Stop"/>으로 끈다.</returns>
        public GameObject Spawn(GameObject prefab, Vector2 screenPoint, float scale)
        {
            if (!IsReady || prefab == null)
            {
                return null;
            }

            EnsureTexture();

            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, EffectDepth));
            GameObject instance = Instantiate(prefab, world, Quaternion.identity, _stage);
            instance.transform.localScale = prefab.transform.localScale * scale;
            SetLayerRecursively(instance.transform, _layer);

            _alive.Add(instance);
            SetRendering(true);
            return instance;
        }

        /// <summary>반복 이펙트를 끈다. 새로 뿜지만 않게 하고, 떠 있는 입자는 자연스럽게 사라지게 둔다.</summary>
        public void Stop(GameObject instance, float destroyAfter = 1.5f)
        {
            if (instance == null)
            {
                return;
            }

            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            // CFXR_Effect가 다 사라진 걸 보고 지우지만, 혹시 남아도 이때는 확실히 치운다.
            Destroy(instance, destroyAfter);
        }

        private void BuildCamera()
        {
            _stage = new GameObject("UIEffectStage").transform;
            _stage.SetParent(transform, false);
            _stage.position = StagePosition;

            var camGo = new GameObject("UIEffectCamera");
            camGo.transform.SetParent(_stage, false);
            camGo.transform.localPosition = Vector3.zero;

            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = orthographicSize;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = EffectDepth * 4f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);     // 투명으로 지워야 밑의 UI가 비친다
            _camera.cullingMask = 1 << _layer;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;

            EnsureTexture();
        }

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("UIEffectOverlay", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            // 레이캐스터를 달지 않는다 — 이펙트가 터치를 가로채면 안 된다.
            var imageGo = new GameObject("Effects", typeof(RectTransform));
            var rt = (RectTransform)imageGo.transform;
            rt.SetParent(canvasGo.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _overlay = imageGo.AddComponent<RawImage>();
            _overlay.raycastTarget = false;
            _overlay.texture = _texture;
            if (compositeMaterial != null)
            {
                _overlay.material = compositeMaterial;
            }
        }

        /// <summary>화면 크기와 같은 렌더 텍스처를 둔다. 화면이 바뀌면(회전·창 크기) 새로 만든다.</summary>
        private void EnsureTexture()
        {
            int w = Mathf.Max(1, Screen.width);
            int h = Mathf.Max(1, Screen.height);

            if (_texture != null && _texture.width == w && _texture.height == h)
            {
                return;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            _texture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "UIEffectTexture" };
            _texture.Create();

            if (_camera != null) { _camera.targetTexture = _texture; }
            if (_overlay != null) { _overlay.texture = _texture; }
        }

        private void SetRendering(bool on)
        {
            if (_camera != null && _camera.enabled != on)
            {
                if (on) { EnsureTexture(); }
                _camera.enabled = on;
            }

            if (_overlay != null && _overlay.enabled != on)
            {
                _overlay.enabled = on;
            }
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i), layer);
            }
        }
    }
}
