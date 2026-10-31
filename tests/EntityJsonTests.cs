namespace Cthangover.Tools.EntityEditor.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Testing;

    /// <summary>The value the game sees (normalised) and the replay of an entity's layers without the editor's own patch.</summary>
    public class EntityJsonTests
    {
        private static PropertyPatch Patch(string json) => JsonSerializer.Deserialize<PropertyPatch>(json);

        [ModTest("entity_json/normalize_fills_defaults_and_keeps_extra")]
        static void NormalizeFillsDefaultsAndKeepsExtra(ModTestContext ctx)
        {
            var normal = EntityJson.Normalize(JsonNode.Parse("{\"id\": \"a\", \"heal\": 5}").AsObject(), typeof(SampleEntity));
            ModAssert.Equal("a", normal["Id"].ToString(), "the DTO's spelling of the key");
            ModAssert.Equal("0", normal["Cost"].ToString(), "a default is written");
            ModAssert.Equal("5", normal["heal"].ToString(), "another mod's field stays");
            ModAssert.Null(EntityJson.Normalize(JsonNode.Parse("{\"Cost\": \"many\"}").AsObject(), typeof(SampleEntity)), "a value of the wrong type does not read");
            var copy = EntityJson.Normalize(JsonNode.Parse("{\"x\": 1}").AsObject(), null);
            ModAssert.Equal("1", copy["x"].ToString(), "without a type: a copy");
        }

        [ModTest("entity_json/replay_parent_then_patches")]
        static void ReplayParentThenPatches(ModTestContext ctx)
        {
            var parent = JsonNode.Parse("{\"Id\": \"base\", \"Cost\": 10, \"Sprite\": \"base.png\"}").AsObject();
            var written = JsonNode.Parse("{\"Id\": \"child\", \"Extends\": \"base\", \"Cost\": 12}").AsObject();
            var patches = new List<PropertyPatch>
            {
                Patch("{\"Id\": \"child\", \"Cost\": 20}"),
                Patch("{\"Id\": \"child\", \"Cost\": \"broken\"}"),
                Patch("{\"Id\": \"*\", \"Sprite\": \"all.png\"}")
            };
            var value = EntityJson.Replay(written, parent, patches, "child", typeof(SampleEntity));
            ModAssert.Equal("child", value["Id"].ToString(), "the child's id");
            ModAssert.Equal("20", value["Cost"].ToString(), "the patch over the child's own value; a broken patch is skipped");
            ModAssert.Equal("all.png", value["Sprite"].ToString(), "patches apply in the given order");

            var alone = EntityJson.Replay(written, null, null, "child", typeof(SampleEntity));
            ModAssert.Equal("12", alone["Cost"].ToString(), "without parent and patches: the definition");
            ModAssert.Null(alone["Sprite"], "nothing inherited");
        }

        [ModTest("entity_json/replay_delete")]
        static void ReplayDelete(ModTestContext ctx)
        {
            var written = JsonNode.Parse("{\"Id\": \"a\", \"Cost\": 1}").AsObject();
            ModAssert.Null(EntityJson.Replay(written, null, new[] { Patch("{\"Id\": \"a\", \"$delete\": true}") }, "a", typeof(SampleEntity)), "a $delete patch removes the entity");
            ModAssert.Null(EntityJson.Replay(null, null, null, "a", typeof(SampleEntity)), "no definition: nothing");
        }
    }
}
