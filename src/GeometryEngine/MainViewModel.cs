using DevSummit2014.Core;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Symbology;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace GeometryEngine
{
	// Key points in demo :
	// 1 ) ViewModel is initialized 
	//		- Tiled layer is created from local Tile Package
	//		- GeodatabaseFeatureTable is fetched from local Geodatabase to provide data access 
	//		- 2 Graphics layers are created with renderers for query area and query results
	// 2 ) Mouse is moved on the map
	//		- Mouses location is got as a MapPoint
	// 3 ) Geodesic buffer is created around mouses location syncronously using GeometryEngine
	//		- Show buffered are on the MapView
	// 4 ) Spatial query is executed with the buffered geometry agains local Geodatabase and it's features
	//		- Show found States on the MapView
	// 5 ) Calculating total area of the found features 
	//		- Create union of found state geometries
	//		- Calculate surface by planar measurement using 2D Cartesian mathematics
	//		- Total area is shown on the view.

	public class MainViewModel : ViewModelBase
    {
        private Map _map;
		private RelayCommand<MouseEventArgs> _mouseMoveCommand;
		private MapViewService _mapViewService;
		private GeodatabaseFeatureTable _featureTable;
		private double _totalArea;

		// Is used to block multiple query situations, when this is set to false,
		// _featureLayer isn't queried. 
		private bool _isQuerying;

		// Saving references to make easier to accessing these layers
		// Could also get layers on the fly by using Map.Layers["layerId"]
		private GraphicsLayer _queryAreaLayer;
		private GraphicsLayer _statesLayer;

		public MainViewModel()
			: base()
		{
			ApplicationTitle = "Using GeometryEngine and querying data";
		}

		/// <summary>
		/// Gets the MouseMove command.
		/// </summary>
		public ICommand MouseMoveCommand
		{
			get
			{
				if (_mouseMoveCommand == null)
				{
					_mouseMoveCommand = new RelayCommand<MouseEventArgs>(MouseMoved);
				}
				return _mouseMoveCommand;
			}
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

		/// <summary>
		/// Gets or sets the total area of the found states.
		/// </summary>
		public double TotalArea
		{
			get
			{
				return _totalArea;
			}
			set
			{
				_totalArea = value;
				NotifyPropertyChanged();
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
		/// Executes ViewModel initialization logic. Called when ViewModel is created from base view model. 
		/// </summary>
		protected override async Task InitializeAsync()
		{
			// Create map with layers
			Map = new Map()
				{
					InitialExtent = new Envelope(-14161146.113642, 3137996.40676956, -7626168.31478212, 6574986.2928574)
				};		

			// Basemap layer from ArcGIS Online hosted service
			var basemap = new ArcGISLocalTiledLayer()
			{
				ID = "Basemap",
				DisplayName = "Basemap",
				Path = @"..\..\Data\TPKs\Topographic.tpk"
			};

			// Initialize layer in Try - Catch 
			Exception exceptionToHandle = null;
			try
			{
				await basemap.InitializeAsync();
				Map.Layers.Add(basemap);

				var geodatabase = await Geodatabase.OpenAsync(@"..\..\Data\usa.geodatabase");
				_featureTable = geodatabase.FeatureTables.First(x => x.Name == "States");

				// Create graphics layer for start and endpoints
				CreateGraphicsLayers();
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

		/// <summary>
		/// Create layers and renderers for states and query areas.
		/// </summary>
		private void CreateGraphicsLayers()
		{
			#region States layer
			_statesLayer = new GraphicsLayer()
			{
				ID = "States"
			};

			var statesAreaSymbol = new SimpleFillSymbol()
			{
				Color = (Color)ColorConverter.ConvertFromString("#440000FF"),
				Outline = new SimpleLineSymbol
									{
										Color = Colors.Blue,
										Width = 2
									}
			};

			var renderer = new SimpleRenderer { Symbol = statesAreaSymbol };
			_statesLayer.Renderer = renderer;

			Map.Layers.Add(_statesLayer);
			#endregion

			#region QueryArea layer
			_queryAreaLayer = new GraphicsLayer()
			{
				ID = "QueryArea"
			};

			var queryAreaLayerSymbol = new SimpleFillSymbol()
			{
				Color = (Color)ColorConverter.ConvertFromString("#66BB0000"),
				Style = SimpleFillStyle.DiagonalCross,
				Outline = new SimpleLineSymbol
				{
					Color = Colors.Red,
					Width = 2
				}
			};

			var queryAreaRenderer = new SimpleRenderer { Symbol = queryAreaLayerSymbol};
			_queryAreaLayer.Renderer = queryAreaRenderer;

			Map.Layers.Add(_queryAreaLayer);
			#endregion
		}

		private async void MouseMoved(MouseEventArgs parameters)
		{
			if (!IsInitialized)
				return;

			Exception exceptionToHandle = null;
			try
			{
				// Gets cursors location as a MapPoint from MapView since MouseEventArgs is general
				// .NET framework event and doesn't contain location information directly
				// MapViewService abstracts reference to MapView and its ScreenToLocation method.
				var mouseMapPoint = MapViewService.GetLocation(parameters);
				_queryAreaLayer.Graphics.Clear();
			
				// Buffering mouses location. Note that operation isn't async.
				var bufferResult = Esri.ArcGISRuntime.Geometry.GeometryEngine.GeodesicBuffer(
					mouseMapPoint, 
					100,
					LinearUnits.Miles);
				
				var bufferGraphic = new Graphic(bufferResult);
				_queryAreaLayer.Graphics.Add(bufferGraphic);

				// If buffer / guery is running, wait until its done
				if (_isQuerying)
					return;
				_isQuerying = true;

				// Execute Spatial Query against the GeodatabaseFeatureTable 
				// that provides data access to local
				// geodatabase and its features.
				var query = new SpatialQueryFilter() { Geometry = bufferResult };
				var queryResults = await _featureTable.QueryAsync(query);
				_statesLayer.Graphics.Clear();

				if (queryResults.Count() < 1)
				{
					_isQuerying = false;
					return;
				}

				// Set features to the graphics layer as a graphics
				foreach (GeodatabaseFeature feature in queryResults)
				{
					_statesLayer.Graphics.Add(feature.AsGraphic());
				}

				// Union all states and calculate total area
				var stateGeometries = new List<Esri.ArcGISRuntime.Geometry.Geometry>();
				foreach (var state in _statesLayer.Graphics)
				{
					stateGeometries.Add(state.Geometry);
				}
				var totalAreaGeometry = Esri.ArcGISRuntime.Geometry.GeometryEngine.Union(stateGeometries);
				TotalArea = Esri.ArcGISRuntime.Geometry.GeometryEngine.Area(totalAreaGeometry);
				_isQuerying = false;
			}
			catch (Exception exception)
			{
				Debug.WriteLine(exception);
				exceptionToHandle = exception;
				_isQuerying = false;
			}

			if (exceptionToHandle != null)
			{
				// Initialization failed, show message and return
				await MessageService.Instance.ShowMessage(string.Format(
					"Something went wrong when buffering or querying data. Error = {0}", exceptionToHandle.ToString()),
					"An error occured");
			}
		}
	}
}
