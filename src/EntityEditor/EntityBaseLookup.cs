namespace Cthangover.Tools.EntityEditor
{
    using System.Text.Json.Nodes;

    /// <summary>Retains the foreign-layer lookup's original mod and excluded patch while resolution remains lazy.</summary>
    internal sealed class EntityBaseLookup
    {
        private readonly string kind;
        private readonly string mod;
        private readonly string patch;
        internal EntityBaseLookup(string kind, string mod, string patch)
        {
            this.kind = kind;
            this.mod = mod;
            this.patch = patch;
        }

        internal JsonObject Resolve(string id)
        {
            return ForeignEntities.BaseWithout(kind, id, mod, patch);
        }
    }
}
