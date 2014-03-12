using DevSummit2014.Core;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Query;
using Esri.ArcGISRuntime.Toolkit.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Editing
{
	// Editing offline geodatabase 
	//
	// 1 ) Editor is created and boudn to MapView to control editing
	//		- progress indication is provided by IProgress<GeometryEditStatus> that is given to editor when needed
	// 2 ) Creates Map layers from local geodatabase dynamically
	//		- Create basemap using ArcGIS Online basemaps
	//		- Create new instance of local Geodatabase
	//		- Loop through all layers in the geodatabase and create FeatureLayers from them
	// 3 ) Add new features using TemplatePicker control toolkit
	//		- TemplatePicker bound layers to construct available features types
	//		- When user selects new FeatureTemplate from UI, AddFeatureCommand is executed
	//		- AddFeature
	//			- Get symbol for editor
	//			- Define what kind of geometry we want to create
	//				- Note hack done here! This is done due definitions done when geodatabase is created.
	//				  This is a reminder how carelessness when creating geodatabase / FeatureService definitions can 
	//				  affect programs using those. In this case, every type of geometries request freehand drawing and we
	//				  want to provide correct editing tools for the user.
	//			- Request new geometry by using Editor.RequestShapeAsync, this awaits until user either cancel or accepts new feature
	//			  all messages are handled by the given IProgress<GeometryEditStatus>
	//			- When request is completed, we get new geometry from the Editor
	//			- Create new feature by using schema of the selected Feature type
	//			- Give created geometry to the new feature
	//			- Create a new feature (row) to GeodatabaseFeatureTable
 	//			- One difference between online and offline editing is that when working with GeodatabaseFeatureServiceTable, 
	//			  we need manually call ApplyEdits that sends the changes/adds/deletes to the service. When using offline tables, 
	//			  changes are automatically saved into geodatabase used.
	//			- Catching TaskCancellationException is crucial when using Editor because that is thrown when editing is cancelled.
	//	4 ) Selecting features when clicking or tapping MapView
	//		- When user Taps or clicks MapView, SelectFeatureCommand is called
	//		- If Editor is active (we are requesting new geometry or editing existing) we don't select features
	//		- First all previous selections are cleared
	//		- Since we are looking only one feature to select, going trough all layers in reverse order to get the topmost features
	//		- Features are looking from the layer by using hit testing.
	//			- HitTesting is abstracted from the ViewModel by using MapViewService helper class
	//			- Since we are doing hit testing against actuall layers on the MapView and relative to it's location,
	//			  we need to have reference to the MapView, that is set to MapViewService using attached property.
	//			- This way we can get features from the layers without violating used MVVM pattern. 
	//		- Found features are set as Selected by using layers SelectFeatures method
	//			- Note that GeodatabaseFeature doesn't have IsSelected proprerty like Grahics
	//		- SelectedFeature is set to provide selected feature information to FeatureDataForm  
	//		- layer where SelectedFeature is located is also stored to since saving changes goes thrugh layer
	//	5 ) Editing selected features
	//		- When SelectedFeature is set, ui shows FeatureDataForm to see and edit features attributes
	//			- Editing attrbutes
	//			- FeatureDataForm handles attribute definitions in a copy that is set to bound feature when Apply is cliked,
	//			  if it is cancelled, changes are not copied to original
	//			- When editing is done and Apply is clicked, SaveFeatureCommand is called
	//				- Feature is updated to used GeodatabaseFeatureTable by using UpdateAsync 
	//		- When SelectedFeature is set, ui shows button to edit current geometry 
	//			- Editing is done by using Editor and requesting EditGeometryAsync when it is line or polyon,
	//			  if it is point then just requesting new point RequestPointAsync
	//			- Editor returns edited geometry and it isn't automatically updated to the SelectedFeature
	//			- Feature is updated as soon editing is completed by using UpdateAsync
	// 6 ) Deleting selected feature
	//		- When SelectedFeature is set, ui shows button to delete it
	//		- When it is clicked, RemoveFeatureCommand is called
	//		- Feature is deleted from the it's GeodatabaseFeatureTable by calling DeleteAsync
	//
	//	NOTE that this sample work with online FeatureServices by just creating FeatureLayers using GeodatabaseFeatureServiceTables.

    public class MainViewModel : ViewModelBase
    {
        private Map _map;
        private Editor _editor;
        private FeatureLayer _selectedLayer;
        private GeodatabaseFeature _selectedFeature;
        
        private IProgress<GeometryEditStatus> _editingProgress;
        private MapViewService _mapViewService;

        // Commands
        private RelayCommand<TemplatePicker.TemplatePickedEventArgs> _addFeatureCommand;
        private RelayCommand<MapViewInputEventArgs> _selectFeatureCommand;
        private RelayCommand<GeodatabaseFeature> _editFeatureCommand;
        private RelayCommand<GeodatabaseFeature> _removeFeatureCommand;
        private RelayCommand<EventArgs> _saveFeatureCommand;

        public MainViewModel()
            : base()
        {
            ApplicationTitle = "Editing and creating new features.";
            Editor = new Editor();

            var progress = new Progress<GeometryEditStatus>();
            progress.ProgressChanged += (a, b) =>
            {
                Debug.WriteLine(b.GeometryEditAction.ToString());
            };
            _editingProgress = progress;
        }

        public ICommand AddFeatureCommand
        {
            get
            {
                if (_addFeatureCommand == null)
                {
					_addFeatureCommand = new RelayCommand<TemplatePicker.TemplatePickedEventArgs>(AddFeature);
                }
                return _addFeatureCommand;
            }
        }

        public ICommand SelectFeatureCommand
        {
            get
            {
                if (_selectFeatureCommand == null)
                {
					_selectFeatureCommand = new RelayCommand<MapViewInputEventArgs>(SelectFeature);
                }
                return _selectFeatureCommand;
            }
        }

        public ICommand EditFeatureCommand
        {
            get
            {
                if (_editFeatureCommand == null)
                {
					_editFeatureCommand = new RelayCommand<GeodatabaseFeature>(EditFeature);
                }
                return _editFeatureCommand;
            }
        }

        public ICommand RemoveFeatureCommand
        {
            get
            {
                if (_removeFeatureCommand == null)
                {
					_removeFeatureCommand = new RelayCommand<GeodatabaseFeature>(RemoveFeature);
                }
                return _removeFeatureCommand;
            }
        }

        public ICommand SaveFeatureCommand
        {
            get
            {
                if (_saveFeatureCommand == null)
                {
                    _saveFeatureCommand = new RelayCommand<EventArgs>(SaveFeature);
                }
                return _saveFeatureCommand;
            }
        }

        /// <summary>
        /// Returns <see cref="MapViewService"/> that is used to control MapView
        /// </summary>
        public MapViewService MapViewService
        {
            get
            {
                if (_mapViewService == null)
                {
                    _mapViewService = new MapViewService();
                }
                return _mapViewService;
            }
        }

        /// <summary>
        /// Gets or sets the Map.
        /// </summary>
        public Map Map
        {
            get { return _map; }
            protected set
            {
                _map = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the Editor. 
        /// </summary>
        public Editor Editor
        {
            get { return _editor; }
            set
            {
                _editor = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the selected feature.
        /// </summary>
        public GeodatabaseFeature SelectedFeature
        {
            get { return _selectedFeature; }
            protected set
            {
                _selectedFeature = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Initializes ViewModel
        /// </summary>
		protected override async Task InitializeAsync()
        {
            // Create map with layers and set startup location
            var map = new Map()
            {
                InitialExtent = new Envelope(-13636132.3698584, 4546349.82732426, -13633579.1021618, 4547513.1599185, SpatialReferences.WebMercator)
            };
            Map = map;

            // Basemap layer local tile package. 
			var basemap = new ArcGISLocalTiledLayer()
            {
                ID = "Basemap",
                DisplayName = "Basemap",
				//ServiceUri = "http://services.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer"
				Path = @"C:\LocalDataStore\TileCache\Layers.tpk"
            };

            // Initialize layer in Try - Catch 
            Exception exceptionToHandle = null;
            try
            {
                await basemap.InitializeAsync();
                map.Layers.Add(basemap);
                
                // Uncomment to this to create online layers
                //await CreateOnlineOperationalLayersAsync();

                var operationalLayers = await CreateOfflineOperationalLayersAsync(@"C:\LocalDataStore\Cache\LocalWildfire.geodatabase");
                foreach (var layer in operationalLayers)
                {
                    Map.Layers.Add(layer);
                }
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
			IsInitialized = true;
        }

        /// <summary>
        /// Adds new feature. Uses Editor to request geometry for the new graphic.
        /// </summary>
        private async void AddFeature(TemplatePicker.TemplatePickedEventArgs parameters)
        {
            var targetLayer = parameters.Layer;
            var featureTemplate = parameters.FeatureTemplate;
            Exception exceptionToHandle = null;
				
            // Clear selection since we are now addding new features
            SelectedFeature = null;
            _selectedLayer = null;
            foreach (var layer in Map.Layers.OfType<FeatureLayer>())
                layer.ClearSelection();

            try
            {
                // Get symbol for the editor that is used when sketching
                Symbol symbol = null;
                var renderer = targetLayer.Renderer ?? targetLayer.FeatureTable.ServiceInfo.DrawingInfo.Renderer;
                if (renderer != null)
                {
                    symbol = renderer.GetSymbol(new Graphic(featureTemplate.Prototype.Attributes));
                }

                DrawShape requestedShape = DrawShape.Point;

                // TODO commented out since always return freehand, in this demo we want to use specific geometry editing
                //switch (featureTemplate.DrawingTool)
                //{
                //    case FeatureEditTool.Polygon:
                //        requestedShape = DrawShape.Polygon; break;
                //    case FeatureEditTool.Freehand:
                //        requestedShape = DrawShape.Freehand; break;
                //    case FeatureEditTool.Point:
                //        requestedShape = DrawShape.Point; break;
                //    case FeatureEditTool.Line:
                //        requestedShape = DrawShape.Polyline; break;
                //    default:
                //        throw new NotImplementedException();
                //}

                if (targetLayer.ID.ToLowerInvariant().Contains("lines"))
                {
                    requestedShape = DrawShape.Polyline;
                }
                else if (targetLayer.ID.ToLowerInvariant().Contains("polygons"))
                {
                    requestedShape = DrawShape.Polygon;
                }
                
                // Enable geometry editing and wait until it is done, returned geometry is the edited version.
                var requestedGeometry = await Editor.RequestShapeAsync(requestedShape, symbol, _editingProgress);

                // Create new feature based on the feature schema
                var geodatabaseFeature = new GeodatabaseFeature(targetLayer.FeatureTable.Schema);
                geodatabaseFeature.Geometry = requestedGeometry;

                // Copy initial vaulues for attributes from prototype
                // This is needed since features might have non-nullable fields and in this case
                // Points uses "EventType" to definde symbol and lines and polygons "symbolId"
                foreach (var attribute in featureTemplate.Prototype.Attributes)
                {
                    geodatabaseFeature.Attributes.Add(attribute.Key, attribute.Value);
                }

                // Add feature to the layer
                var newID = await targetLayer.FeatureTable.AddAsync(geodatabaseFeature);

                // When working with GeodatabaseFeatureServiceTable, edits are not automatically sent to the server
                // So you can have fine grained control when to do apply edits, here it automatically sends 
                // update when it is done on the client.
                if (targetLayer.FeatureTable is GeodatabaseFeatureServiceTable)
                {
                    var featureTable = (GeodatabaseFeatureServiceTable)targetLayer.FeatureTable;
                    await featureTable.ApplyEditsAsync();
                }
            }
            catch (TaskCanceledException editCancelledException)
            {
                // This is raised when editing is cancelled so eat it.
            }
            catch (Exception exception)
            {
                exceptionToHandle = exception;
            }

            if (exceptionToHandle != null)
            {
                // Initialization failed, show message and return
                await MessageService.Instance.ShowMessage(string.Format(
                    "Could not create basemap. Error = {0}", exceptionToHandle.ToString()),
                    "An error occured");
            }
        }

        /// <summary>
        /// Selects feature.
        /// </summary>
        private async void SelectFeature(MapViewInputEventArgs parameters)
        {
            // If editor is active skip selecting features
            if (Editor.IsActive)
                return;

            // Unselect all features
            var featureLayers = Map.Layers.OfType<FeatureLayer>();
            foreach (var layer in featureLayers)
                layer.ClearSelection();

            GeodatabaseFeature feature = null;
			Exception exceptionToHandle = null;
			try
			{
				foreach (var layer in featureLayers.Where(l => l.Status == LayerStatus.Initialized).Reverse())
				{
					// Using MapViewService to handle hit testing. 
					var hit = await MapViewService.HitTestAsync(parameters.Position, layer) as GeodatabaseFeature;
					if (hit != null)
					{
						// Set feature selected
						layer.SelectFeatures(new[] { (long)hit.Attributes[layer.FeatureTable.ServiceInfo.ObjectIdField] });

						// Take feature and its layer for later use
						feature = hit;
						_selectedLayer = layer;
						break;
					}
				}
			}
			catch (Exception exception)
			{
				exceptionToHandle = exception;
			}

			if (exceptionToHandle != null)
			{
				// Initialization failed, show message and return
				await MessageService.Instance.ShowMessage(string.Format(
					"Could not create basemap. Error = {0}", exceptionToHandle.ToString()),
					"An error occured");
			}
		
            // Select or clear selection
            SelectedFeature = feature;
        }

        /// <summary>
        /// Removes selected feature and updates the layer.
        /// </summary>
        /// <param name="feature">Feature to remove.</param>
        private async void RemoveFeature(GeodatabaseFeature feature)
        {
            if (feature == null)
                return;

            Exception exceptionToHandle = null;
            try
            {
                await _selectedLayer.FeatureTable.DeleteAsync(feature);

                // Save edits to the service if using ServiceTable
                if (_selectedLayer.FeatureTable is GeodatabaseFeatureServiceTable)
                {
                    var featureTable = (GeodatabaseFeatureServiceTable)_selectedLayer.FeatureTable;
                    await featureTable.ApplyEditsAsync();
                }
            }
            catch (Exception exception)
            {
                exceptionToHandle = exception;
            }

            if (exceptionToHandle != null)
            {
                await MessageService.Instance.ShowMessage(string.Format(
                    "Could not delete selected feature. Error = {0}", exceptionToHandle.ToString()),
                    "An error occured");
            }
        }

        /// <summary>
        /// Edits the feature. On points it request new location and on polylines and
        /// polygons editing existing geometry is requested by using Editor.
        /// </summary>
        /// <param name="feature">Feature to edit.</param>
        /// <remarks>Edits are saved immediately.</remarks>
        private async void EditFeature(GeodatabaseFeature feature)
        {
            if (feature == null)
                return;

            Exception exceptionToHandle = null;
            try
            {
                switch (feature.Geometry.GeometryType)
                {
                    case GeometryType.Point:
                        var newLocation = await Editor.RequestPointAsync();
                        feature.Geometry = newLocation;
                        break;
                    case GeometryType.Polyline:
                        var polyine = await Editor.EditGeometryAsync(feature.Geometry, null, _editingProgress);
                        feature.Geometry = polyine;
                        break;
                    case GeometryType.Polygon:
                        var polygon = await Editor.EditGeometryAsync(feature.Geometry, null, _editingProgress);
                        feature.Geometry = polygon;
                        break;
                }

                await _selectedLayer.FeatureTable.UpdateAsync(feature);
                
                // Save edits to the service if using ServiceTable
                if (_selectedLayer.FeatureTable is GeodatabaseFeatureServiceTable)
                {
                    var featureTable = (GeodatabaseFeatureServiceTable)_selectedLayer.FeatureTable;
                    await featureTable.ApplyEditsAsync();
                }
            }
            catch (TaskCanceledException editCancelledException)
            {
                // This is raised when editing is cancelled so eat it.
            }
            catch (Exception exception)
            {
                exceptionToHandle = exception;
            }

            if (exceptionToHandle != null)
            {
                await MessageService.Instance.ShowMessage(string.Format(
                    "Could not edit or update selected feature. Error = {0}", exceptionToHandle.ToString()),
                    "An error occured");
			}
        }

        /// <summary>
        /// Saves changes done to SelectedFeature.
        /// </summary>
        private async void SaveFeature(EventArgs parameters)
        {
            if (SelectedFeature == null)
                return;

            Exception exceptionToHandle = null;
            try
            {
                await _selectedLayer.FeatureTable.UpdateAsync(SelectedFeature);
                
                // Save edits to the service if using ServiceTable
                if (_selectedLayer.FeatureTable is GeodatabaseFeatureServiceTable)
                {
                    var featureTable = (GeodatabaseFeatureServiceTable)_selectedLayer.FeatureTable;
                    await featureTable.ApplyEditsAsync();
                }
            }
            catch (Exception exception)
            {
                exceptionToHandle = exception;
            }

            if (exceptionToHandle != null)
            {
				await MessageService.Instance.ShowMessage(string.Format(
					"Could not update selected feature. Error = {0}", exceptionToHandle.ToString()),
					"An error occured");
            }
        }

        /// <summary>
        /// Create layers from all tables in given geodatabase.
        /// </summary>
        /// <param name="path">Full path to geodatabase</param>
        /// <returns>Returns list of <see cref="FeatureLayer"/> that are defind in the local geodatabase.</returns>
        private async Task<List<FeatureLayer>> CreateOfflineOperationalLayersAsync(string path)
        {
            var layers = new List<FeatureLayer>();

			// When working with files, API differs on Windows Phone / Store and WPF
			// This works for WPF and Windows Phone but for Windows Store doesn't 
			// implement FileInfo class so this should be abstracted
			//var fileInfo = new FileInfo(path);
			//if (!fileInfo.Exists)
			//{
			//	throw new FileNotFoundException("Geodatabase not found.", path);
			//}

			// Open instance of local geodatabase 
            var geodatabase = await Geodatabase.OpenAsync(path);

			// Loop through all the feature classes / services that are in the geodatabase
			// and create new FeatureLayer from those
            foreach (var table in geodatabase.FeatureTables)
            {
				var layer = new FeatureLayer()
				{
					ID = table.Name,
					DisplayName = table.Name,
					FeatureTable = table
				};

				layers.Add(layer);
            }

            return layers;
        }

        #region Online layers
        private async Task CreateOnlineOperationalLayersAsync()
        {
            var operationalLayers = new List<Layer>();

            var wildfirePoints = await CreateFeatureLayerAsync(
               "http://services.arcgis.com/P3ePLMYs2RVChkJx/ArcGIS/rest/services/Wildfire/FeatureServer/0",
               "WildfirePoints",
               "Wildfire - points");

            Map.Layers.Add(wildfirePoints);

            var wildfireLines = await CreateFeatureLayerAsync(
               "http://services.arcgis.com/P3ePLMYs2RVChkJx/ArcGIS/rest/services/Wildfire/FeatureServer/1",
               "WildfireLines",
               "Wildfire - lines");

            Map.Layers.Add(wildfireLines);

            var wildfirePolygons = await CreateFeatureLayerAsync(
               "http://services.arcgis.com/P3ePLMYs2RVChkJx/ArcGIS/rest/services/Wildfire/FeatureServer/2",
               "WildfirePolygons",
               "Wildfire - polygons");

            Map.Layers.Add(wildfirePolygons);
        }

        private async Task<FeatureLayer> CreateFeatureLayerAsync(string uri, string name, string displayName)
        {
            FeatureLayer layer = null;

            var gdbFeatureServiceTable = new GeodatabaseFeatureServiceTable()
            {
                ServiceUri = uri,
                OutFields = OutFields.All
            };

            // Not in Try - Catch so exception is thrown and catched on higher level
            await gdbFeatureServiceTable.InitializeAsync();

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
        #endregion // Online layers
    }
}
