namespace Cthangover.Tools.EntityEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Tools.Common;

    /// <summary>
    /// What a field badge says about one top-level property of the selected entity: the edited mod's
    /// own patch, another mod's patch (with a warning when it hides the edited mod's own value), an
    /// <c>Extends</c> parent. Shared by the entity editors and the Data Editor, so both windows tell
    /// the same story about the same value (<see cref="EntityExplanation"/> is the source).
    /// </summary>
    public static class FieldOrigins
    {
        /// <summary>
        /// The badge text for <paramref name="key"/> (null when there is nothing to say). Patches always
        /// apply after definitions, so another mod's patch on an own entity hides the own value; on a
        /// foreign entity the own patch is hidden only by a patch of a mod that is not a dependency (it
        /// may load later). Wildcard patches apply before addressed ones and never hide them.
        /// </summary>
        public static string Describe(EntityRecord selected, string editedMod, EntityDocument document, EntityExplanation explanation,
            ISet<string> dependencies, string key, out bool warning, out bool canReset)
        {
            warning = false;
            canReset = false;
            var origins = explanation?.Fields?.Values.Where(o => PathUnder(o.Path, key)).ToList() ?? new List<EntityFieldOrigin>();
            var other = origins.FirstOrDefault(o => o.Layer == EntityLayer.Patch && !string.Equals(o.ModId, editedMod, StringComparison.OrdinalIgnoreCase));
            var inherited = origins.FirstOrDefault(o => o.Layer == EntityLayer.Definition && !string.Equals(o.EntityId, selected.Id, StringComparison.OrdinalIgnoreCase));
            if (selected.IsForeign)
            {
                var mine = PatchTouches(document.PatchOf(selected.Id), key);
                canReset = mine;
                if (mine && other != null && other.EntityId != "*" && !dependencies.Contains(other.ModId))
                {
                    warning = true;
                    return string.Format(ToolUi.Tr("tools/entity_editor/badge_own_overridden"), other.ModId, GameValue(explanation, key));
                }
                if (mine)
                    return ToolUi.Tr("tools/entity_editor/badge_own_patch");
                if (other != null)
                    return string.Format(ToolUi.Tr("tools/entity_editor/badge_patched_by"), other.ModId);
                return inherited == null ? null : string.Format(ToolUi.Tr("tools/entity_editor/badge_inherited"), inherited.EntityId, inherited.ModId);
            }

            var has = EntityJson.Get(selected.Data, key, out _) != null;
            canReset = has && EntityExtendsResolution<object>.ExtendsOf(selected.Data) != null;
            if (other != null)
            {
                warning = has;
                return has
                    ? string.Format(ToolUi.Tr("tools/entity_editor/badge_overridden"), other.ModId, GameValue(explanation, key))
                    : string.Format(ToolUi.Tr("tools/entity_editor/badge_patched_by"), other.ModId);
            }
            return inherited != null && !has ? string.Format(ToolUi.Tr("tools/entity_editor/badge_inherited"), inherited.EntityId, inherited.ModId) : null;
        }

        /// <summary>True when <paramref name="path"/> is <paramref name="key"/> or a part of it (<c>Key.sub</c>, <c>Key[0]</c>).</summary>
        public static bool PathUnder(string path, string key)
            => path != null && path.StartsWith(key, StringComparison.OrdinalIgnoreCase)
               && (path.Length == key.Length || path[key.Length] == '.' || path[key.Length] == '[');

        /// <summary>True when a patch entry sets, removes or operates on <paramref name="key"/> (<c>Key</c>, <c>Key$add</c>…).</summary>
        public static bool PatchTouches(JsonObject entry, string key)
            => entry != null && entry.Any(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)
                                               || p.Key.StartsWith(key + "$", StringComparison.OrdinalIgnoreCase));

        /// <summary>The value the game sees for a property, as short JSON.</summary>
        public static string GameValue(EntityExplanation explanation, string key)
        {
            var value = EntityJson.Get(explanation?.Value, key, out _);
            var text = value == null ? "null" : value.ToJsonString();
            return text.Length > 48 ? text.Substring(0, 45) + "…" : text;
        }
    }
}
