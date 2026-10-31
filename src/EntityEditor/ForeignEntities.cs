namespace Cthangover.Tools.EntityEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Factories;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Tools.Services;

    /// <summary>
    /// What a data editor needs from the running game to edit other mods' entities: which entities
    /// the edited mod may patch, where a field's value comes from (<see cref="EntityResolver.Explain"/>)
    /// and an entity's value without the edited mod's own patch.
    /// <para/>
    /// Only entities of the edited mod's dependencies (transitive <c>depends</c>) are offered: those
    /// mods load first, so the patch applies after their data and the resource atlas does not report
    /// a patch of an undeclared dependency.
    /// </summary>
    public static class ForeignEntities
    {
        /// <summary>The edited mod's patch file of a kind: <c>patches/&lt;kind&gt;/&lt;mod&gt;.json</c>.</summary>
        public static string PatchFile(string kind, string modId) => $"patches/{kind}/{modId}.json";

        /// <summary>The DTO type registered for <paramref name="kind"/>, or null.</summary>
        public static Type TypeOf(string kind) => EntityKindRegistry.TryResolve(kind, out var type) ? type : null;

        /// <summary>The mods <paramref name="modId"/> depends on, directly or not (without itself).</summary>
        public static HashSet<string> Dependencies(string modId)
        {
            var mods = ModManager.Instance?.Mods;
            if (mods == null || ModManager.Instance.GetMod(modId) is not ModInfo info)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var closure = ModCompiler.DependencyClosure(info, mods.ToDictionary(p => p.Key, p => p.Value));
            closure.RemoveAll(id => string.Equals(id, modId, StringComparison.OrdinalIgnoreCase));
            return new HashSet<string>(closure, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The entities of <paramref name="kind"/> defined by the dependencies of <paramref name="modId"/>,
        /// as written in their files (the owner is the mod whose file holds the id).
        /// </summary>
        public static List<(string Id, string OwnerMod, string File, JsonObject Written)> Scan(string kind, string modId)
        {
            var result = new List<(string, string, string, JsonObject)>();
            var marker = "\"" + kind + "\"";
            foreach (var mod in Dependencies(modId).OrderBy(m => m, StringComparer.OrdinalIgnoreCase))
            {
                if (ModManager.Instance.GetMod(mod) == null)
                    continue;
                foreach (var path in ModFileService.ListFiles(mod, string.Empty, ".json"))
                {
                    var text = ModFileService.ReadText(mod, path);
                    if (text == null || text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    foreach (var item in ItemsOf(text, kind))
                    {
                        var id = item["Id"]?.ToString();
                        if (!string.IsNullOrEmpty(id))
                            result.Add((id, mod, path, item));
                    }
                }
            }
            return result;
        }

        /// <summary>Field origins of an entity as the game resolves it now (from the files on disk), or null.</summary>
        public static EntityExplanation Explain(string kind, string id)
            => string.IsNullOrEmpty(id) ? null : ModManager.Instance?.GetResolver<EntityResolver>()?.Explain(kind, id);

        /// <summary>
        /// The value the game would see for <paramref name="id"/> without the edited mod's patch file
        /// <paramref name="patchFile"/>: the resolved value when that file does not touch the entity,
        /// otherwise the entity replayed from its definition, its parent and every other patch.
        /// Null when the entity is unknown or another mod deletes it.
        /// </summary>
        public static JsonObject BaseWithout(string kind, string id, string modId, string patchFile)
        {
            var explanation = Explain(kind, id);
            var type = TypeOf(kind);
            if (explanation == null || type == null)
                return null;
            var patches = ModManager.Instance.GetResolver<PatchResolver>()?.For(kind, id) ?? Array.Empty<PropertyPatch>();
            var own = patches.Where(p => IsOwn(p, modId, patchFile)).ToList();
            if (own.Count == 0)
                return explanation.Deleted ? null : explanation.Value;

            if (!explanation.Fields.TryGetValue("Id", out var definition) || definition.Layer != EntityLayer.Definition)
                return null;
            var text = ModManager.Instance.ReadResolvedText(definition.ModId, definition.File);
            var written = text == null ? null : ItemsOf(text, kind).FirstOrDefault(i => string.Equals(i["Id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));
            var parentId = EntityExtendsResolution<object>.ExtendsOf(written);
            var parent = parentId == null ? null : Explain(kind, parentId)?.Value;
            return EntityJson.Replay(written, parent, patches.Where(p => !own.Contains(p)), id, type);
        }

        /// <summary>True when <paramref name="patch"/> is an addressed entry of the edited mod's patch file.</summary>
        public static bool IsOwn(PropertyPatch patch, string modId, string patchFile)
            => patch.ID != "*" && string.Equals(patch.ModId, modId, StringComparison.OrdinalIgnoreCase)
               && string.Equals(ModPath.Normalize(patch.File ?? string.Empty), ModPath.Normalize(patchFile), StringComparison.OrdinalIgnoreCase);

        private static List<JsonObject> ItemsOf(string text, string kind)
        {
            JsonObject root;
            try
            {
                root = JsonNode.Parse(text, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
            }
            catch (JsonException)
            {
                return new List<JsonObject>();
            }
            if (root == null || !string.Equals(EntityJson.Get(root, "Kind", out _)?.ToString(), kind, StringComparison.OrdinalIgnoreCase))
                return new List<JsonObject>();
            return (EntityJson.Get(root, "Items", out _) as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        }
    }
}
