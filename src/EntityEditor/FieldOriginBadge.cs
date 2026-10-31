namespace Cthangover.Tools.EntityEditor
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Tools.Common;
    using Godot;

    /// <summary>
    /// The line under a form field of an <see cref="EntityEditorWindow"/> that says where the
    /// field's value comes from when that is not simply "this entity's file": the edited mod's
    /// patch, another mod's patch (a warning when it hides the edited mod's own value), an
    /// <c>Extends</c> parent. Its "↺" button resets the field to the inherited value. Hidden when
    /// there is nothing to say.
    /// </summary>
    public partial class FieldOriginBadge : MarginContainer
    {
        private readonly Label label;
        private readonly Button reset;

        /// <summary>The top-level property the badge describes.</summary>
        public string Key { get; }

        /// <summary>The text shown now (empty when hidden).</summary>
        public string Text => Visible ? label.Text : string.Empty;

        /// <summary>True when the reset button is offered now.</summary>
        public bool CanReset => Visible && reset.Visible;

        /// <summary>Builds a hidden badge for <paramref name="key"/>; <paramref name="onReset"/> gets the key.</summary>
        public FieldOriginBadge(string key, Action<string> onReset)
        {
            Key = key;
            Visible = false;
            AddThemeConstantOverride("margin_left", ToolUi.LabelWidth + UiTokens.Space3);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", UiTokens.Space2);
            AddChild(row);
            label = ToolUi.Hint(string.Empty);
            row.AddChild(label);
            reset = UiKit.IconButton("↺", ToolUi.Tr("tools/entity_editor/reset_field_tip"));
            reset.Pressed += () => onReset(Key);
            row.AddChild(reset);
        }

        /// <summary>Shows <paramref name="text"/> (null or empty hides the badge); a warning is coloured.</summary>
        public void Show(string text, bool warning, bool canReset)
        {
            Visible = !string.IsNullOrEmpty(text) || canReset;
            label.Text = text ?? string.Empty;
            label.AddThemeColorOverride("font_color", warning ? UiTokens.Warning : UiTokens.Info);
            reset.Visible = canReset;
        }
    }
}
