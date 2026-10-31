namespace Cthangover.Tools.EntityEditor.Tests
{
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Dotted paths into an entity: reads, writes that create blocks, removals that prune the blocks they empty.</summary>
    public class EntityPathTests
    {
        private static JsonObject Json(string text) => JsonNode.Parse(text).AsObject();

        [ModTest("entity_path/get_nested_and_indexed")]
        static void GetNestedAndIndexed(ModTestContext ctx)
        {
            var data = Json("{\"Id\": \"a\", \"Parts\": [{\"Image\": \"x/y\", \"Crop\": [0, 0, 32, 32]}], \"interact\": {\"Trigger\": \"Step\"}}");
            ModAssert.Equal("x/y", EntityPath.Get(data, "Parts.0.Image")?.ToString(), "an index");
            ModAssert.Equal("Step", EntityPath.Get(data, "Interact.Trigger")?.ToString(), "names match case-insensitively");
            ModAssert.Null(EntityPath.Get(data, "Parts.3.Image"), "a missing index");
            ModAssert.Null(EntityPath.Get(data, "Surface.Image"), "a missing block");
        }

        [ModTest("entity_path/set_creates_blocks")]
        static void SetCreatesBlocks(ModTestContext ctx)
        {
            var data = Json("{\"Id\": \"a\"}");
            EntityPath.Set(data, "Parts.0.Image", JsonValue.Create("x/y"));
            EntityPath.Set(data, "Footprint.Size", JsonNode.Parse("[2, 3]"));
            ModAssert.Equal("[{\"Image\":\"x/y\"}]", data["Parts"].ToJsonString(), "an array of one block is created");
            ModAssert.Equal("[2,3]", data["Footprint"]["Size"].ToJsonString(), "a nested array");
        }

        [ModTest("entity_path/remove_prunes_empty_blocks")]
        static void RemovePrunesEmptyBlocks(ModTestContext ctx)
        {
            var data = Json("{\"Id\": \"a\", \"Interact\": {\"Scenario\": \"s/x\"}, \"Parts\": [{\"Image\": \"x/y\"}]}");
            ModAssert.True(EntityPath.Remove(data, "Interact.Scenario"), "removed");
            ModAssert.Null(data["Interact"], "an interaction block left empty goes away: an empty one would make the object dynamic");
            ModAssert.True(EntityPath.Remove(data, "Parts.0.Image"), "the picture of the only part");
            ModAssert.Null(data["Parts"], "the part list left empty goes away");
            ModAssert.False(EntityPath.Remove(data, "Parts.0.Image"), "nothing left to remove");
            ModAssert.Equal("a", data["Id"].ToString(), "the rest stays");
        }

        [ModTest("entity_path/remove_keeps_other_properties_of_a_block")]
        static void RemoveKeepsOtherProperties(ModTestContext ctx)
        {
            var data = Json("{\"Interact\": {\"Trigger\": \"Use\", \"Commands\": [\"flag self.open 1\"]}}");
            EntityPath.Remove(data, "Interact.Trigger");
            ModAssert.Equal("[\"flag self.open 1\"]", data["Interact"]["Commands"].ToJsonString(), "the commands stay");
        }

        [ModTest("entity_path/top_and_nested")]
        static void TopAndNested(ModTestContext ctx)
        {
            ModAssert.Equal("Surface", EntityPath.Top("Surface.Image"), "the block");
            ModAssert.Equal("Name", EntityPath.Top("Name"), "a plain key");
            ModAssert.True(EntityPath.IsNested("Parts.0.Crop"), "nested");
            ModAssert.False(EntityPath.IsNested("Anchor"), "not nested");
        }
    }
}
