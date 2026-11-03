namespace Cthangover.Tools.EntityEditor
{
    using Cthangover.Core.Events;

    /// <summary>
    /// Published on the mod event bus by <see cref="EntityEditorWindow"/> after a Save has written the files and
    /// dropped the entity caches, so a window that lists entities of <see cref="Kind"/> rebuilds its list at once.
    /// This is how an entity made in one editor reaches an open editor of another kind: the map editor's Characters
    /// tab lists a character saved in the characters editor without the map editor being reopened (plan 14.G17).
    /// </summary>
    public class EntitiesSavedEvent : ModEvent
    {
        /// <summary>The entity kind of the editor that saved, as it names it (<c>character</c>, <c>item</c>).</summary>
        public string Kind { get; init; }

        /// <summary>The mod whose files were written.</summary>
        public string ModId { get; init; }
    }
}
