using GitObjectDb.Compare;
using GitObjectDb.Models;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace GitObjectDb.Migrations
{
    /// <summary>
    /// Scaffolds migrations to apply pending model changes.
    /// </summary>
    public class MigrationScaffolder
    {
        readonly JsonSerializer _serializer;
        readonly IRepository _repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationScaffolder"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="repository">The repository.</param>
        /// <exception cref="ArgumentNullException">
        /// serviceProvider
        /// or
        /// repository
        /// </exception>
        public MigrationScaffolder(IServiceProvider serviceProvider, IRepository repository)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _serializer = serviceProvider.GetRequiredService<IInstanceLoader>().GetJsonSerializer();
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Scaffolds a code based migration to apply any pending model changes to the database.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The <see cref="Migrator"/> used to apply migrations.</returns>
        public IImmutableList<Migrator> Scaffold(ObjectId start, ObjectId end, MigrationMode mode)
        {
            var log = _repository.Commits.QueryBy(Migration.GitPath, new CommitFilter
            {
                IncludeReachableFrom = start
            });
            var deferred = new List<IMigration>();
            var result = ImmutableList.CreateBuilder<Migrator>();
            result.AddRange(GetLogMigrators(log, deferred, end, mode));
            if (deferred.Any())
            {
                var uniqueDeferredMigrations = deferred.Distinct(MetadataObjectIdComparer<IMigration>.Instance);
                result.Add(new Migrator(uniqueDeferredMigrations.ToImmutableList(), mode));
            }
            return result.ToImmutable();
        }

        IEnumerable<Migrator> GetLogMigrators(IEnumerable<LogEntry> log, List<IMigration> deferred, ObjectId end, MigrationMode mode)
        {
            Commit previousCommit = default;
            foreach (var entry in log)
            {
                var commit = entry.Commit;
                if (previousCommit != null)
                {
                    var migrations = GetCommitMigrations(previousCommit, commit).ToList();

                    deferred.AddRange(migrations.Where(m => m.IsIdempotent));

                    migrations.RemoveAll(m => !m.IsIdempotent);
                    if (migrations.Any(m => !m.IsIdempotent))
                    {
                        yield return new Migrator(migrations.Where(m => !m.IsIdempotent).ToImmutableList(), mode, commit.Id);
                    }
                }
                if (commit.Id == end)
                {
                    break;
                }
                previousCommit = commit;
            }
        }

        IEnumerable<IMigration> GetCommitMigrations(Commit previousCommit, Commit commit)
        {
            using (var changes = _repository.Diff.Compare<TreeChanges>(previousCommit.Tree, commit.Tree, new[] { AbstractInstance.MigrationFolder }))
            {
                foreach (var change in changes.Where(c => c.Status == ChangeKind.Added || c.Status == ChangeKind.Modified))
                {
                    var blob = commit[change.Path].Target.Peel<Blob>();
                    var jobject = blob.GetContentStream().ToJson<JObject>(_serializer);
                    var objectType = Type.GetType(jobject.Value<string>("$type"));
                    yield return (IMigration)jobject.ToObject(objectType, _serializer);
                }
            }
        }
    }
}
