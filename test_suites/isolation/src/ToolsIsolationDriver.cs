namespace Cthangover.Tools.Tests.Isolation
{
    using System;
    using System.Linq;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods;
    using Cthangover.Core.UI.Tool;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Tools.Contracts;
    using Godot;

    /// <summary>Exercises tools through their public contracts without depending on a concrete editor assembly.</summary>
    public partial class ToolsIsolationDriver : Node
    {
        private double elapsed;
        private double next;
        private int passed;
        private int failed;
        private int index = -1;
        private IToolProvider[] tools;
        private Window window;
        private bool finished;

        public override void _Process(double delta)
        {
            if (finished)
                return;
            elapsed += delta;
            if (elapsed > 60)
            {
                Check(false, "isolation watchdog");
                Finish();
                return;
            }
            if (!Boot.IsReady || elapsed < next)
                return;
            next = elapsed + 0.3;
            try
            {
                Step();
            }
            catch (Exception ex)
            {
                Check(false, ex.ToString());
                Finish();
            }
        }

        private void Step()
        {
            if (index < 0)
            {
                tools = ToolFactory.Instance.GetAll().OrderBy(t => t.Id, StringComparer.Ordinal).ToArray();
                Check(string.Join(",", tools.Select(t => t.Id)) == string.Join(",", Argument("--expected-tools=").Split(',', StringSplitOptions.RemoveEmptyEntries).OrderBy(t => t, StringComparer.Ordinal)), "only selected tools are registered");
                foreach (var id in Argument("--absent-mods=").Split(',', StringSplitOptions.RemoveEmptyEntries))
                    Check(!ModRegistry.Instance.Mods.ContainsKey(id), "mod absent: " + id);
                var buttons = ToolBoxButtonFactory.Instance.GetVisible();
                foreach (var tool in tools)
                    Check(buttons.Any(b => b.ToolId == tool.Id), "toolbar entry: " + tool.Id);
                index = 0;
            }
            if (window != null)
            {
                Check(window.IsInsideTree() && window.Visible, "window opens: " + tools[index - 1].Id);
                window.Free();
                window = null;
                Check(ScenarioTextTargets.Current == null, "closed window releases insertion target");
            }
            if (index >= tools.Length)
            {
                Finish();
                return;
            }
            var provider = tools[index++];
            window = ToolWindow.ShowWindow(provider.CreateWindow());
            if (window is IScenarioResourceEditor editor && OS.GetCmdlineUserArgs().Contains("--exercise-scenario-contract"))
            {
                editor.OpenScenario("my_novel_game", "scenarios/my_novel_town.scenario");
                var target = ScenarioTextTargets.Current;
                Check(target != null, "resource contract selects an insertion target");
                var code = window.FindChildren("*", "", true, false).OfType<CodeEdit>().First();
                var original = code.Text;
                target.InsertLine("# isolation insertion");
                Check(code.Text.Contains("# isolation insertion"), "contract inserts into the open buffer");
                code.Undo();
                Check(code.Text == original, "one undo removes the insertion");
            }
        }

        private static string Argument(string prefix)
            => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal))?.Substring(prefix.Length) ?? "";

        private void Check(bool value, string message)
        {
            if (value) passed++; else failed++;
            GameLogger.Log("TEST", (value ? "[PASS] " : "[FAIL] ") + message);
        }

        private void Finish()
        {
            finished = true;
            if (window != null && IsInstanceValid(window))
                window.Free();
            GameLogger.Log("TEST", $"[TEST_RESULT] name=tools_isolation passed={passed} failed={failed}");
            Cthangover.Core.Bootstrap.ApplicationExit.Quit(GetTree(), failed == 0 ? 0 : 1);
        }
    }
}
