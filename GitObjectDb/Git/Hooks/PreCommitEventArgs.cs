using System;
using System.ComponentModel;

namespace GitObjectDb.Git.Hooks
{
    /// <summary>
    /// Provides data for a pre-commit event.
    /// </summary>
    /// <seealso cref="System.ComponentModel.CancelEventArgs" />
    public class PreCommitEventArgs : CancelEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PreCommitEventArgs"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <exception cref="ArgumentNullException">message</exception>
        public PreCommitEventArgs(string message)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>
        /// Gets the message.
        /// </summary>
        public string Message { get; }
    }
}
