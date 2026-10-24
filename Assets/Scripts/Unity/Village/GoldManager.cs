using System;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 마을에서 보는 총 골드의 창구. 실제 값은 <see cref="RunProgress"/>에 저장되고, 이 클래스는 그 값을 읽어 주고
    /// 골드가 실제로 바뀌었을 때만 <see cref="GoldChanged"/>를 알린다.
    ///
    /// RunProgress.Changed는 골드 말고도(조합법·보관함 등) 바뀔 때마다 오므로 여기서 골드 변화만 걸러낸다.
    /// 표시는 하지 않는다 — 화면에 찍는 건 <see cref="GoldView"/>의 일이다.
    /// </summary>
    public sealed class GoldManager : MonoBehaviour
    {
        [SerializeField, Tooltip("골드가 저장되는 진행 데이터.")]
        private RunProgress progress;

        /// <summary>골드가 바뀌면 새 값과 함께 알린다.</summary>
        public event Action<int> GoldChanged;

        private int _lastGold;

        public int Gold => progress != null ? progress.Gold : 0;

        private void OnEnable()
        {
            _lastGold = Gold;
            if (progress != null)
            {
                progress.Changed += OnProgressChanged;
            }
        }

        private void OnDisable()
        {
            if (progress != null)
            {
                progress.Changed -= OnProgressChanged;
            }
        }

        public void AddGold(int amount)
        {
            if (progress != null)
            {
                progress.AddGold(amount);
            }
        }

        /// <summary>골드를 쓴다. 모자라면 아무것도 안 하고 false.</summary>
        public bool SpendGold(int amount)
        {
            return progress != null && progress.SpendGold(amount);
        }

        private void OnProgressChanged()
        {
            int gold = Gold;
            if (gold == _lastGold)
            {
                return;
            }

            _lastGold = gold;
            GoldChanged?.Invoke(gold);
        }
    }
}
