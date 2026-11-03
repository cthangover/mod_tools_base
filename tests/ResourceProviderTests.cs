namespace Cthangover.Tools.EntityEditor.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// The pure lookup of <see cref="ResourceProvider.Of"/>: the mod of a file named by its id without extension, the
    /// last mod in load order having won the group (the caller passes the winners), with a group prefix and case allowed.
    /// </summary>
    public class ResourceProviderTests
    {
        private static readonly List<(string Path, string ModId)> Files = new()
        {
            ("village/chest.png", "village_pack"),
            ("village/Crate.webp", "rpg_plus_game"),
            ("sign.png", "rpg"),
        };

        [ModTest("provider/finds_the_mod_of_the_file")]
        static void FindsTheMod(ModTestContext ctx)
        {
            ModAssert.True(ResourceProvider.Of(Files, "rpg_images", "village/chest") == "village_pack", "the picture without its extension names its mod");
            ModAssert.True(ResourceProvider.Of(Files, "rpg_images", "sign") == "rpg", "a top-level file is found too");
        }

        [ModTest("provider/ignores_case_and_group_prefix")]
        static void CaseAndPrefix(ModTestContext ctx)
        {
            ModAssert.True(ResourceProvider.Of(Files, "rpg_images", "VILLAGE/chest") == "village_pack", "the id is case-insensitive, as the file lookup");
            ModAssert.True(ResourceProvider.Of(Files, "rpg_images", "rpg_images/village/chest") == "village_pack", "a group/ prefix is stripped");
            ModAssert.True(ResourceProvider.Of(Files, "rpg_images", "village/crate") == "rpg_plus_game", "a webp file counts with its own name");
        }

        [ModTest("provider/null_when_nothing_has_the_id")]
        static void Missing(ModTestContext ctx)
        {
            ModAssert.True(ResourceProvider.Of(Files, "rpg_images", "village/barrel") == null, "an id no file has provides nothing");
            ModAssert.True(ResourceProvider.Of(Files, "rpg_images", "village") == null, "a folder is not a file");
            ModAssert.True(ResourceProvider.Of(Files, "rpg_images", "") == null, "an empty id provides nothing");
            ModAssert.True(ResourceProvider.Of(null, "rpg_images", "sign") == null, "no files, no provider");
        }

        [ModTest("provider/withoutextension_keeps_dots_in_folders")]
        static void WithoutExtension(ModTestContext ctx)
        {
            ModAssert.True(ResourceProvider.WithoutExtension("a.b/c") == "a.b/c", "a dot in a folder name is not an extension");
            ModAssert.True(ResourceProvider.WithoutExtension("a/c.d.png") == "a/c.d", "only the last extension goes");
        }
    }
}
