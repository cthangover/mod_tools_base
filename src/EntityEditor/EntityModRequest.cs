namespace Cthangover.Tools.EntityEditor
{
    /// <summary>Preserves the requested mod until the existing discard-confirmation flow invokes the action.</summary>
    internal sealed class EntityModRequest
    {
        private readonly EntityEditorWindow owner;
        private readonly string mod;
        private readonly bool create;
        internal EntityModRequest(EntityEditorWindow owner, string mod, bool create)
        {
            this.owner = owner;
            this.mod = mod;
            this.create = create;
        }

        internal void Open()
        {
            owner.OpenRequestedMod(mod, create);
        }
    }
}
