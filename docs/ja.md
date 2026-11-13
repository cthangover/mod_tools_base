# ツール作成ベース — エディターウィンドウの共通キット

`tools_base` はエディターウィンドウを書く MOD 作者向けのライブラリです。単体ではウィンドウもツールバーのボタンも追加しません。プレイヤーが個別に入れる必要はなく、これを使う各エディターが依存関係として一緒に持ってきます。

## 利用しているもの

シーン、シナリオ、世界地図、操作、照明の各エディタ、Scene Builder、Catalog Export、アイテム・キャラクターエディタ、`rpg_editor`、`rpg_object_editor`、`schema_tools` がこのライブラリを使います。

## ツールに提供するもの

- **`ToolUi`** — ツールウィンドウ共通の見た目: ボタン、フィールド、セクション、区切り線、ステータス行、翻訳済みラベル（`ToolUi.Tr`）。ウィンドウ枠そのもの（`ToolWindow`）は `ui_base` にあります。
- **ウィジェット** — `ModFileSelector`（書き込み可能な MOD とフォルダー内のファイルを選ぶ）、`FilteredList`、`CollapsibleSection`、`BackgroundCanvas`（ズームできるシーン背景）、`DragHandle`。
- **ファイル** — `ModFileService`（MOD 内の相対パスでファイルを読み書き、フォルダー MOD のみ）、`ModResourceService`、`SceneFileService`/`SceneBackgroundService`（論理シーンとその背景）、`LocaleFileService` と `PropertiesText`（MOD の全言語の `.properties` テキストをコメントと順序を保ったまま編集）。
- **エンティティエディター** — `EntityEditorWindow`、`EntityDocument`、`EntityField`: 検索付きリストと、全言語のテキスト・画像・JSON フィールドを持つフォーム。アイテムとキャラクターのエディターはこれで作られています（`docs/concepts/tools.md` 参照）。
- **スキーマによるフォーム** — `schema_base` の注釈から生成されるフィールドエディター（スカラー、リスト、マップ、ローカライズテキスト）。
- **ツール間の取り決め** — `IScenarioResourceEditor` により、あるウィンドウが MOD 内のアドレスでファイルを開くようシナリオエディターに頼めます。`IScenarioTextTarget` と `ScenarioTextTargets` はコマンドパレットからの挿入を最後にフォーカスされたシナリオへ送ります。エディターはウィンドウが閉じると登録を解除します。

- **他のモッドの項目** — 一覧には依存先モッドの項目を表示できます。そのような項目の編集は、編集中のモッドにパッチとして書かれ、所有者のファイルは変わりません。フィールドの印で値の出どころ（自分のパッチ、他のモッドのパッチ、継承）がわかり、**↺** でフィールドを元に戻します。
- **読み取り専用のフィールド** — `EntityField.Locked` は、他所から計算される値（統計のあるキャラクターの体力など）を変更できないように表示するフィールドです。
- **ツール間の契約 `IMapPickupEditor`** — アイテムエディタの「Place on map」は、拾得物を置く準備をした状態で RPG マップエディタを開くよう依頼します。

## これを使ったツールの書き方

`tools_base>=0.1.0` に依存し、`IToolProvider`（ウィンドウを作る）を実装し、ツールボックスに項目を出すなら `IToolBoxButton` も実装します。ゲームが MOD のアセンブリから自動で見つけるので、マニフェストへの記述は不要です。ファイルは `ModFileService` だけで書けば、パックされた MOD は読み取り専用のままです。

## 依存関係

`ui_base`、`schema_base`。テストスイート `isolation`（`--itest=tools_isolation`）は、エディターウィンドウが一つもなくても動くことを確認します。

## フォームと名前変更

`schema_base` のフォームは他のエンティティの値を選ぶ `pick` と翻訳付きクエスト段階の `stages`（`StageFieldEditor`、`ITextFieldEditor`）を含みます。`EntityRename` と `EntityRenameDialog` は ID 変更前に計画を示し、`SchemaReferenceScanner` がスキーマと DSL の参照を探します。変更は選択 MOD のみで、外部参照は一覧します。保留中の編集を先に保存し、Undo rename で元のファイルを復元します。`IScenarioResourceEditor`、`IScenarioTextTarget`、`ScenarioTextTargets` がウィンドウと最後にフォーカスしたシナリオへの挿入を結びます。
