namespace Cthangover.Tools.Contracts
{
    /// <summary>Optional capability of a tool window that opens a mod's scenario resource.</summary>
    public interface IScenarioResourceEditor
    {
        /// <summary>Opens a mod-relative scenario, deferring selection until the window is ready if necessary.</summary>
        void OpenScenario(string modId, string relativePath);
    }
}
