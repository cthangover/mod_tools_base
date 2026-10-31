namespace Cthangover.Tools.EntityEditor
{
    using System;
    using System.Linq;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Schemas;
    using Cthangover.Tools.Common;
    using Godot;

    /// <summary>
    /// The rename dialog of an entity: a new id, then "Show changes" lists the files that will change (the edited mod) and
    /// the files of other mods that name the id (listed, not changed). Apply is enabled after a preview with no problem.
    /// </summary>
    public partial class EntityRenameDialog : AcceptDialog
    {
        private readonly Func<string, string> problemOf;
        private readonly Func<string, ReferencePlan> planOf;
        private readonly Func<string, string> apply;
        private readonly LineEdit newEdit;
        private readonly Label problem;
        private readonly VBoxContainer list;
        private bool ready;

        /// <summary>
        /// Builds the dialog for <paramref name="oldId"/>. <paramref name="problemOf"/> gives the locale key of why a new id is refused (or null),
        /// <paramref name="planOf"/> the references of a new id, <paramref name="apply"/> renames (null on success, else the error's locale key).
        /// </summary>
        public EntityRenameDialog(string oldId, Func<string, string> problemOf, Func<string, ReferencePlan> planOf, Func<string, string> apply)
        {
            this.problemOf = problemOf;
            this.planOf = planOf;
            this.apply = apply;
            Theme = ToolTheme.Get();
            Title = string.Format(ToolUi.Tr("tools/rename/title"), oldId);
            MinSize = new Vector2I(640, 540);
            var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            newEdit = ToolUi.Field(box, "tools/rename/new_id", ToolUi.Text(string.Empty, OnTyped), "tools/rename/new_id_tip");
            newEdit.Name = "NewId";
            newEdit.Text = oldId;
            newEdit.AddThemeFontOverride("font", ToolUi.Mono());
            box.AddChild(ToolUi.Button("tools/rename/preview", ShowPreview, ButtonKind.Secondary, "tools/rename/preview_tip"));
            problem = ToolUi.Hint(string.Empty);
            problem.Name = "Problem";
            box.AddChild(problem);
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 340) };
            list = new VBoxContainer { Name = "Changes", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            scroll.AddChild(list);
            box.AddChild(scroll);
            AddChild(box);
            GetOkButton().Text = ToolUi.Tr("tools/rename/apply");
            GetOkButton().Disabled = true;
            Confirmed += OnConfirmed;
            CloseRequested += QueueFree;
            Canceled += QueueFree;
        }

        /// <summary>The new id typed (trimmed).</summary>
        public string NewId => newEdit.Text.Trim();

        /// <summary>Lists what a rename to the typed id changes; enables Apply when there is no problem.</summary>
        public void ShowPreview()
        {
            ClearList();
            ready = false;
            GetOkButton().Disabled = true;
            var key = problemOf(NewId);
            if (key != null)
            {
                problem.Text = ToolUi.Tr(key);
                return;
            }
            problem.Text = string.Empty;
            var plan = planOf(NewId);
            if (plan.Own.Count == 0 && plan.Other.Count == 0)
                list.AddChild(Line(ToolUi.Tr("tools/rename/none")));
            if (plan.Own.Count > 0)
            {
                var places = plan.Own.Sum(h => h.Count);
                list.AddChild(Line(string.Format(ToolUi.Tr("tools/rename/own_title"), plan.Own.Count, places)));
                foreach (var hit in plan.Own)
                    list.AddChild(Line(string.Format(ToolUi.Tr("tools/rename/own_file"), hit.Path, hit.Count, string.Join(", ", hit.Places.Distinct().Take(4)))));
            }
            if (plan.Other.Count > 0)
            {
                list.AddChild(Line(ToolUi.Tr("tools/rename/other_title")));
                foreach (var hit in plan.Other)
                    list.AddChild(Line(string.Format(ToolUi.Tr("tools/rename/other_file"), hit.Mod, hit.Path, hit.Count)));
            }
            ready = true;
            GetOkButton().Disabled = false;
        }

        private void OnTyped(string text)
        {
            if (!ready)
                return;
            ready = false;
            GetOkButton().Disabled = true;
            ClearList();
            problem.Text = ToolUi.Tr("tools/rename/preview_again");
        }

        private void OnConfirmed()
        {
            if (!ready)
            {
                ShowPreview();
                return;
            }
            var error = apply(NewId);
            if (error != null)
            {
                problem.Text = ToolUi.Tr(error);
                ready = false;
                GetOkButton().Disabled = true;
                return;
            }
            QueueFree();
        }

        private void ClearList()
        {
            foreach (var child in list.GetChildren())
            {
                list.RemoveChild(child);
                child.QueueFree();
            }
        }

        private static Label Line(string text)
        {
            var label = ToolUi.Hint(text);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            return label;
        }
    }
}
