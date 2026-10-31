namespace Cthangover.Tools.EntityEditor
{
    using System;
    using System.Text.Json.Nodes;

    /// <summary>
    /// One entity of an <see cref="EntityDocument"/>. An own entity is its JSON object inside the
    /// <c>Items</c> array of a file of the edited mod. A foreign entity (<see cref="IsForeign"/>) is
    /// defined by another mod: <see cref="Data"/> is then the value the game sees with the edited
    /// mod's patch applied, edited by the form like an own entity, and the document turns it back
    /// into the minimal patch (<see cref="EntityDocument.CommitForeign"/>). The owner's file is never
    /// written.
    /// </summary>
    public class EntityRecord
    {
        private readonly Func<EntityRecord, JsonObject> resolve;
        private readonly string foreignId;
        private JsonObject data;

        /// <summary>Mod-relative path of the file holding the entity (for a foreign entity: in its owner mod).</summary>
        public string File { get; }

        /// <summary>
        /// The entity itself; unknown properties are kept as they are. For a foreign entity it is
        /// resolved on first use (the resolver reads the kind's files) and may be null when the
        /// game cannot resolve it.
        /// </summary>
        public JsonObject Data => data ??= resolve?.Invoke(this);

        /// <summary>The entity's <c>Id</c> (empty when missing).</summary>
        public string Id => foreignId ?? Data?["Id"]?.ToString() ?? string.Empty;

        /// <summary>The mod defining a foreign entity; null for an entity of the edited mod.</summary>
        public string OwnerMod { get; }

        /// <summary>True when another mod defines the entity and edits are written as a patch.</summary>
        public bool IsForeign => OwnerMod != null;

        /// <summary>A foreign entity as its owner wrote it (shown in the list without resolving it); for an own entity, <see cref="Data"/>.</summary>
        public JsonObject Written { get; }

        /// <summary>Wraps an entity of a file.</summary>
        public EntityRecord(string file, JsonObject data)
        {
            File = file;
            this.data = data;
            Written = data;
        }

        /// <summary>A foreign entity; <paramref name="resolver"/> builds its <see cref="Data"/> when first asked.</summary>
        public EntityRecord(string id, string ownerMod, string file, JsonObject written, Func<EntityRecord, JsonObject> resolver)
        {
            foreignId = id;
            OwnerMod = ownerMod;
            File = file;
            Written = written;
            resolve = resolver;
        }

        /// <summary>Forgets the resolved value of a foreign entity (undo changed its patch); a no-op for own ones.</summary>
        public void Invalidate()
        {
            if (IsForeign)
                data = null;
        }
    }
}
