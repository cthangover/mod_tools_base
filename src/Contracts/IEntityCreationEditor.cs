namespace Cthangover.Tools.Contracts
{
    /// <summary>Allows a calling tool to start an entity form in its own content mod without depending on that editor.</summary>
    public interface IEntityCreationEditor
    {
        /// <summary>Opens the requested mod and selects a new unsaved entity in its form.</summary>
        void CreateNewInMod(string mod);
    }
}
