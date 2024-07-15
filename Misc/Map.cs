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
using NetTopologySuite.Geometries;
using Serilog;
using SQLite;
using Xamarin.Essentials;
using BruTile.Wms;

namespace Velociraptor
{
    internal class Map
    {
        private static MapControl? mapControl = null;
        private static Mapsui.Map map = new();

        public static Mapsui.Map GetMap()
        {
            return map;
        }

        public static void CreateMap()
        {
            var cActivity = Platform.CurrentActivity;

            mapControl = cActivity.FindViewById<MapControl>(Resource.Id.mapcontrol);
            if (mapControl == null)
            {
                Log.Error($"mapControl can't be null here");
                return;
            }

            map = new Mapsui.Map
            {
                CRS = "EPSG:3857", //https://epsg.io/3857
            };
            mapControl.Map = map;

            //Base map, w/caching to local mbtiles file
            LoadOSMLayer();
        }

        private static void LoadOSMLayer()
        {
            try
            {
                //Make sure folder exists
                if (Directory.Exists(PrefsFragment.rootPath) == false)
                {
                    Directory.CreateDirectory(PrefsFragment.rootPath);
                }

                var tileSource = TileCache.GetOSMBasemap(PrefsFragment.rootPath + "/" + PrefsFragment.CacheDB);
                if (tileSource == null)
                {
                    return;
                }

                var tileLayer = new TileLayer(tileSource)
                {
                    Name = "OSM",
                };
                map.Layers.Insert(0, tileLayer);
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, $"LoadOSMLayer()");
            }
        }

        public static void UpdateLocationMarker(Android.Locations.Location? cLocation)
        {
            if (map != null && cLocation != null)
            {
                //Zoom and Center on our cLocation. North Up
                MPoint? sphericalMercatorCoordinate = (SphericalMercator.FromLonLat((double)cLocation.Longitude, (double)cLocation.Latitude)).ToMPoint();
                map.Navigator.CenterOn(sphericalMercatorCoordinate);
                if (cLocation.HasBearing)
                {
                    map.Navigator.RotateTo(360-cLocation.Bearing, -1);
                }

                //Update marker
                ILayer? layer = map.Layers.FindLayer(PrefsFragment.LocationLayerName).FirstOrDefault();
                if (layer != null)
                {
                    map.Layers.Remove(layer);
                }
                map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));
            }
        }

        private static MemoryLayer CreateLocationLayer(MPoint GPSLocation)
        {
            return new MemoryLayer
            {
                Name = PrefsFragment.LocationLayerName,
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
