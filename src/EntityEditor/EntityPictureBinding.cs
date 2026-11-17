namespace Cthangover.Tools.EntityEditor
{
    /// <summary>Keeps each picker bound to its own field even while several dialogs are open.</summary>
    internal sealed class EntityPictureBinding
    {
        private readonly EntityEditorWindow owner;
        private readonly EntityField field;
        internal EntityPictureBinding(EntityEditorWindow owner, EntityField field)
        {
            this.owner = owner;
            this.field = field;
        }

        internal void Pick()
        {
            owner.OpenPicker(field);
        }

        internal void SetPath(string id)
        {
            owner.SetPickedPicture(field, id);
        }
    }
}
