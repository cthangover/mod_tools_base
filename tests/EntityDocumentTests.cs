namespace Cthangover.Tools.EntityEditor.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Testing;

    /// <summary>The data editors' document: files of one kind, add/copy/delete, texts, undo and what gets written.</summary>
    public class EntityDocumentTests
    {
        private const string Items = "{\n  \"Kind\": \"item\",\n  \"Items\": [\n    {\n      \"Id\": \"food/bread\",\n      \"Name\": \"items/food/bread/name\",\n      \"Cost\": 2,\n      \"heal\": 5\n    }\n  ]\n}\n";

        private static EntityDocument Load(out List<string> skipped) => EntityDocument.Load("item", "items/my_mod.json", new[]
        {
            ("items/food/bread.json", Items),
            ("characters/hero.json", "{\"Kind\": \"character\", \"Items\": [{\"Id\": \"hero\"}]}"),
            ("items/broken.json", "{\"Kind\": \"item\", \"Items\": [ {"),
            ("scenes/town.json", "{\"Name\": \"town\"}")
        }, out skipped);

        [ModTest("entity_document/load_takes_only_its_kind")]
        static void LoadTakesOnlyItsKind(ModTestContext ctx)
        {
            var document = Load(out var skipped);
            ModAssert.Equal(1, document.Records.Count, "only the item file");
            ModAssert.Equal("food/bread", document.Records[0].Id, "its entity");
            ModAssert.Equal("items/food/bread.json", document.Records[0].File, "its file");
            ModAssert.Equal(1, skipped.Count, "the broken file of this kind is reported");
            ModAssert.Equal("items/broken.json", skipped[0], "by path");
            ModAssert.Equal(0, document.ChangedFiles().Count, "nothing to write after loading");
        }

        [ModTest("entity_document/unknown_fields_survive")]
        static void UnknownFieldsSurvive(ModTestContext ctx)
        {
            var document = Load(out _);
            document.Records[0].Data["Cost"] = 3;

            var changed = document.ChangedFiles();
            ModAssert.Equal(1, changed.Count, "the changed file");
            var root = JsonNode.Parse(changed[0].Text).AsObject();
            var item = root["Items"][0].AsObject();
            ModAssert.Equal("3", item["Cost"].ToString(), "the edit");
            ModAssert.Equal("5", item["heal"].ToString(), "another mod's field is kept");
            ModAssert.Equal("item", root["Kind"].ToString(), "the kind is kept");
            ModAssert.True(changed[0].Text.EndsWith("}\n"), "LF line break at the end");
            ModAssert.Equal(Items.Replace("\"Cost\": 2", "\"Cost\": 3"), changed[0].Text, "the rest of the file is byte for byte the same");
        }

        [ModTest("entity_document/add_copy_delete")]
        static void AddCopyDelete(ModTestContext ctx)
        {
            var document = Load(out _);
            var added = document.Add(new JsonObject { ["Id"] = document.FreeId("food/bread") });
            ModAssert.Equal("food/bread_2", added.Id, "a taken id gets a number");
            ModAssert.Equal("items/my_mod.json", added.File, "new entries go to the new file");

            var copy = document.Duplicate(document.Find("food/bread"), document.FreeId("food/bread"));
            ModAssert.Equal("food/bread_3", copy.Id, "the copy gets a free id");
            ModAssert.Equal("items/food/bread.json", copy.File, "and stays in the original's file");
            ModAssert.Equal("5", copy.Data["heal"].ToString(), "with all fields");
            ModAssert.Equal("food/bread", document.Records[0].Id, "the original first");
            ModAssert.Equal("food/bread_3", document.Records[1].Id, "the copy right after it");

            document.Remove(document.Find("food/bread"));
            ModAssert.Null(document.Find("food/bread"), "removed");
            var files = document.ChangedFiles().Select(f => f.Path).OrderBy(p => p).ToList();
            ModAssert.Equal(2, files.Count, "both files are written");
            ModAssert.Equal("items/food/bread.json", files[0], "the changed one");
            ModAssert.Equal("items/my_mod.json", files[1], "the new one");
            ModAssert.True(document.ChangedFiles().First(f => f.Path == "items/my_mod.json").Text.Contains("\"Kind\": \"item\""), "a new file declares the kind");
        }

        [ModTest("entity_document/undo_restores_data_and_texts")]
        static void UndoRestoresDataAndTexts(ModTestContext ctx)
        {
            var document = Load(out _);
            document.LoadText("ru", "items/food/bread/name", "Хлеб");

            document.BeginChange();
            document.Records[0].Data["Cost"] = 9;
            document.SetText("ru", "items/food/bread/name", "Каравай");
            document.BeginChange();
            document.Add(new JsonObject { ["Id"] = "food/cake" });

            ModAssert.True(document.Undo(), "undo the add");
            ModAssert.Null(document.Find("food/cake"), "the new entry is gone");
            ModAssert.True(document.Undo(), "undo the edit");
            ModAssert.Equal("2", document.Find("food/bread").Data["Cost"].ToString(), "the cost is back");
            ModAssert.Equal("Хлеб", document.GetText("ru", "items/food/bread/name"), "the text is back");
            ModAssert.False(document.Undo(), "nothing more");
            ModAssert.Equal(0, document.ChangedFiles().Count + document.ChangedTexts().Count, "back to what is on disk");

            ModAssert.True(document.Redo(), "redo the edit");
            var texts = document.ChangedTexts();
            ModAssert.Equal(1, texts.Count, "one changed text");
            ModAssert.Equal("ru", texts[0].Language, "its language");
            ModAssert.Equal("items/food/bread/name", texts[0].Key, "its key");
            ModAssert.Equal("Каравай", texts[0].Value, "its value");

            document.MarkSaved();
            ModAssert.Equal(0, document.ChangedFiles().Count + document.ChangedTexts().Count, "nothing left after saving");
        }

        private const string Patch = "{\n  \"Items\": [\n    {\n      \"Id\": \"other/sword\",\n      \"Cost\": 40\n    }\n  ]\n}\n";

        /// <summary>Base values of other mods' entities, as the game would resolve them without the edited mod's patch.</summary>
        private static JsonObject BaseOf(string id) => id switch
        {
            "other/sword" => new JsonObject { ["Id"] = "other/sword", ["Cost"] = 30, ["Traits"] = new JsonArray("sharp") },
            "other/shield" => new JsonObject { ["Id"] = "other/shield", ["Cost"] = 12 },
            _ => null
        };

        private static EntityDocument WithForeign(string patchText)
        {
            var document = Load(out _);
            document.AttachForeign("patches/item/my_mod.json", patchText, null, new[]
            {
                ("other/sword", "other", "items/other.json", new JsonObject { ["Id"] = "other/sword", ["Cost"] = 30 }),
                ("other/shield", "other", "items/other.json", new JsonObject { ["Id"] = "other/shield" }),
                ("food/bread", "other", "items/other.json", new JsonObject { ["Id"] = "food/bread" })
            }, BaseOf);
            return document;
        }

        [ModTest("entity_document/foreign_listed_with_own_patch")]
        static void ForeignListedWithOwnPatch(ModTestContext ctx)
        {
            var document = WithForeign(Patch);
            ModAssert.Equal(2, document.Foreign.Count, "an id the mod defines itself is not foreign");
            ModAssert.Equal(1, document.Records.Count, "own records are unchanged");
            var sword = document.FindForeign("OTHER/SWORD");
            ModAssert.True(sword.IsForeign, "foreign");
            ModAssert.Equal("other", sword.OwnerMod, "its owner");
            ModAssert.Equal("40", sword.Data["Cost"].ToString(), "the form sees the value with the own patch");
            ModAssert.Equal("30", document.BaseOf(sword)["Cost"].ToString(), "the base is without it");
            ModAssert.Null(document.ChangedPatch(), "nothing to write after loading");
            ModAssert.True(document.FindAny("food/bread") == document.Find("food/bread"), "an own entity wins the lookup");
        }

        [ModTest("entity_document/foreign_edit_writes_minimal_patch")]
        static void ForeignEditWritesMinimalPatch(ModTestContext ctx)
        {
            var document = WithForeign(null);
            var shield = document.FindForeign("other/shield");
            document.BeginChange();
            shield.Data["Cost"] = 15;
            document.CommitForeign(shield);

            var patch = document.ChangedPatch();
            ModAssert.NotNull(patch, "the patch file changes");
            ModAssert.Equal("patches/item/my_mod.json", patch.Value.Path, "in the edited mod's patch file");
            var entry = JsonNode.Parse(patch.Value.Text)["Items"][0].AsObject();
            ModAssert.Equal("other/shield", entry["Id"].ToString(), "addressed to the entity");
            ModAssert.Equal(2, entry.Count, "only the id and the changed field");
            ModAssert.Equal("15", entry["Cost"].ToString(), "the edit");
            ModAssert.True(patch.Value.Text.EndsWith("}\n") && !patch.Value.Text.Contains("\r"), "LF text");
            ModAssert.Equal(0, document.ChangedFiles().Count, "no own data file is touched");

            shield.Data["Cost"] = 12;
            document.CommitForeign(shield);
            ModAssert.Null(document.PatchOf("other/shield"), "back to the base value: no entry");
            ModAssert.Null(document.ChangedPatch(), "and nothing to write (there was no file)");
        }

        [ModTest("entity_document/foreign_reset_and_delete")]
        static void ForeignResetAndDelete(ModTestContext ctx)
        {
            var document = WithForeign(Patch);
            var sword = document.FindForeign("other/sword");

            document.BeginChange();
            sword.Data["Traits"] = new JsonArray("sharp", "holy");
            document.CommitForeign(sword);
            ModAssert.NotNull(document.PatchOf("other/sword")["Cost"], "the earlier patched field stays");
            ModAssert.Equal("holy", document.PatchOf("other/sword")["Traits$add"][0].ToString(), "a grown list is an $add");

            document.ResetField(sword, "Cost");
            ModAssert.Equal("30", sword.Data["Cost"].ToString(), "reset shows the inherited value");
            ModAssert.Null(document.PatchOf("other/sword")["Cost"], "and removes the key from the patch");

            document.BeginChange();
            document.DeleteForeign(sword);
            ModAssert.True(document.IsDeleted(sword), "deleting a foreign entity is a patch");
            ModAssert.Equal("true", document.PatchOf("other/sword")["$delete"].ToString(), "with $delete");
            ModAssert.Equal(2, document.Foreign.Count, "the entity stays listed");

            ModAssert.True(document.Undo(), "undo the delete");
            ModAssert.False(document.IsDeleted(sword), "not deleted");
            ModAssert.Equal("holy", sword.Data["Traits"][1].ToString(), "the edit is back in the form's data");

            document.ResetForeign(sword);
            ModAssert.Null(document.PatchOf("other/sword"), "reset removes the entry");
            var patch = document.ChangedPatch();
            ModAssert.NotNull(patch, "the patch file changed");
            ModAssert.Null(patch.Value.Text, "its last entry is gone: the file is to be removed");
            document.MarkSaved();
            ModAssert.Null(document.ChangedPatch(), "nothing left after saving");
        }

        [ModTest("entity_document/free_id_counts_foreign")]
        static void FreeIdCountsForeign(ModTestContext ctx)
        {
            var document = WithForeign(null);
            ModAssert.Equal("other/sword_2", document.FreeId("other/sword"), "a foreign id is taken too");
        }

        [ModTest("entity_document/validate_reports_ids")]
        static void ValidateReportsIds(ModTestContext ctx)
        {
            var document = Load(out _);
            ModAssert.Equal(0, document.Validate().Count, "a loaded mod is valid");
            document.Add(new JsonObject { ["Id"] = "FOOD/BREAD" });
            document.Add(new JsonObject { ["Name"] = "x" });

            var problems = document.Validate();
            ModAssert.Equal(2, problems.Count, "an empty id and a duplicate");
            ModAssert.Equal("tools/entity_editor/problem_id", problems[0].Key, "empty id first");
            ModAssert.Equal("tools/entity_editor/problem_duplicate", problems[1].Key, "a duplicate ignores case");
        }
    }
}
