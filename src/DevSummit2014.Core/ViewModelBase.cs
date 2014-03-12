using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DevSummit2014.Core
{
    /// <summary>
    /// Base implementation for ViewModel.
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged 
    {
        private string _applicationTitle;
        private bool _isInitialized;

        public ViewModelBase()
        {
            var _ = InitializeAsync();
        }

        /// <summary>
        /// Gets the title for application
        /// </summary>
        public string ApplicationTitle
        {
            get { return _applicationTitle; }
            protected set
            {
                _applicationTitle = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Gets a value indicating whether ViewModel is initialized.
        /// </summary>
        public bool IsInitialized
        {
            get { return _isInitialized; }
            protected set
            {
                _isInitialized = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Initialized ViewModel.
		/// Override to implement custom initialization logic for ViewModel.
		/// </summary>
        protected virtual async Task InitializeAsync()
        {
            await InitializeAsync();
            IsInitialized = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
