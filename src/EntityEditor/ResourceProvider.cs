namespace Cthangover.Tools.EntityEditor
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Which mod serves a file of a group by its id, the way the game resolves it: a picture, a scenario or a map is named
    /// without its extension, and the mod that wins the group (the last one in load order) is the provider (plan 14.G16).
    /// The lookup is pure, so the editors and the tests share one rule; the caller passes the group's files.
    /// </summary>
    public static class ResourceProvider
    {
        /// <summary>
        /// The mod of the file whose path without extension is <paramref name="id"/> (case-insensitive, a <c>group/</c> prefix
        /// on the id is allowed, as data files often write it); null when no file of the group has that id.
        /// </summary>
        public static string Of(IEnumerable<(string Path, string ModId)> files, string group, string id)
        {
            if (string.IsNullOrWhiteSpace(id) || files == null)
                return null;
            var path = !string.IsNullOrEmpty(group) && id.StartsWith(group + "/", StringComparison.OrdinalIgnoreCase)
                ? id.Substring(group.Length + 1)
                : id;
            foreach (var (filePath, modId) in files)
            {
                if (string.Equals(WithoutExtension(filePath), path, StringComparison.OrdinalIgnoreCase))
                    return modId;
            }
            return null;
        }

        /// <summary>The path without its last extension (<c>food/ration.png</c> → <c>food/ration</c>).</summary>
        public static string WithoutExtension(string path)
        {
            var slash = path.LastIndexOf('/');
            var dot = path.LastIndexOf('.');
            return dot > slash ? path.Substring(0, dot) : path;
        }
    }
}
