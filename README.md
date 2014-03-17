DS2014-BuildingDesktopApps
======================

Samples from the 2014 Developer Summit session "Building WPF Apps with the New .NET ArcGIS Runtime SDK". Samples demonstrates how to use [ArcGIS Runtime SDK for .NET](https://developers.arcgis.com/net/) to create a map, edit online and offline data, access to ArcGIS Portal and its basemap gallery, use geocoding and routing and using GeometryEngine following a simplified Model-View-ViewModel (MVVM) implementation pattern.

See presentation from [TO-BE-UPDATED](http://video.esri.com/ ""). 

# Demos #

1. Creating basic map

	Creating a Map in ViewModel with layers and binding that to MapView while handling initialization errors. 

2. Create map from Webmap

	Creating simple basemap swicher using ArcGIS Portal. Querying basemap gallery from ArcGIS online and webmaps from ArcGISPortalItems. See crossplatform demo from [getting started repo](https://github.com/ArcGISDotNetTeam/DS2014-GettingStarted "")

3. Editing

	Creating new features by using Editor and TemplatePicker, editing features geometry using Editor or attributes using FeatureDataForm and removing existing feature. This sample uses Toolkit that can be found from Beta site. 

4. Geocoding and Routing

	On the fly reverse geocoding and routing while moving mouse. Also uses GrapicsLayer new labeling capability to show the addresses.

5. GeometryEngine

	Using GeometryEngine to buffer, make unions and calculating area. Also demonstrates how to use GeodatabaseFeatureTable to query information from local geodatabase.

# Content #

- [Source](https://github.com/ArcGISDotNetTeam/DS2014-BuildingDesktopApps/tree/master/src "Source")
- [Data](https://github.com/ArcGISDotNetTeam/DS2014-BuildingDesktopApps/tree/master/data "Data")

[](Esri Tags: ArcGIS Runtime SDK .NET WPF C# C-Sharp DotNet XAML DevSummit)
[](Esri Language: DotNet)
