using System;
using System.Collections.Generic;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 이번 스테이지에 들고 있는 아이템. 유물은 한 종류당 하나, 일회성은 개수로 쌓인다.
    ///
    /// 유물 효과 자체는 <see cref="RunModifiers"/>에 누적되고, 여기는 <b>무엇을 갖고 있는지</b>만 센다.
    /// 목록 UI가 이걸 읽고, 상점은 "이미 가진 유물"을 막는 데 쓴다.
    ///
    /// 스테이지가 끝나면 비워진다 — 유물은 런 한정이라는 밸런싱 전제(§0-2) 때문이다.
    /// </summary>
    public sealed class RunInventory
    {
        private readonly List<string> _order = new List<string>(16);
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(16);

        /// <summary>바뀌었을 때. 목록 UI가 구독한다.</summary>
        public event Action Changed;

        /// <summary>얻은 순서대로의 아이템 id. 목록 UI가 이 순서로 그린다.</summary>
        public IReadOnlyList<string> Ids => _order;

        public int DistinctCount => _order.Count;

        public bool Has(string id) => !string.IsNullOrEmpty(id) && _counts.ContainsKey(id);

        public int CountOf(string id)
        {
            int n;
            return !string.IsNullOrEmpty(id) && _counts.TryGetValue(id, out n) ? n : 0;
        }

        /// <summary>
        /// 아이템을 넣는다. 유물을 이미 갖고 있으면 아무것도 하지 않는다(중복 불가).
        /// </summary>
        /// <returns>실제로 들어갔으면 true.</returns>
        public bool Add(string id, bool stackable)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (_counts.ContainsKey(id))
            {
                if (!stackable)
                {
                    return false;
                }

                _counts[id]++;
                Changed?.Invoke();
                return true;
            }

            _counts[id] = 1;
            _order.Add(id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>일회성을 하나 쓴다. 다 쓰면 목록에서 빠진다.</summary>
        public bool Consume(string id)
        {
            int n;
            if (string.IsNullOrEmpty(id) || !_counts.TryGetValue(id, out n) || n <= 0)
            {
                return false;
            }

            if (n == 1)
            {
                _counts.Remove(id);
                _order.Remove(id);
            }
            else
            {
                _counts[id] = n - 1;
            }

            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (_order.Count == 0)
            {
                return;
            }

            _order.Clear();
            _counts.Clear();
            Changed?.Invoke();
        }
    }
}
