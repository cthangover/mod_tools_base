using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Cthangover.Tools.EntityEditor
{
    /// <summary>
    /// One property of an entity as an <see cref="EntityEditorWindow"/> form shows
    /// it. A concrete editor lists its fields; properties no field names are
    /// shown and edited as raw JSON ("other fields"), so the form never hides or
    /// drops data a mod added.
    /// </summary>
    public class EntityField
    {
        /// <summary>
        /// JSON property name, or a dotted path into the entity (<c>Surface.Image</c>,
        /// <c>Parts.0.Crop</c>; a number indexes an array). A path field is written only when its
        /// value was edited, and an empty value removes the property and the blocks left empty
        /// above it, so a form never creates <c>Interact</c> on an object that has none.
        /// </summary>
        public string Key { get; init; }

        /// <summary>Locale key of the label.</summary>
        public string LabelKey { get; init; }

        /// <summary>Locale key of the tooltip, or null.</summary>
        public string TooltipKey { get; init; }

        /// <summary>How the value is edited.</summary>
        public EntityFieldKind Kind { get; init; }

        /// <summary>Picture: the file group of the pictures (<c>items</c>, <c>characters</c>…).</summary>
        public string Group { get; init; }

        /// <summary>Choice, flags, list: the values offered (read when the form is built).</summary>
        public Func<IEnumerable<string>> Options { get; init; }

        /// <summary>Locale text: how a new entity's key is made from its id (<c>{0}</c> = id).</summary>
        public string KeyPattern { get; init; }

        /// <summary>
        /// Shown only when this is true for the entity's JSON (the blocks of one form, e.g. the
        /// surface fields of a <c>surface</c> object). A hidden field is not written. Null = always shown.
        /// </summary>
        public Func<JsonObject, bool> When { get; init; }

        /// <summary>
        /// Shown but not editable for the entity's JSON when this is true: a value the game computes from other
        /// data (a character's health when its <c>Stats</c> bind it). A locked field is still written back as
        /// shown, so the file keeps its own value. Null = always editable.
        /// </summary>
        public Func<JsonObject, bool> Locked { get; init; }
    }
}
