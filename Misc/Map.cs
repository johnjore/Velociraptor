using Android.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI;
using Mapsui.UI.Android;
using Mapsui.Utilities;
using Serilog;
using SQLite;
using Xamarin.Essentials;

namespace Velociraptor
{
    internal class Map
    {
        public static void UpdateLocationMarker(Android.Locations.Location? cLocation)
        {
            var map = Fragments.Fragment_Map.GetMap();

            if (map == null || cLocation == null)
            {
                return;
            }

            //Center on our cLocation. North Up
            MPoint? sphericalMercatorCoordinate = (SphericalMercator.FromLonLat((double)cLocation.Longitude, (double)cLocation.Latitude)).ToMPoint();
            map.Navigator.CenterOn(sphericalMercatorCoordinate);
            if (cLocation.HasBearing)
            {
                map.Navigator.RotateTo(360-cLocation.Bearing, -1);
            }

            //Update marker
            ILayer? layer = map.Layers.FindLayer(Fragments.Fragment_Preferences.LocationLayerName).FirstOrDefault();
            if (layer != null)
            {
                map.Layers.Remove(layer);
            }
            map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));
        }

        private static MemoryLayer CreateLocationLayer(MPoint GPSLocation)
        {
            return new MemoryLayer
            {
                Name = Fragments.Fragment_Preferences.LocationLayerName,
                Features = CreateLocationFeatures(GPSLocation),
                Style = null,
                IsMapInfoLayer = true
            };
        }

        private static List<IFeature> CreateLocationFeatures(MPoint GPSLocation)
        {
            return new List<IFeature>
            {
                new PointFeature(CreateLocationMarker(GPSLocation)),
            };
        }

        private static PointFeature CreateLocationMarker(MPoint GPSLocation)
        {
            var feature = new PointFeature(GPSLocation);

            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.5f,
                Fill = null,
                Outline = new Pen { Color = Color.Blue, Width = 2.0 }
            });

            return feature;
        }
    }
}
