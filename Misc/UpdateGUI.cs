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

namespace Velociraptor
{
    internal class UpdateScreen
    {
        public static void UpdateGUI(Android.Locations.Location? cLocation)
        {
            if ((MainActivity.txtlatitude is null) ||
                (MainActivity.txtlong is null) ||
                (MainActivity.txtspeed is null) ||
                (MainActivity.txtstreetname is null) ||
                (MainActivity.txtspeedlimit is null) ||
                (MainActivity.txtspeeding is null) ||
                (MainActivity.txtgpsdatetime is null) ||
                (MainActivity.txtlastupdated is null) ||
                (MainActivity.mapControl is null)) 
            {
                Serilog.Log.Error($"UpdateGUI - One or more GUI objects are null");
                return;
            }

            if (cLocation == null)
            {
                Serilog.Log.Warning($"UpdateGUI - currentLocation is null, set all TextView fields to N/A");
                MainActivity.txtlatitude.Text = Platform.AppContext.Resources.GetString(Resource.String.str_na);
                MainActivity.txtlong.Text = Platform.AppContext.Resources.GetString(Resource.String.str_na);
                MainActivity.txtspeed.Text = Platform.AppContext.Resources.GetString(Resource.String.str_na);
                MainActivity.txtstreetname.Text = Platform.AppContext.Resources.GetString(Resource.String.str_na);
                MainActivity.txtspeedlimit.Text = Platform.AppContext.Resources.GetString(Resource.String.str_na);
                MainActivity.txtspeeding.Text = Platform.AppContext.Resources.GetString(Resource.String.str_na);
                MainActivity.txtgpsdatetime.Text = Platform.AppContext.Resources.GetString(Resource.String.str_na);
                return;
            }

            //Updated
            MainActivity.txtlastupdated.Text = (DateTime.Now).ToString("HH:mm:ss");

            //GPS Information
            Serilog.Log.Debug($"UpdateGUI - Update GPS related TextView fields");
            MainActivity.txtlatitude.Text = cLocation.Latitude.ToString("0.00000");
            MainActivity.txtlong.Text = cLocation.Longitude.ToString("0.00000");

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
                MainActivity.txtgpsdatetime.Text = gpslocalDateTime.ToString("HH:mm:ss");
            }

            //Update GUI with OSM data (streetname and street max speed)
            //string streetName = DataItinero.GetStreetname();
            var streetName = LocationForegroundService.GetStreetname();
            if (streetName == String.Empty || streetName is null)
            {
                MainActivity.txtstreetname.Text = "Unknown street/road";
            }
            else
            {
                MainActivity.txtstreetname.Text = streetName;
            }

            string streetSpeed = LocationForegroundService.GetStreetSpeed();
            if (streetSpeed == String.Empty || streetSpeed is null)
            {
                MainActivity.txtspeedlimit.Text = Platform.AppContext.Resources.GetString(Resource.String.str_na);
            }
            else
            {
                MainActivity.txtspeedlimit.Text = streetSpeed + " " + Platform.AppContext.Resources.GetString(Resource.String.str_kmh);
            }

            //GPS Speed?
            if (cLocation.HasSpeed == false)
            {
                Serilog.Log.Debug($"UpdateGUI - No Speed information. Update GUI and return");
                MainActivity.txtspeed.Text = Platform.AppContext.Resources.GetString(Resource.String.str_na);
                MainActivity.txtspeeding.Text = String.Empty;

                return;
            }

            int carspeed_kmh = (int)(cLocation.Speed * 3.6);
            MainActivity.txtspeed.Text = carspeed_kmh.ToString() + " " + Platform.AppContext.Resources.GetString(Resource.String.str_kmh);

            //If streetspeed is not defined, we can't calculate if car is speeding or not
            if (streetSpeed == String.Empty || streetSpeed is null)
            {
                MainActivity.txtspeeding.Text = String.Empty;
                return;
            }

            if (Int32.TryParse(streetSpeed, out int streetspeed_int) == false)
            {
                Serilog.Log.Error($"UpdateGUI - Failed to convert streetspeed string to int. Clear speeding field and return");
                MainActivity.txtspeeding.Text = String.Empty;

                return;
            }

            int speedmargin = Int32.Parse(Preferences.Get("SpeedGracePercent", PrefsActivity.default_speed_margin.ToString()));
            if (carspeed_kmh <= (int)(streetspeed_int * speedmargin / 100 + streetspeed_int))
            {
                MainActivity.txtspeeding.Text = String.Empty;
            }
            else
            {
                MainActivity.txtspeeding.Text = Platform.AppContext.Resources.GetString(Resource.String.str_speeding);
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
