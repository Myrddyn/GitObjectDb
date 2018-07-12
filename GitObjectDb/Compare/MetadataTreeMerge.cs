using DiffPatch;
using DiffPatch.Data;
using GitObjectDb.Attributes;
using GitObjectDb.Git;
using GitObjectDb.Models;
using GitObjectDb.Reflection;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GitObjectDb.Compare
{
    /// <inheritdoc/>
    [ExcludeFromGuardForNull]
    public sealed class MetadataTreeMerge : IMetadataTreeMerge
    {
        readonly IRepositoryProvider _repositoryProvider;
        readonly IModelDataAccessorProvider _modelDataProvider;
        readonly IInstanceLoader _instanceLoader;
        readonly RepositoryDescription _repositoryDescription;
        readonly Lazy<JsonSerializer> _serializer;

        ObjectId _branchTip;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataTreeMerge"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="repositoryDescription">The repository description.</param>
        /// <param name="commitId">The commit identifier.</param>
        /// <param name="branchName">Name of the branch.</param>
        /// <exception cref="ArgumentNullException">
        /// serviceProvider
        /// or
        /// repositoryDescription
        /// or
        /// commitId
        /// or
        /// branchName
        /// or
        /// merger
        /// </exception>
        public MetadataTreeMerge(IServiceProvider serviceProvider, RepositoryDescription repositoryDescription, ObjectId commitId, string branchName)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
            _repositoryDescription = repositoryDescription ?? throw new ArgumentNullException(nameof(repositoryDescription));
            CommitId = commitId ?? throw new ArgumentNullException(nameof(commitId));
            BranchName = branchName ?? throw new ArgumentNullException(nameof(branchName));

            _repositoryProvider = serviceProvider.GetRequiredService<IRepositoryProvider>();
            _modelDataProvider = serviceProvider.GetRequiredService<IModelDataAccessorProvider>();
            _instanceLoader = serviceProvider.GetRequiredService<IInstanceLoader>();
            _serializer = new Lazy<JsonSerializer>(() => _instanceLoader.GetJsonSerializer(ReturnEmptyChildren));

            Initialize();
        }

        /// <inheritdoc/>
        public ObjectId CommitId { get; }

        /// <inheritdoc/>
        public string BranchName { get; }

        /// <inheritdoc/>
        public IList<MetadataTreeMergeChunkChange> ModifiedChunks { get; } = new List<MetadataTreeMergeChunkChange>();

        static JObject GetContent(Commit mergeBase, string path, string branchInfo)
        {
            var content = mergeBase[path]?.Target?.Peel<Blob>()?.GetContentText() ??
                throw new NotImplementedException($"Could not find node {path} in {branchInfo} tree.");
            return JsonConvert.DeserializeObject<JObject>(content);
        }

        ILazyChildren ReturnEmptyChildren(Type parentType, string propertyName)
        {
            var childProperty = _modelDataProvider.Get(parentType).ChildProperties[propertyName];
            return LazyChildrenHelper.Create(childProperty, (o, r) => Enumerable.Empty<IMetadataObject>());
        }

        void Initialize()
        {
            _repositoryProvider.Execute(_repositoryDescription, repository =>
            {
                EnsureHeadCommit(repository);

                var branch = repository.Branches[BranchName];
                var branchTip = branch.Tip;
                _branchTip = branchTip.Id;
                var headTip = repository.Head.Tip;
                var baseCommit = repository.ObjectDatabase.FindMergeBase(headTip, branchTip);

                ComputeMerge(repository, baseCommit, branchTip, headTip);
            });
        }

        void EnsureHeadCommit(IRepository repository)
        {
            if (!repository.Head.Tip.Id.Equals(CommitId))
            {
                throw new NotSupportedException("The current head commit id is different from the commit used by current instance.");
            }
        }

        void ComputeMerge(IRepository repository, Commit mergeBase, Commit branchTip, Commit headTip)
        {
            var branchChanges = repository.Diff.Compare<Patch>(mergeBase.Tree, branchTip.Tree);
            var headChanges = repository.Diff.Compare<Patch>(mergeBase.Tree, headTip.Tree);
            foreach (var change in branchChanges)
            {
                switch (change.Status)
                {
                    case ChangeKind.Modified:
                        var mergeBaseObject = GetContent(mergeBase, change.Path, "merge base");
                        var branchObject = GetContent(branchTip, change.Path, "branch tip");
                        var headObject = GetContent(headTip, change.Path, "head tip");

                        AddModifiedChunks(change, mergeBaseObject, branchObject, headObject, headChanges);
                        break;
                    case ChangeKind.Added:
                    case ChangeKind.Deleted:
                    default:
                        throw new NotImplementedException("Deletion for branch merge is not supported.");
                }
            }
        }

        void AddModifiedChunks(PatchEntryChanges branchChange, JObject mergeBaseObject, JObject newObject, JObject headObject, Patch headChanges)
        {
            var headChange = headChanges[branchChange.Path];
            if (headChange?.Status == ChangeKind.Deleted)
            {
                throw new NotImplementedException($"Conflict as a modified node {branchChange.Path} in merge branch source has been deleted in head.");
            }
            var type = Type.GetType(mergeBaseObject.Value<string>("$type"));
            var properties = _modelDataProvider.Get(type).ModifiableProperties;

            JToken headValue = null;
            ModifiablePropertyInfo p = null;
            var changes = from kvp in (IEnumerable<KeyValuePair<string, JToken>>)newObject
                          where properties.TryGetValue(kvp.Key, out p)
                          let mergeBaseValue = mergeBaseObject[kvp.Key]
                          where mergeBaseValue == null || !JToken.DeepEquals(kvp.Value, mergeBaseValue)
                          where headObject.TryGetValue(kvp.Key, StringComparison.OrdinalIgnoreCase, out headValue) || ((headValue = null) == null)
                          select new MetadataTreeMergeChunkChange(branchChange.Path, mergeBaseObject, newObject, headObject, p, mergeBaseValue, kvp.Value, headValue);

            foreach (var modifiedProperty in changes)
            {
                ModifiedChunks.Add(modifiedProperty);
            }
        }

        /// <inheritdoc/>
        public Commit Apply(Signature merger)
        {
            if (merger == null)
            {
                throw new ArgumentNullException(nameof(merger));
            }
            var remainingConflicts = ModifiedChunks.Where(c => c.IsInConflict).ToList();
            if (remainingConflicts.Any())
            {
                throw new RemainingConflictsException(remainingConflicts);
            }

            return _repositoryProvider.Execute(_repositoryDescription, repository =>
            {
                EnsureHeadCommit(repository);
                var branch = repository.Branches[BranchName];
                var branchTip = branch.Tip;
                if (branchTip.Id != _branchTip)
                {
                    throw new NotImplementedException($"The branch {branch.FriendlyName} has changed since merge started.");
                }
                var treeDefinition = CreateTree(repository);
                var message = $"Merge branch {branch.FriendlyName} into {repository.Head.FriendlyName}";
                return repository.Commit(treeDefinition, message, merger, merger, mergeParent: branchTip);
            });
        }

        TreeDefinition CreateTree(IRepository repository)
        {
            var definition = TreeDefinition.From(repository.Head.Tip);
            var buffer = new StringBuilder();
            foreach (var change in ModifiedChunks.GroupBy(c => c.HeadNode))
            {
                var modified = (JObject)change.Key.DeepClone();
                foreach (var chunkChange in change)
                {
                    chunkChange.ApplyTo(modified);
                }
                Serialize(modified, buffer);
                definition.Add(change.First().Path, repository.CreateBlob(buffer), Mode.NonExecutableFile);
            }
            return definition;
        }

        void Serialize(JToken modified, StringBuilder buffer)
        {
            buffer.Clear();
            using (var writer = new StringWriter(buffer))
            {
                _serializer.Value.Serialize(writer, modified);
            }
        }
    }
}
