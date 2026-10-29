namespace Cthangover.Tools.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Tools.Contracts;

    /// <summary>Insertion follows focus and excludes closed or non-text documents.</summary>
    public class ScenarioTextTargetsTests
    {
        [ModTest("tools/scenario_target_focus_and_lifetime")]
        static void FocusAndLifetime(ModTestContext ctx)
        {
            var first = new ScenarioTextTargetFixture();
            var second = new ScenarioTextTargetFixture();
            try
            {
                ScenarioTextTargets.Activate(first);
                ScenarioTextTargets.Activate(second);
                ScenarioTextTargets.Current.InsertLine("second line");
                ModAssert.Equal("second line", second.Inserted, "last focused editor receives insertion");
                ModAssert.Null(first.Inserted, "other editor is unchanged");
                second.CanInsert = false;
                ScenarioTextTargets.Current.InsertLine("first line");
                ModAssert.Equal("first line", first.Inserted, "a graph tab does not receive insertion");
                second.CanInsert = true;
                ScenarioTextTargets.Activate(first);
                ModAssert.True(ReferenceEquals(first, ScenarioTextTargets.Current), "focus changes target");
                ScenarioTextTargets.Unregister(first);
                ModAssert.True(ReferenceEquals(second, ScenarioTextTargets.Current), "closing releases target");
                ScenarioTextTargets.Unregister(second);
                ModAssert.False(ReferenceEquals(second, ScenarioTextTargets.Current), "closed editor is not retained");
            }
            finally
            {
                ScenarioTextTargets.Unregister(first);
                ScenarioTextTargets.Unregister(second);
            }
        }
    }
}
