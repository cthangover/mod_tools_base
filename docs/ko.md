# 도구 제작 기반 — 에디터 창의 공통 키트

`tools_base`는 에디터 창을 만드는 모드 제작자를 위한 라이브러리입니다. 그 자체로는 창도, 툴바 버튼도 추가하지 않습니다. 플레이어가 따로 설치할 필요는 없으며, 이를 쓰는 각 에디터가 의존성으로 함께 가져옵니다.

## 사용하는 곳

장면, 시나리오, 월드맵, 상호작용, 조명 에디터, 장면 빌더, 카탈로그 내보내기, 아이템과 캐릭터 에디터, RPG 맵과 타일셋 에디터, 스키마 도구(`schema_tools`).

## 도구에 제공하는 것

- **`ToolUi`** — 도구 창의 공통 모양: 버튼, 필드, 섹션, 구분선, 상태 줄, 번역된 레이블(`ToolUi.Tr`). 창 틀 자체(`ToolWindow`)는 `ui_base`에 있습니다.
- **위젯** — `ModFileSelector`(쓰기 가능한 모드와 폴더 안의 파일 선택), `FilteredList`, `CollapsibleSection`, `BackgroundCanvas`(확대 가능한 장면 배경), `DragHandle`.
- **파일** — `ModFileService`(모드 안 상대 경로로 파일 읽기/쓰기, 폴더 모드만), `ModResourceService`, `SceneFileService`/`SceneBackgroundService`(논리 장면과 배경), `LocaleFileService`와 `PropertiesText`(모드의 모든 언어 `.properties` 텍스트를 주석과 순서를 유지하며 편집).
- **엔티티 에디터** — `EntityEditorWindow`, `EntityDocument`, `EntityField`: 검색 가능한 목록과 모든 언어의 텍스트, 그림, JSON 필드가 있는 양식. 아이템과 캐릭터 에디터가 이것으로 만들어졌습니다(`docs/concepts/tools.md` 참고).
- **스키마 양식** — `schema_base` 주석에서 생성되는 필드 에디터(스칼라, 목록, 맵, 현지화 텍스트).
- **도구 사이의 약속** — `IScenarioResourceEditor`로 한 창이 시나리오 에디터에게 모드 안 주소의 파일을 열어 달라고 요청합니다. `IScenarioTextTarget`과 `ScenarioTextTargets`는 명령 팔레트의 삽입을 마지막으로 포커스된 시나리오로 보냅니다. 에디터는 창이 닫히면 등록을 해제합니다.

## 이것으로 도구 만들기

`tools_base>=1`에 의존하고 `IToolProvider`(창을 만듦)를, 도구 상자 항목이 필요하면 `IToolBoxButton`도 구현합니다. 게임이 모드 어셈블리에서 자동으로 찾으므로 매니페스트에 적을 필요가 없습니다. 파일은 `ModFileService`로만 쓰면 패키지된 모드는 읽기 전용으로 남습니다.

## 의존성

`ui_base`, `schema_base`. 테스트 묶음 `isolation`(`--itest=tools_isolation`)은 에디터 창이 하나도 없어도 동작하는지 확인합니다.
