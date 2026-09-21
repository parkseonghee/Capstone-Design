using System.Collections.Generic;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 런 하나가 지나갈 웨이브 목록. 밸런싱 v1 §2-1 = 3스테이지 × 3웨이브 = 9개.
    ///
    /// 순서가 곧 진행 순서다. 테스트할 웨이브만 남기고 지우면 거기서부터 시작하게 되므로,
    /// 특정 웨이브 밸런스를 볼 때 유용하다.
    ///
    /// 비워 두면 웨이브 없이 무한히 도는 기존 동작으로 돌아간다.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveSet", menuName = "RecycleLife/Wave Set", order = 11)]
    public sealed class WaveSet : ScriptableObject
    {
        [SerializeField, Tooltip("진행할 순서대로. 비우면 웨이브를 쓰지 않는다.")]
        private WaveConfig[] waves = new WaveConfig[0];

        [SerializeField, Tooltip("벽을 부순 것도 진행도에 넣을지. " +
                                 "기본은 끔 — '진행도는 적 처치 기준'이 확정 사항이다(밸런싱 v1 §2-1-4). " +
                                 "켜면 벽을 부숴도 바가 찬다.")]
        private bool countWallsTowardProgress;

        /// <summary>인터페이스 목록으로 한 번만 만들어 재사용한다(Hard Rule 8).</summary>
        private IWaveConfig[] _cached;

        public bool CountWallsTowardProgress => countWallsTowardProgress;

        public IReadOnlyList<IWaveConfig> Waves
        {
            get
            {
                if (_cached == null || _cached.Length != CountValid())
                {
                    Rebuild();
                }

                return _cached;
            }
        }

        /// <summary>이 설정으로 러너를 만든다. 런을 새로 시작할 때마다 불린다.</summary>
        public WaveRunner CreateRunner() => new WaveRunner(Waves, countWallsTowardProgress);

        /// <summary>
        /// 특정 웨이브부터 시작하는 러너. 마을 지도에서 스테이지를 골라 들어올 때 쓴다.
        /// </summary>
        /// <param name="startIndex">시작할 웨이브 인덱스(0부터).</param>
        /// <param name="stopAfterEachWave">한 웨이브를 깨면 멈출지. 스테이지 선택 방식이면 켠다.</param>
        public WaveRunner CreateRunner(int startIndex, bool stopAfterEachWave)
            => new WaveRunner(Waves, countWallsTowardProgress, startIndex, stopAfterEachWave);

        private int CountValid()
        {
            int n = 0;
            if (waves != null)
            {
                for (int i = 0; i < waves.Length; i++)
                {
                    if (waves[i] != null) { n++; }
                }
            }

            return n;
        }

        /// <summary>비어 있는 슬롯은 건너뛴다 — 인스펙터에서 배열 크기만 늘려 둔 상태를 견디게.</summary>
        private void Rebuild()
        {
            var list = new IWaveConfig[CountValid()];
            int n = 0;
            if (waves != null)
            {
                for (int i = 0; i < waves.Length; i++)
                {
                    if (waves[i] != null) { list[n++] = waves[i]; }
                }
            }

            _cached = list;
        }

        private void OnValidate()
        {
            // 인스펙터에서 목록을 고치면 캐시를 버린다.
            _cached = null;
        }
    }
}
