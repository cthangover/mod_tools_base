namespace Cthangover.Tools.Contracts
{
    using System.Collections.Generic;

    /// <summary>
    /// Routes palette insertions to the most recently activated eligible editor.
    /// Editors remove themselves on tree exit; no closed window or unloaded mod is retained.
    /// </summary>
    public static class ScenarioTextTargets
    {
        private static readonly List<IScenarioTextTarget> Targets = new();

        /// <summary>The last activated writable document, or null when no scenario is open.</summary>
        public static IScenarioTextTarget Current
        {
            get
            {
                for (var i = Targets.Count - 1; i >= 0; i--)
                    if (Targets[i].CanInsert)
                        return Targets[i];
                return null;
            }
        }

        /// <summary>Registers or promotes an editor after a document selection or focus change.</summary>
        public static void Activate(IScenarioTextTarget target)
        {
            if (target == null)
                return;
            Targets.Remove(target);
            Targets.Add(target);
        }

        /// <summary>Releases an editor before its node or assembly leaves the session.</summary>
        public static void Unregister(IScenarioTextTarget target) => Targets.Remove(target);
    }
}
