using System;
using System.Collections.Generic;

namespace RecycleLife.Core
{
    /// <summary>
    /// 웨이브 진행을 센다. 밸런싱 v1 §2-1-4 확정: <b>진행도는 적 처치 수로만 찬다.</b>
    /// 턴을 넘기는 것만으로는 오르지 않으므로, 도망만 다니는 플레이로는 클리어할 수 없다.
    ///
    /// 시간도 MonoBehaviour도 모르는 순수 로직이다 — 처치 수를 받아 상태를 옮길 뿐이고,
    /// 화면에 어떻게 보일지는 뷰가 정한다(Hard Rule 5).
    ///
    /// 웨이브가 넘어가도 <b>보드는 그대로 둔다.</b> 판을 비우고 다시 까는 연출은
    /// 아직 기획에 없어서(§2-1-5 미확정) 임의로 넣지 않았다. 쌓여 있던 블록은 남고,
    /// 다음 스폰부터 새 웨이브의 종류가 내려온다.
    /// </summary>
    public sealed class WaveRunner
    {
        private readonly IReadOnlyList<IWaveConfig> _waves;
        private readonly bool _countWalls;
        private readonly bool _stopAfterEachWave;
        private readonly int _startIndex;

        /// <param name="waves">순서대로 진행할 웨이브 목록. 비어 있으면 웨이브 없이 무한 플레이가 된다.</param>
        /// <param name="countWalls">벽 파괴도 진행도에 넣을지. 기본은 끔 — "적 처치 기준"이 확정 사항이다.</param>
        /// <param name="startIndex">
        /// 몇 번째 웨이브부터 시작할지. 마을 지도에서 스테이지를 골라 들어올 때 쓴다.
        /// </param>
        /// <param name="stopAfterEachWave">
        /// 한 웨이브를 깨면 <b>거기서 멈출지</b>. 스테이지 선택 방식에서는 켠다 —
        /// 1-1을 깨면 클리어 화면을 띄우고 물어본 뒤에 1-2로 넘어가야 하기 때문에,
        /// 러너가 제멋대로 다음 웨이브로 이어 달리면 안 된다.
        /// 끄면 9웨이브를 연속으로 달리는 기존 동작이다.
        /// </param>
        public WaveRunner(
            IReadOnlyList<IWaveConfig> waves,
            bool countWalls = false,
            int startIndex = 0,
            bool stopAfterEachWave = false)
        {
            _waves = waves ?? throw new ArgumentNullException(nameof(waves));
            _countWalls = countWalls;
            _stopAfterEachWave = stopAfterEachWave;
            _startIndex = Clamp(startIndex, _waves.Count);
            Index = _startIndex;
        }

        private static int Clamp(int index, int count)
        {
            if (count <= 0 || index < 0) { return 0; }
            return index >= count ? count - 1 : index;
        }

        /// <summary>지금 몇 번째 웨이브인지(0부터). 전부 끝나면 목록 길이와 같아진다.</summary>
        public int Index { get; private set; }

        /// <summary>현재 웨이브. 목록이 비었거나 전부 끝났으면 null이다.</summary>
        public IWaveConfig Current => Index >= 0 && Index < _waves.Count ? _waves[Index] : null;

        /// <summary>현재 웨이브에서 지금까지 채운 수.</summary>
        public int Progress { get; private set; }

        /// <summary>현재 웨이브의 목표 수. 웨이브가 없으면 0이다.</summary>
        public int Goal
        {
            get
            {
                IWaveConfig wave = Current;
                return wave == null ? 0 : Math.Max(0, wave.KillGoal);
            }
        }

        /// <summary>진행도 0~1. 바의 채움 비율로 그대로 쓰면 된다.</summary>
        public float Fill
        {
            get
            {
                int goal = Goal;
                if (goal <= 0)
                {
                    return IsRunCleared ? 1f : 0f;
                }

                float fill = Progress / (float)goal;
                return fill < 0f ? 0f : (fill > 1f ? 1f : fill);
            }
        }

        /// <summary>웨이브를 전부 끝냈는지.</summary>
        public bool IsRunCleared => _waves.Count > 0 && Index >= _waves.Count;

        /// <summary>웨이브 자체를 쓰지 않는 런인지(목록이 비었을 때). 뷰가 바를 숨기는 데 쓴다.</summary>
        public bool IsIdle => _waves.Count == 0;

        /// <summary>
        /// 직전 <see cref="Report"/>에서 웨이브가 넘어갔는지. 뷰가 "웨이브 클리어" 연출을 띄울 때 읽는다.
        /// 다음 Report 때 덮어써진다.
        /// </summary>
        public bool JustAdvanced { get; private set; }

        /// <summary>직전 <see cref="Report"/>에서 스테이지까지 바뀌었는지.</summary>
        public bool JustChangedStage { get; private set; }

        /// <summary>
        /// 지금 웨이브의 목표를 채웠는지. stopAfterEachWave가 켜져 있을 때만 true가 되며,
        /// <see cref="AdvanceToNext"/>를 부르기 전까지 유지된다.
        /// 뷰가 클리어 화면을 띄울 조건이 이것이다.
        /// </summary>
        public bool IsWaveCleared { get; private set; }

        /// <summary>뒤에 더 남은 웨이브가 있는지. 클리어 화면이 "다음 스테이지" 버튼을 켤지 정할 때 쓴다.</summary>
        public bool HasNext => Index + 1 < _waves.Count;

        /// <summary>
        /// 다음 웨이브로 넘긴다. stopAfterEachWave 모드에서 "다음 스테이지로 이동"을 눌렀을 때 부른다.
        /// </summary>
        /// <returns>넘어갔으면 true. 마지막 웨이브였으면 false.</returns>
        public bool AdvanceToNext()
        {
            if (!HasNext)
            {
                return false;
            }

            Index++;
            Progress = 0;
            IsWaveCleared = false;
            return true;
        }

        /// <summary>
        /// 이번 스텝의 처치 결과를 넘긴다. 목표를 채우면 그 자리에서 다음 웨이브로 넘어간다.
        /// </summary>
        /// <param name="enemiesKilled">처치한 <b>적</b> 수. 벽과 포션은 빠져 있다.</param>
        /// <param name="wallsDestroyed">부순 벽 수. countWalls가 꺼져 있으면 무시된다.</param>
        /// <returns>이번 호출로 웨이브가 넘어갔으면 true.</returns>
        public bool Report(int enemiesKilled, int wallsDestroyed)
        {
            JustAdvanced = false;
            JustChangedStage = false;

            // 이미 깬 웨이브에서 더 잡아도 진행도가 넘치지 않게 한다.
            // (클리어 화면이 떠 있는 동안 들어오는 마지막 연쇄 같은 것)
            if (Current == null || IsWaveCleared)
            {
                return false;
            }

            int gained = Math.Max(0, enemiesKilled) + (_countWalls ? Math.Max(0, wallsDestroyed) : 0);
            if (gained <= 0)
            {
                return false;
            }

            Progress += gained;

            // 목표가 0 이하인 웨이브는 설정 실수다. 그냥 넘겨서 판이 멈추지 않게 한다.
            int goal = Goal;
            if (goal > 0 && Progress < goal)
            {
                return false;
            }

            // 스테이지 선택 방식: 여기서 멈춘다. 다음으로 넘기는 건 플레이어가 버튼을 눌렀을 때다.
            if (_stopAfterEachWave)
            {
                Progress = goal;        // 바를 가득 찬 상태로 보여 준다
                IsWaveCleared = true;
                JustAdvanced = true;
                return true;
            }

            int stageBefore = Current.StageNumber;

            // 넘치게 잡은 분은 버린다. 연쇄로 한 번에 많이 잡았다고 두 웨이브를 건너뛰면
            // 등장 조합이 통째로 생략돼 기획 의도(§2-1 "새 적은 웨이브마다 1종씩")가 깨진다.
            Index++;
            Progress = 0;
            JustAdvanced = true;
            JustChangedStage = Current != null && Current.StageNumber != stageBefore;

            return true;
        }

        /// <summary>런을 다시 시작할 때 첫 웨이브로 되돌린다.</summary>
        public void Reset()
        {
            Index = _startIndex;
            Progress = 0;
            IsWaveCleared = false;
            JustAdvanced = false;
            JustChangedStage = false;
        }
    }
}
