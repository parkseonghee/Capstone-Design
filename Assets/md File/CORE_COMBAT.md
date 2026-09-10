# 리사이클 라이프 — 코어 전투 & 연쇄 시스템 스펙

> Claude Code 구현용 스펙. 원작 레퍼런스: *Shovel Knight Pocket Dungeon* (범프 전투 + 인접 동종 연쇄).
> 엔진: Unity 6 / C# · 타깃: Android.

---

## 0. 이 문서의 스코프

**포함** — 순수 게임 로직 계층만 다룬다.
- 그리드/셀 데이터 모델
- 범프(bump) 전투 규칙
- 공간 연쇄 판정 (Flood Fill / BFS)
- 데미지 · 반격 해결

**제외** (각각 별도 문서에서 다룸)
- 낙하 · 중력 · 반실시간 하강 타이밍 → `GAME_LOOP.md`
- 절차적 생성 · 스폰 테이블 → `PCG.md`
- 로그라이트 메타(런/렐릭/캐릭터) → `META.md`
- **재질 콤보 보상 규칙** → `SCORING.md` (여기서는 `StepResult`로 수치만 흘려보낸다, §5-5)
- UI · 터치 입력 → `INPUT_UI.md`

**전제**: 전투는 게임 루프의 한 페이즈(`IMoveResolver`)에서 호출되는 **결정론적 순수 로직**이다.
MonoBehaviour·코루틴·연출은 이 계층에 두지 않는다 (테스트 가능하게 유지).

---

## 1. 데이터 모델

```
Grid          : Cell[cols, rows]        // GridConfig(SO): 8열 × 9행(상단 1줄=프리뷰, 플레이 8×8=64). 리터럴 금지
                                        //   좌표·프리뷰 규칙은 WEEK1_MOVEMENT_FALLING.md §1 참조
Cell          : Entity? occupant        // null = 빈 칸
Entity        : abstract { EntityKind Kind, Vector2Int Position }
  ├ Player    : { int Hp, int MaxHp, int Attack }                    // 구현 완료
  └ Trash     : { TrashType Type, int Hp, int MaxHp, int Attack }    // 구현 완료 = 이 문서의 Enemy
  (미구현) Item    : { ItemKind kind }        // Potion, Food, Key ...
  (미구현) Terrain : { bool blocking }        // Wall, Dirt ...

enum EntityKind  { Player, Trash }                 // Item·Terrain은 아직 없음
enum TrashType   { A, B, C, D }                    // "모양/종류" — 연쇄 판정 단위(확정)
enum MaterialType{ Plastic, Glass, Metal, Paper }  // "재질" — 콤보 보너스 축 (아직 미구현)
```

- **클래스 이름은 `Enemy`가 아니라 `Trash`다.** 스폰·중력·뷰가 전부 이 이름을 쓰고 있어
  전면 개명을 미뤘을 뿐, 역할은 이 문서의 Enemy와 같다.
- **HP·공격력은 `TrashStatsConfig`(SO)에서 종류별로 주입**된다. 플레이어는 `PlayerStatsConfig`.
  코드에는 스탯 리터럴이 없다(Hard Rule 1).
- `TrashType`(종류)와 `MaterialType`(재질)은 **별도 축**으로 둘 예정이다. 한 재질에 여러 종류가 속할 수 있다.
  **`MaterialType`은 아직 넣지 않았다** — 쓰는 곳(SCORING)이 없어 죽은 코드가 되기 때문이다 → §5, §8 참조.

---

## 2. 좌표계 & 인접

- 좌표: `Vector2Int(col, row)`, 좌상단 원점 권장, row 증가 = 아래.
- 인접: **4방향**(상하좌우) — **확정**(§8-3). 대각선은 이어지지 않는다.
- 경계 밖 접근은 항상 "이동 불가"로 처리.

---

## 3. 범프 전투 규칙

플레이어 방향 입력 1회 → 목표 셀 = `player.pos + dir`. 목표 셀 내용에 따라 분기:

| 목표 셀 | 처리 |
|---|---|
| 빈 칸 | 플레이어 이동 |
| Trash | **공격**(제자리, 이동 안 함) → §4·§5 · **구현 완료** |
| 프리뷰 줄(row 0) | 무효 입력. 이동도 공격도 안 된다 → WEEK1 §1 |
| Item | 획득(HP 회복/키 등) 후 이동 · *미구현* |
| Terrain(blocking) | 아무 일 없음(막힘) · *미구현* |

**원작 핵심 규칙**: 적을 공격해 **처치하면 그 적의 반격은 받지 않는다.** 처치 못 하면 데미지를 서로 교환한다.

---

## 4. 연쇄 판정 — Flood Fill

목표 적과 **연결 조건이 같은** 적들을 한 그룹으로 묶어 동시에 타격한다.

```
IChainRule.AreConnected(Trash origin, Trash candidate) : bool
  확정 구현 SameTypeChainRule => origin.Type == candidate.Type   // 원작과 동일
```

> **확정(§8-1): 연쇄 단위는 "종류"다.** 재질 콤보는 연쇄 규칙이 아니라 점수 계층에서 얹는다.

BFS 의사코드 (구현: `ChainFinder`):
```
Find(startCell):
  group.Clear(); frontier.Clear(); Array.Clear(visited)   // 버퍼 재사용, 할당 0
  origin = cellAt(startCell) as Trash;  if null: return 0
  visited[startCell] = true; frontier.Push(startCell)
  while frontier:
    c = frontier.Pop()
    group.Add(c)
    for n in Neighbors4(c):
      if visited[n]: continue
      candidate = cellAt(n) as Trash
      if candidate == null or !rule.AreConnected(origin, candidate): continue
      visited[n] = true; frontier.Push(n)
  return group.Count
```

> 방문 표시를 큐에 <b>넣을 때</b> 찍는다. 꺼낼 때 찍으면 같은 칸이 프론티어에 여러 번 들어간다.

> 연결 조건을 `IChainRule`로 추상화한 이유: 기획이 "종류 기준"에서 "재질 기준"으로 바꿔도 코어를 안 건드리게 하기 위함(§8).

---

## 5. 데미지 해결 순서

1. `chain = FindChain(targetCell, chainRule)`
2. 그룹의 **모든 적**에게 `player.Attack` 적용 → 각 적 hp 감소
   (제거를 섞지 않는다. 중간에 칸을 비우면 남은 좌표 조회가 흔들린다)
3. hp ≤ 0 인 적 = 처치 → 그리드에서 제거. 생긴 빈 칸은 다음 페이즈의 중력이 메운다
4. **반격은 부딪힌 한 마리만 한다** — 그 마리가 살아남았을 때, 그 마리의 `Attack`만큼 플레이어 hp 감소
   - ~~처치되지 않은 적 전원의 attack 합산~~ → **기각(§8-2).** 원작 원형을 따른다
   - 부딪힌 놈을 처치했으면 무피해. 옆에 살아남은 같은 종류가 있어도 반격하지 않는다
5. ~~`OnChainResolved(ChainResult)` 훅~~ → **보류.** 구독자(SCORING)가 없어 죽은 코드가 된다.
   대신 `StepResult`가 `ChainSize / Killed / DamageTaken`을 실어 나르므로,
   점수 계층은 기존 `GameSession.Stepped` 이벤트만 구독하면 된다
6. 결과 반환(연쇄 크기, 처치 수, 받은 피해)

이 계층은 **HP 계산까지만** 책임진다. 보너스 수치·자원 획득은 그 너머의 관심사다.
**패배 판정은 여기서 하지 않는다** — `GameOverChecker` 한 곳에만 있다(WEEK1 §7).

---

## 6. 진입점 & 내부 흐름

구현 클래스는 `CombatMoveResolver : IMoveResolver`다. `BlockingMoveResolver`를 대체하며,
`GameLoop`·`GravityResolver`·`GameSession`은 한 줄도 바뀌지 않았다.

```
MoveResult CombatMoveResolver.Resolve(Direction dir)
  target = player.Position + dir
  if !CanAct(target):              return OutOfBounds   // 경계 밖 + 프리뷰 줄
  cellAt(target) is empty:         MovePlayer(target);  return Moved
  cellAt(target) is Trash:
        chain = ChainFinder.Find(target)   // 같은 종류, 4방향 BFS
        ApplyDamage(chain)                 // §5
        return Attacked(chainSize, killed, damageTaken)
  // 이후 게임루프가 Gravity/Spawn 페이즈 호출
  // 낙하/스폰 순서 = WEEK1_MOVEMENT_FALLING.md §2 그대로 (신규 설계 아님)
```

`CanAct(target)`은 부작용 없는 질의로, "이 칸으로의 입력이 행동이 되는가"를 답한다.
`GameOverChecker`의 갇힘 판정이 이걸 그대로 쓴다 — 그래서 전투가 붙는 순간
"쓰레기에 둘러싸임"이 자동으로 갇힘에서 빠진다(WEEK1 §7-3).

- 이 함수는 **1 논리 스텝**만 처리한다. 하강·스폰·연출은 호출자(게임 루프)가 순서대로 돌린다.

---

## 7. C# 인터페이스 (구현 완료)

초안이 아니라 **현재 구현된 실제 시그니처**다.

```csharp
public enum Direction   { Up, Down, Left, Right }
public enum EntityKind  { Player, Trash }
public enum MoveOutcome { Moved, BlockedByEntity, OutOfBounds, Attacked }

public sealed class Trash : Entity {
    public TrashType Type { get; }
    public int MaxHp { get; }
    public int Attack { get; }
    public int Hp { get; private set; }
    public bool IsDead => Hp <= 0;
    public int TakeDamage(int amount);
}

// 스탯 주입 (Core는 ScriptableObject를 모른다)
public readonly struct TrashStats { public int MaxHp { get; } public int Attack { get; } }
public interface ITrashStatsProvider { TrashStats For(TrashType type); }
public interface IPlayerStatsConfig  { int MaxHp { get; } int Attack { get; } }

public readonly struct MoveResult {
    public MoveOutcome Outcome { get; }
    public int ChainSize { get; }     // 함께 맞은 쓰레기 수
    public int Killed { get; }
    public int DamageTaken { get; }
}

public interface IChainRule { bool AreConnected(Trash origin, Trash candidate); }
public sealed class SameTypeChainRule : IChainRule { /* origin.Type == candidate.Type */ }

// 4방향 Flood Fill. 버퍼를 필드로 재사용해 공격당 할당이 0이다(Hard Rule 8).
// 그래서 Group은 다음 Find 호출 전까지만 유효하다.
public sealed class ChainFinder {
    public IReadOnlyList<Vector2Int> Group { get; }
    public int Find(Vector2Int start);
}

public sealed class CombatMoveResolver : IMoveResolver {
    public MoveResult Resolve(Direction direction);   // §6
    public bool CanAct(Vector2Int target);            // 부작용 없는 질의
}
```

> `CombatSystem`이라는 단일 클래스는 만들지 않았다. 연결 판정(`ChainFinder`)과
> 데미지 해결(`CombatMoveResolver`)을 나눠 두면 연쇄 규칙만 갈아끼우기 쉽다(Hard Rule 3·4).

---

## 8. ✅ 확정 완료 (기획 확인 끝)

1. ~~연쇄 단위~~ → **확정: 종류 기준(`SameTypeChainRule`).** 원작과 같다.
   재질 콤보는 연쇄 규칙이 아니라 점수 계층에서 얹는다 — `MaterialType`은 그때 추가한다.
2. ~~그룹 반격~~ → **확정: 목표로 부딪힌 1마리만 반격(원작 원형).** 그 마리를 처치했으면 무피해.
3. ~~대각선 인접~~ → **확정: 4방향만.** 대각선은 이어지지 않는다.
4. ~~보드 크기·비율~~ → **확정: 8열 × 9행 (플레이 8×8=64 + 상단 프리뷰 1줄, `GridConfig` SO).** 상세 = WEEK1 §1.

### 다음 단계로 넘긴 항목 (지금 정할 필요 없음)
- `MaterialType` 축과 재질 콤보 보상 → SCORING
- `Item` / `Terrain` 엔티티 → 별도 스펙
- `Trash` → `Enemy` 클래스 개명 여부

---

## 9. 구현 순서 체크리스트

- [x] 데이터 모델(§1) + Grid 컨테이너 + `InBounds`/`GetNeighbors4`
- [x] `Resolve` 이동/공격/무효입력 분기 (아이템·지형은 미구현)
- [x] `ChainFinder` BFS + `SameTypeChainRule` **단위 테스트**
- [x] 데미지·처치·반격 (~~`OnChainResolved` 훅~~ → `StepResult`로 대체, §5-5)
- [x] `MoveResult` 반환 + 게임오버(HP0) 판정
- [x] EditMode 테스트: 단일 적 / 3연쇄 / 대각선 제외 / 전멸-무피해 / 부분처치-반격 /
      프리뷰 줄 공격 불가 / 처치 후 중력 / 사망 종료 / 반격 누적 / 스탯 SO 반영
- [ ] (다음) 재질 콤보·점수 → SCORING

---

## 🚫 Claude Code — 하지 말아야 할 것 (Hard Rules)

> 이 규칙은 개별 요청보다 **우선**한다. 규칙을 어기게 되는 상황이면, 코드를 짜지 말고 **먼저 물어봐라.**

### 팀 지정 규칙
1. **하드코딩 금지.** 인스펙터에서 조절 가능한 값(이동 속도, 중력, 스폰 개수, 보드 크기 등)을 코드 리터럴로 박지 말 것.
   → `[SerializeField] private` 필드 또는 `ScriptableObject`로 노출.
   (나쁜 예: 코드에 `gravity = 0;`을 고정 — 인스펙터에서 못 바꿈)
2. **UI 강제 고정 금지.** 게임 시작 시 **코드로 UI 위치·앵커·해상도·레이아웃을 강제 세팅/잠그지 말 것.** 나중에 수정이 매우 어려워진다.
   → 배치는 씬/프리팹/앵커로, 코드는 상태 전환·데이터 바인딩만.
3. **확장 가능한 구조로 짤 것.** 나중에 추가/교체될 여지가 있는 부분(적 종류, 재질, 스폰 규칙, 아이템 등)은 `enum switch` 떡칠·`if` 분기 대신 **인터페이스 / 데이터(SO) / 전략 패턴**으로 갈아끼울 수 있게.
4. **God object 금지.** `GameManager` 하나에 전부 모으고 인스펙터에 다 꽂는 구조 거부. **책임별로 클래스를 나누고**, 참조는 이벤트/주입으로 연결. (찾기 어렵고 지저분함)

### 추가 규칙 (Unity·모바일 실무 — Claude가 제안, 팀 검토)
5. **로직 ↔ MonoBehaviour 분리.** 게임 규칙을 `Update()`나 MonoBehaviour 안에 섞지 말 것. **순수 C# 로직(테스트 가능) + 얇은 View 계층**으로 분리.
6. **취약한 참조 금지.** `GameObject.Find`, `FindObjectOfType`, `Resources.Load("문자열경로")` 남발 금지. → 직렬화 참조·이벤트·경량 DI로 연결.
7. **Singleton/static 남발 금지.** 전역 상태를 여기저기 만들지 말 것(4번의 변형). 꼭 필요한 경우만, 이유를 주석으로 남기고.
8. **핫패스 할당 금지(Android 타깃).** 매 프레임 `Update`·전투 루프에서 `new`, LINQ, 문자열 결합 등 GC 유발 코드 금지. 컬렉션은 재사용.
9. **요청 안 한 대규모 변경 금지.** 시키지 않은 전면 리팩터, 파일·씬 삭제, 폴더 구조 변경 금지. **하기 전에 반드시 확인.**
10. **임의 패키지/에셋 추가 금지.** 서드파티 패키지·에셋 설치는 먼저 물어볼 것.
11. **미확정(⚠️) 항목 임의 확정 금지.** 문서의 "미확정 / 기획 확인 필요" 항목을 스스로 정하지 말고 질문.
12. **검증 없이 "완료" 선언 금지.** 최소 EditMode 테스트 또는 재현 방법을 함께 제시.