using System;
using Cthangover.Core.Mods.Resolvers;

namespace Cthangover.Tools.Services
{
    /// <summary>Finds a scene background without depending on scenario parsing or editor windows.</summary>
    public static class SceneBackgroundService
    {
        /// <summary>Default background ID of a logical scene, or <c>null</c>.</summary>
        public static string ForScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || sceneName == "-")
                return null;

            var defs = SceneDefinitions.Collect();
            if (defs == null)
                return null;

            foreach (var pair in defs)
            {
                if (!string.Equals(pair.Key, sceneName, StringComparison.OrdinalIgnoreCase) || pair.Value == null)
                    continue;

                // Several mods may define the scene; the last one wins at runtime.
                for (var i = pair.Value.Count - 1; i >= 0; i--)
                {
                    if (pair.Value[i]?.DefaultBackground is { Count: > 0 } list)
                        return list[0];
                }
            }

            return null;
        }

    }
}
