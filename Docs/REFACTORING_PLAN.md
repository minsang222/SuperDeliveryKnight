# 게임잼 코드 정리 Implementation Plan

> **For agentic workers:** 이 계획은 한 작업씩 순서대로 실행하고 각 단계 뒤에 테스트한다. 병렬 작업이나 새 기능 구현은 금지한다.

**Goal:** GDD 기능을 추가하지 않고 현재 수직 슬라이스의 동작과 Unity 에셋 연결을 보존하면서 가장 위험한 결합만 줄인다.

**Architecture:** 새 계층이나 범용 인터페이스를 만들지 않는다. 먼저 에셋 계약 테스트를 안전망으로 두고, 직렬화 참조와 씬 설정을 명시한 뒤, 실제 책임 충돌이 있는 카메라 구성만 `Player`에서 제거한다.

**Tech Stack:** Unity 6000.5.9f1, C#, Unity Test Framework 1.7.0, NUnit EditMode tests

**Spec:** `Docs/PROJECT_GUIDE.md`

## Global Constraints

- GDD v0.1의 F1~F6 외 기능을 추가하지 않는다.
- 게임플레이 수치와 조작 결과를 바꾸지 않는다.
- 새 패키지, 서비스, 인터페이스, 상태 머신을 추가하지 않는다.
- 직렬화 필드 변경에는 `[FormerlySerializedAs]`를 사용한다.
- Unity 직렬화 필드는 `camelCase`, 그 외 private 필드는 `_camelCase`로 쓴다.
- `[Obsolete]` 멤버는 호출부를 함께 정리한 뒤 저장소에서 제거한다.
- 각 작업 뒤 EditMode 테스트와 C# 빌드를 실행한다.

---

### Task 1: 현재 에셋 계약을 테스트로 고정

**Files:**
- Create: `Assets/Tests/EditMode/SuperDeliveryKnight.EditModeTests.asmdef`
- Create: `Assets/Tests/EditMode/SceneContractTests.cs`

**Interfaces:**
- Consumes: `Assets/Scenes/SampleScene.unity`, 건물·장애물 프리팹의 현재 직렬화 계약
- Produces: 씬과 프리팹의 누락 참조를 잡는 EditMode 테스트 3개

- [x] 씬의 Player/PlatformManager/Main Camera 및 직렬화 참조를 검사한다.
- [x] 모든 건물 프리팹의 StartPoint/EndPoint/ObstaclePoints를 검사한다.
- [x] 모든 장애물 프리팹의 Collider2D/Rigidbody2D/Obstacle 조합을 검사한다.
- [x] Unity EditMode 테스트를 실행해 통과를 확인한다.
- [x] `dotnet build SuperDeliveryKnight.sln --no-restore`를 실행한다.

검증 결과(2026-08-21): EditMode 5/5 통과, C# 빌드 오류 0개. Unity가 생성한 테스트 프로젝트에서 BCL 참조 버전 충돌 경고 2개가 발생하지만 Unity 컴파일에는 나타나지 않는다.

### Task 2: 공격 히트박스의 순서 의존 제거

**Files:**
- Modify: `Assets/Scripts/Player.cs`
- Modify: `Assets/Scenes/SampleScene.unity`
- Test: `Assets/Tests/EditMode/SceneContractTests.cs`

**Interfaces:**
- Consumes: 씬에 이미 직렬화된 `slashHitbox` 참조
- Produces: 콜라이더 배열 순서와 무관한 공격 판정 초기화

- [x] 씬 계약 테스트가 `slashHitbox` 누락 시 실패하는지 확인한다.
- [x] `Player.Awake`의 두 번째 `BoxCollider2D` fallback만 삭제한다.
- [x] 공격 지속시간, 입력, 히트박스 활성 시점이 바뀌지 않았는지 EditMode와 PlayMode 테스트로 확인한다.
- [x] EditMode 테스트와 C# 빌드를 실행한다.

### Task 3: 직렬화 필드와 튜닝 이름 정리

**Files:**
- Modify: `Assets/Scripts/Player.cs`
- Modify: `Assets/Scripts/PlatBuilding.cs`
- Modify: `Assets/Scripts/PlatformManager.cs`
- Test: `Assets/Tests/EditMode/SceneContractTests.cs`

**Interfaces:**
- Consumes: 기존 씬·프리팹의 직렬화 값과 `PlatformManager`의 청크 앵커 읽기
- Produces: GDD §10에 맞는 private 필드와 필요한 읽기 전용 프로퍼티

- [x] `PlatBuilding`의 public 필드를 `[FormerlySerializedAs]`가 붙은 private 필드와 읽기 전용 프로퍼티로 바꾼다.
- [x] 혼용된 private 필드를 `_camelCase`로 바꾸되 모든 직렬화 이름을 보존한다.
- [x] `Player.Move`의 `0.01f`를 `[SerializeField]`인 콤보당 속도 증가율로 옮기고 기존 값 `0.01f`를 유지한다.
- [x] 씬·프리팹 계약 테스트와 C# 빌드를 실행한다.

### Task 4: 카메라 설정을 씬 책임으로 이동

**Files:**
- Modify: `Assets/Scripts/Player.cs`
- Modify: `Assets/Scenes/SampleScene.unity`
- Test: `Assets/Tests/EditMode/SceneContractTests.cs`

**Interfaces:**
- Consumes: Player Transform과 현재 `cameraFollowOffset`
- Produces: 씬에 저장된 CinemachineBrain/CinemachineCamera/CinemachineFollow 연결

- [x] 씬 계약 테스트에 Main Camera의 CinemachineBrain과 Player를 Follow하는 CinemachineCamera 검사를 추가한다.
- [x] 현재 런타임 생성 결과와 같은 오프셋으로 Cinemachine 오브젝트를 씬에 저장한다.
- [x] `Player`에서 카메라 필드와 생성 메서드만 삭제한다.
- [x] EditMode 테스트, C# 빌드, PlayMode 스모크 테스트로 씬 시작과 카메라 중복 방지를 확인한다.

### Task 5: 여기서 중단하고 기능 작업과 분리

`Player`의 이동·점프·공격·게임 오버를 지금 별도 클래스로 나누지 않는다. 체력, 패링, 결과 화면 중 하나를 실제로 구현할 때 상태 충돌과 테스트 비용을 다시 측정하고 필요한 책임만 분리한다. 장애물 파편, 콤보 증가, 좌우 입력, 스폰 규칙 변경도 리팩터링이 아니라 GDD 기능 작업으로 별도 계획을 세운다.

- [x] 새 기능 없이 계획 범위에서 중단한다.
