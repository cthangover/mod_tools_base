namespace Cthangover.Tools.Schemas
{
    using Cthangover.Mods.Schemas;
    using SchemaFieldDefinition = Cthangover.Mods.Schemas.FieldSchema;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Tools.Common;
    using Cthangover.Tools.EntityEditor;
    using Cthangover.Tools.Services;
    using Godot;

    /// <summary>
    /// Edits a <c>stages</c> field: the numbered steps of a quest, a JSON object whose keys are step numbers and whose values
    /// are locale keys (<c>{"0": "mod/quest/x/0", "1": "…"}</c>). One block per step: its number, its key and the step's text
    /// in every language of the mod, written with the window's Save. A new step gets the next number and a key made from the
    /// field's <c>KeyPattern</c> (<c>{mod}</c>, <c>{id}</c> = the entity's last id segment, <c>{n}</c> = the step number).
    /// </summary>
    public class StageFieldEditor : ITextFieldEditor
    {
        private readonly SchemaFieldDefinition field;
        private readonly Action changed;
        private readonly VBoxContainer lines;
        private readonly List<Row> rows = new();
        private EntityDocument document;
        private string modId;
        private string entityId = string.Empty;
        private List<string> languages = new();
        private bool suppress;

        private sealed class Row
        {
            public VBoxContainer Box;
            public SpinBox Number;
            public LineEdit Key;
            public Dictionary<string, LineEdit> Texts = new();
        }

        /// <inheritdoc />
        public SchemaFieldDefinition Field => field;

        /// <summary>Adds the editor for <paramref name="field"/> to <paramref name="parent"/>.</summary>
        public StageFieldEditor(Container parent, SchemaFieldDefinition field, Action changed)
        {
            this.field = field;
            this.changed = changed;
            var box = new VBoxContainer { Name = "Stages_" + field.Key };
            lines = new VBoxContainer();
            lines.AddThemeConstantOverride("separation", UiTokens.Space2);
            box.AddChild(lines);
            var add = ToolUi.Button("tools/schema/map_add", AddStep, ButtonKind.Secondary);
            add.Name = "Add";
            add.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            box.AddChild(add);
            ToolUi.Field(parent, string.IsNullOrEmpty(field.Name) ? field.Key : field.Name, box, field.Description);
        }

        /// <inheritdoc />
        public void Open(EntityDocument doc, string mod, IEnumerable<string> languageCodes)
        {
            document = doc;
            modId = mod;
            languages = languageCodes.ToList();
            Clear();
        }

        /// <inheritdoc />
        public void SetEntityId(string id) => entityId = id ?? string.Empty;

        /// <summary>Shows the steps <paramref name="node"/> holds, in number order.</summary>
        public void Load(JsonNode node)
        {
            suppress = true;
            Clear();
            if (node is JsonObject obj)
            {
                var steps = obj.Select(p => (Number: ParseNumber(p.Key), Key: p.Value?.ToString()))
                    .Where(s => s.Number.HasValue).OrderBy(s => s.Number.Value);
                foreach (var step in steps)
                    AddRow(step.Number.Value, step.Key ?? string.Empty);
            }
            suppress = false;
        }

        /// <summary>The steps as a JSON object (null when there are none); the texts go into the document.</summary>
        public JsonNode Save()
        {
            var obj = new JsonObject();
            foreach (var row in rows.OrderBy(r => (int)r.Number.Value))
            {
                var key = row.Key.Text.Trim();
                if (key.Length == 0)
                    continue;
                obj[((int)row.Number.Value).ToString(CultureInfo.InvariantCulture)] = key;
                foreach (var pair in row.Texts)
                    document?.SetText(pair.Key, key, pair.Value.Text);
            }
            return obj.Count == 0 ? null : obj;
        }

        private void Clear()
        {
            foreach (var row in rows)
            {
                lines.RemoveChild(row.Box);
                row.Box.QueueFree();
            }
            rows.Clear();
        }

        private void AddStep()
        {
            var number = rows.Count == 0 ? 0 : rows.Max(r => (int)r.Number.Value) + 1;
            AddRow(number, KeyFor(number));
            changed?.Invoke();
        }

        private void AddRow(int number, string key)
        {
            var box = new VBoxContainer { Name = "Step" + number.ToString(CultureInfo.InvariantCulture) };
            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", UiTokens.Space2);
            head.AddChild(new Label { Text = ToolUi.Tr("tools/schema/stage_number"), CustomMinimumSize = new Vector2(ToolUi.LabelWidth, 0), HorizontalAlignment = HorizontalAlignment.Right });
            var row = new Row { Box = box };
            row.Number = ToolUi.Number(0, 999, 1, _ => OnTyped());
            row.Number.Value = number;
            head.AddChild(row.Number);
            row.Key = ToolUi.Text(string.Empty, _ => OnTyped());
            row.Key.Text = key;
            row.Key.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.Key.AddThemeFontOverride("font", ToolUi.Mono());
            head.AddChild(row.Key);
            head.AddChild(ToolUi.Button("tools/schema/list_remove", () => Remove(row), ButtonKind.Secondary));
            box.AddChild(head);
            foreach (var language in languages)
            {
                var line = new HBoxContainer();
                line.AddThemeConstantOverride("separation", UiTokens.Space3);
                line.AddChild(new Label { Text = string.Format(ToolUi.Tr("tools/entity_editor/text_in"), language), CustomMinimumSize = new Vector2(ToolUi.LabelWidth, 0), HorizontalAlignment = HorizontalAlignment.Right });
                var edit = ToolUi.Text(string.Empty, _ => OnTyped());
                edit.Name = "StageText_" + number.ToString(CultureInfo.InvariantCulture) + "_" + language;
                edit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                edit.Text = TextOf(language, key);
                line.AddChild(edit);
                box.AddChild(line);
                row.Texts[language] = edit;
            }
            rows.Add(row);
            lines.AddChild(box);
        }

        private void Remove(Row row)
        {
            rows.Remove(row);
            lines.RemoveChild(row.Box);
            row.Box.QueueFree();
            changed?.Invoke();
        }

        private string KeyFor(int number)
        {
            var id = entityId.Length == 0 ? "new" : entityId.Substring(entityId.LastIndexOf('/') + 1);
            var pattern = string.IsNullOrEmpty(field.KeyPattern) ? "{mod}/{id}/{n}" : field.KeyPattern;
            return pattern.Replace("{mod}", modId ?? string.Empty).Replace("{id}", id)
                .Replace("{n}", number.ToString(CultureInfo.InvariantCulture));
        }

        private string TextOf(string language, string key)
        {
            if (string.IsNullOrEmpty(key) || document == null)
                return string.Empty;
            var text = document.GetText(language, key);
            if (text != null)
                return text;
            text = LocaleFileService.Read(modId, language, key) ?? string.Empty;
            document.LoadText(language, key, text);
            return text;
        }

        private static int? ParseNumber(string key)
            => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

        private void OnTyped()
        {
            if (!suppress)
                changed?.Invoke();
        }
    }
}
