namespace Cthangover.Tools.EntityEditor
{
    /// <summary>Optional subject validation runs before serialization, dependency changes or the first file write.</summary>
    public abstract partial class EntityEditorWindow
    {
        /// <summary>
        /// Derived editors may reject the current document and show their own field diagnostic. The default keeps
        /// existing editors unchanged; rejection must not write files, normalize input or add history entries.
        /// </summary>
        protected virtual bool ValidateForSave()
        {
            return true;
        }
    }
}
