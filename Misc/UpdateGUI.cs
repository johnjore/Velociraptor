using Android.Content.Res;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Xamarin.Essentials;
using Mapsui.UI.Android;

using Android;
using Android.Locations;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using Android.Content;
using Android.Content.PM;
using Android.Views;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using AndroidX.AppCompat.App;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Serilog.Sink.AppCenter;
using Google.Android.Material.Navigation;
using TelegramSink;
using AndroidX.Fragment.App;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Snackbar;


namespace Velociraptor
{
    internal class UpdateScreen
    {
        // GUI
        public static TextView? txtlatitude = null;
        public static TextView? txtlong = null;
        public static TextView? txtspeed = null;
        public static TextView? txtstreetname = null;
        public static TextView? txtspeedlimit = null;
        public static TextView? txtspeeding = null;
        public static TextView? txtgpsdatetime = null;
        public static TextView? txtlastupdated = null;
        public static TextView? txtcountryname = null;
        public static MapControl? mapControl = null;

        public static void UpdateGUI(Android.Locations.Location? cLocation)
        {
            var cActivity = Platform.CurrentActivity;
            txtlatitude = cActivity.FindViewById<TextView>(Resource.Id.txtlatitude);
            txtlong = cActivity.FindViewById<TextView>(Resource.Id.txtlong);
            txtspeed = cActivity.FindViewById<TextView>(Resource.Id.txtspeed);
            txtspeeding = cActivity.FindViewById<TextView>(Resource.Id.txtspeeding);
            txtstreetname = cActivity.FindViewById<TextView>(Resource.Id.txtstreetname);
            txtspeedlimit = cActivity.FindViewById<TextView>(Resource.Id.txtspeedlimit);
            txtgpsdatetime = cActivity.FindViewById<TextView>(Resource.Id.txtgpsdatetime);
            txtlastupdated = cActivity.FindViewById<TextView>(Resource.Id.txtlastupdated);
            txtcountryname = cActivity.FindViewById<TextView>(Resource.Id.txtcountryname);
            mapControl = cActivity.FindViewById<MapControl>(Resource.Id.mapcontrol);

            if ((txtlatitude is null) ||
                (txtlong is null) ||
                (txtspeed is null) ||
                (txtstreetname is null) ||
                (txtspeedlimit is null) ||
                (txtspeeding is null) ||
                (txtgpsdatetime is null) ||
                (txtlastupdated is null) ||
                (mapControl is null)) 
            {
                Serilog.Log.Error($"UpdateGUI - One or more GUI objects are null");
                return;
            }

            if (cLocation == null)
            {
                Serilog.Log.Warning($"UpdateGUI - currentLocation is null, set all TextView fields to N/A");
                txtlatitude.Text = cActivity.Resources?.GetString(Resource.String.str_na);
                txtlong.Text = cActivity.Resources?.GetString(Resource.String.str_na);
                txtspeed.Text = cActivity.Resources?.GetString(Resource.String.str_na);
                txtstreetname.Text = cActivity.Resources?.GetString(Resource.String.str_na);
                txtspeedlimit.Text = cActivity.Resources?.GetString(Resource.String.str_na);
                txtspeeding.Text = cActivity.Resources?.GetString(Resource.String.str_na);
                txtgpsdatetime.Text = cActivity.Resources?.GetString(Resource.String.str_na);
                return;
            }

            //Updated
            txtlastupdated.Text = (DateTime.Now).ToString("HH:mm:ss");

            //GPS Information
            Serilog.Log.Debug($"UpdateGUI - Update GPS related TextView fields");
            txtlatitude.Text = cLocation.Latitude.ToString("0.00000");
            txtlong.Text = cLocation.Longitude.ToString("0.00000");

            /*
            //Update map
            if (MainActivity.mapControl is not null)
            {
                var (x, y) = SphericalMercator.FromLonLat(currentLocation.Longitude, currentLocation.Latitude);
                //MainActivity.mapControl.Map.Home = n => n.CenterOnAndZoomTo(new MPoint(x, y), n.Resolutions[17]);  //0 zoomed out-19 zoomed in
                MainActivity.mapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 1);

                //Rotate map so it matches direction of travel
                if (currentLocation.HasBearing)
                {
                    if (OperatingSystem.IsAndroidVersionAtLeast(26))
                    {
                        if (currentLocation.HasBearingAccuracy)
                        {
                            MainActivity.mapControl.Rotation = currentLocation.Bearing;
                        }
                    }
                    else
                    {
                        MainActivity.mapControl.Rotation = currentLocation.Bearing;
                    }
                }
                else
                {
                    MainActivity.mapControl.Rotation = 0;
                }
                */

            /*
            Point? sphericalMercatorCoordinate = null;
            ILayer layer = MainActivity.mapControl.Map.Layers.FindLayer(PrefsActivity.LocationLayerName).FirstOrDefault();
            if (layer == null)
            {
                MainActivity.mapControl.Map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));
                layer = MainActivity.mapControl.Map.Layers.FindLayer(PrefsActivity.LocationLayerName).FirstOrDefault();
            }


            MainActivity.mapControl.Map.Refresh();
            MainActivity.mapControl.Refresh();
        }
        */

            //Convert GPS time in ms since epoch in UTC to local datetime
            DateTime gpslocalDateTime = default;
            try
            {
                TimeZoneInfo systemTimeZone = TimeZoneInfo.Local;
                DateTime gpsUTCDateTime = DateTimeOffset.FromUnixTimeMilliseconds(cLocation.Time).DateTime;
                gpslocalDateTime = TimeZoneInfo.ConvertTimeFromUtc(gpsUTCDateTime, systemTimeZone);
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
            }
            finally
            {
                txtgpsdatetime.Text = gpslocalDateTime.ToString("HH:mm:ss");
            }

            //Update GUI with OSM data (streetname and street max speed)
            //string streetName = DataItinero.GetStreetname();
            var streetName = LocationForegroundService.GetStreetname();
            if (streetName == String.Empty || streetName is null)
            {
                txtstreetname.Text = "Unknown street/road";
            }
            else
            {
                txtstreetname.Text = streetName;
            }

            string streetSpeed = LocationForegroundService.GetStreetSpeed();
            if (streetSpeed == String.Empty || streetSpeed is null)
            {
                txtspeedlimit.Text = Platform.AppContext?.Resources?.GetString(Resource.String.str_na);
            }
            else
            {
                txtspeedlimit.Text = streetSpeed + " " + cActivity.Resources?.GetString(Resource.String.str_kmh);
            }

            //GPS Speed?
            if (cLocation.HasSpeed == false)
            {
                Serilog.Log.Debug($"UpdateGUI - No Speed information. Update GUI and return");
                txtspeed.Text = Platform.AppContext?.Resources?.GetString(Resource.String.str_na);
                txtspeeding.Text = String.Empty;

                return;
            }

            int carspeed_kmh = (int)(cLocation.Speed * 3.6);
            txtspeed.Text = carspeed_kmh.ToString() + " " + Platform.AppContext?.Resources?.GetString(Resource.String.str_kmh);

            //If streetspeed is not defined, we can't calculate if car is speeding or not
            if (streetSpeed == String.Empty || streetSpeed is null)
            {
                txtspeeding.Text = String.Empty;
                return;
            }

            if (Int32.TryParse(streetSpeed, out int streetspeed_int) == false)
            {
                Serilog.Log.Error($"UpdateGUI - Failed to convert streetspeed string to int. Clear speeding field and return");
                txtspeeding.Text = String.Empty;

                return;
            }

            int speedmargin = Int32.Parse(Preferences.Get("SpeedGracePercent", PrefsFragment.default_speed_margin.ToString()));
            if (carspeed_kmh <= (int)(streetspeed_int * speedmargin / 100 + streetspeed_int))
            {
                txtspeeding.Text = String.Empty;
            }
            else
            {
                txtspeeding.Text = Platform.AppContext?.Resources?.GetString(Resource.String.str_speeding);
            }
        }

        /*
        public static ILayer CreateLocationLayer(Point GPSLocation)
        {
            
            return new MemoryLayer
            {
                Name = PrefsActivity.LocationLayerName,
                DataSource = CreateMemoryProviderWithDiverseSymbols(GPSLocation),
                Style = null,
                IsMapInfoLayer = true
            };
        }
        */
        /*
        private static MemoryProvider CreateMemoryProviderWithDiverseSymbols(Point GPSLocation)
        {
            return new MemoryProvider(CreateLocationMarker(GPSLocation));
        }
        */
        /*
        private static GeometryFeature CreateLocationMarker(Point GPSLocation)
        {
            var features = new GeometryFeature
            {
                CreateLocationFeature(GPSLocation)
            };
            return features;
        }
        */
        /*
        private static IFeature CreateLocationFeature(Point GPSLocation)
        {
            
            var feature = new GeometryFeature { Geometry = GPSLocation };
            
            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.5f,
                Fill = null,
                Outline = new Pen { Color = Mapsui.Styles.Color.Blue, Width = 2.0 }
            });
            
            return feature;            
        }
        */        
    }
}
