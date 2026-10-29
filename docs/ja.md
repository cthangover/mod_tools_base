# ツール作成ベース — エディターウィンドウの共通キット

`tools_base` はエディターウィンドウを書く MOD 作者向けのライブラリです。単体ではウィンドウもツールバーのボタンも追加しません。プレイヤーが個別に入れる必要はなく、これを使う各エディターが依存関係として一緒に持ってきます。

## 利用しているもの

シーン、シナリオ、ワールドマップ、インタラクティブ、ライトの各エディター、シーンビルダー、カタログのエクスポート、アイテムとキャラクターのエディター、RPG マップとタイルセットのエディター、スキーマツール（`schema_tools`）。

## ツールに提供するもの

- **`ToolUi`** — ツールウィンドウ共通の見た目: ボタン、フィールド、セクション、区切り線、ステータス行、翻訳済みラベル（`ToolUi.Tr`）。ウィンドウ枠そのもの（`ToolWindow`）は `ui_base` にあります。
- **ウィジェット** — `ModFileSelector`（書き込み可能な MOD とフォルダー内のファイルを選ぶ）、`FilteredList`、`CollapsibleSection`、`BackgroundCanvas`（ズームできるシーン背景）、`DragHandle`。
- **ファイル** — `ModFileService`（MOD 内の相対パスでファイルを読み書き、フォルダー MOD のみ）、`ModResourceService`、`SceneFileService`/`SceneBackgroundService`（論理シーンとその背景）、`LocaleFileService` と `PropertiesText`（MOD の全言語の `.properties` テキストをコメントと順序を保ったまま編集）。
- **エンティティエディター** — `EntityEditorWindow`、`EntityDocument`、`EntityField`: 検索付きリストと、全言語のテキスト・画像・JSON フィールドを持つフォーム。アイテムとキャラクターのエディターはこれで作られています（`docs/concepts/tools.md` 参照）。
- **スキーマによるフォーム** — `schema_base` の注釈から生成されるフィールドエディター（スカラー、リスト、マップ、ローカライズテキスト）。
- **ツール間の取り決め** — `IScenarioResourceEditor` により、あるウィンドウが MOD 内のアドレスでファイルを開くようシナリオエディターに頼めます。`IScenarioTextTarget` と `ScenarioTextTargets` はコマンドパレットからの挿入を最後にフォーカスされたシナリオへ送ります。エディターはウィンドウが閉じると登録を解除します。

## これを使ったツールの書き方

`tools_base>=1` に依存し、`IToolProvider`（ウィンドウを作る）を実装し、ツールボックスに項目を出すなら `IToolBoxButton` も実装します。ゲームが MOD のアセンブリから自動で見つけるので、マニフェストへの記述は不要です。ファイルは `ModFileService` だけで書けば、パックされた MOD は読み取り専用のままです。

## 依存関係

`ui_base`、`schema_base`。テストスイート `isolation`（`--itest=tools_isolation`）は、エディターウィンドウが一つもなくても動くことを確認します。
