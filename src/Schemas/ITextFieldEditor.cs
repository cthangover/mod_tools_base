namespace Cthangover.Tools.Schemas
{
    using System.Collections.Generic;
    using Cthangover.Tools.EntityEditor;

    /// <summary>
    /// A field editor whose texts live in the mod's locale files: it is given the window's document and the mod's languages
    /// when a mod is opened, and the id of the shown entity before each load (a new text key is made from it).
    /// </summary>
    public interface ITextFieldEditor : ISchemaFieldEditor
    {
        /// <summary>Gives the document the texts go into, the mod and its languages (texts are written by the window's Save).</summary>
        void Open(EntityDocument document, string mod, IEnumerable<string> languages);

        /// <summary>The shown entity's id (null or empty: none); called before the entity is loaded.</summary>
        void SetEntityId(string id);
    }
}
