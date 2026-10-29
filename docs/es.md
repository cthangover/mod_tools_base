# Base de herramientas de autor — el kit común de las ventanas de edición

`tools_base` es una biblioteca para autores de mods que escriben ventanas de edición. Por sí sola no añade ventanas ni botones a la barra de herramientas; el jugador no necesita instalarla aparte: cada editor que la usa la trae como dependencia.

## Quién la usa

Los editores de escenas, escenarios, mapa del mundo, interactivos y luces, el constructor de escenas, la exportación de catálogo, los editores de objetos y personajes, los editores de mapas RPG y de tilesets y las herramientas de esquemas (`schema_tools`).

## Qué aporta a una herramienta

- **`ToolUi`** — el aspecto común de las ventanas: botones, campos, secciones, separadores, línea de estado, etiquetas traducidas (`ToolUi.Tr`). El marco de la ventana (`ToolWindow`) sigue en `ui_base`.
- **Widgets** — `ModFileSelector` (elige un mod escribible y un archivo de una carpeta), `FilteredList`, `CollapsibleSection`, `BackgroundCanvas` (fondo de escena con zoom), `DragHandle`.
- **Archivos** — `ModFileService` (lee y escribe un archivo relativo al mod, solo en mods de carpeta), `ModResourceService`, `SceneFileService`/`SceneBackgroundService` (escenas lógicas y sus fondos), `LocaleFileService` y `PropertiesText` (editan los textos `.properties` en todos los idiomas del mod sin perder comentarios ni orden).
- **Editores de entidades** — `EntityEditorWindow`, `EntityDocument`, `EntityField`: una lista con búsqueda y un formulario con textos en todos los idiomas, imágenes y campos JSON. Los editores de objetos y de personajes se basan en ellos (ver `docs/concepts/tools.md`).
- **Formularios por esquema** — editores de campos generados a partir de las anotaciones de `schema_base` (escalares, listas, mapas, textos localizados).
- **Contratos entre herramientas** — `IScenarioResourceEditor` permite a una ventana pedir al editor de escenarios que abra un archivo por su dirección dentro del mod; `IScenarioTextTarget` y `ScenarioTextTargets` dirigen la inserción de la paleta de comandos al último escenario enfocado. Los editores se dan de baja al cerrarse su ventana.

## Escribir una herramienta sobre ella

Depende de `tools_base>=1`, implementa `IToolProvider` (construye la ventana) y, para una entrada en la caja de herramientas, `IToolBoxButton`. El juego los descubre en los ensamblados de los mods; no hace falta nada en el manifiesto. Escribe archivos solo con `ModFileService`, así los mods empaquetados siguen siendo de solo lectura.

## Dependencias

`ui_base`, `schema_base`. La suite de pruebas `isolation` (`--itest=tools_isolation`) comprueba que funciona sin ninguna ventana de edición instalada.
