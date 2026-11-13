# Tool Authoring Base — the shared kit of the authoring windows

`tools_base` is a library for mod authors who write editor windows. It adds no windows and no toolbar entries by itself; players never need to install it on its own — every editor that uses it brings it along as a dependency.

## Who uses it

The scene, scenario, world map, interactive and light editors, the Scene Builder, Catalog Export, the item and character editors, the RPG map editor (`rpg_editor`), the RPG object editor (`rpg_object_editor`) and the schema tools (`schema_tools`) all depend on it.

## What it gives a tool

- **`ToolUi`** — the common look of tool windows: buttons, fields, sections, dividers, status line, translated labels (`ToolUi.Tr`). Window chrome itself (`ToolWindow`) stays in `ui_base`.
- **Widgets** — `ModFileSelector` (pick a writable mod and a file under a folder), `FilteredList`, `CollapsibleSection`, `BackgroundCanvas` (a scene background with zoom), `DragHandle`.
- **Files** — `ModFileService` (read/write a mod-relative file only in folder mods), `ModResourceService`, `SceneFileService`/`SceneBackgroundService` (logical scenes and their backgrounds), `LocaleFileService` and `PropertiesText` (edit `.properties` texts in every language of a mod without losing comments and order).
- **Entity editors** — `EntityEditorWindow`, `EntityDocument`, `EntityField`: a searchable list plus a form with texts in all languages, pictures and JSON fields. The item, character and RPG object editors are built on it (see `docs/concepts/tools.md`).
- **Other mods' entities** — the list can show the entries of the mod's dependencies. An edit of such an entry is written as a patch in the edited mod (`patches/<kind>/<mod>.json`), so the owner's file never changes. Field badges show whether a value is your patch, another mod's patch or inherited; **↺** resets a field.
- **Read-only fields** — `EntityField.Locked` shows a field without letting it change, for a value the entity computes from elsewhere (a character's health with stats).
- **Contracts between tools** also include `IMapPickupEditor`: the item editor's *Place on map* asks the RPG map editor to open with a pickup ready to place.
- **Schema forms** — field editors generated from `schema_base` annotations (scalars, lists, maps, localized texts). A `pick` field offers the values another kind's entities hold (an equip slot's accepted types); a `stages` field is a quest's numbered steps, each with a text in every language of the mod (`StageFieldEditor`, which implements `ITextFieldEditor` with the other text-holding editors).
- **Renaming an id** — `EntityRename` and `EntityRenameDialog`: a saved entity's id is changed through **Rename…**, which lists every file that names it before anything is written. The references in the edited mod are rewritten; the same id named by other mods is listed and left as it is. **Undo rename** restores the files byte for byte. The window saves its pending edits first. The reference search is `SchemaReferenceScanner` in `schema_base`, and it finds only the references the schemas and the DSL describe.
- **Contracts between tools** — `IScenarioResourceEditor` lets one window ask the scenario editor to open a file by its mod-relative address; `IScenarioTextTarget` and `ScenarioTextTargets` route command-palette insertion to the last focused scenario document. Editors unregister themselves when their window closes.

## Writing a tool on it

Depend on `tools_base>=0.1.0`, implement `IToolProvider` (builds the window) and, for a tool box entry, `IToolBoxButton`. The game discovers both in mod assemblies; no manifest entry is needed. Write files only through `ModFileService`, so packed mods stay read-only.

## Dependencies

`ui_base`, `schema_base`. Test suite `isolation` (`--itest=tools_isolation`) checks that it works without any editor window installed.
