using GitObjectDb.Git;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitObjectDb.Models
{
    public partial class AbstractInstance
    {
        /// <inheritdoc />
        public Commit Commit(AbstractInstance newInstance, Signature signature, string message, CommitOptions options = null)
        {
            if (newInstance == null)
            {
                throw new ArgumentNullException(nameof(newInstance));
            }
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return _repositoryProvider.Execute(_repositoryDescription, repository =>
            {
                if (!repository.Head.Tip.Id.Equals(CommitId))
                {
                    throw new NotSupportedException("The current head commit id is different from the commit used by current instance.");
                }

                var computeChanges = _computeTreeChangesFactory(_repositoryDescription);
                var changes = computeChanges.Compare(this, newInstance);
                return changes.AnyChange ?
                    repository.Commit(_hooks, changes.NewTree, message, signature, signature, options) :
                    null;
            });
        }
    }
}
