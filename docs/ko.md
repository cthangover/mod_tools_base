# 도구 제작 기반 — 에디터 창의 공통 키트

`tools_base`는 에디터 창을 만드는 모드 제작자를 위한 라이브러리입니다. 그 자체로는 창도, 툴바 버튼도 추가하지 않습니다. 플레이어가 따로 설치할 필요는 없으며, 이를 쓰는 각 에디터가 의존성으로 함께 가져옵니다.

## 사용하는 곳

씬, 시나리오, 월드맵, 상호작용, 조명 편집기, Scene Builder, Catalog Export, 아이템·캐릭터 편집기, `rpg_editor`, `rpg_object_editor`, `schema_tools`가 사용합니다.

## 도구에 제공하는 것

- **`ToolUi`** — 도구 창의 공통 모양: 버튼, 필드, 섹션, 구분선, 상태 줄, 번역된 레이블(`ToolUi.Tr`). 창 틀 자체(`ToolWindow`)는 `ui_base`에 있습니다.
- **위젯** — `ModFileSelector`(쓰기 가능한 모드와 폴더 안의 파일 선택), `FilteredList`, `CollapsibleSection`, `BackgroundCanvas`(확대 가능한 장면 배경), `DragHandle`.
- **파일** — `ModFileService`(모드 안 상대 경로로 파일 읽기/쓰기, 폴더 모드만), `ModResourceService`, `SceneFileService`/`SceneBackgroundService`(논리 장면과 배경), `LocaleFileService`와 `PropertiesText`(모드의 모든 언어 `.properties` 텍스트를 주석과 순서를 유지하며 편집).
- **엔티티 에디터** — `EntityEditorWindow`, `EntityDocument`, `EntityField`: 검색 가능한 목록과 모든 언어의 텍스트, 그림, JSON 필드가 있는 양식. 아이템과 캐릭터 에디터가 이것으로 만들어졌습니다(`docs/concepts/tools.md` 참고).
- **스키마 양식** — `schema_base` 주석에서 생성되는 필드 에디터(스칼라, 목록, 맵, 현지화 텍스트).
- **도구 사이의 약속** — `IScenarioResourceEditor`로 한 창이 시나리오 에디터에게 모드 안 주소의 파일을 열어 달라고 요청합니다. `IScenarioTextTarget`과 `ScenarioTextTargets`는 명령 팔레트의 삽입을 마지막으로 포커스된 시나리오로 보냅니다. 에디터는 창이 닫히면 등록을 해제합니다.

- **다른 모드의 항목** — 목록은 모드 의존 대상의 항목을 보여 줄 수 있습니다. 그런 항목의 편집은 편집 중인 모드에 패치로 기록되며, 소유자의 파일은 바뀌지 않습니다. 필드 표시로 값의 출처(여러분의 패치, 다른 모드의 패치, 상속)를 알 수 있고, **↺**는 필드를 되돌립니다.
- **읽기 전용 필드** — `EntityField.Locked`는 다른 곳에서 계산되는 값(스탯이 있는 캐릭터의 체력 등)을 바꾸지 못하게 보여 주는 필드입니다.
- **도구 간 계약 `IMapPickupEditor`** — 아이템 편집기의 "Place on map"은 줍기 오브젝트를 놓을 준비가 된 상태로 RPG 맵 편집기를 열도록 요청합니다.

## 이것으로 도구 만들기

`tools_base>=1`에 의존하고 `IToolProvider`(창을 만듦)를, 도구 상자 항목이 필요하면 `IToolBoxButton`도 구현합니다. 게임이 모드 어셈블리에서 자동으로 찾으므로 매니페스트에 적을 필요가 없습니다. 파일은 `ModFileService`로만 쓰면 패키지된 모드는 읽기 전용으로 남습니다.

## 의존성

`ui_base`, `schema_base`. 테스트 묶음 `isolation`(`--itest=tools_isolation`)은 에디터 창이 하나도 없어도 동작하는지 확인합니다.

## 폼과 이름 변경

`schema_base` 폼은 다른 엔티티 값 선택 `pick`과 번역된 퀘스트 단계 `stages`(`StageFieldEditor`, `ITextFieldEditor`)를 포함합니다. `EntityRename`, `EntityRenameDialog`는 ID 변경 전 계획을 보여 주고 `SchemaReferenceScanner`가 스키마와 DSL의 참조를 찾습니다. 선택 모드만 수정하며 외부 참조는 나열합니다. 보류 편집을 먼저 저장하고 Undo rename으로 원본 파일을 복원합니다. `IScenarioResourceEditor`, `IScenarioTextTarget`, `ScenarioTextTargets`가 창과 마지막 포커스 시나리오 삽입을 연결합니다.
