using System;

namespace RecycleLife.Core
{
    /// <summary>
    /// 몬스터가 죽을 때 재료를 떨구는 역할만 맡는다.
    ///
    /// <see cref="TrapResolver"/>와 같은 자리에 놓인다 — 적이 죽는 곳은 네 군데(평타·폭발·
    /// 전체 피해 아이템·처치 아이템)인데, 그 넷이 전부 이 클래스 하나를 거치게 해서
    /// 드롭 규칙이 바뀌어도 여기만 고치면 되게 한다(Hard Rule 4).
    ///
    /// 결과를 목록에 쌓지 않고 <b>이벤트로 바로 흘린다.</b> 목록으로 모으면 "누가 언제
    /// 비우는가"가 생기는데, 적은 스텝 밖에서도 죽기 때문에(아이템 사용) 비우는 시점을
    /// 한 군데로 정할 수가 없다. 떨어지는 즉시 넘기면 그 문제가 아예 없다.
    /// </summary>
    public sealed class MaterialDropResolver
    {
        private readonly IMaterialDropTable _table;
        private readonly IRandomSource _random;

        public MaterialDropResolver(IMaterialDropTable table, IRandomSource random)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>재료가 떨어졌을 때. 인자는 아이템 id다. Unity 쪽 인벤토리가 구독한다.</summary>
        public event Action<string> Dropped;

        /// <summary>
        /// 방금 죽은 쓰레기로 드롭을 굴린다. 적이 아니면(벽·포션) 굴리지 않는다 —
        /// 재료는 몬스터를 잡아야 나온다는 기획이기 때문이다.
        /// </summary>
        public void MaybeDrop(Trash killed)
        {
            if (killed == null || !killed.IsEnemy)
            {
                return;
            }

            string materialId;
            if (_table.TryRoll(killed.Type, _random, out materialId) && !string.IsNullOrEmpty(materialId))
            {
                Dropped?.Invoke(materialId);
            }
        }
    }
}
