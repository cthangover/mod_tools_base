# 创作工具基础 —— 编辑器窗口的公共工具包

`tools_base` 是给编写编辑器窗口的模组作者用的库。它自己不添加任何窗口，也不在工具栏上加按钮；玩家不需要单独安装它：每个用到它的编辑器都会把它作为依赖一起带上。

## 谁在用它

场景、剧本、世界地图、交互物和光照编辑器，场景构建器，目录导出，物品和角色编辑器，RPG 地图和图块集编辑器，以及模式工具（`schema_tools`）。

## 它为工具提供什么

- **`ToolUi`** —— 工具窗口的统一外观：按钮、字段、分区、分隔线、状态栏、翻译后的标签（`ToolUi.Tr`）。窗口外框本身（`ToolWindow`）仍在 `ui_base`。
- **控件** —— `ModFileSelector`（选择可写的模组和某个文件夹下的文件）、`FilteredList`、`CollapsibleSection`、`BackgroundCanvas`（可缩放的场景背景）、`DragHandle`。
- **文件** —— `ModFileService`（按模组内相对路径读写文件，仅限文件夹模组）、`ModResourceService`、`SceneFileService`/`SceneBackgroundService`（逻辑场景及其背景）、`LocaleFileService` 和 `PropertiesText`（编辑模组所有语言的 `.properties` 文本，不丢注释和顺序）。
- **实体编辑器** —— `EntityEditorWindow`、`EntityDocument`、`EntityField`：带搜索的列表，加上包含所有语言文本、图片和 JSON 字段的表单。物品和角色编辑器就是在它们之上构建的（见 `docs/concepts/tools.md`）。
- **按模式生成的表单** —— 根据 `schema_base` 注解生成的字段编辑器（标量、列表、字典、本地化文本）。
- **工具之间的约定** —— 通过 `IScenarioResourceEditor`，一个窗口可以请剧本编辑器按模组内地址打开文件；`IScenarioTextTarget` 和 `ScenarioTextTargets` 把命令面板的插入送到最后获得焦点的剧本。窗口关闭时编辑器会自行注销。

- **其他模组的条目** —— 列表可显示模组依赖的条目。编辑这类条目会以补丁形式写入被编辑的模组，所有者的文件不变。字段上的标记显示值的来源（你的补丁、其他模组的补丁或继承）；**↺** 可以重置字段。
- **只读字段** —— `EntityField.Locked` 显示一个不允许修改的字段，用于由其他地方计算出的值（带属性的角色的生命值）。
- **工具间的契约 `IMapPickupEditor`** —— 物品编辑器的「Place on map」请求 RPG 地图编辑器打开，并准备好要放置的拾取对象。

## 在它之上编写工具

依赖 `tools_base>=1`，实现 `IToolProvider`（构建窗口），如需在工具箱中出现，再实现 `IToolBoxButton`。游戏会在模组程序集中自动发现它们，无需在清单中登记。只通过 `ModFileService` 写文件，这样打包的模组保持只读。

## 依赖

`ui_base`、`schema_base`。测试套件 `isolation`（`--itest=tools_isolation`）检查它在没有安装任何编辑器窗口时也能工作。
