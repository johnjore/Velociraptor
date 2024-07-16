using Android.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xamarin.Essentials;
using Microsoft.AppCenter.Crashes;
using Mapsui.UI.Android;
using Mapsui.UI;

namespace Velociraptor.Fragments
{
    public class Fragment_Text : AndroidX.Fragment.App.Fragment
    {
        // GUI
        private static TextView? txtlatitude = null;
        private static TextView? txtlong = null;
        private static TextView? txtspeed = null;
        private static TextView? txtstreetname = null;
        private static TextView? txtspeedlimit = null;
        private static TextView? txtspeeding = null;
        private static TextView? txtgpsdatetime = null;
        private static TextView? txtlastupdated = null;
        public static TextView? txtcountryname = null;

        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            try
            {
                var view = inflater.Inflate(Resource.Layout.fragment_text, container, false);
                view?.SetBackgroundColor(Android.Graphics.Color.White);

                txtlatitude = view?.FindViewById<TextView>(Resource.Id.txtlatitude);
                txtlong = view?.FindViewById<TextView>(Resource.Id.txtlong);
                txtspeed = view?.FindViewById<TextView>(Resource.Id.txtspeed);
                txtstreetname = view?.FindViewById<TextView>(Resource.Id.txtstreetname);
                txtspeedlimit = view?.FindViewById<TextView>(Resource.Id.txtspeedlimit);
                txtspeeding = view?.FindViewById<TextView>(Resource.Id.txtspeeding);
                txtgpsdatetime = view?.FindViewById<TextView>(Resource.Id.txtgpsdatetime);
                txtlastupdated = view?.FindViewById<TextView>(Resource.Id.txtlastupdated);
                txtcountryname = view?.FindViewById<TextView>(Resource.Id.txtcountryname);

                return view;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Fragment_text - OnCreateView()");
            }

            return null;
        }

        public static void UpdateGUI(Android.Locations.Location? cLocation)
        {
            if ((txtlatitude is null) ||
                (txtlong is null) ||
                (txtspeed is null) ||
                (txtstreetname is null) ||
                (txtspeedlimit is null) ||
                (txtspeeding is null) ||
                (txtgpsdatetime is null) ||
                (txtlastupdated is null) ||
                (txtcountryname is null))
            {
                Serilog.Log.Error($"UpdateGUI - One or more GUI objects are null");
                return;
            }

            if (cLocation == null)
            {
                Serilog.Log.Warning($"UpdateGUI - currentLocation is null, set all TextView fields to N/A");
                txtlatitude.Text = Platform.CurrentActivity.Resources?.GetString(Resource.String.str_na);
                txtlong.Text = Platform.CurrentActivity.Resources?.GetString(Resource.String.str_na);
                txtspeed.Text = Platform.CurrentActivity.Resources?.GetString(Resource.String.str_na);
                txtstreetname.Text = Platform.CurrentActivity.Resources?.GetString(Resource.String.str_na);
                txtspeedlimit.Text = Platform.CurrentActivity.Resources?.GetString(Resource.String.str_na);
                txtspeeding.Text = Platform.CurrentActivity.Resources?.GetString(Resource.String.str_na);
                txtgpsdatetime.Text = Platform.CurrentActivity.Resources?.GetString(Resource.String.str_na);
                txtcountryname.Text = Platform.CurrentActivity.Resources?.GetString(Resource.String.str_na);
                return;
            }

            //Updated
            txtlastupdated.Text = (DateTime.Now).ToString("HH:mm:ss");
            txtcountryname.Text = InitializeLocationData.GetCountryName();
            
            //GPS Information
            Serilog.Log.Debug($"UpdateGUI - Update GPS related TextView fields");
            txtlatitude.Text = cLocation.Latitude.ToString("0.00000");
            txtlong.Text = cLocation.Longitude.ToString("0.00000");

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
            var streetName = LocationForegroundService.GetStreetname();
            if (streetName == null || streetName == String.Empty)
            {
                txtstreetname.Text = "Unknown street/road";
            }
            else
            {
                txtstreetname.Text = streetName;
            }

            string streetSpeed = LocationForegroundService.GetStreetSpeed();
            if (streetSpeed == null || streetSpeed == String.Empty)
            {
                txtspeedlimit.Text = Platform.AppContext?.Resources?.GetString(Resource.String.str_na);
            }
            else
            {
                txtspeedlimit.Text = streetSpeed + " " + Platform.CurrentActivity.Resources?.GetString(Resource.String.str_kmh);
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
            if (streetSpeed == null || streetSpeed == String.Empty)
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

            int speedmargin = Int32.Parse(Preferences.Get("SpeedGracePercent", Fragment_Preferences.default_speed_margin.ToString()));
            if (carspeed_kmh <= (int)(streetspeed_int * speedmargin / 100 + streetspeed_int))
            {
                txtspeeding.Text = String.Empty;
            }
            else
            {
                txtspeeding.Text = Platform.AppContext?.Resources?.GetString(Resource.String.str_speeding);
            }
        }
    }
}
