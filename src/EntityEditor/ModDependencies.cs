namespace Cthangover.Tools.EntityEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization.Metadata;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Schemas;
    using Cthangover.Tools.Common;
    using Cthangover.Tools.Services;

    /// <summary>
    /// Adds the <c>depends</c> a novice's own mod needs when it refers to another mod's resource: the editors call
    /// it on save, so the novice never edits <c>manifest.json</c> (plan 14.G16). The rule is the one the resource
    /// atlas checks: a reference to a foreign entity needs the provider in the transitive <c>depends</c>.
    /// <para/>
    /// Rules of <see cref="Insert"/>: the entry is <c>"&lt;provider&gt;&gt;=0.1.0"</c> (the style of the packaged mods), appended
    /// to the array; a provider already in the transitive closure is left alone; the own mod is never added; a provider
    /// that depends on the own mod is refused (a cycle). An unchanged manifest is never written, so its bytes stay.
    /// The file is written with 2-space JSON and LF line ends; the running game keeps its loaded mod set until the next
    /// start, which the editor's status line says.
    /// </summary>
    public static class ModDependencies
    {
        /// <summary>The manifest file of a mod.</summary>
        public const string ManifestFile = "manifest.json";

        /// <summary>The reference kind of a scenario: its id is the path under <c>scenarios/</c> without <c>.scenario</c>.</summary>
        public const string ScenarioKind = "scenario";

        /// <summary>The prefix of a picture's reference kind: <c>picture:</c> followed by the file group (<c>rpg_images</c>).</summary>
        public const string PictureKind = "picture:";

        private const string ScenarioGroup = "scenarios";
        private const string ScenarioSuffix = ".scenario";
        private const string LogCategory = "TOOLS";

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            // Names and descriptions stay as typed: no \u escapes for non-ASCII text, '<', '>' or '&'.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // The writer needs a type resolver in this runtime; items_editor sets the same one for its indented JSON.
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        /// <summary>
        /// Pure rule without file access: the manifest text with <paramref name="provider"/> added to its <c>depends</c>
        /// (returned as <paramref name="text"/>, only for <see cref="ModDependencyOutcome.Added"/>). <paramref name="dependsOf"/>
        /// gives the direct dependencies of any other mod and is used for the transitive closures.
        /// </summary>
        public static ModDependencyOutcome Insert(string manifestText, string provider, Func<string, IEnumerable<string>> dependsOf, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(provider) || manifestText == null)
                return ModDependencyOutcome.NotWritable;
            var root = ParseObject(manifestText);
            if (root == null || root["id"]?.GetValue<string>() is not { Length: > 0 } own)
                return ModDependencyOutcome.NotWritable;
            if (string.Equals(provider, own, StringComparison.Ordinal))
                return ModDependencyOutcome.Self;

            var closure = Closure(DirectOf(root), dependsOf);
            if (closure.Contains(provider))
                return ModDependencyOutcome.Present;
            if (Closure(new[] { provider }, dependsOf).Contains(own))
                return ModDependencyOutcome.Cycle;

            if (root["depends"] is not JsonArray depends)
            {
                depends = new JsonArray();
                root["depends"] = depends;
            }
            depends.Add(provider + ">=0.1.0");
            text = root.ToJsonString(WriteOptions).Replace("\r\n", "\n") + "\n";
            return ModDependencyOutcome.Added;
        }

        /// <summary>
        /// Adds <paramref name="providerMod"/> to the <c>depends</c> of <paramref name="ownMod"/> in its folder-backed
        /// manifest, when the rules of <see cref="Insert"/> allow it. A refused cycle is logged, not shown.
        /// </summary>
        public static ModDependencyOutcome Ensure(string ownMod, string providerMod)
        {
            if (string.IsNullOrEmpty(ownMod) || string.IsNullOrEmpty(providerMod))
                return ModDependencyOutcome.NotWritable;
            var current = ModFileService.ReadText(ownMod, ManifestFile);
            var outcome = Insert(current, providerMod, DependsOf, out var next);
            if (outcome == ModDependencyOutcome.Cycle)
            {
                GameLogger.Log(LogCategory, $"dependency of '{ownMod}' on '{providerMod}' not added: '{providerMod}' depends on '{ownMod}'", LogLevel.Warning);
                return outcome;
            }
            if (outcome != ModDependencyOutcome.Added)
                return outcome;
            if (!ModFileService.WriteText(ownMod, ManifestFile, next))
                return ModDependencyOutcome.NotWritable;
            GameLogger.Log(LogCategory, $"dependency of '{ownMod}' on '{providerMod}' added to manifest.json");
            return ModDependencyOutcome.Added;
        }

        /// <summary>
        /// The references of one entity: for every field of its kind's schema that holds a reference (<c>ref</c>,
        /// <c>ref_list</c>, <c>who</c>, <c>scene</c>), the (kind, id) pairs the value names. Non-text values are skipped.
        /// </summary>
        public static IEnumerable<(string Kind, string Id)> ReferencesOf(JsonObject data, IEnumerable<FieldSchema> fields)
        {
            if (data == null || fields == null)
                yield break;
            foreach (var field in fields)
            {
                if (data[field.Key] is JsonArray items && field.Type == "list" && field.Of != null)
                {
                    foreach (var item in items.OfType<JsonObject>())
                    {
                        foreach (var reference in ReferencesOf(item, field.Of))
                            yield return reference;
                    }
                    continue;
                }
                if (data[field.Key] is not JsonValue value || !value.TryGetValue(out string text))
                    continue;
                if (field.Type == "picture")
                {
                    if (!string.IsNullOrEmpty(field.Group))
                        yield return (PictureKind + field.Group, text);
                    continue;
                }
                foreach (var reference in SchemaReferences.Of(field, text))
                    yield return reference;
            }
        }

        /// <summary>
        /// Ensures the dependencies of <paramref name="ownMod"/> for the given references: the mod that defines each
        /// referenced entity is added when the own mod does not reach it yet. Returns the providers added, in order.
        /// </summary>
        public static List<string> EnsureReferenced(string ownMod, IEnumerable<(string Kind, string Id)> references)
            => EnsureProviders(ownMod, references.Select(r => ProviderOf(r.Kind, r.Id)));

        /// <summary>
        /// Adds each provider mod to the own mod's <c>depends</c> when the own mod does not reach it (see <see cref="Ensure"/>);
        /// null and duplicate providers are skipped. Returns the providers added, in order.
        /// </summary>
        public static List<string> EnsureProviders(string ownMod, IEnumerable<string> providers)
        {
            var added = new List<string>();
            foreach (var provider in providers.Where(p => p != null).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Ensure(ownMod, provider) == ModDependencyOutcome.Added)
                    added.Add(provider);
            }
            return added;
        }

        /// <summary>
        /// The mod that provides a reference, or null when nothing does. An entity kind (<c>rpg_map</c>, <c>shop</c>, <c>item</c>)
        /// is looked up in the game's entities; <see cref="ScenarioKind"/> names a scenario file, and <c>picture:&lt;group&gt;</c>
        /// (see <see cref="PictureKind"/>) a picture of a file group.
        /// </summary>
        public static string ProviderOf(string kind, string id)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(kind))
                return null;
            if (kind == ScenarioKind)
                return ResourceProvider.Of(ScenarioFiles(), ScenarioGroup, id);
            if (kind.StartsWith(PictureKind, StringComparison.Ordinal))
            {
                var group = kind.Substring(PictureKind.Length);
                // A picture the group does not list (a half-typed path) provides nothing, as the form shows it.
                return ModResourceService.GetGroupImageIds(group).Contains(id.StartsWith(group + "/", StringComparison.OrdinalIgnoreCase) ? id.Substring(group.Length + 1) : id, StringComparer.OrdinalIgnoreCase)
                    ? ResourceProvider.Of(Files.Collect(group)?.Select(f => (f.Key, f.Value.ModId)), group, id)
                    : null;
            }
            var explanation = ForeignEntities.Explain(kind, id);
            return explanation != null && explanation.Fields.TryGetValue("Id", out var definition) && definition.Layer == EntityLayer.Definition
                ? definition.ModId
                : null;
        }

        /// <summary>The scenario files of the game: their paths relative to the scenarios group, with the extension.</summary>
        private static IEnumerable<(string Path, string ModId)> ScenarioFiles()
            => Files.Collect(ScenarioGroup)?
                .Where(f => f.Key.EndsWith(ScenarioSuffix, StringComparison.OrdinalIgnoreCase))
                .Select(f => (f.Key, f.Value.ModId)) ?? Enumerable.Empty<(string Path, string ModId)>();

        /// <summary>The status-line text for one added provider (localized).</summary>
        public static string Notice(IEnumerable<string> providers)
            => string.Join(" ", providers.Select(p => string.Format(ToolUi.Tr("tools/common/dependency_added"), p)));

        /// <summary>The manifest as a JSON object; null when the text is missing or not a JSON object.</summary>
        private static JsonObject ParseObject(string text)
        {
            if (text == null)
                return null;
            try
            {
                return JsonNode.Parse(text) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>The direct dependencies of a mod by its loaded manifest; empty for an unknown mod.</summary>
        private static IEnumerable<string> DependsOf(string modId)
            => ModManager.Instance?.GetMod(modId) is ModInfo info && info.Manifest != null
                ? info.Manifest.DependencyIds ?? new List<string>()
                : Array.Empty<string>();

        /// <summary>The ids a manifest's <c>depends</c> names, with or without a version operator.</summary>
        private static List<string> DirectOf(JsonObject root)
        {
            var result = new List<string>();
            if (root["depends"] is not JsonArray depends)
                return result;
            foreach (var entry in depends)
            {
                var parsed = ModDependency.Parse(entry?.GetValue<string>());
                if (parsed.IsValid)
                    result.Add(parsed.ModId);
            }
            return result;
        }

        /// <summary>Every id reachable from <paramref name="start"/> through <paramref name="dependsOf"/>, the starts included.</summary>
        private static HashSet<string> Closure(IEnumerable<string> start, Func<string, IEnumerable<string>> dependsOf)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(start);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id))
                    continue;
                foreach (var next in dependsOf(id))
                    queue.Enqueue(next);
            }
            return seen;
        }
    }
}
