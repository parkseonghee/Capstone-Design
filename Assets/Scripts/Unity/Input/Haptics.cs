using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 아주 짧은 "틱" 진동. 한 칸 움직일 때마다 울려 <b>입력이 먹었다</b>를 알린다.
    /// 화면 표시를 못 보고 있어도(손가락이 가리거나, 시선이 판 위쪽에 있을 때) 전달되는 신호다.
    ///
    /// <b>Handheld.Vibrate()를 쓰지 않는다.</b> 그건 안드로이드에서 길이를 못 고르는
    /// 통짜 진동(대략 0.5초)이라, 한 칸마다 울리면 손이 계속 떨린다.
    /// 안드로이드는 VibrationEffect로 밀리초를 직접 준다.
    ///
    /// iOS는 Unity 기본 API로 짧은 틱을 낼 방법이 없다(UIImpactFeedbackGenerator는 네이티브 플러그인이 필요하다).
    /// 통짜 진동을 울리느니 조용한 편이 낫다고 보고 아무것도 하지 않는다.
    /// </summary>
    public static class Haptics
    {
        private const int MinMilliseconds = 1;
        private const int MaxMilliseconds = 200;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static int _apiLevel = -1;
        private static bool _unavailable;
#endif

        /// <summary>지정한 길이(밀리초)만큼 짧게 울린다. 지원하지 않는 기기에서는 조용히 넘어간다.</summary>
        public static void Tick(int milliseconds)
        {
            int clamped = Mathf.Clamp(milliseconds, MinMilliseconds, MaxMilliseconds);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_unavailable)
            {
                return;
            }

            try
            {
                AndroidJavaObject vibrator = Vibrator();
                if (vibrator == null)
                {
                    _unavailable = true;
                    return;
                }

                // API 26(오레오)부터 길이와 세기를 지정할 수 있다. 그 아래는 길이만 준다.
                if (ApiLevel() >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                               "createOneShot", (long)clamped, -1 /* DEFAULT_AMPLITUDE */))
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    vibrator.Call("vibrate", (long)clamped);
                }
            }
            catch (System.Exception e)
            {
                // 진동은 있으면 좋은 정도다. 여기서 터져서 조작이 멈추면 안 된다.
                Debug.LogWarning($"{nameof(Haptics)}: 진동을 쓸 수 없어 끕니다. ({e.Message})");
                _unavailable = true;
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject Vibrator()
        {
            if (_vibrator != null)
            {
                return _vibrator;
            }

            using (var playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null)
                {
                    return null;
                }

                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }

            return _vibrator;
        }

        private static int ApiLevel()
        {
            if (_apiLevel >= 0)
            {
                return _apiLevel;
            }

            using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                _apiLevel = versionClass.GetStatic<int>("SDK_INT");
            }

            return _apiLevel;
        }
#endif
    }
}
