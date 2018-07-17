using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using GitObjectDb.Models;

namespace GitObjectDb.Git.Hooks
{
    /// <summary>
    /// Allows listeners to subscribe to Git events.
    /// </summary>
    public class GitHooks
    {
        /// <summary>
        /// Occurs when a commit is about to be made.
        /// </summary>
        public event EventHandler<PreCommitEventArgs> CommitStarted;

        /// <summary>
        /// Called when a commit is about to be started.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The <see cref="CancelEventArgs"/>.</returns>
        internal bool OnCommitStarted(IInstance old, IInstance @new, string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var args = new PreCommitEventArgs(message);
            CommitStarted?.Invoke(this, args);
            return !args.Cancel;
        }
    }
}
