using Android.Views;
using Mapsui.UI.Android;
using Mapsui.Widgets.ScaleBar;
using Mapsui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Mapsui.UI;
using Xamarin.Essentials;
using Mapsui.Tiling.Layers;

namespace Velociraptor.Fragments
{
    public class Fragment_map : AndroidX.Fragment.App.Fragment
    {
        private static MapControl? mapControl = null;
        private static Mapsui.Map map = new();

        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override View? OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            if (inflater == null)
            {
                Log.Error($"inflator can't be null here");
                return null;
            }

            try
            {
                var view = inflater.Inflate(Resource.Layout.fragment_map, container, false);
                if (view == null)
                {
                    Log.Error($"View can't be null here");
                    return null;
                }
                view.SetBackgroundColor(Android.Graphics.Color.White);

                Log.Debug($"Create mapControl");
                mapControl = view.FindViewById<MapControl>(Resource.Id.mapcontrol);
                if (mapControl == null)
                {
                    Log.Error($"mapControl can't be null here");
                    return null;
                }

                map = new Mapsui.Map
                {
                    CRS = "EPSG:3857", //https://epsg.io/3857
                };
                mapControl.Map = map;

                //Base map, w/caching to local mbtiles file
                LoadOSMLayer();

                return view;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Fragment_map - OnCreateView() Crashed");
            }

            return null;
        }

        public static Mapsui.Map GetMap()
        {
            return map;
        }

        private static void LoadOSMLayer()
        {
            try
            {
                //Make sure folder exists
                if (Directory.Exists(Fragment_Preferences.rootPath) == false)
                {
                    Directory.CreateDirectory(Fragment_Preferences.rootPath);
                }

                var tileSource = TileCache.GetOSMBasemap(Fragment_Preferences.rootPath + "/" + Fragment_Preferences.CacheDB);
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
    }
}
