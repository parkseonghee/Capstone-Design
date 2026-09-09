using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 버튼을 누르면 지정된 씬으로 전환한다.
    ///
    /// 이동할 씬 이름은 인스펙터에서 정한다(Hard Rule 1) — 코드에 씬 이름을 리터럴로 박지 않는다.
    /// 대상 씬은 Build Settings에 등록되어 있어야 한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class SceneTransitionButton : MonoBehaviour
    {
        [SerializeField, Tooltip("이동할 씬 이름. Build Settings에 등록된 이름과 정확히 일치해야 한다.")]
        private string targetSceneName;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(LoadTargetScene);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(LoadTargetScene);
        }

        private void LoadTargetScene()
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning($"{nameof(SceneTransitionButton)}: targetSceneName이 비어 있습니다.", this);
                return;
            }

            SceneManager.LoadScene(targetSceneName);
        }
    }
}
