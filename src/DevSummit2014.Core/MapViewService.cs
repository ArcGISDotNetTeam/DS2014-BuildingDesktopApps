using Esri.ArcGISRuntime.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Layers;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Runtime.CompilerServices;
using System.Windows;

namespace DevSummit2014.Core
{
    public class MapViewService : INotifyPropertyChanged
    {
        private WeakReference<MapView> _mapView;

        public Thickness DefaultMargin { get; set; }

        public Task<bool> SetViewAsync(Geometry geometry)
        {
            var map = MapView;
            if (map != null && geometry != null)
            {
                return map.SetViewAsync(geometry);
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// Hit tests given point against given layer to get features from that position.
        /// </summary>
        /// <param name="screenPoint">Screen point of that is used in hittest.</param>
        /// <param name="layer">Layer that is hittested against</param>
        /// <returns>Returns feature if there was one in the given location.</returns>
        /// <remarks>Hit test uses 3x3 area around given point.</remarks>
        public async Task<Feature> HitTestAsync(Point screenPoint, Esri.ArcGISRuntime.Layers.Layer layer)
        {
            var mapView = MapView;
            if (mapView != null)
            {
                if (layer is GraphicsLayer)
                {
                    Rect rect = new Rect(screenPoint.X - 1, screenPoint.Y - 1, 3, 3);
                    var glayer = (GraphicsLayer)layer;
                    return await glayer.HitTestAsync(mapView, rect).ContinueWith(t => { return (Feature)t.Result; });
                }
                else if (layer is FeatureLayer)
                {
                    var flayer = (FeatureLayer)layer;
                    Rect rect = new Rect(screenPoint.X - 1, screenPoint.Y - 1, 3, 3);
                    var featureID = await flayer.HitTestAsync(mapView, rect, 1).ContinueWith(t => { return (long)t.Result.FirstOrDefault(); });
                    if (featureID > 0)
                    {
                        return await flayer.FeatureTable.QueryAsync(new long[] { featureID })
                            .ContinueWith(t => { return (Feature)t.Result.FirstOrDefault(); });
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Returns MapPoint from given ScreenPoint.
        /// </summary>
        public MapPoint GetLocation(MouseEventArgs mouseEventArgs)
        {
            var mapView = MapView;
            if (mapView != null)
            {
                var point = mouseEventArgs.GetPosition(mapView);
                return mapView.ScreenToLocation(point);
            }
            return null;
        }

        /// <summary>
        /// Gets <see cref="Map.SpatialReference"/> of the Map.
        /// </summary>
        public SpatialReference SpatialReference
        {
            get
            {
                MapView map = MapView;
                if (map != null)
                {
                    return map.SpatialReference;
                }
                return null;
            }
        }

        /// <summary>
        /// Gets <see cref="Map.Extent"/> of the Map.
        /// </summary>
        public Envelope Extent
        {
            get
            {
                MapView map = MapView;
                if (map != null)
                {
                    return map.Extent;
                }
                return null;
            }
        }

        private MapView MapView
        {
            get
            {
                MapView map = null;
                if (_mapView != null && _mapView.TryGetTarget(out map))
                    return map;
                return null;
            }
        }

        public static MapView GetMapView(DependencyObject obj)
        {
            return (MapView)obj.GetValue(MapViewProperty);
        }

        public static void SetMapView(DependencyObject obj, MapView value)
        {
            obj.SetValue(MapViewProperty, value);
        }

        public static readonly DependencyProperty MapViewProperty =
            DependencyProperty.RegisterAttached("MapView", typeof(MapViewService), typeof(MapViewService), new PropertyMetadata(null, OnMapViewPropertyChanged));

        private static void OnMapViewPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MapView && e.OldValue is MapViewService)
            {
                var controller = (e.OldValue as MapViewService);
                controller._mapView = null;
            }
            if (d is MapView && e.NewValue is MapViewService)
            {
                var controller = (e.NewValue as MapViewService);
                controller._mapView = new WeakReference<MapView>(d as MapView);

                WeakEventListener<MapView, object, PropertyChangedEventArgs> loadedListener
                    = new WeakEventListener<MapView, object, PropertyChangedEventArgs>(d as MapView);
                loadedListener.OnEventAction =
                    (instance, source, eventArgs) => controller.MapViewController_PropertyChanged(source, eventArgs);

                // the instance passed to the action is referenced (i.e. instance.Loaded) so the lambda expression is 
                // compiled as a static method.  Otherwise it targets the map instance and holds it in memory.
                loadedListener.OnDetachAction = (instance, listener) =>
                {
                    if (instance != null)
                        instance.PropertyChanged -= listener.OnEvent;
                };
                (d as MapView).PropertyChanged += loadedListener.OnEvent;
                loadedListener = null;
            }
        }

        private void MapViewController_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SpatialReference")
                NotifyPropertyChanged("SpatialReference");
            if (e.PropertyName == "Extent")
                NotifyPropertyChanged("Extent");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property. 
        // The CallerMemberName attribute that is applied to the optional propertyName 
        // parameter causes the property name of the caller to be substituted as an argument. 
        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }
}
