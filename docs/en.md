# Tool Authoring Base — the shared kit of the authoring windows

`tools_base` is a library for mod authors who write editor windows. It adds no windows and no toolbar entries by itself; players never need to install it on its own — every editor that uses it brings it along as a dependency.

## Who uses it

The scene, scenario, world map, interactive and light editors, the Scene Builder, Catalog Export, the item and character editors, the RPG map and tileset editors and the schema tools (`schema_tools`) all depend on it.

## What it gives a tool

- **`ToolUi`** — the common look of tool windows: buttons, fields, sections, dividers, status line, translated labels (`ToolUi.Tr`). Window chrome itself (`ToolWindow`) stays in `ui_base`.
- **Widgets** — `ModFileSelector` (pick a writable mod and a file under a folder), `FilteredList`, `CollapsibleSection`, `BackgroundCanvas` (a scene background with zoom), `DragHandle`.
- **Files** — `ModFileService` (read/write a mod-relative file only in folder mods), `ModResourceService`, `SceneFileService`/`SceneBackgroundService` (logical scenes and their backgrounds), `LocaleFileService` and `PropertiesText` (edit `.properties` texts in every language of a mod without losing comments and order).
- **Entity editors** — `EntityEditorWindow`, `EntityDocument`, `EntityField`: a searchable list plus a form with texts in all languages, pictures and JSON fields. The item and character editors are built on it (see `docs/concepts/tools.md`).
- **Schema forms** — field editors generated from `schema_base` annotations (scalars, lists, maps, localized texts).
- **Contracts between tools** — `IScenarioResourceEditor` lets one window ask the scenario editor to open a file by its mod-relative address; `IScenarioTextTarget` and `ScenarioTextTargets` route command-palette insertion to the last focused scenario document. Editors unregister themselves when their window closes.

## Writing a tool on it

Depend on `tools_base>=1`, implement `IToolProvider` (builds the window) and, for a tool box entry, `IToolBoxButton`. The game discovers both in mod assemblies; no manifest entry is needed. Write files only through `ModFileService`, so packed mods stay read-only.

## Dependencies

`ui_base`, `schema_base`. Test suite `isolation` (`--itest=tools_isolation`) checks that it works without any editor window installed.
