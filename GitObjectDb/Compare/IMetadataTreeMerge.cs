using GitObjectDb.Models;
using GitObjectDb.Reflection;
using LibGit2Sharp;
using System.Collections.Generic;

namespace GitObjectDb.Compare
{
    /// <summary>
    /// Provides the ability to merge changes between two branches.
    /// </summary>
    public interface IMetadataTreeMerge
    {
        /// <summary>
        /// Gets the commit identifier.
        /// </summary>
        ObjectId CommitId { get; }

        /// <summary>
        /// Gets the name of the merge source branch.
        /// </summary>
        string BranchName { get; }

        /// <summary>
        /// Gets the modified chunks.
        /// </summary>
        IList<MetadataTreeMergeChunkChange> ModifiedChunks { get; }

        /// <summary>
        /// Applies the changes in the repository.
        /// </summary>
        /// <param name="merger">The merger.</param>
        /// <returns>The merge commit.</returns>
        Commit Apply(Signature merger);
    }
}