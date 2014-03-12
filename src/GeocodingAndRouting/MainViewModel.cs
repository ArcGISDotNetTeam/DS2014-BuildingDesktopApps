using DevSummit2014.Core;
using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.Tasks.NetworkAnalyst;
using Esri.ArcGISRuntime.Tasks.Query;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace GeocodingAndRouting
{
	// Reverse Geocoding, routing and graphics labeling
	//
	//	1 ) On initialization LocalRuoteTask and LocalLocatorTasks are initialized
	//		- LocalRouteTask uses Network dataset extracted using ArcMap
	//		- LocalLoctorTask uses locator dataset extracted using ArcMap
	//	2 ) On initialization graphcis layers are created to show point and the route
	//		- Layer with start and endpoints contains label definition that shows ADDRESS attribute
	//	3) When mouse is moved on the map
	//		- Mouses location is got by using MapViewService helper class that uses MapView's ScreenToLocation method
	//		- Location gets reverse geocoded and shown in the label
	//		- Routing is done between starting location and this location
	//		- 


    public class MainViewModel : ViewModelBase
    {
        private Map _map;
        private Editor _editor;

		// Using base class as a backend type so you can add either online or offline task
        private LocatorTask _locatorTask;
        private RouteTask _routeTask;
        
		private Graphic startGraphic;
        private MapViewService _mapViewService;
        private GraphicsLayer _graphicLayer;
        private GraphicsLayer _routeLayer;

		private bool _isRouting;

        // Commands
        private RelayCommand<object> _addStartingPointCommand;
        private RelayCommand<MouseEventArgs> _mouseMoveCommand;

        public MainViewModel()
            : base()
        {
            ApplicationTitle = "Offline routing and geocoding";
            Editor = new Editor();
        }
    
        public ICommand AddStartingPointCommand
        {
            get
            {
                if (_addStartingPointCommand == null)
                {
					_addStartingPointCommand = new RelayCommand<object>(AddStartingPoint);
                }
                return _addStartingPointCommand;
            }
        }

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
		/// Initializes layers and tasks
		/// </summary>
        protected override async Task InitializeAsync()
        {
            // Create map with layers and set startup location
            var map = new Map()
                {
                    InitialExtent = new Envelope(-13636132.3698584, 4546349.82732426, -13633579.1021618, 4547513.1599185, SpatialReferences.WebMercator)
                };
            Map = map;

            // Basemap layer from ArcGIS Online hosted service
            var basemap = new ArcGISLocalTiledLayer()
            {
                ID = "Basemap",
                DisplayName = "Basemap",
			//	ServiceUri = "http://services.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer"
				Path = @"C:\LocalDataStore\TileCache\Layers.tpk"
			};

            // Initialize layer in Try - Catch 
            Exception exceptionToHandle = null;
            try
            {
                await basemap.InitializeAsync();
                map.Layers.Add(basemap);

                // Create graphics layer for start and endpoints
                CreateEndpointLayer();
                CreateRouteLayer();

                // Create geocoding and routing tasks
				_locatorTask = new LocalLocatorTask(@"C:\LocalDataStore\Locators\SanFrancisco\SanFranciscoLocator.loc");
				_routeTask = new LocalRouteTask(@"C:\LocalDataStore\Network\RuntimeSanFrancisco.geodatabase", "Routing_ND");
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
            }
        }

        private async void MouseMoved(MouseEventArgs parameters)
        {
            if (startGraphic == null)
                return;

			// Get mouses location relative to MapView.
            var point = MapViewService.GetLocation(parameters);

			Exception exceptionToHandle = null; ;
            try
            {
                var graphic = await GetGeocodedGraphicAsync(point);
                graphic.Attributes.Add("PointType", "EndPoint");

				// Get endpoint graphic from the layer and replace that with new location
                var existingGraphic = _graphicLayer
                    .Graphics
                    .FirstOrDefault(g => g.Attributes["PointType"].ToString() == "EndPoint");
                if (existingGraphic != null)
                    _graphicLayer.Graphics.Remove(existingGraphic);

                _graphicLayer.Graphics.Add(graphic);

                var routeGraphic = await GetRouteGraphicAsync(
					startGraphic.Geometry as MapPoint, 
					graphic.Geometry as MapPoint);
                if (routeGraphic == null)
                    return;

                _routeLayer.Graphics.Clear();
                _routeLayer.Graphics.Add(routeGraphic);
            }
            catch (Exception exception)
            {
		
            }
        }

		/// <summary>
		/// Request and add new starting point.
		/// </summary>
		private async void AddStartingPoint(object parameters)
		{
			_graphicLayer.Graphics.Clear();
			startGraphic = null;

			Exception exceptionToHandle = null;
			try
			{
				// Get point from the map
				var requestedGeometry = await Editor.RequestPointAsync();

				// Gets add
				var resultGraphic = await GetGeocodedGraphicAsync(requestedGeometry);
				// Null will be returned if geocode failed, skip rest
				//if (resultGraphic == null)
				//	return;

				resultGraphic.Attributes.Add("PointType", "StartPoint");

				startGraphic = resultGraphic; ;
				_graphicLayer.Graphics.Add(resultGraphic);
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
					"Could not get starting location. Error = {0}", exceptionToHandle.ToString()),
					"An error occured");
			}
		}

        private async Task<Graphic> GetGeocodedGraphicAsync(MapPoint location)
        {
            Graphic resultGraphic = null;
            try
            {
				// Get address by location
                var reverseGeocodeResult = await _locatorTask.ReverseGeocodeAsync(location,
                                 50,
								_mapViewService.SpatialReference,
                                 new CancellationToken());

                // Create graphic for startpoint and make sure its in correct spatial reference
                resultGraphic = new Graphic
                {
                    Geometry = reverseGeocodeResult.Location
                };

				// We are only interested about street attribute in this demo
                resultGraphic.Attributes.Add("Address", reverseGeocodeResult.AddressFields["Street"]);
            }
            catch (Exception exception)
            {
				// At the moment exception is thrown if location couldn't be reverse geocoded
            }

            return resultGraphic;
        }

        private async Task<Graphic> GetRouteGraphicAsync(MapPoint startLocation, MapPoint endLocation)
        {
			// Wait until previous routing is completed before doing another
            if (_isRouting)
                return null;

            _isRouting = true;
            RouteParameters routeParameters = await _routeTask.GetDefaultParametersAsync();

            routeParameters.OutSpatialReference = MapViewService.SpatialReference;
            routeParameters.ReturnDirections = false;
            routeParameters.DirectionsLengthUnit = LinearUnits.Kilometers;

			// Add endpoints to for the route
            List<Graphic> graphicsStops = new List<Graphic>();
            graphicsStops.Add(new Graphic() { Geometry = startLocation });
            graphicsStops.Add(new Graphic() { Geometry = endLocation });

            var stops = new FeaturesAsFeature();
            stops.Features = graphicsStops;
            stops.SpatialReference = graphicsStops[0].Geometry.SpatialReference;
            routeParameters.Stops = stops;

            try
            {
				// Since we are ignoring cancellation just give a new token everytime
                var routeResult = await _routeTask.SolveAsync(routeParameters, new CancellationToken());
                _isRouting = false;

                if (routeResult.Routes.Count > 0)
                    return routeResult.Routes[0].RouteGraphic;
            }
            catch (Exception)
            {
				// At the moment exception is thrown if route task couldn't solve the route so catch 
            }
            _isRouting = false;
            return null;            
        }

        private void CreateEndpointLayer()
        {
            var endpointLayer = new GraphicsLayer()
            {
                ID = "EndpointLayer", 
                IsLabelingEnabled = true
            };

			// We are adding start and endpoints to same layer so render them differently
            var endpointLayerRenderer = new UniqueValueRenderer()
            {
                DefaultSymbol = new SimpleMarkerSymbol
                {
                    Color = Colors.Red,
                    Size = 12
                },
                Fields = new ObservableCollection<string>() { "PointType" },
                Infos = new UniqueValueInfoCollection()
                                {
                                    new UniqueValueInfo("EndPoint", 
                                        new SimpleMarkerSymbol() 
                                            { 
                                                Color = Colors.Blue, 
                                                Size = 12 
                                            })
                                }
            };

            endpointLayer.Renderer = endpointLayerRenderer;

			// Crate labeling definition for start and end locations
            var labelClass = new LabelClass()
            {
                Symbol = new TextSymbol()
                {
                    Color = Colors.Yellow,
                    BorderLineColor = Colors.Black,
                    BorderLineSize = 2
                },
                TextExpression = "[Address]" 
            };

            endpointLayer.LabelClasses = new LabelClassCollection { labelClass };

            Map.Layers.Add(endpointLayer);
            _graphicLayer = endpointLayer;
        }

		private void CreateRouteLayer()
        {

            var routeLayer = new GraphicsLayer()
            {
                ID = "RouteLayer"
            };

            var renderer = new SimpleRenderer()
            {
                Symbol = new SimpleLineSymbol()
                {
                    Color = Colors.Black,
                    Width = 5,
                    Style = SimpleLineStyle.Solid
                }
            };

            routeLayer.Renderer = renderer;
            Map.Layers.Add(routeLayer);
            _routeLayer = routeLayer;
        }
    }
}
