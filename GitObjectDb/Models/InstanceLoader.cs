using GitObjectDb.Git;
using GitObjectDb.JsonConverters;
using GitObjectDb.Reflection;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitObjectDb.Models
{
    /// <inheritdoc />
    internal class InstanceLoader : IInstanceLoader
    {
        /// <summary>
        /// The data file name used to store information in Git.
        /// </summary>
        internal const string DataFile = "data.json";

        readonly IContractResolver _contractResolver = new DefaultContractResolver();
        readonly IServiceProvider _serviceProvider;
        readonly IModelDataAccessorProvider _dataAccessorProvider;
        readonly IRepositoryProvider _repositoryProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="InstanceLoader"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        public InstanceLoader(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _dataAccessorProvider = _serviceProvider.GetRequiredService<IModelDataAccessorProvider>();
            _repositoryProvider = _serviceProvider.GetRequiredService<IRepositoryProvider>();
        }

        /// <inheritdoc />
        public AbstractInstance LoadFrom(RepositoryDescription repositoryDescription, ObjectId commitId = null)
        {
            if (repositoryDescription == null)
            {
                throw new ArgumentNullException(nameof(repositoryDescription));
            }

            return _repositoryProvider.Execute(repositoryDescription, repository =>
            {
                Commit currentCommit;
                if (commitId == null)
                {
                    currentCommit = repository.Head.Tip;
                    commitId = currentCommit.Id;
                }
                else
                {
                    currentCommit = repository.Lookup<Commit>(commitId);
                }

                var instance = (AbstractInstance)LoadEntry(commitId, currentCommit[DataFile], string.Empty);
                instance.SetRepositoryData(repositoryDescription, commitId);
                return instance;
            });
        }

        /// <inheritdoc />
        public TInstance LoadFrom<TInstance>(RepositoryDescription repositoryDescription, ObjectId commitId = null)
            where TInstance : AbstractInstance
        {
            return (TInstance)LoadFrom(repositoryDescription, commitId);
        }

        IMetadataObject LoadEntry(ObjectId commitId, TreeEntry entry, string path)
        {
            var blob = entry.Target.Peel<Blob>();
            ILazyChildren ResolveChildren(Type type, string propertyName)
            {
                var dataAccessor = _dataAccessorProvider.Get(type);
                if (!dataAccessor.ChildProperties.TryGetValue(propertyName, out var childProperty))
                {
                    throw new NotSupportedException($"Unable to find property details for '{propertyName}'.");
                }
                return LoadEntryChildren(commitId, path, childProperty);
            }
            var serializer = GetJsonSerializer(ResolveChildren);
            var jobject = blob.GetContentStream().ToJson<JObject>(serializer);
            var objectType = Type.GetType(jobject.Value<string>("$type"));
            return (IMetadataObject)jobject.ToObject(objectType, serializer);
        }

        /// <inheritdoc />
        public JsonSerializer GetJsonSerializer(ChildrenResolver childrenResolver)
        {
            var serializer = new JsonSerializer
            {
                TypeNameHandling = TypeNameHandling.Objects,
                Formatting = Formatting.Indented
            };
            serializer.Converters.Add(new MetadataObjectJsonConverter(_serviceProvider, childrenResolver));

            // Optimization: prevent reflection for each new object!
            serializer.ContractResolver = _contractResolver;

            return serializer;
        }

        ILazyChildren LoadEntryChildren(ObjectId commitId, string path, ChildPropertyInfo childProperty) =>
            LazyChildrenHelper.Create(childProperty, (parent, repository) =>
            {
                var childPath = string.IsNullOrEmpty(path) ? childProperty.Name : $"{path}/{childProperty.Name}";
                var commit = repository.Lookup<Commit>(commitId);
                var subTree = commit[childPath]?.Target.Peel<Tree>();
                return (subTree?.Any() ?? false) ?

                    from c in subTree
                    where c.TargetType == TreeEntryTargetType.Tree
                    let childTree = c.Target.Peel<Tree>()
                    let data = childTree[DataFile]
                    where data != null
                    select LoadEntry(commitId, data, $"{childPath}/{c.Name}") :

                    Enumerable.Empty<IMetadataObject>();
            });
    }
}
