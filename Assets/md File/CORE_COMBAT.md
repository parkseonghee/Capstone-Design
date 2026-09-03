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
- **재질 콤보 보상 규칙** → `SCORING.md` (여기서는 훅만 노출)
- UI · 터치 입력 → `INPUT_UI.md`

**전제**: `CombatSystem`은 게임 루프의 한 페이즈에서 호출되는 **결정론적 순수 로직**이다.
MonoBehaviour·코루틴·연출은 이 계층에 두지 않는다 (테스트 가능하게 유지).

---

## 1. 데이터 모델

```
Grid          : Cell[cols, rows]        // 크기는 GridConfig(SO)에서 읽음. 기본 8 x 12 (Week1과 동일 소스). 리터럴 금지
Cell          : Entity? occupant        // null = 빈 칸
Entity        : abstract { EntityKind Kind }
  ├ Player    : { int hp, int maxHp, int attack, Vector2Int pos }
  ├ Enemy     : { TrashType type, MaterialType material, int hp, int maxHp, int attack }
  ├ Item      : { ItemKind kind }        // Potion, Food, Key ...
  └ Terrain   : { bool blocking }        // Wall, Dirt ...

enum EntityKind  { Player, Enemy, Item, Terrain }
enum TrashType   { CanA, BottleB, WrapC, ... }     // "모양/종류" — 연쇄 판정 단위(기본값)
enum MaterialType{ Plastic, Glass, Metal, Paper }  // "재질" — 콤보 보너스 축
```

- `TrashType`(종류)와 `MaterialType`(재질)은 **별도 축**으로 둔다. 한 재질에 여러 종류가 속할 수 있다 (예: Metal = CanA, CanD).
- 이 분리가 "분리수거 콤보"를 원작 연쇄 위에 얹기 위한 핵심 설계다 → §5, §8 참조.

---

## 2. 좌표계 & 인접

- 좌표: `Vector2Int(col, row)`, 좌상단 원점 권장, row 증가 = 아래.
- 인접: **4방향**(상하좌우) 기본. 대각선 포함 여부는 §8 미확정.
- 경계 밖 접근은 항상 "이동 불가"로 처리.

---

## 3. 범프 전투 규칙

플레이어 방향 입력 1회 → 목표 셀 = `player.pos + dir`. 목표 셀 내용에 따라 분기:

| 목표 셀 | 처리 |
|---|---|
| 빈 칸 | 플레이어 이동 |
| Enemy | **공격**(제자리, 이동 안 함) → §4·§5 |
| Item | 획득(HP 회복/키 등) 후 이동 |
| Terrain(blocking) | 아무 일 없음(막힘) |

**원작 핵심 규칙**: 적을 공격해 **처치하면 그 적의 반격은 받지 않는다.** 처치 못 하면 데미지를 서로 교환한다.

---

## 4. 연쇄 판정 — Flood Fill

목표 적과 **연결 조건이 같은** 적들을 한 그룹으로 묶어 동시에 타격한다.

```
IChainRule.AreConnected(Enemy a, Enemy b) : bool
  기본 구현 SameTypeChainRule => a.type == b.type   // 원작과 동일
```

BFS 의사코드:
```
FindChain(startCell, rule):
  group = []; visited = set(); queue = [startCell]
  while queue:
    c = queue.pop()
    if c in visited: continue
    visited.add(c)
    group.add(c)
    for n in Neighbors4(c):
      if n has Enemy and rule.AreConnected(startEnemy, n.enemy):
        queue.push(n)
  return group   // ChainResult { cells, size, materials }
```

> 연결 조건을 `IChainRule`로 추상화한 이유: 기획이 "종류 기준"에서 "재질 기준"으로 바꿔도 코어를 안 건드리게 하기 위함(§8).

---

## 5. 데미지 해결 순서

1. `chain = FindChain(targetCell, chainRule)`
2. 그룹의 **모든 적**에게 `player.attack` 적용 → 각 적 hp 감소
3. hp ≤ 0 인 적 = 처치 → 그리드에서 제거
4. **반격 합산**: 이번에 처치되지 **않은** 적들의 `attack` 총합만큼 플레이어 hp 감소
   - (그룹 전원 처치 시 무피해 — 원작 규칙의 그룹 확장 해석)
5. `OnChainResolved(ChainResult)` 훅 호출 → 재질 콤보/젬/점수는 `SCORING.md`가 구독
6. 결과 반환(처치 수, 받은 피해, 게임오버 여부)

이 계층은 **HP 계산까지만** 책임진다. 보너스 수치·자원 획득은 훅 너머의 관심사다.

---

## 6. 진입점 & 내부 흐름

```
CombatResult CombatSystem.ResolveMove(Direction dir)
  target = player.pos + dir
  if !InBounds(target):            return NoOp
  switch cellAt(target):
    Empty:            MovePlayer(target);           return Moved
    Item:             ApplyItem(); MovePlayer(target); return Moved
    Terrain(block):                                 return Blocked
    Enemy:
        chain  = FindChain(target, chainRule)
        result = ApplyDamage(chain)   // §5
        return result   // 이후 게임루프가 Gravity/Spawn 페이즈 호출
                        // 낙하/스폰 순서 = WEEK1_MOVEMENT_FALLING.md §2 그대로 (신규 설계 아님).
                        // 추후 GAME_LOOP.md로 승격 예정.
```

- 이 함수는 **1 논리 스텝**만 처리한다. 하강·스폰·연출은 호출자(게임 루프)가 순서대로 돌린다.

---

## 7. C# 인터페이스 초안 (시그니처만, 로직은 TODO)

```csharp
public enum Direction { Up, Down, Left, Right }
public enum EntityKind { Player, Enemy, Item, Terrain }

public sealed class Enemy {
    public TrashType Type;
    public MaterialType Material;
    public int Hp, MaxHp, Attack;
}

public readonly struct ChainResult {
    public readonly IReadOnlyList<Vector2Int> Cells;
    public readonly int Size;
    public readonly IReadOnlyList<MaterialType> Materials; // 재질 분포(콤보 판정용)
}

public readonly struct CombatResult {
    public readonly CombatOutcome Outcome; // NoOp/Moved/Blocked/Attacked
    public readonly int Killed;
    public readonly int DamageTaken;
    public readonly bool PlayerDead;
}

public interface IChainRule {
    bool AreConnected(Enemy a, Enemy b);
}

public sealed class SameTypeChainRule : IChainRule {
    public bool AreConnected(Enemy a, Enemy b) => a.Type == b.Type;
}

public sealed class CombatSystem {
    public event Action<ChainResult> OnChainResolved;   // SCORING이 구독
    public CombatResult ResolveMove(Direction dir);     // §6
    // 내부: FindChain, ApplyDamage, MovePlayer, ApplyItem ...
}
```

---

## 8. ⚠️ 미확정 — 기획 확인 필요 (구현 전 결정)

1. **연쇄 단위**: `SameTypeChainRule`(종류=원작) vs `SameMaterialRule`(재질=분리수거 직결).
   - 기본값은 **종류 기준**으로 진행하고, 재질 콤보는 §5 훅에서 "시간축 보너스"로 얹는 방향 추천(원작 재미 유지 + 테마 확보). 확정 시 이 문단만 갱신.
2. **그룹 반격**: "처치 안 된 적만 반격"(현재안) vs "목표 1마리 기준만"(원작 원형).
3. **대각선 인접** 포함 여부 (난이도·가독성 영향).
4. ~~보드 크기·비율~~ → **확정: 8 × 12 (`GridConfig` SO).** 팀 변경 시 SO 값만 수정.

---

## 9. 구현 순서 체크리스트

- [ ] 데이터 모델(§1) + Grid 컨테이너 + `InBounds`/`Neighbors4`
- [ ] `ResolveMove` 이동/아이템/막힘 분기 (적 없이 먼저)
- [ ] `FindChain` BFS + `SameTypeChainRule` **단위 테스트**
- [ ] `ApplyDamage` 데미지·처치·반격 + `OnChainResolved` 훅
- [ ] `CombatResult` 반환 + 게임오버(HP0) 판정
- [ ] EditMode 테스트: 단일 적 / 3연쇄 / 전멸-무피해 / 부분처치-반격

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