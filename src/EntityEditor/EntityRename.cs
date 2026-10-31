namespace Cthangover.Tools.EntityEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Mods.Schemas;
    using Cthangover.Tools.Services;

    /// <summary>
    /// Renames the id of an own entity and fixes the references to it in the edited mod (plan 14.G6). The plan
    /// lists the files of the edited mod that change and the files of other mods that name the id; those are only
    /// listed, never changed. Apply saves the window's pending edits first, then writes the changed files; the last
    /// rename can be undone, which writes back the files' texts as they were before it.
    /// </summary>
    public sealed class EntityRename
    {
        private static readonly Regex ValidId = new("^[A-Za-z0-9_\\-]+(/[A-Za-z0-9_\\-]+)*$", RegexOptions.CultureInvariant);

        private List<(string Path, string Text)> undoFiles;
        private string undoMod;

        /// <summary>True after a rename that <see cref="Undo"/> can take back.</summary>
        public bool CanUndo => undoFiles != null;

        /// <summary>Whether <paramref name="id"/> has the shape of an id (letters, digits, <c>_</c>, <c>-</c>, and <c>/</c> between parts).</summary>
        public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && ValidId.IsMatch(id);

        /// <summary>The locale key of why <paramref name="newId"/> cannot be used, or null when it can.</summary>
        public static string Problem(string kind, string newId, Func<string, bool> takenInWindow)
        {
            if (!IsValidId(newId))
                return "tools/rename/bad_id";
            GameEntityIndex.Refresh();
            if (GameEntityIndex.Exists(kind, newId) || takenInWindow(newId))
                return "tools/rename/taken";
            return null;
        }

        /// <summary>The references to <paramref name="oldId"/> as <paramref name="newId"/> would have them.</summary>
        public static ReferencePlan Plan(string kind, string mod, string oldId, string newId)
            => SchemaReferenceScanner.Plan(SchemaCatalog.Current, mod, GameEntityIndex.ReferenceFiles(), kind, oldId, newId);

        /// <summary>
        /// Renames: saves the window first (<paramref name="saveFirst"/>, false stops), plans again on the saved files, writes the
        /// changed files of the edited mod. Returns null on success, else a locale key of the error (the status line shows it).
        /// </summary>
        public string Apply(string kind, string mod, string oldId, string newId, Func<bool> saveFirst, out int filesChanged)
        {
            filesChanged = 0;
            if (!saveFirst())
                return "tools/rename/save_first";
            var plan = Plan(kind, mod, oldId, newId);
            var written = new List<(string Path, string Text)>();
            foreach (var hit in plan.Own)
            {
                var before = ModFileService.ReadText(mod, hit.Path);
                if (before == null || !ModFileService.WriteText(mod, hit.Path, hit.NewText))
                {
                    RestoreFiles(mod, written);
                    ClearCaches();
                    return "tools/rename/failed";
                }
                written.Add((hit.Path, before));
            }
            undoFiles = written;
            undoMod = mod;
            filesChanged = written.Count;
            ClearCaches();
            return null;
        }

        /// <summary>Writes back the files the last rename changed, as they were. Returns how many; 0 when there is nothing to undo.</summary>
        public int Undo()
        {
            if (undoFiles == null)
                return 0;
            var count = undoFiles.Count;
            RestoreFiles(undoMod, undoFiles);
            undoFiles = null;
            undoMod = null;
            ClearCaches();
            return count;
        }

        private static void RestoreFiles(string mod, IEnumerable<(string Path, string Text)> files)
        {
            foreach (var (path, text) in files)
                ModFileService.WriteText(mod, path, text);
        }

        /// <summary>Drops the caches the game and the tools read the data through, so the renamed entity is seen at once.</summary>
        public static void ClearCaches()
        {
            ModManager.Instance.GetResolver<PatchResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<EntityResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<ScenarioResolver>()?.ClearCache();
            GameEntityIndex.Refresh();
        }
    }
}
