namespace Cthangover.Tools.EntityEditor
{
    using System;
    using Cthangover.Mods.Interface.Base.Lists;
    using Godot;

    /// <summary>Owns the utility form tree and its minimum size while the window retains document behavior and public API.</summary>
    public sealed partial class EntityEditorContent : ModWidget
    {
        private Action<EntityEditorContent> build;
        private Control body;
        internal EntityEditorContent(Action<EntityEditorContent> build)
        {
            this.build = build;
            Name = "EntityEditorContent";
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
        }

        protected override void Construct()
        {
            build(this);
            build = null;
        }

        /// <summary>
        /// Creates detached utility nodes during form construction; the caller transfers each control or popup to its owner
        /// immediately.
        /// </summary>
        public T Create<T>()
            where T : Node, new()
        {
            return new T();
        }

        internal void Mount(Control value)
        {
            body = value;
            AddChild(body);
            body.MinimumSizeChanged += UpdateMinimum;
            UpdateMinimum();
        }

        private void UpdateMinimum()
        {
            var size = body.GetCombinedMinimumSize();
            if (CustomMinimumSize != size)
            {
                CustomMinimumSize = size;
            }
        }

        protected override void Destruct()
        {
            body.MinimumSizeChanged -= UpdateMinimum;
            base.Destruct();
        }
    }
}
