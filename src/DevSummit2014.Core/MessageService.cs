using System;
using System.Threading.Tasks;
using System.Windows;

namespace DevSummit2014.Core
{
    public class MessageService
    {
        // Lazy initialized MessageService
        private static readonly Lazy<MessageService> lazy = new Lazy<MessageService>(() => new MessageService());

        /// <summary>
        /// Gets singleton instance of MessageService to show messages.
        /// </summary>
        public static MessageService Instance { get { return lazy.Value; } }

        /// <summary>
        /// Show message.
        /// </summary>
        public Task<bool> ShowMessage(string message)
        {
            var result = MessageBox.Show(message);
            return Task.FromResult(false);
        }

        /// <summary>
        /// Show message with title.
        /// </summary>
        public Task<bool> ShowMessage(string message, string title)
        {
            var result = MessageBox.Show(
                message,
                title, MessageBoxButton.OK);
            return Task.FromResult(false);
        }
    }
}
