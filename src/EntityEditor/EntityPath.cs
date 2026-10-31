namespace Cthangover.Tools.EntityEditor
{
    using System;
    using System.Globalization;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Dotted paths into an entity's JSON (<c>Surface.Image</c>, <c>Parts.0.Crop</c>), the keys of
    /// <see cref="EntityField"/>s that sit inside a block. Names match case-insensitively like
    /// <see cref="EntityJson.Get"/>; a numeric segment indexes an array. A write creates the blocks
    /// it needs, a removal takes away the blocks it leaves empty.
    /// </summary>
    public static class EntityPath
    {
        /// <summary>The first name of a path (the block a field belongs to), or the key itself.</summary>
        public static string Top(string path)
        {
            if (path == null)
                return null;
            var dot = path.IndexOf('.');
            return dot < 0 ? path : path.Substring(0, dot);
        }

        /// <summary>True when the path has more than one segment.</summary>
        public static bool IsNested(string path) => path != null && path.IndexOf('.') >= 0;

        /// <summary>The node at <paramref name="path"/>, or null.</summary>
        public static JsonNode Get(JsonNode root, string path)
        {
            var node = root;
            foreach (var segment in Segments(path))
            {
                node = Child(node, segment, out _);
                if (node == null)
                    return null;
            }
            return node;
        }

        /// <summary>Sets the value at <paramref name="path"/>, creating the blocks on the way. <paramref name="value"/> must not belong to another tree.</summary>
        public static void Set(JsonObject root, string path, JsonNode value)
        {
            var segments = Segments(path);
            if (segments.Length == 0)
                throw new ArgumentException("empty path", nameof(path));
            JsonNode node = root;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var last = i == segments.Length - 1;
                if (node is JsonObject obj)
                {
                    Child(obj, segment, out var key);
                    key ??= segment;
                    if (last)
                    {
                        obj[key] = value;
                        return;
                    }
                    if (Child(obj, segment, out _) == null)
                        obj[key] = NewBlock(segments[i + 1]);
                    node = obj[key];
                }
                else if (node is JsonArray array && IsIndex(segment, out var index))
                {
                    while (array.Count <= index)
                        array.Add(NewBlock(last ? null : segments[i + 1]));
                    if (last)
                    {
                        array[index] = value;
                        return;
                    }
                    array[index] ??= NewBlock(segments[i + 1]);
                    node = array[index];
                }
                else
                {
                    throw new InvalidOperationException($"path '{path}' does not fit the entity at '{segment}'");
                }
            }
        }

        /// <summary>Removes the value at <paramref name="path"/> and the blocks it leaves empty; false when nothing was there.</summary>
        public static bool Remove(JsonObject root, string path)
            => RemoveFrom(root, Segments(path), 0);

        private static bool RemoveFrom(JsonNode node, string[] segments, int at)
        {
            var child = Child(node, segments[at], out var key);
            if (child == null)
                return false;
            key ??= segments[at];
            if (at == segments.Length - 1)
            {
                Detach(node, key);
                return true;
            }
            if (!RemoveFrom(child, segments, at + 1))
                return false;
            if (IsEmpty(child))
                Detach(node, key);
            return true;
        }

        private static JsonNode Child(JsonNode parent, string segment, out string key)
        {
            key = null;
            if (parent is JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    if (string.Equals(pair.Key, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        key = pair.Key;
                        return pair.Value;
                    }
                }
                return null;
            }
            if (parent is JsonArray array && IsIndex(segment, out var index) && index < array.Count)
            {
                key = segment;
                return array[index];
            }
            return null;
        }

        private static void Detach(JsonNode parent, string key)
        {
            if (parent is JsonObject obj)
                obj.Remove(key);
            else if (parent is JsonArray array && IsIndex(key, out var index) && index < array.Count)
                array.RemoveAt(index);
        }

        private static bool IsEmpty(JsonNode node)
            => node is JsonObject obj ? obj.Count == 0 : node is JsonArray array && array.Count == 0;

        private static JsonNode NewBlock(string nextSegment)
            => nextSegment != null && IsIndex(nextSegment, out _) ? new JsonArray() : new JsonObject();

        private static bool IsIndex(string segment, out int index)
            => int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0;

        private static string[] Segments(string path)
            => (path ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries);
    }
}
