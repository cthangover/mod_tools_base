using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Cthangover.Mods.Interface.Theming;
using Cthangover.Mods.Interface.Tool;
using Cthangover.Mods.Schemas;
using Cthangover.Tools.Common;
using Cthangover.Tools.Schemas;
using Cthangover.Tools.Services;
using Godot;

namespace Cthangover.Tools.EntityEditor
{
    using Cthangover.Core.Events;

    /// <summary>
    /// Base of the data editors (items, characters…): every entity of one kind in
    /// one writable mod, a search list on the left and a form on the right built
    /// from the editor's <see cref="EntityField"/> list. Layout and behaviour are
    /// shared by all such editors, so they look and work alike; a concrete editor
    /// only names its kind, its fields and how a new entity starts out.
    /// <para/>
    /// For a beginner the form is all there is: a name is typed in each language
    /// of the mod (the locale key is made from the id), a picture is picked from a
    /// grid, lists are check boxes. Save writes the mod's data and locale files and
    /// makes the running game re-read them (entities, then texts), so the change is
    /// visible at once. For a professional nothing is hidden: the locale key stays
    /// editable, and every property the form does not know is shown as JSON under
    /// "other fields", edited there and written back as it is.
    /// <para/>
    /// Fields other mods describe for the kind in their schemas (<c>schemas/*.json</c>: the
    /// equipment mod adds <c>Equip</c> and <c>Traits</c> to items, the stats add <c>Archetype</c>
    /// and <c>Stats</c> to characters) get their own block, "fields of other mods", with the
    /// schema editors, so a data editor grows with the mods loaded next to it without
    /// knowing them.
    /// <para/>
    /// Every change is undoable (Ctrl+Z / Ctrl+Y): one undo step per editing
    /// session of one entity, and one per add, duplicate or delete.
    /// <para/>
    /// Entities of the mod's dependencies are listed too (marked "↗ owner"; a check box hides
    /// them). The form shows what the game sees, and an edit is written as the edited mod's patch
    /// (<c>patches/&lt;kind&gt;/&lt;mod&gt;.json</c>, the minimal difference), never into the
    /// owner's file; Delete writes <c>"$delete": true</c>, "Reset to inherited" removes the entry.
    /// Under each field a badge tells where the value comes from (<see cref="EntityResolver.Explain"/>):
    /// the own patch, another mod's patch — with a warning when it hides the own value — or an
    /// <c>Extends</c> parent; its "↺" removes the own key (from the file or the patch).
    /// </summary>
    public abstract partial class EntityEditorWindow : ToolWindow, Cthangover.Tools.Contracts.IEntityCreationEditor
    {
        private readonly string kind;
        private readonly string folder;
        private readonly List<EntityField> fields;
        private readonly Dictionary<EntityField, Control> controls = new();
        private readonly Dictionary<EntityField, LineEdit> keyEdits = new();
        private readonly Dictionary<EntityField, Dictionary<string, LineEdit>> textEdits = new();
        private readonly Dictionary<EntityField, TextureRect> previews = new();
        private readonly Dictionary<string, Texture2D> icons = new();
        private readonly List<string> mods = new();
        private readonly List<ISchemaFieldEditor> schemaEditors = new();
        private readonly Dictionary<string, FieldOriginBadge> badges = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> dependencies = new(StringComparer.OrdinalIgnoreCase);
        private EntityExplanation explanation;
        private CheckBox foreignToggle;
        private Button resetEntity;
        private readonly Dictionary<EntityField, Container> blocks = new();
        private readonly Dictionary<EntityField, string> shown = new();
        private readonly EntityRename rename = new();
        private HashSet<string> savedIds = new(StringComparer.OrdinalIgnoreCase);
        private Button renameButton;
        private Button undoRenameButton;

        private OptionButton modPicker;
        private FilteredList list;
        private VBoxContainer form;
        private Label emptyHint;
        private Label fileLabel;
        private LineEdit idEdit;
        private CodeEdit otherEdit;
        private EntityDocument document;
        private EntityRecord selected;
        private List<string> languages = new();
        private string modId;
        private bool suppress;
        private bool editing;

        /// <summary>Builds the window for entities of <paramref name="entityKind"/>; new ones go to <c>&lt;folder&gt;/&lt;mod&gt;.json</c>.</summary>
        protected EntityEditorWindow(string titleKey, string entityKind, string entityFolder, List<EntityField> entityFields) : base(titleKey)
        {
            kind = entityKind;
            folder = entityFolder;
            fields = entityFields;
            CthangoverTheme.Apply(this);
            BuildUi();
            SetHint(ToolUi.Tr("tools/entity_editor/hint"));
            FillMods();
        }

        /// <summary>A new entity's JSON: the kind's required fields with starting values.</summary>
        protected abstract JsonObject CreateEntity(string id);

        /// <summary>Id a new entity starts from (numbered when taken), e.g. <c>&lt;mod&gt;/new_item</c>.</summary>
        protected abstract string NewIdBase(string mod);

        /// <summary>
        /// The picture a row shows, as a file group and a path read from the entity's JSON (null when none).
        /// Default: the first picture field. A foreign row passes its owner's definition.
        /// </summary>
        protected virtual (string Group, string Path) ThumbnailOf(JsonObject data)
        {
            var first = fields.FirstOrDefault(f => f.Kind == EntityFieldKind.Picture);
            return first == null ? (null, null) : (first.Group, EntityPath.Get(data, first.Key)?.ToString());
        }

        /// <summary>
        /// A block a concrete editor adds below its fields (a preview, say); null = none. Called while
        /// the window is built, before the subclass constructor body runs: keep its own state in fields
        /// without initialisers.
        /// </summary>
        protected virtual Control CreatePreviewBlock() => null;

        /// <summary>Fields edited by a specialized inspector instead of a generic schema widget; raw JSON remains available for recovering malformed drafts.</summary>
        protected virtual bool IsCustomField(string key) => false;

        /// <summary>
        /// References of a record that the schema does not describe, checked on save so the own mod gets the provider in
        /// <c>depends</c> (plan 14.G16): an object's interaction names a scenario, a map or a shop. The default names none.
        /// </summary>
        protected virtual IEnumerable<(string Kind, string Id)> ExtraReferencesOf(JsonObject data)
            => Enumerable.Empty<(string Kind, string Id)>();

        /// <summary>Called after the form shows an entity (null: none is selected) and after each edit of it.</summary>
        protected virtual void OnSelectionChanged(EntityRecord record)
        {
        }

        /// <summary>The edited mod, or null.</summary>
        public string ModId => modId;

        /// <summary>The selected entity's id, or null.</summary>
        public string SelectedId => selected?.Id;

        /// <summary>The edited entities (null before a mod is opened).</summary>
        public EntityDocument Document => document;

        // ── layout ─────────────────────────────────────────────────

        private void BuildUi()
        {
            var outer = CreateFillContainer();
            AddChild(outer);
            outer.AddChild(BuildToolbar());

            var main = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            outer.AddChild(main);

            var listColumn = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            listColumn.AddChild(ToolUi.Section("tools/entity_editor/section_list"));
            foreignToggle = new CheckBox { Text = ToolUi.Tr("tools/entity_editor/show_foreign"), TooltipText = ToolUi.Tr("tools/entity_editor/show_foreign_tip"), ButtonPressed = true };
            foreignToggle.Toggled += OnForeignToggled;
            listColumn.AddChild(foreignToggle);
            list = new FilteredList(320f, "tools/entity_editor/search");
            list.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            list.FitIcons(64);
            list.Chosen += OnChosen;
            listColumn.AddChild(list);
            var listPanel = CreateSidebar(listColumn, 320);
            main.AddChild(listPanel);

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            main.AddChild(scroll);
            var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", UiTokens.Space2);
            scroll.AddChild(column);
            emptyHint = ToolUi.Hint(ToolUi.Tr("tools/entity_editor/no_entity"));
            column.AddChild(emptyHint);
            form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            form.AddThemeConstantOverride("separation", UiTokens.Space2);
            column.AddChild(form);
            BuildForm();
        }

        private HBoxContainer BuildToolbar()
        {
            var toolbar = CreateToolbar();
            modPicker = new OptionButton { TooltipText = ToolUi.Tr("tools/entity_editor/mod_tip") };
            modPicker.ItemSelected += OnModPicked;
            toolbar.AddChild(modPicker);
            toolbar.AddChild(ToolUi.Button("tools/common/save", OnSavePressed, ButtonKind.Primary, "tools/common/save_tip"));
            toolbar.AddChild(ToolUi.Divider());
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/new", AddNew, ButtonKind.Secondary, "tools/entity_editor/new_tip"));
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/duplicate", DuplicateSelected, ButtonKind.Secondary, "tools/entity_editor/duplicate_tip"));
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/delete", DeleteSelected, ButtonKind.Secondary, "tools/entity_editor/delete_tip"));
            toolbar.AddChild(ToolUi.Divider());
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/undo", Undo, ButtonKind.Secondary, "tools/entity_editor/undo_tip"));
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/redo", Redo, ButtonKind.Secondary, "tools/entity_editor/redo_tip"));
            undoRenameButton = ToolUi.Button("tools/rename/undo", UndoRename, ButtonKind.Secondary, "tools/rename/undo_tip");
            undoRenameButton.Disabled = true;
            toolbar.AddChild(undoRenameButton);
            return toolbar;
        }

        private void BuildForm()
        {
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", UiTokens.Space3);
            fileLabel = ToolUi.Hint(string.Empty);
            header.AddChild(fileLabel);
            resetEntity = ToolUi.Button("tools/entity_editor/reset", ResetEntity, ButtonKind.Secondary, "tools/entity_editor/reset_tip");
            resetEntity.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            header.AddChild(resetEntity);
            form.AddChild(header);
            idEdit = ToolUi.Field(form, "tools/entity_editor/id", ToolUi.Text("", OnTextChanged), "tools/entity_editor/id_tip");
            idEdit.AddThemeFontOverride("font", ToolUi.Mono());
            // A saved id changes only through the rename: it rewrites the references (plan 14.G6).
            renameButton = ToolUi.Button("tools/rename/rename", Rename, ButtonKind.Secondary, "tools/rename/rename_tip");
            renameButton.Visible = false;
            form.AddChild(renameButton);
            foreach (var field in fields)
                AddBadge(EntityPath.Top(field.Key), BuildField(field));
            BuildSchemaFields();
            var preview = CreatePreviewBlock();
            if (preview != null)
                form.AddChild(preview);

            form.AddChild(ToolUi.Section("tools/entity_editor/section_other"));
            form.AddChild(ToolUi.Hint(ToolUi.Tr("tools/entity_editor/other_hint")));
            otherEdit = ToolUi.Code();
            otherEdit.Name = "OtherFields";
            otherEdit.CustomMinimumSize = new Vector2(0, 160);
            otherEdit.SizeFlagsVertical = Control.SizeFlags.Fill;
            otherEdit.TextChanged += OnOtherChanged;
            form.AddChild(otherEdit);
        }

        private Container BuildField(EntityField field)
        {
            // One block per field, so a form can hide the fields of the forms it does not need.
            var host = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            form.AddChild(host);
            blocks[field] = host;
            switch (field.Kind)
            {
                case EntityFieldKind.Text:
                    controls[field] = ToolUi.Field(host, field.LabelKey, ToolUi.Text("", OnTextChanged), field.TooltipKey);
                    break;
                case EntityFieldKind.Numbers:
                    controls[field] = ToolUi.Field(host, field.LabelKey, ToolUi.Text("", OnTextChanged), field.TooltipKey);
                    break;
                case EntityFieldKind.Integer:
                    controls[field] = ToolUi.Field(host, field.LabelKey, ToolUi.Number(-999999, 999999, 1, OnNumberChanged), field.TooltipKey);
                    break;
                case EntityFieldKind.Choice:
                    var choice = ToolUi.Field(host, field.LabelKey, new OptionButton(), field.TooltipKey);
                    choice.ItemSelected += OnChoicePicked;
                    controls[field] = choice;
                    break;
                case EntityFieldKind.Flags:
                case EntityFieldKind.List:
                    controls[field] = ToolUi.Field(host, field.LabelKey, new HFlowContainer(), field.TooltipKey);
                    break;
                case EntityFieldKind.Picture:
                    BuildPicture(field, host);
                    break;
                case EntityFieldKind.LocaleText:
                    // The texts get their rows when a mod is opened: its languages decide them.
                    keyEdits[field] = ToolUi.Field(host, field.LabelKey, ToolUi.Text("", OnTextChanged), "tools/entity_editor/locale_key_tip");
                    keyEdits[field].AddThemeFontOverride("font", ToolUi.Mono());
                    var texts = new VBoxContainer();
                    host.AddChild(texts);
                    controls[field] = texts;
                    textEdits[field] = new Dictionary<string, LineEdit>();
                    break;
            }
            return host;
        }

        private void BuildSchemaFields()
        {
            var described = Cthangover.Mods.Schemas.SchemaCatalog.Current.FieldsOf(kind)
                .Where(f => !IsCustomField(f.Key) && !string.Equals(f.Key, "Id", StringComparison.OrdinalIgnoreCase) && !fields.Any(e => string.Equals(EntityPath.Top(e.Key), f.Key, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (described.Count == 0)
                return;
            form.AddChild(ToolUi.Section("tools/entity_editor/section_schema"));
            var sources = string.Join(", ", described.Select(f => f.SourceMod).Where(m => m != null).Distinct());
            form.AddChild(ToolUi.Hint(string.Format(ToolUi.Tr("tools/entity_editor/schema_hint"), sources)));
            foreach (var field in described)
            {
                schemaEditors.Add(SchemaFieldEditors.Create(form, field, WriteForm));
                AddBadge(field.Key, form);
            }
        }

        private void AddBadge(string key, Container host)
        {
            if (badges.ContainsKey(key))
                return;
            var badge = new FieldOriginBadge(key, ResetField);
            host.AddChild(badge);
            badges[key] = badge;
        }

        private void BuildPicture(EntityField field, Container host)
        {
            var row = new HBoxContainer();
            var path = ToolUi.Text(field.Group + "/…", OnPictureTyped);
            path.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(path);
            var pick = UiKit.IconButton("…", ToolUi.Tr("tools/entity_editor/picture_pick_tip"));
            pick.Pressed += () => OpenPicker(field);
            row.AddChild(pick);
            ToolUi.Field(host, field.LabelKey, row, field.TooltipKey);
            controls[field] = path;
            var preview = new TextureRect
            {
                CustomMinimumSize = new Vector2(96, 96),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
            };
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", ToolUi.LabelWidth + UiTokens.Space3);
            margin.AddChild(preview);
            host.AddChild(margin);
            previews[field] = preview;
        }

        // ── mods and the list ──────────────────────────────────────

        private void FillMods()
        {
            mods.Clear();
            mods.AddRange(ModFileService.ListWritableMods());
            modPicker.Clear();
            foreach (var mod in mods)
                modPicker.AddItem(mod);
            if (mods.Count == 0)
            {
                SetStatus(ToolUi.Tr("tools/common/no_writable_mods"), ToolStatusKind.Error);
                return;
            }
            // Start in the first mod that already has such entities: usually the game's own content.
            var start = mods.FindIndex(m => LoadDocument(m).Records.Count > 0);
            OpenMod(mods[Math.Max(0, start)]);
        }

        private EntityDocument LoadDocument(string mod)
        {
            var sources = ModFileService.ListFiles(mod, string.Empty, ".json")
                .Select(path => (path, ModFileService.ReadText(mod, path)));
            var loaded = EntityDocument.Load(kind, $"{folder}/{mod}.json", sources, out var skipped);
            if (skipped.Count > 0)
                SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/broken_files"), string.Join(", ", skipped)), ToolStatusKind.Error);
            return loaded;
        }

        /// <summary>Opens the entities of a mod (asks first about unsaved changes).</summary>
        public void OpenMod(string mod)
        {
            RunDiscardingChanges(() => DoOpenMod(mod), () => modPicker.Select(mods.IndexOf(modId)));
        }

        private void DoOpenMod(string mod)
        {
            modId = mod;
            modPicker.Select(mods.IndexOf(mod));
            document = LoadDocument(mod);
            AttachForeign(mod);
            languages = LocaleFileService.Languages(mod);
            foreach (var field in fields.Where(f => f.Kind == EntityFieldKind.LocaleText))
                BuildTextRows(field);
            foreach (var text in schemaEditors.OfType<ITextFieldEditor>())
                text.Open(document, mod, languages);
            savedIds = new HashSet<string>(document.Records.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
            MarkClean();
            selected = null;
            RefreshList();
            Select(document.Records.FirstOrDefault());
            SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/opened"), document.Records.Count, mod));
        }

        private void AttachForeign(string mod)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            dependencies = ForeignEntities.Dependencies(mod);
            var patchFile = ForeignEntities.PatchFile(kind, mod);
            document.AttachForeign(patchFile, ModFileService.ReadText(mod, patchFile), ForeignEntities.TypeOf(kind),
                ForeignEntities.Scan(kind, mod), id => ForeignEntities.BaseWithout(kind, id, mod, patchFile));
            GameLogger.Log("TOOLS", $"entity editor: {document.Foreign.Count} {kind} entities of {dependencies.Count} dependencies of '{mod}' listed in {started.ElapsedMilliseconds} ms");
        }

        private void BuildTextRows(EntityField field)
        {
            var box = (VBoxContainer)controls[field];
            ClearChildren(box);
            textEdits[field].Clear();
            foreach (var language in languages)
            {
                var label = string.Format(ToolUi.Tr("tools/entity_editor/text_in"), language);
                var edit = ToolUi.Text("", OnTextChanged);
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", UiTokens.Space3);
                row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(ToolUi.LabelWidth, 0), HorizontalAlignment = HorizontalAlignment.Right });
                edit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(edit);
                box.AddChild(row);
                textEdits[field][language] = edit;
            }
        }

        /// <summary>Rebuilds the list from the document (own and listed foreign entities), keeping the selection.</summary>
        protected void RefreshList()
        {
            var name = fields.FirstOrDefault(f => f.Kind == EntityFieldKind.LocaleText);
            var rows = document.Records.Select(r => (r.Id, RowText(r, name), Thumbnail(r.Data)));
            if (foreignToggle.ButtonPressed)
                rows = rows.Concat(document.Foreign.Select(r => (r.Id, ForeignRowText(r, name), Thumbnail(r.Written))));
            list.SetItems(rows.ToList());
            list.Select(selected?.Id);
        }

        /// <summary>A foreign entity's list text without resolving it: name in the current language, id, owner and the own patch's mark.</summary>
        private string ForeignRowText(EntityRecord record, EntityField nameField)
        {
            var key = nameField == null ? null : Shown(record, nameField.Key);
            var language = languages.FirstOrDefault() ?? "ru";
            var text = key == null ? null : document.GetText(language, key) ?? GameText(language, key);
            var mark = document.IsDeleted(record) ? "✕ " : document.PatchOf(record.Id) != null ? "✎ " : string.Empty;
            return mark + (string.IsNullOrWhiteSpace(text) ? record.Id : $"{text}  ·  {record.Id}") + "  ↗ " + record.OwnerMod;
        }

        /// <summary>A property of a foreign entity for the list: the own patch's value, else the owner's.</summary>
        private string Shown(EntityRecord record, string key)
        {
            var patched = EntityJson.Get(document.PatchOf(record.Id), key, out _);
            return patched is JsonValue ? patched.ToString() : EntityJson.Get(record.Written, key, out _)?.ToString();
        }

        /// <summary>The game's text of a key, for the current language only (null when untranslated).</summary>
        private static string GameText(string language, string key)
        {
            if (language != (LanguageCode.Normalize(TranslationServer.GetLocale()) ?? "ru"))
                return null;
            var text = TranslationServer.Translate(key).ToString();
            return text == key ? null : text;
        }

        private void OnForeignToggled(bool on)
        {
            if (document != null)
                RefreshList();
        }

        private string RowText(EntityRecord record, EntityField nameField)
        {
            var key = nameField == null ? null : Str(record.Data, nameField.Key);
            var text = key == null ? null : document.GetText(languages.FirstOrDefault() ?? "ru", key) ?? LocaleFileService.Read(modId, languages.FirstOrDefault() ?? "ru", key);
            return string.IsNullOrWhiteSpace(text) ? record.Id : $"{text}  ·  {record.Id}";
        }

        private Texture2D Thumbnail(JsonObject data)
        {
            if (data == null)
                return null;
            var (group, path) = ThumbnailOf(data);
            return group == null || path == null ? null : Icon(group, path);
        }

        private Texture2D Icon(string group, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            var cacheKey = group + "|" + id;
            if (!icons.TryGetValue(cacheKey, out var texture))
            {
                texture = ModResourceService.LoadGroupImage(group, id);
                icons[cacheKey] = texture;
            }
            return texture;
        }

        private void OnChosen(string id) => Select(document?.FindAny(id));

        private void OnModPicked(long index)
        {
            if (index >= 0 && index < mods.Count && mods[(int)index] != modId)
                OpenMod(mods[(int)index]);
        }

        // ── form ───────────────────────────────────────────────────

        /// <summary>Shows an entity in the form (null: none).</summary>
        public void Select(EntityRecord record)
        {
            if (record != null && record.IsForeign && record.Data == null)
            {
                SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/foreign_unresolved"), record.Id), ToolStatusKind.Error);
                record = null;
            }
            selected = record;
            editing = false;
            emptyHint.Visible = record == null;
            form.Visible = record != null;
            list.Select(record?.Id);
            explanation = record == null ? null : ForeignEntities.Explain(kind, record.Id);
            if (record == null)
            {
                OnSelectionChanged(null);
                return;
            }

            suppress = true;
            fileLabel.Text = HeaderText(record);
            resetEntity.Visible = record.IsForeign && document.PatchOf(record.Id) != null;
            // A saved own entity is renamed through the rename button; only a new one has its id typed in the field.
            var saved = !record.IsForeign && savedIds.Contains(record.Id);
            idEdit.Editable = !record.IsForeign && !saved;
            renameButton.Visible = saved;
            idEdit.Text = record.Id;
            foreach (var field in fields)
                ShowField(field, record.Data);
            foreach (var text in schemaEditors.OfType<ITextFieldEditor>())
                text.SetEntityId(record.Id);
            foreach (var editor in schemaEditors)
                editor.Load(record.Data.FirstOrDefault(p => string.Equals(p.Key, editor.Field.Key, StringComparison.OrdinalIgnoreCase)).Value);
            otherEdit.Text = OtherJson(record.Data);
            RefreshVisibility(record.Data);
            RefreshLocks(record.Data);
            suppress = false;
            RefreshBadges();
            OnSelectionChanged(record);
        }

        private string HeaderText(EntityRecord record)
        {
            if (!record.IsForeign)
                return string.Format(ToolUi.Tr("tools/entity_editor/file"), modId, record.File);
            var text = string.Format(ToolUi.Tr("tools/entity_editor/foreign_file"), record.OwnerMod, record.File, modId, document.PatchFile);
            return document.IsDeleted(record) ? text + " " + ToolUi.Tr("tools/entity_editor/foreign_deleted") : text;
        }

        // ── where values come from ─────────────────────────────────

        private void RefreshBadges()
        {
            foreach (var badge in badges.Values)
            {
                if (selected == null)
                {
                    badge.Show(null, false, false);
                    continue;
                }
                var text = BadgeText(badge.Key, out var warning, out var canReset);
                badge.Show(text, warning, canReset);
            }
        }

        private string BadgeText(string key, out bool warning, out bool canReset)
            => FieldOrigins.Describe(selected, modId, document, explanation, dependencies, key, out warning, out canReset);

        private void ResetField(string key)
        {
            if (selected == null)
                return;
            document.BeginChange();
            document.ResetField(selected, key);
            SetDirty();
            RefreshList();
            Select(selected);
            SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/reset_field_done"), key, selected.Id));
        }

        private void ResetEntity()
        {
            if (selected == null || !selected.IsForeign)
                return;
            document.BeginChange();
            document.ResetForeign(selected);
            SetDirty();
            RefreshList();
            Select(selected);
            SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/reset_done"), selected.Id));
        }

        private void ShowField(EntityField field, JsonObject data)
        {
            if (ViaPath(field))
            {
                ShowPathField(field, data);
                return;
            }
            switch (field.Kind)
            {
                case EntityFieldKind.Text:
                    ((LineEdit)controls[field]).Text = Str(data, field.Key) ?? string.Empty;
                    break;
                case EntityFieldKind.Integer:
                    ((SpinBox)controls[field]).Value = data[field.Key] is JsonValue v && v.TryGetValue(out int number) ? number : 0;
                    break;
                case EntityFieldKind.Choice:
                    FillChoice((OptionButton)controls[field], field, Str(data, field.Key));
                    break;
                case EntityFieldKind.Flags:
                    FillChecks((HFlowContainer)controls[field], field, (Str(data, field.Key) ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    break;
                case EntityFieldKind.List:
                    FillChecks((HFlowContainer)controls[field], field, (data[field.Key] as JsonArray)?.Select(n => n?.ToString()).Where(s => s != null).ToArray() ?? Array.Empty<string>());
                    break;
                case EntityFieldKind.Picture:
                    var path = Str(data, field.Key) ?? string.Empty;
                    ((LineEdit)controls[field]).Text = path;
                    previews[field].Texture = ModResourceService.LoadGroupImage(field.Group, path);
                    break;
                case EntityFieldKind.LocaleText:
                    var key = Str(data, field.Key) ?? string.Empty;
                    keyEdits[field].Text = key;
                    foreach (var pair in textEdits[field])
                        pair.Value.Text = TextOf(pair.Key, key);
                    break;
            }
        }

        private string TextOf(string language, string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            var text = document.GetText(language, key);
            if (text != null)
                return text;
            text = LocaleFileService.Read(modId, language, key);
            if (text == null && selected?.IsForeign == true)
                text = LocaleFileService.Read(selected.OwnerMod, language, key) ?? GameText(language, key);
            text ??= string.Empty;
            document.LoadText(language, key, text);
            return text;
        }

        private static void FillChoice(OptionButton picker, EntityField field, string value)
        {
            var options = (field.Options?.Invoke() ?? Enumerable.Empty<string>()).ToList();
            if (!string.IsNullOrEmpty(value) && !options.Contains(value))
                options.Insert(0, value);
            picker.Clear();
            foreach (var option in options)
                picker.AddItem(option);
            picker.Select(options.IndexOf(value ?? string.Empty));
        }

        private void FillChecks(HFlowContainer box, EntityField field, string[] values)
        {
            ClearChildren(box);
            var options = (field.Options?.Invoke() ?? Enumerable.Empty<string>()).ToList();
            options.AddRange(values.Where(v => !options.Contains(v)));
            foreach (var option in options)
            {
                var check = new CheckBox { Text = option, ButtonPressed = values.Contains(option) };
                check.Toggled += OnChecked;
                box.AddChild(check);
            }
        }

        private void OnTextChanged(string text) => WriteForm();

        private void OnNumberChanged(double value) => WriteForm();

        private void OnChoicePicked(long index) => WriteForm();

        private void OnChecked(bool on) => WriteForm();

        private void OnPictureTyped(string text)
        {
            WriteForm();
            foreach (var pair in previews)
                pair.Value.Texture = ModResourceService.LoadGroupImage(pair.Key.Group, ((LineEdit)controls[pair.Key]).Text);
        }

        private void OpenPicker(EntityField field)
        {
            if (selected == null)
                return;
            var dialog = new PicturePickerDialog(field.Group, ((LineEdit)controls[field]).Text);
            dialog.Picked += id => ((LineEdit)controls[field]).Text = id;
            dialog.Picked += OnPictureTyped;
            AddChild(dialog);
            dialog.PopupCentered();
        }

        /// <summary>Copies the form into the selected entity; the first change of a session records an undo step.</summary>
        private void WriteForm()
        {
            if (suppress || selected == null)
                return;
            if (!editing)
            {
                document.BeginChange();
                editing = true;
            }

            var data = selected.Data;
            data["Id"] = idEdit.Text.Trim();
            foreach (var field in fields)
                WriteField(field, data);
            foreach (var editor in schemaEditors)
            {
                var key = data.FirstOrDefault(p => string.Equals(p.Key, editor.Field.Key, StringComparison.OrdinalIgnoreCase)).Key ?? editor.Field.Key;
                var value = editor.Save();
                if (value == null)
                    data.Remove(key);
                else
                    data[key] = value;
            }
            if (selected.IsForeign)
                document.CommitForeign(selected);
            SetDirty();
            RefreshList();
            RefreshBadges();
            resetEntity.Visible = selected.IsForeign && document.PatchOf(selected.Id) != null;
            RefreshVisibility(selected.Data);
            OnSelectionChanged(selected);
        }

        private void WriteField(EntityField field, JsonObject data)
        {
            if (field.When != null && !field.When(data))
                return;
            if (ViaPath(field))
            {
                WritePathField(field, data);
                return;
            }
            switch (field.Kind)
            {
                case EntityFieldKind.Text:
                case EntityFieldKind.Picture:
                    SetOrRemove(data, field.Key, ((LineEdit)controls[field]).Text.Trim());
                    break;
                case EntityFieldKind.Integer:
                    var number = (int)((SpinBox)controls[field]).Value;
                    if (number != 0 || data.ContainsKey(field.Key))
                        data[field.Key] = number;
                    break;
                case EntityFieldKind.Choice:
                    var picker = (OptionButton)controls[field];
                    if (picker.Selected >= 0)
                        data[field.Key] = picker.GetItemText(picker.Selected);
                    break;
                case EntityFieldKind.Flags:
                    var flags = Checked((HFlowContainer)controls[field]);
                    if (flags.Count > 0 || data.ContainsKey(field.Key))
                        data[field.Key] = flags.Count == 0 ? "None" : string.Join(",", flags);
                    break;
                case EntityFieldKind.List:
                    var values = Checked((HFlowContainer)controls[field]);
                    if (values.Count > 0 || data.ContainsKey(field.Key))
                        data[field.Key] = new JsonArray(values.Select(v => (JsonNode)v).ToArray());
                    break;
                case EntityFieldKind.LocaleText:
                    var key = keyEdits[field].Text.Trim();
                    SetOrRemove(data, field.Key, key);
                    if (key.Length == 0)
                        break;
                    foreach (var pair in textEdits[field])
                        document.SetText(pair.Key, key, pair.Value.Text);
                    break;
            }
        }

        /// <summary>True for a field written only when edited: a number list, or a text, choice, number or picture inside a block.</summary>
        private static bool ViaPath(EntityField field)
            => field.Kind == EntityFieldKind.Numbers
               || (EntityPath.IsNested(field.Key) && field.Kind is EntityFieldKind.Text or EntityFieldKind.Choice or EntityFieldKind.Integer or EntityFieldKind.Picture);

        private void ShowPathField(EntityField field, JsonObject data)
        {
            var text = TextOfNode(field, EntityPath.Get(data, field.Key));
            shown[field] = text;
            SetControlText(field, text);
        }

        /// <summary>Writes a path field only when its control differs from what was shown: an untouched block keeps the file's own form.</summary>
        private void WritePathField(EntityField field, JsonObject data)
        {
            var text = CurrentText(field);
            if (shown.TryGetValue(field, out var before) && before == text)
                return;
            if (!TryNodeOf(field, text, out var node, out var problem))
            {
                SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/numbers_invalid"), problem), ToolStatusKind.Error);
                return;
            }
            if (node == null)
                EntityPath.Remove(data, field.Key);
            else
                EntityPath.Set(data, field.Key, node);
            shown[field] = text;
        }

        private static string TextOfNode(EntityField field, JsonNode node)
        {
            switch (field.Kind)
            {
                case EntityFieldKind.Integer:
                    return node != null && int.TryParse(node.ToJsonString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                        ? number.ToString(CultureInfo.InvariantCulture)
                        : "0";
                case EntityFieldKind.Numbers:
                    if (node is JsonArray items)
                        return string.Join(", ", items.Select(FormatNumber));
                    return node == null ? string.Empty : FormatNumber(node);
                default:
                    return node?.ToString() ?? string.Empty;
            }
        }

        private static string FormatNumber(JsonNode node)
            => double.TryParse(node?.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : node?.ToString() ?? string.Empty;

        /// <summary>The JSON value of a form text: null for an empty text (the property goes). False, with the offending text, when a number does not read.</summary>
        private static bool TryNodeOf(EntityField field, string text, out JsonNode node, out string problem)
        {
            node = null;
            problem = null;
            if (text.Length == 0)
                return true;
            switch (field.Kind)
            {
                case EntityFieldKind.Integer:
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    {
                        problem = text;
                        return false;
                    }
                    node = JsonValue.Create(number);
                    return true;
                case EntityFieldKind.Numbers:
                    var values = new JsonArray();
                    foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        {
                            problem = text;
                            return false;
                        }
                        values.Add(JsonValue.Create(value));
                    }
                    node = values;
                    return true;
                default:
                    node = JsonValue.Create(text);
                    return true;
            }
        }

        private string CurrentText(EntityField field)
        {
            var control = controls[field];
            switch (field.Kind)
            {
                case EntityFieldKind.Choice:
                    var picker = (OptionButton)control;
                    return picker.Selected >= 0 ? picker.GetItemText(picker.Selected) : string.Empty;
                case EntityFieldKind.Integer:
                    return ((int)((SpinBox)control).Value).ToString(CultureInfo.InvariantCulture);
                default:
                    return ((LineEdit)control).Text.Trim();
            }
        }

        private void SetControlText(EntityField field, string text)
        {
            switch (field.Kind)
            {
                case EntityFieldKind.Choice:
                    FillChoice((OptionButton)controls[field], field, text);
                    break;
                case EntityFieldKind.Integer:
                    ((SpinBox)controls[field]).Value = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
                    break;
                case EntityFieldKind.Picture:
                    ((LineEdit)controls[field]).Text = text;
                    previews[field].Texture = ModResourceService.LoadGroupImage(field.Group, text);
                    break;
                default:
                    ((LineEdit)controls[field]).Text = text;
                    break;
            }
        }

        /// <summary>Makes the fields whose <see cref="EntityField.Locked"/> holds read-only for the entity.</summary>
        private void RefreshLocks(JsonObject data)
        {
            foreach (var field in fields.Where(f => f.Locked != null))
            {
                if (!controls.TryGetValue(field, out var control))
                    continue;
                var locked = data != null && field.Locked(data);
                switch (control)
                {
                    case SpinBox spin:
                        spin.Editable = !locked;
                        break;
                    case LineEdit edit:
                        edit.Editable = !locked;
                        break;
                    case BaseButton button:
                        button.Disabled = locked;
                        break;
                }
            }
        }

        /// <summary>Shows the fields whose <see cref="EntityField.When"/> holds for the entity (none when <paramref name="data"/> is null).</summary>
        private void RefreshVisibility(JsonObject data)
        {
            foreach (var field in fields.Where(f => f.When != null))
            {
                if (blocks.TryGetValue(field, out var block))
                    block.Visible = data != null && field.When(data);
            }
        }

        private static List<string> Checked(HFlowContainer box)
            => box.GetChildren().OfType<CheckBox>().Where(c => c.ButtonPressed).Select(c => c.Text).ToList();

        /// <summary>Takes the children out of the tree at once (a search of the tree must not find them) and frees them.</summary>
        private static void ClearChildren(Node box)
        {
            foreach (var child in box.GetChildren())
            {
                box.RemoveChild(child);
                child.QueueFree();
            }
        }

        private static void SetOrRemove(JsonObject data, string key, string value)
        {
            if (value.Length > 0)
                data[key] = value;
            else
                data.Remove(key);
        }

        /// <summary>The properties no field shows, as an indented JSON object.</summary>
        private string OtherJson(JsonObject data)
        {
            var other = new JsonObject();
            foreach (var pair in WithoutFormPaths(data).Where(p => !IsFormKey(p.Key)))
                other[pair.Key] = pair.Value?.DeepClone();
            return EntityDocument.Serialize(other).TrimEnd();
        }

        /// <summary>
        /// A copy of the entity without the values the dotted-path fields show: a block the form shows
        /// only in part (<c>Interact</c> with its trigger, <c>Parts</c> with its first picture) keeps its
        /// other properties here, under "other fields".
        /// </summary>
        private JsonObject WithoutFormPaths(JsonObject data)
        {
            var rest = (JsonObject)data.DeepClone();
            foreach (var field in fields.Where(f => EntityPath.IsNested(f.Key)))
                EntityPath.Remove(rest, field.Key);
            return rest;
        }

        /// <summary>The values of the dotted-path fields as the form holds them (kept across an edit of "other fields").</summary>
        private JsonObject FormPathValues(JsonObject data)
        {
            var values = new JsonObject();
            foreach (var field in fields.Where(f => EntityPath.IsNested(f.Key)))
            {
                var value = EntityPath.Get(data, field.Key);
                if (value != null)
                    EntityPath.Set(values, field.Key, value.DeepClone());
            }
            return values;
        }

        /// <summary>The form's dotted-path values win over "other fields": each one is set as the form has it, or removed when the form has none.</summary>
        private void ApplyFormPathValues(JsonObject data, JsonObject values)
        {
            foreach (var field in fields.Where(f => EntityPath.IsNested(f.Key)))
            {
                var value = EntityPath.Get(values, field.Key);
                if (value == null)
                    EntityPath.Remove(data, field.Key);
                else
                    EntityPath.Set(data, field.Key, value.DeepClone());
            }
        }

        /// <summary>Whether a top-level property belongs to a whole field (<c>Name</c>, <c>Anchor</c>), the id or a schema editor: those are not "other fields".</summary>
        private bool IsFormKey(string key)
            => key == "Id" || fields.Any(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)) || schemaEditors.Any(e => string.Equals(e.Field.Key, key, StringComparison.OrdinalIgnoreCase));

        private void OnOtherChanged()
        {
            if (suppress || selected == null)
                return;
            JsonObject other;
            try
            {
                other = JsonNode.Parse(otherEdit.Text) as JsonObject;
            }
            catch (JsonException ex)
            {
                SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/other_invalid"), ex.Message), ToolStatusKind.Error);
                return;
            }
            if (other == null)
                return;
            if (!editing)
            {
                document.BeginChange();
                editing = true;
            }
            var data = selected.Data;
            var kept = FormPathValues(data);
            foreach (var key in data.Select(p => p.Key).Where(k => !IsFormKey(k)).ToList())
                data.Remove(key);
            foreach (var pair in other.Where(p => !IsFormKey(p.Key)).ToList())
                data[pair.Key] = pair.Value?.DeepClone();
            ApplyFormPathValues(data, kept);
            if (selected.IsForeign)
                document.CommitForeign(selected);
            SetDirty();
            SetStatus(ToolUi.Tr("tools/entity_editor/other_applied"));
            OnSelectionChanged(selected);
        }

        // ── commands ───────────────────────────────────────────────

        /// <summary>Starts a new entity in the caller's content mod, respecting any unsaved form edits.</summary>
        public void CreateNewInMod(string mod)
        {
            if (!mods.Contains(mod)) return;
            RunDiscardingChanges(() => { DoOpenMod(mod); AddNew(); });
        }

        private void AddNew()
        {
            if (document == null)
                return;
            document.BeginChange();
            var id = document.FreeId(NewIdBase(modId));
            var entity = CreateEntity(id);
            foreach (var field in fields.Where(f => f.Kind == EntityFieldKind.LocaleText && f.KeyPattern != null))
            {
                var key = string.Format(field.KeyPattern, id);
                entity[field.Key] = key;
                foreach (var language in languages)
                    document.SetText(language, key, field.Key == "Name" ? id.Substring(id.LastIndexOf('/') + 1) : string.Empty);
            }
            var record = document.Add(entity);
            SetDirty();
            RefreshList();
            Select(record);
            SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/added"), id, record.File));
        }

        private void DuplicateSelected()
        {
            if (selected == null)
                return;
            document.BeginChange();
            // A foreign entity is copied into the edited mod under a free id: an own entity to change freely.
            var record = selected.IsForeign
                ? document.Add(CopyWithId(selected.Data, document.FreeId(selected.Id)))
                : document.Duplicate(selected, document.FreeId(selected.Id));
            SetDirty();
            RefreshList();
            Select(record);
        }

        private void DeleteSelected()
        {
            if (selected == null)
                return;
            ShowConfirm("tools/entity_editor/delete_title", "tools/entity_editor/delete_text", ConfirmDelete);
        }

        private static JsonObject CopyWithId(JsonObject data, string id)
        {
            var copy = (JsonObject)data.DeepClone();
            EntityJson.Get(copy, "Id", out var key);
            if (key != null)
                copy.Remove(key);
            var result = new JsonObject { ["Id"] = id };
            foreach (var pair in copy.ToList())
            {
                copy.Remove(pair.Key);
                result[pair.Key] = pair.Value;
            }
            return result;
        }

        private void ConfirmDelete()
        {
            if (selected == null)
                return;
            document.BeginChange();
            var id = selected.Id;
            if (selected.IsForeign)
            {
                document.DeleteForeign(selected);
                SetDirty();
                RefreshList();
                Select(null);
                SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/deleted_foreign"), id));
                return;
            }
            document.Remove(selected);
            SetDirty();
            Select(null);
            RefreshList();
            SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/deleted"), id));
        }

        private void Undo() => StepHistory(document != null && document.Undo());

        private void Redo() => StepHistory(document != null && document.Redo());

        private void StepHistory(bool stepped)
        {
            if (!stepped)
                return;
            // The document holds new JSON objects now: find the selection again by id.
            var id = selected?.Id;
            SetDirty();
            RefreshList();
            Select(document.FindAny(id) ?? document.Records.FirstOrDefault());
        }

        /// <summary>"Rename…" of the selected saved entity: the dialog lists the references before anything is written (plan 14.G6).</summary>
        public void Rename()
        {
            if (selected == null || selected.IsForeign || document == null)
                return;
            var oldId = selected.Id;
            var dialog = new EntityRenameDialog(
                oldId,
                newId => EntityRename.Problem(kind, newId, id => document.FindAny(id) != null),
                newId => EntityRename.Plan(kind, modId, oldId, newId),
                newId => RenameTo(oldId, newId));
            AddChild(dialog);
            dialog.PopupCentered();
        }

        /// <summary>Applies the rename (the window saves first) and shows the renamed entity; null on success, else the error's locale key.</summary>
        private string RenameTo(string oldId, string newId)
        {
            var error = rename.Apply(kind, modId, oldId, newId, Save, out var files);
            if (error != null)
            {
                SetStatus(ToolUi.Tr(error), ToolStatusKind.Error);
                return error;
            }
            undoRenameButton.Disabled = false;
            DoOpenMod(modId);
            Select(document.Find(newId));
            SetStatus(string.Format(ToolUi.Tr("tools/rename/done"), oldId, newId, files), ToolStatusKind.Ok);
            return null;
        }

        private void UndoRename()
        {
            var files = rename.Undo();
            undoRenameButton.Disabled = true;
            if (files == 0)
                return;
            DoOpenMod(modId);
            SetStatus(string.Format(ToolUi.Tr("tools/rename/undone"), files), ToolStatusKind.Ok);
        }

        /// <summary>Ctrl+S.</summary>
        protected override void OnSaveShortcut() => Save();

        private void OnSavePressed() => Save();

        /// <summary>
        /// Writes the changed data and locale files and makes the game re-read
        /// them: the entity cache is dropped (factories rebuild from the new
        /// snapshot) and the current language's texts are reloaded.
        /// </summary>
        public bool Save()
        {
            if (document == null)
                return false;
            var problems = document.Validate();
            if (problems.Count > 0)
            {
                SetStatus(string.Format(ToolUi.Tr(problems[0].Key), problems[0].Argument), ToolStatusKind.Error);
                return false;
            }

            var changedFiles = document.ChangedFiles();
            var changedTexts = document.ChangedTexts();
            foreach (var (path, text) in changedFiles)
            {
                if (!ModFileService.WriteText(modId, path, text))
                {
                    SetStatus(ToolUi.Tr("tools/common/readonly"), ToolStatusKind.Error);
                    return false;
                }
            }
            var patch = document.ChangedPatch();
            if (patch != null)
            {
                var (patchPath, patchText) = patch.Value;
                var written = patchText == null ? ModFileService.DeleteFile(modId, patchPath) : ModFileService.WriteText(modId, patchPath, patchText);
                if (!written)
                {
                    SetStatus(ToolUi.Tr("tools/common/readonly"), ToolStatusKind.Error);
                    return false;
                }
                changedFiles.Add((patchPath, patchText));
            }
            foreach (var (language, key, value) in changedTexts)
                LocaleFileService.Write(modId, language, key, value);
            // A reference to another mod's entity needs that mod in depends (plan 14.G16): the novice does not edit the manifest.
            // Pictures, scenarios and maps a record names count too (plan 14.G16): the schema describes the first, ExtraReferencesOf the rest.
            var fieldsOfKind = Cthangover.Mods.Schemas.SchemaCatalog.Current.FieldsOf(kind).ToList();
            var added = ModDependencies.EnsureReferenced(modId, document.Records.Where(r => !r.IsForeign)
                .SelectMany(r => ModDependencies.ReferencesOf(r.Data, fieldsOfKind).Concat(ExtraReferencesOf(r.Data))).ToList());

            document.MarkSaved();
            MarkClean();
            editing = false;
            savedIds.UnionWith(document.Records.Select(r => r.Id));
            ModManager.Instance.GetResolver<PatchResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<EntityResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            icons.Clear();
            if (changedTexts.Count > 0)
                LocaleFileService.ReloadGame();
            // Open windows that list this kind (the map editor's Characters tab) read the new caches at once (plan 14.G17).
            ModEventBus.Publish(new EntitiesSavedEvent { Kind = kind, ModId = modId });
            GameLogger.Log("TOOLS", $"entity editor: saved {changedFiles.Count} {kind} file(s), {changedTexts.Count} text(s) of '{modId}'");
            var savedText = string.Format(ToolUi.Tr("tools/entity_editor/saved"), changedFiles.Count, changedTexts.Count, modId);
            SetStatus(added.Count == 0 ? savedText : savedText + " " + ModDependencies.Notice(added), ToolStatusKind.Ok);
            if (selected != null)
                Select(selected);
            return true;
        }

        /// <summary>Shortcuts: Ctrl+Z / Ctrl+Y undo and redo, Ctrl+N adds, Ctrl+D duplicates.</summary>
        protected override bool HandleKey(InputEventKey key)
        {
            if (!key.CtrlPressed)
                return false;
            switch (key.Keycode)
            {
                case Key.Z when key.ShiftPressed:
                case Key.Y:
                    Redo();
                    return true;
                case Key.Z:
                    Undo();
                    return true;
                case Key.N:
                    AddNew();
                    return true;
                case Key.D:
                    DuplicateSelected();
                    return true;
            }
            return false;
        }

        private static string Str(JsonObject data, string key) => data[key]?.ToString();
    }
}
