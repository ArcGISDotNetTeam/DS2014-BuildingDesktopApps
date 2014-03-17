using DevSummit2014.Core;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Tasks.Query;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DesktopCreateMap
{
	// Creating a Map in ViewModel with layers and binding that to MapView while handling initialization errors 
	//
	// 1 ) Create a Map and set initial extent for the MapView
	// 2 ) Create a tiled layer using ArcGIS Online basemap
	//		- Creating new instance of online based tile layer
	//		- Calling InitializeAsync and wait that it's completed before setting into a Map
	//		- Handling possible initialization errors with Try - Catch block
	// 3 ) Create operational layers 
	//		- Using FeatureLayers with GeodatabaseFeatureServiceTable
	//		- Creating new GeodatabseFeatureServiceTable with OpenAsync() factory method initialized the table
	//		- Initialization errors handled by parent Try - Catch block
	// 4 ) Showing Map loading indicator and removing that when map is loaded

    public class MainViewModel : ViewModelBase
    {
        private Map _map;

        public MainViewModel() : base()
        {
            ApplicationTitle = "Create map using MVVM.";
        }

        /// <summary>
        /// Gets or sets the Map.
        /// </summary>
        public Map Map
        {
            get
            {
                return _map;
            }
            set
            {
                _map = value;
                NotifyPropertyChanged();
            }
        }

		protected override async Task InitializeAsync()
        {
            // Create map with layers and set startup location
            var map = new Map()
                {
                    InitialExtent = new Envelope(-13636132.3698584, 4546349.82732426, -13633579.1021618, 4547513.1599185, SpatialReferences.WebMercator)
                };
            Map = map;

            // Basemap layer from ArcGIS Online hosted service
            var basemap = new ArcGISTiledMapServiceLayer()
            {
                ID = "Basemap",
                DisplayName = "Basemap",
				ServiceUri = "http://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer"
            };

            // Initialize layer in Try - Catch 
            Exception exceptionToHandle = null;
            try
            {
                await basemap.InitializeAsync();
                map.Layers.Add(basemap);
                await CreateOperationalLayersAsync();
				IsInitialized = true;
            }
            catch (Exception exception)
            {
                // Exception is thrown ie if layer url is not found - ie. {"Error code '400' : 'Invalid URL'"} 
                exceptionToHandle = exception;
            }

            if (exceptionToHandle != null)
            {
                // Initialization failed, show message and return
                await MessageService.Instance.ShowMessage(string.Format(
                    "Could not create basemap. Error = {0}", exceptionToHandle.ToString()), 
                    "An error occured");
                return;
            }
        }

        private async Task CreateOperationalLayersAsync()
        {
            var operationalLayers = new List<Layer>();

            var wildfirePoints = await CreateFeatureLayerAsync(
               "http://sampleserver6.arcgisonline.com/arcgis/rest/services/Sync/WildfireSync/FeatureServer/0",
               "WildfirePoints",
               "Wildfire - points");

            Map.Layers.Add(wildfirePoints);

            var wildfireLines = await CreateFeatureLayerAsync(
               "http://sampleserver6.arcgisonline.com/arcgis/rest/services/Sync/WildfireSync/FeatureServer/1",
               "WildfireLines",
               "Wildfire - lines");

            Map.Layers.Add(wildfireLines);

            var wildfirePolygons = await CreateFeatureLayerAsync(
               "http://sampleserver6.arcgisonline.com/arcgis/rest/services/Sync/WildfireSync/FeatureServer/2",
               "WildfirePolygons",
               "Wildfire - polygons");

            Map.Layers.Add(wildfirePolygons);
        }

		/// <summary>
		/// Helper method to create individual FeatureLayers
		/// </summary>
        private async Task<FeatureLayer> CreateFeatureLayerAsync(string uri, string name, string displayName)
        {
            FeatureLayer layer = null;

            // Creates and initializes the layer
            var gdbFeatureServiceTable = await GeodatabaseFeatureServiceTable.OpenAsync(new Uri(uri));
                  
            if (gdbFeatureServiceTable.IsInitialized)
            {
                layer = new FeatureLayer()
                {
                    ID = name,
                    DisplayName = displayName,
                    FeatureTable = gdbFeatureServiceTable
                };
            }

            return layer;
        }
    }
}
