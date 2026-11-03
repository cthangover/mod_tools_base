namespace Cthangover.Tools.EntityEditor
{
    /// <summary>What <see cref="ModDependencies"/> did about one provider mod.</summary>
    public enum ModDependencyOutcome
    {
        /// <summary>The dependency was written into the own mod's manifest.</summary>
        Added,

        /// <summary>The provider is already in the own mod's transitive <c>depends</c>; nothing to do.</summary>
        Present,

        /// <summary>The provider is the own mod itself; a mod never depends on itself.</summary>
        Self,

        /// <summary>The provider depends on the own mod (directly or not): a dependency would make a cycle.</summary>
        Cycle,

        /// <summary>The manifest cannot be read, is not JSON or has no folder to write into.</summary>
        NotWritable,
    }
}
