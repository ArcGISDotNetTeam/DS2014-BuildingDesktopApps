using DevSummit2014.Core;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.WebMap;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CreateWebMap
{
	// Creating simple basemap swicher using ArcGIS Portal

	// 1) Creating new instance of ArcGIS portal that links to ArcGIS Online
	// 2) Querying all basemaps that are defined as in basemap gallery
	// 3) Showing list of items to the user in a list
	//		- Using ArcGISPortalItem to load metadata to items
	// 4) Selecting first one to the map and load it
	//		- Loading selected WebMap from ArcGISPortalItems 
	//		- Loading WebMapViewModel from WebMap and provide Map to the MapView

    public class MainViewModel : ViewModelBase
    {
        private WebMapViewModel _webMapViewModel;
        private ObservableCollection<ArcGISPortalItem> _basemaps;
        private ArcGISPortalItem _selectedBaseMap;
        private ArcGISPortal _portal;

        public MainViewModel()
        {
            ApplicationTitle = "Create a WebMap using MVVM.";
        }

        /// <summary>
        /// Gets or sets the list of basemap items.
        /// </summary>
        public ObservableCollection<ArcGISPortalItem> Basemaps
        {
            get { return _basemaps; }
            set
            {
                _basemaps = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or set the selected basemap. When changed, invokes changing basemap behavior.
        /// </summary>
        public ArcGISPortalItem SelectedBasemap
        {
            get { return _selectedBaseMap; }
            set
            {
                _selectedBaseMap = value;
                
                if (_selectedBaseMap != null)
				{
					ChangeBasemap(_selectedBaseMap);
                }

                NotifyPropertyChanged();
            }
        }
       
        /// <summary>
        /// Gets or sets the WebMapViewModel.
        /// </summary>
        public WebMapViewModel WebMapViewModel
        {
            get
            {
                return _webMapViewModel;
            }
            set
            {
                _webMapViewModel = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Initialize application. Load basemaps from ArcGISPortal and selecte first one.
        /// </summary>
        protected override async Task InitializeAsync()
        {
			Exception exceptionToHandle = null;
			try
			{
				// Store portal instance for later use. Default creates ArcGIS Online portal.
				_portal = await ArcGISPortal.CreateAsync();

				// Get all basemap items that are linked to your portal. Remember that this is dynamic data.
				var items = await _portal.ArcGISPortalInfo.SearchBasemapGalleryAsync();
				Basemaps = new ObservableCollection<ArcGISPortalItem>(items.Results);

				// Load first basemap on initialization
				var webmap = await WebMap.FromPortalItemAsync(Basemaps[0]);
				WebMapViewModel = await WebMapViewModel.LoadAsync(webmap, _portal);
				IsInitialized = true;
			}
			catch (Exception exception)
			{
				exceptionToHandle = exception;
			}

			if (exceptionToHandle != null)
			{
				await MessageService.Instance.ShowMessage(string.Format(
					"Initialization failed. Error = {0}", exceptionToHandle.ToString()),
					"An error occured");
			}
        }

        /// <summary>
        /// Changes used basemap.
        /// </summary>
        /// <param name="item">Item to load</param>
        private async void ChangeBasemap(ArcGISPortalItem item)
        {
            Exception exceptionToHandle =null;
            try
            {
				var webmap = await WebMap.FromPortalItemAsync(item);
				WebMapViewModel = await WebMapViewModel.LoadAsync(webmap, _portal);
			}
            catch (Exception exception)
            {
                exceptionToHandle = exception;
            }

            if (exceptionToHandle != null)
            {
                await MessageService.Instance.ShowMessage(string.Format(
                    "Could not create basemap. Error = {0}", exceptionToHandle.ToString()),
                    "An error occured");
            }
        }
    }
}
