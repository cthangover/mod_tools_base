namespace Cthangover.Tools.Tests
{
    using Cthangover.Tools.Contracts;

    /// <summary>A document double for routing tests without Godot nodes.</summary>
    public class ScenarioTextTargetFixture : IScenarioTextTarget
    {
        public bool CanInsert { get; set; } = true;
        public string DocumentKey => "test:scenario";
        public string Inserted { get; private set; }
        public void InsertLine(string line) => Inserted = line;
    }
}
