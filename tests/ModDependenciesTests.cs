namespace Cthangover.Tools.EntityEditor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// The pure rule of <see cref="ModDependencies.Insert"/>: a provider mod is added to the own manifest's <c>depends</c>
    /// once, never when the transitive closure already reaches it, never for the mod itself, never when it would make a cycle.
    /// </summary>
    public class ModDependenciesTests
    {
        private const string Own = "my_village";
        private const string Manifest = "{\n  \"id\": \"my_village\",\n  \"name\": \"Моя деревня\",\n  \"depends\": [\n    \"ui_base>=1\"\n  ]\n}\n";

        /// <summary>The direct dependencies of the test mods: rpg rests on ui_base, village_pack on rpg, a loop of two mods for the cycle test.</summary>
        private static IEnumerable<string> DependsOf(string modId) => modId switch
        {
            "rpg_plus_game" => new[] { "village_pack" },
            "village_pack" => new[] { "ui_base" },
            "cycle_pack" => new[] { Own },
            _ => Array.Empty<string>(),
        };

        [ModTest("deps/adds_the_provider_once")]
        static void AddsOnce(ModTestContext ctx)
        {
            var outcome = ModDependencies.Insert(Manifest, "classic_rpg_pack", DependsOf, out var text);
            ModAssert.True(outcome == ModDependencyOutcome.Added, "a provider outside the closure is added");
            ModAssert.True(text.Contains("\"classic_rpg_pack>=1\""), "as <id>>=1, the packaged style");
            ModAssert.True(JsonNode.Parse(text)!["depends"]!.AsArray().Count == 2, "the old entry is kept");
            ModAssert.True(Insert(text, "classic_rpg_pack").Equals(ModDependencyOutcome.Present), "a second add finds it there");
        }

        [ModTest("deps/unchanged_manifest_is_not_rewritten")]
        static void ByteStable(ModTestContext ctx)
        {
            var outcome = ModDependencies.Insert(Manifest, "ui_base", DependsOf, out var text);
            ModAssert.True(outcome == ModDependencyOutcome.Present, "a direct dependency is present");
            ModAssert.True(text == null, "no new text: the caller does not write the file");
        }

        [ModTest("deps/formatting_is_two_space_lf_and_names_stay_typed")]
        static void Formatting(ModTestContext ctx)
        {
            var outcome = ModDependencies.Insert(Manifest, "village_pack", DependsOf, out var text);
            ModAssert.True(outcome == ModDependencyOutcome.Added, "added");
            ModAssert.False(text.Contains("\r"), "LF line ends");
            ModAssert.True(text.EndsWith("\n") && !text.EndsWith("\n\n"), "one final newline");
            ModAssert.True(text.Contains("\n  \"depends\""), "2-space indentation");
            ModAssert.True(text.Contains("\"Моя деревня\""), "non-ASCII names are not escaped");
        }

        [ModTest("deps/manifest_without_depends_gets_the_array")]
        static void NoDependsField(ModTestContext ctx)
        {
            var text0 = "{\n  \"id\": \"my_village\",\n  \"version\": \"0.1.0\"\n}\n";
            var outcome = ModDependencies.Insert(text0, "rpg_plus_game", DependsOf, out var text);
            ModAssert.True(outcome == ModDependencyOutcome.Added, "added");
            ModAssert.True(JsonNode.Parse(text)!["depends"]!.AsArray().First()!.GetValue<string>() == "rpg_plus_game>=1", "the first entry");
        }

        [ModTest("deps/the_mod_itself_is_never_added")]
        static void NoSelf(ModTestContext ctx)
        {
            ModAssert.True(ModDependencies.Insert(Manifest, Own, DependsOf, out var text) == ModDependencyOutcome.Self, "own id is refused");
            ModAssert.True(text == null, "nothing written");
        }

        [ModTest("deps/a_provider_that_depends_on_us_is_refused")]
        static void NoCycle(ModTestContext ctx)
        {
            var outcome = ModDependencies.Insert(Manifest, "cycle_pack", DependsOf, out var text);
            ModAssert.True(outcome == ModDependencyOutcome.Cycle, "cycle_pack depends on my_village, adding it would loop");
            ModAssert.True(text == null, "nothing written");
        }

        [ModTest("deps/transitive_dependency_is_already_there")]
        static void TransitiveSkip(ModTestContext ctx)
        {
            var withGame = "{\n  \"id\": \"my_village\",\n  \"depends\": [\"rpg_plus_game>=1\"]\n}\n";
            ModAssert.True(ModDependencies.Insert(withGame, "village_pack", DependsOf, out var text) == ModDependencyOutcome.Present,
                "village_pack comes through rpg_plus_game, nothing to add");
            ModAssert.True(text == null, "the manifest keeps its bytes");
            ModAssert.True(ModDependencies.Insert(withGame, "ui_base", DependsOf, out _) == ModDependencyOutcome.Present,
                "a second level is reached too");
        }

        [ModTest("deps/unreadable_manifest_is_not_writable")]
        static void Broken(ModTestContext ctx)
        {
            ModAssert.True(ModDependencies.Insert("{ not json", "village_pack", DependsOf, out _) == ModDependencyOutcome.NotWritable, "not JSON");
            ModAssert.True(ModDependencies.Insert(null, "village_pack", DependsOf, out _) == ModDependencyOutcome.NotWritable, "no manifest");
            ModAssert.True(ModDependencies.Insert(Manifest, "", DependsOf, out _) == ModDependencyOutcome.NotWritable, "no provider");
        }

        [ModTest("deps/references_come_from_the_schema_fields")]
        static void References(ModTestContext ctx)
        {
            var fields = new List<Cthangover.Mods.Schemas.FieldSchema>
            {
                new() { Key = "Scene", Type = "scene" },
                new() { Key = "Shop", Type = "ref", Kind = "shop" },
                new() { Key = "Name", Type = "text" },
            };
            var data = new JsonObject { ["Scene"] = "village_square", ["Shop"] = "village/general", ["Name"] = "Lira" };
            var refs = ModDependencies.ReferencesOf(data, fields).ToList();
            ModAssert.True(refs.Count == 2, "a scene and a shop, the text is not a reference");
            ModAssert.True(refs[1] == ("shop", "village/general"), "the ref names its kind");
        }

        [ModTest("deps/pictures_and_list_items_name_their_group")]
        static void PicturesAndLists(ModTestContext ctx)
        {
            var fields = new List<Cthangover.Mods.Schemas.FieldSchema>
            {
                new() { Key = "Icon", Type = "picture", Group = "items" },
                new()
                {
                    Key = "Parts", Type = "list",
                    Of = new List<Cthangover.Mods.Schemas.FieldSchema> { new() { Key = "Image", Type = "picture", Group = "rpg_images" } },
                },
            };
            var data = new JsonObject
            {
                ["Icon"] = "food/ration",
                ["Parts"] = new JsonArray(new JsonObject { ["Image"] = "village/chest" }, new JsonObject { ["Image"] = "village/crate" }),
            };
            var refs = ModDependencies.ReferencesOf(data, fields).ToList();
            ModAssert.True(refs.Count == 3, "one icon and two part pictures");
            ModAssert.True(refs[0] == ("picture:items", "food/ration"), "a picture names its group");
            ModAssert.True(refs[2] == ("picture:rpg_images", "village/crate"), "the list's items are checked too");
        }

        private static ModDependencyOutcome Insert(string text, string provider)
            => ModDependencies.Insert(text, provider, DependsOf, out _);
    }
}
