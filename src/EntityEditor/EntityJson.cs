namespace Cthangover.Tools.EntityEditor
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Patches;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The JSON steps a data editor needs to write another mod's entity as a patch: the value the
    /// game sees (<see cref="Normalize"/>) and the value the game would see without the editor's own
    /// patch (<see cref="Replay"/>), which is the base of the minimal patch
    /// (<see cref="JsonPatchDiff"/>). Both go through the same merge engine and patch step as the
    /// entity resolver, so the editor never guesses what the game does.
    /// </summary>
    public static class EntityJson
    {
        /// <summary>
        /// The JSON the game reads: <paramref name="node"/> read as <paramref name="type"/> and written
        /// back, so defaults are filled in and values have the DTO's spelling (a flags enum typed as
        /// <c>"Food,Resource"</c> and read from a file as <c>"Food, Resource"</c> compare equal).
        /// A copy of the node when <paramref name="type"/> is null; null when it does not read.
        /// </summary>
        public static JsonObject Normalize(JsonObject node, Type type)
        {
            if (node == null)
                return null;
            if (type == null)
                return (JsonObject)node.DeepClone();
            try
            {
                var options = JsonOptionsProvider.ReadOptions;
                var read = JsonSerializer.Deserialize(node.ToJsonString(), type, options);
                return read == null ? null : JsonPatchMerger.ParseObject(JsonSerializer.Serialize(read, type, options));
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is InvalidOperationException || ex is FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves one entity from its layers the way the resolver does: the definition as written
        /// (<paramref name="written"/>) over its <c>Extends</c> parent's resolved value
        /// (<paramref name="parent"/>, null without a parent), then <paramref name="patches"/> in order;
        /// a patch that fails is skipped, as in the game. Null when a patch deletes the entity or the
        /// definition does not read as <paramref name="type"/>. An editor passes every patch except its
        /// own to get the base its own patch is the difference from.
        /// </summary>
        public static JsonObject Replay(JsonObject written, JsonObject parent, IEnumerable<PropertyPatch> patches, string id, Type type)
        {
            if (written == null)
                return null;
            JsonObject node;
            if (parent != null)
            {
                var merged = (JsonObject)parent.DeepClone();
                try
                {
                    JsonPatchMerger.Apply(merged, (JsonObject)written.DeepClone(), type);
                }
                catch (JsonPatchException)
                {
                    return null;
                }
                node = Normalize(merged, type);
            }
            else
            {
                node = Normalize(written, type);
            }
            if (node == null)
                return null;

            foreach (var patch in patches ?? Array.Empty<PropertyPatch>())
            {
                switch (PatchResolver.ApplyOne(node, patch, id, type, false, out var after, out _))
                {
                    case PatchStep.Deleted:
                        return null;
                    case PatchStep.Applied:
                        node = after;
                        break;
                }
            }
            return Normalize(node, type) ?? node;
        }

        /// <summary>The property named <paramref name="key"/> (case-insensitive) or null; <paramref name="found"/> is its spelling.</summary>
        public static JsonNode Get(JsonObject data, string key, out string found)
        {
            found = null;
            if (data == null || key == null)
                return null;
            foreach (var pair in data)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    found = pair.Key;
                    return pair.Value;
                }
            }
            return null;
        }
    }
}
