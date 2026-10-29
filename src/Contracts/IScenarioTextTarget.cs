namespace Cthangover.Tools.Contracts
{
    /// <summary>A live scenario document that can accept one undoable line from another tool.</summary>
    public interface IScenarioTextTarget
    {
        /// <summary>False for a graph, closed document or an editor that is being removed.</summary>
        bool CanInsert { get; }

        /// <summary>Address of the active document for the insertion receipt.</summary>
        string DocumentKey { get; }

        /// <summary>Inserts below the caret as a single undo operation.</summary>
        void InsertLine(string line);
    }
}
