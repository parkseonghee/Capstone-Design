using NUnit.Framework;
using UnityEngine;
using RecycleLife.Core;

namespace RecycleLife.Tests
{
    /// <summary>
    /// 마을 이동판. 본편 낙하·전투와 무관한 아주 단순한 규칙만 검증한다:
    /// 빈 칸이면 이동, 캐릭터 상이면 부딪혀서 꼬리표를 알려 주고(제자리 유지), 보드 밖은 무시.
    /// </summary>
    public sealed class VillageMapTests
    {
        [Test]
        public void MovingIntoAnEmptyCell_MovesTheAvatar()
        {
            var map = new VillageMap(5, 5, new Vector2Int(2, 2));

            VillageStepResult result = map.TryMove(Direction.Right);

            Assert.AreEqual(VillageMoveOutcome.Moved, result.Outcome);
            Assert.AreEqual(new Vector2Int(3, 2), map.Avatar.Position);
        }

        [Test]
        public void MovingOutOfBounds_DoesNothing()
        {
            var map = new VillageMap(5, 5, new Vector2Int(0, 0));

            VillageStepResult result = map.TryMove(Direction.Up);

            Assert.AreEqual(VillageMoveOutcome.OutOfBounds, result.Outcome);
            Assert.AreEqual(new Vector2Int(0, 0), map.Avatar.Position, "제자리여야 한다");
        }

        [Test]
        public void BumpingACharacterStand_ReportsItsIdAndDoesNotMoveIn()
        {
            var map = new VillageMap(5, 5, new Vector2Int(2, 2));
            map.PlaceCharacterStand("char_b", new Vector2Int(3, 2));

            VillageStepResult result = map.TryMove(Direction.Right);

            Assert.AreEqual(VillageMoveOutcome.SwappedCharacter, result.Outcome);
            Assert.AreEqual("char_b", result.CharacterId);
            Assert.AreEqual(new Vector2Int(2, 2), map.Avatar.Position,
                "상 위로 걸어 들어가지 않는다 — 그 자리에서 바로 부딪힌 것으로 친다");
        }

        [Test]
        public void DifferentStands_ReportDifferentIds()
        {
            var map = new VillageMap(5, 5, new Vector2Int(2, 2));
            map.PlaceCharacterStand("char_a", new Vector2Int(1, 2));
            map.PlaceCharacterStand("char_b", new Vector2Int(3, 2));

            Assert.AreEqual("char_a", map.TryMove(Direction.Left).CharacterId);
            Assert.AreEqual("char_b", map.TryMove(Direction.Right).CharacterId);
        }

        [Test]
        public void AfterBumpingAStand_TheAvatarCanStillMoveElsewhere()
        {
            var map = new VillageMap(5, 5, new Vector2Int(2, 2));
            map.PlaceCharacterStand("char_b", new Vector2Int(3, 2));

            map.TryMove(Direction.Right);   // bump, stays put
            VillageStepResult result = map.TryMove(Direction.Down);

            Assert.AreEqual(VillageMoveOutcome.Moved, result.Outcome);
            Assert.AreEqual(new Vector2Int(2, 3), map.Avatar.Position);
        }

        [Test]
        public void ConstructorRejectsAnOutOfBoundsStart()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new VillageMap(3, 3, new Vector2Int(10, 10)));
        }
    }
}
