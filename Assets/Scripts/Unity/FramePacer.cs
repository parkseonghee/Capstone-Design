using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 프레임 상한을 건다. 게임 규칙과 무관한 플랫폼 설정이라 따로 뗀 컴포넌트다(Hard Rule 4).
    ///
    /// 이게 없으면 안드로이드에서 화면이 허용하는 만큼 계속 그린다. 이 게임은
    /// 입력이 있을 때만 보드가 움직이는 턴제라, 남는 프레임은 전부 배터리와 발열로만 나간다.
    /// 발열이 오르면 기기가 클럭을 낮춰서 결국 실제 프레임도 같이 떨어진다.
    ///
    /// 값은 인스펙터에서 조절한다(Hard Rule 1). 30으로 낮추면 배터리를 더 아끼고,
    /// 60이면 블록이 내려오는 보간이 부드럽다.
    /// </summary>
    public sealed class FramePacer : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("목표 프레임(0이면 건드리지 않고 플랫폼 기본값을 쓴다). " +
                                         "턴제라 30도 충분하지만 낙하 보간을 위해 60을 기본으로 둔다.")]
        private int targetFrameRate = 60;

        [SerializeField, Tooltip("vSync를 끈다. targetFrameRate가 실제로 먹으려면 꺼야 한다. " +
                                 "모바일에서는 원래 vSync가 무시되지만 에디터에서 확인하려면 필요하다.")]
        private bool disableVSync = true;

        [SerializeField, Tooltip("절전을 위해 화면 자동 꺼짐을 막지 않을지. " +
                                 "켜면 기본 동작(일정 시간 뒤 화면 꺼짐)을 그대로 둔다.")]
        private bool allowScreenDimming = true;

        private void Awake()
        {
            if (disableVSync)
            {
                QualitySettings.vSyncCount = 0;
            }

            if (targetFrameRate > 0)
            {
                Application.targetFrameRate = targetFrameRate;
            }

            Screen.sleepTimeout = allowScreenDimming
                ? SleepTimeout.SystemSetting
                : SleepTimeout.NeverSleep;
        }
    }
}
