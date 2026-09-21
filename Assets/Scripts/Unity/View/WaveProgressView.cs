using UnityEngine;
using UnityEngine.UI;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 화면 상단의 웨이브 진행도 바. 밸런싱 v1 §2-1-4 확정:
    /// <b>적을 처치할 때마다 차고, 다 차면 다음 웨이브로 넘어간다.</b>
    ///
    /// 바의 위치·크기·색은 전부 씬에서 정한다. 이 스크립트는 fillAmount와 문자열만 만진다 —
    /// RectTransform은 읽지도 쓰지도 않는다(Hard Rule 2).
    ///
    /// 진행 판정 자체는 Core의 WaveRunner가 한다. 여기는 그 값을 비추기만 한다(Hard Rule 5).
    /// </summary>
    public sealed class WaveProgressView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("구독할 세션. 씬에서 연결한다.")]
        private GameSession session;

        [Header("표시 대상 (씬에서 배치)")]
        [SerializeField, Tooltip("차오르는 바. Image Type을 Filled로 둬야 fillAmount가 먹는다.")]
        private Image fill;

        [SerializeField, Tooltip("(선택) 'STAGE 1 · WAVE 1' 같은 라벨. 비우면 표시하지 않는다.")]
        private Text waveLabel;

        [SerializeField, Tooltip("(선택) '3 / 15' 같은 처치 수 라벨. 비우면 표시하지 않는다.")]
        private Text countLabel;

        [SerializeField, Tooltip("(선택) 웨이브가 없는 런에서 숨길 대상. " +
                                 "비우면 이 오브젝트의 자식들을 대신 끈다. " +
                                 "이 컴포넌트가 붙은 오브젝트 자신을 넣지 말 것 — 꺼지면 다시 못 켠다.")]
        private GameObject root;

        [Header("문구")]
        [SerializeField] private string waveFormat = "STAGE {0}  ·  WAVE {1}";
        [SerializeField] private string countFormat = "{0} / {1}";
        [SerializeField, Tooltip("웨이브를 전부 끝냈을 때 표시할 문구.")]
        private string clearedText = "ALL WAVES CLEAR";

        // 값이 그대로면 문자열을 새로 만들지 않는다(Hard Rule 8).
        private int _shownWave = -1;
        private int _shownProgress = -1;
        private int _shownGoal = -1;

        private void OnEnable()
        {
            if (session != null)
            {
                session.RunStarted += HandleRunStarted;
                session.Stepped += HandleStepped;
            }

            EnsureFillHasSprite();
            Refresh();
        }

        /// <summary>
        /// Image.type이 Filled여도 <b>스프라이트가 없으면 fillAmount가 무시되고</b>
        /// 항상 가득 찬 사각형으로 그려진다. 아트가 없는 단계에서 바가 늘 꽉 차 보이던 원인이라,
        /// 비어 있으면 단색 사각형을 끼워 넣는다.
        /// 인스펙터에서 스프라이트를 꽂으면 그쪽이 그대로 쓰인다.
        /// </summary>
        private void EnsureFillHasSprite()
        {
            if (fill != null && fill.sprite == null)
            {
                fill.sprite = PlaceholderSprite.Square;
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.RunStarted -= HandleRunStarted;
                session.Stepped -= HandleStepped;
            }
        }

        private void HandleRunStarted(GameLoop loop)
        {
            _shownWave = -1;
            _shownProgress = -1;
            _shownGoal = -1;
            Refresh();
        }

        private void HandleStepped(StepResult result)
        {
            Refresh();
        }

        /// <summary>
        /// 바를 보이거나 감춘다.
        ///
        /// <b>이 컴포넌트가 붙은 오브젝트는 절대 끄지 않는다.</b> 한 번 꺼지면 OnDisable에서
        /// 구독을 끊어 버려 RunStarted를 못 받고, 그러면 영영 다시 켜지지 않는다.
        /// 그래서 root를 지정하지 않았으면 자식들만 끈다.
        /// </summary>
        private void SetVisible(bool show)
        {
            if (root != null && root != gameObject)
            {
                if (root.activeSelf != show)
                {
                    root.SetActive(show);
                }

                return;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.activeSelf != show)
                {
                    child.SetActive(show);
                }
            }
        }

        private void Refresh()
        {
            WaveRunner waves = session != null && session.Loop != null ? session.Loop.Waves : null;

            // 웨이브를 안 쓰는 런에서는 바가 의미 없으므로 숨긴다.
            // Loop가 아직 없는 시점(런 시작 전)에도 숨긴 상태로 둔다.
            bool show = waves != null && !waves.IsIdle;
            SetVisible(show);

            if (!show)
            {
                return;
            }

            if (fill != null)
            {
                fill.fillAmount = waves.Fill;
            }

            if (waves.IsRunCleared)
            {
                if (waveLabel != null && _shownWave != int.MaxValue)
                {
                    _shownWave = int.MaxValue;
                    waveLabel.text = clearedText;
                }

                if (countLabel != null && _shownProgress != int.MaxValue)
                {
                    _shownProgress = int.MaxValue;
                    countLabel.text = string.Empty;
                }

                return;
            }

            IWaveConfig wave = waves.Current;
            if (wave == null)
            {
                return;
            }

            if (waveLabel != null && _shownWave != wave.WaveNumber)
            {
                _shownWave = wave.WaveNumber;
                waveLabel.text = string.Format(waveFormat, wave.StageNumber, wave.WaveNumber);
            }

            if (countLabel != null && (_shownProgress != waves.Progress || _shownGoal != waves.Goal))
            {
                _shownProgress = waves.Progress;
                _shownGoal = waves.Goal;
                countLabel.text = string.Format(countFormat, waves.Progress, waves.Goal);
            }
        }
    }
}
