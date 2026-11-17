namespace Cthangover.Tools.EntityEditor
{
    using Cthangover.Mods.Schemas;

    /// <summary>Keeps the source id stable while the dialog reads the window's current document and destination mod.</summary>
    internal sealed class EntityRenameRequest
    {
        private readonly EntityEditorWindow owner;
        private readonly string oldId;
        internal EntityRenameRequest(EntityEditorWindow owner, string oldId)
        {
            this.owner = owner;
            this.oldId = oldId;
        }

        internal ReferencePlan Plan(string newId)
        {
            return owner.RenamePlan(oldId, newId);
        }

        internal string Apply(string newId)
        {
            return owner.RenameTo(oldId, newId);
        }
    }
}
