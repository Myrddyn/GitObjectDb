using GitObjectDb.Models;
using GitObjectDb.Reflection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace GitObjectDb.Compare
{
    /// <summary>
    /// Represents a chunk change in a <see cref="IMetadataObject"/> while performing a merge.
    /// </summary>
    public class MetadataTreeMergeChunkChange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataTreeMergeChunkChange"/> class.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="mergeBaseNode">The merge base node.</param>
        /// <param name="branchNode">The branch node.</param>
        /// <param name="headNode">The head node.</param>
        /// <param name="property">The property.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentNullException">
        /// path
        /// or
        /// mergeBaseNode
        /// or
        /// branchNode
        /// or
        /// headNode
        /// or
        /// property
        /// or
        /// value
        /// </exception>
        public MetadataTreeMergeChunkChange(string path, JObject mergeBaseNode, JObject branchNode, JObject headNode, ModifiablePropertyInfo property, JToken value)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            MergeBaseNode = mergeBaseNode ?? throw new ArgumentNullException(nameof(mergeBaseNode));
            BranchNode = branchNode ?? throw new ArgumentNullException(nameof(branchNode));
            HeadNode = headNode ?? throw new ArgumentNullException(nameof(headNode));
            Property = property ?? throw new ArgumentNullException(nameof(property));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets the path.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the merge base node.
        /// </summary>
        public JObject MergeBaseNode { get; }

        /// <summary>
        /// Gets the branch node.
        /// </summary>
        public JObject BranchNode { get; }

        /// <summary>
        /// Gets the head node.
        /// </summary>
        public JObject HeadNode { get; }

        /// <summary>
        /// Gets the property.
        /// </summary>
        public ModifiablePropertyInfo Property { get; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public JToken Value { get; }
    }
}
