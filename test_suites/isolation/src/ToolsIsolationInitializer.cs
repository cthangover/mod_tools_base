namespace Cthangover.Tools.Tests.Isolation
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Godot;

    /// <summary>Activates the isolation driver only for its integration-test argument.</summary>
    public class ToolsIsolationInitializer : IModInitializer
    {
        private static bool queued;
        public void OnModLoaded(string modId) { }
        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains("--itest=tools_isolation"))
                return;
            queued = true;
            ModRoot.AddDeferred(new ToolsIsolationDriver());
        }
    }
}
