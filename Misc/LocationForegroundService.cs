using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Util;
using Android.Graphics;
using Android.Content;
using Android.Runtime;
using Android.Locations;
using Android.OS;
using Android.Preferences;
using Android.Views;
using Android.Media;
using AndroidX.Core.App;
using AndroidX.Fragment.App;
using Itinero.Attributes;
using Itinero;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Xamarin.Essentials;
using Random = System.Random;
using Wibci.CountryReverseGeocode.Models;
using Wibci.CountryReverseGeocode;

namespace Velociraptor
{
    [Service(ForegroundServiceType = Android.Content.PM.ForegroundService.TypeLocation)]
    public class LocationForegroundService : Service, ILocationListener
    {
        private static LocationManager? locationManager = null;
        private static bool isStarted;                                         //Is ForegroundService running?

        //OSM Data
        private static Itinero.RouterDb routerDb = new();
        private static Itinero.Router? router = null;
        private static Itinero.Profiles.Profile? profile = null;

        private static string countryName = string.Empty;
        private static string streetName = string.Empty;
        private static string streetSpeed = string.Empty;

        //Debug
        private string strLastUpdate = string.Empty;
        private static readonly Random rand = new();
        private static int intCounter = 0;

        public static string GetStreetname()
        {
            return streetName;
        }

        public static string GetStreetSpeed()
        {
            return streetSpeed;
        }

        public void OnProviderDisabled(string provider)
        {
            Serilog.Log.Debug("OnProviderDisabled - '" + provider + "'");
        }

        public void OnProviderEnabled(string provider)
        {
            Serilog.Log.Debug("OnProviderEnabled - '" + provider + "'");
        }

        public void OnStatusChanged(string? provider, [GeneratedEnum] Availability status, Bundle? extras)
        {
            Serilog.Log.Debug("OnStatusChanged - '" + provider + "'");

            locationManager?.RemoveUpdates(this);

            string lProvider = InitializeLocationManager();

            if (lProvider != null && locationManager != null)
            {
                var intDistance = PrefsFragment.intDistance;
                var intTimer = PrefsFragment.intTimer;
                Serilog.Log.Debug($"ServiceRunning: Creating callback service for LocationUpdates, every " + intTimer.ToString() + "s and " + intDistance.ToString() + "m");
                locationManager.RequestLocationUpdates(lProvider, intTimer, intDistance, this, Looper.MainLooper);
            }
        }

        public void OnLocationChanged(Android.Locations.Location location)
        {
            Serilog.Log.Debug("OnLocationChanged - " + DateTime.Now.ToString("HH:mm:ss"));

            Task.Run(() => ProcessLocationData(location, DateTime.Now));

            //Foreground?
            ActivityManager.RunningAppProcessInfo appProcessInfo = new ActivityManager.RunningAppProcessInfo();
            ActivityManager.GetMyMemoryState(appProcessInfo);
            bool inForeground = (appProcessInfo.Importance == Importance.Foreground || appProcessInfo.Importance == Importance.Visible);

            if (inForeground)
            {
                Task.Run(() => UpdateScreen.UpdateGUI(location));
            }
        }

        private Task? ProcessLocationData(Android.Locations.Location? cLocation, DateTime datetimeTaskStarted)
        {
            if (cLocation is null)
            {
                return Task.CompletedTask;
            }

            //Its to old
            if (datetimeTaskStarted.AddMinutes(1) < DateTime.Now)
            {
                Serilog.Log.Warning($"Date of location data '{datetimeTaskStarted}' is older than '{DateTime.Now}'");
                return Task.CompletedTask;
            }

            intCounter += 1;
            Serilog.Log.Debug($"Location Changed. Counter incremented: {intCounter}");

            //Get/Change/Open current Country
            var cName = GetCurrentCountry(cLocation);
            if (cName != countryName && cName != string.Empty || router == null)
            {
                Serilog.Log.Information($"Country Changed from '{countryName}' to '{cName}'");
                countryName = cName;
                OpenCountryDataBase(countryName);
            }

            int carspeed_kmh = -1;
            if (cLocation?.Speed != null && cLocation?.Speed >= 0)
            {
                carspeed_kmh = (int)(cLocation.Speed * 3.6);

                //Lookup street and speed information
                (streetName, streetSpeed) = GetStreetInformation(cLocation);

                if (streetSpeed != null && streetSpeed != string.Empty)
                {
                    Serilog.Log.Debug($"Parse street speed");
                    CheckStreetInformation(cLocation, streetSpeed);
                }
            }

            //Lookup street and speed information, PBF Version
            //(pbf_StreetName, pbf_StreetSpeed) = GetStreetInformation_pbf(cLocation);

            //Notification
            string strText = String.Empty;
            if (streetName != null && streetName.Length > 0)
            {
                strText += streetName + ", ";
            }
            
            if (cLocation?.Speed != null && cLocation?.Speed >= 0)
            {
                strText += carspeed_kmh.ToString() + "/";
            }
            else
            {
                strText += "?/";
            }

            if (streetSpeed == null)
            {
                strText += "?";
            }
            else
            {
                strText += streetSpeed;
            }

            strText += "," + strLastUpdate;

            NotificationCompat.Builder notificationBuilder = new(this, PrefsFragment.NOTIFICATION_CHANNEL_ID);
            Notification notification = notificationBuilder
                .SetAutoCancel(true)
                .SetSmallIcon(Resource.Drawable.dyno)
                .SetContentText(strText)
                .SetPriority((int)NotificationPriority.Default)
                .SetCategory(Notification.CategoryRecommendation)
                .SetContentIntent(BuildIntentToShowMainActivity())
                .SetSound(null)
                .Build();
            NotificationManager? nManager = GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
            nManager?.Notify(PrefsFragment.SERVICE_RUNNING_NOTIFICATION_ID, notification);

            intCounter -= 1;
            strLastUpdate = ", " + cLocation?.Latitude.ToString("0.00000000") + ", " + cLocation?.Longitude.ToString("0.00000000") + "," + rand.Next(0, 9) + "/" + intCounter.ToString();

            return Task.CompletedTask;
        }

        public string InitializeLocationManager()
        {
            locationManager = GetSystemService(LocationService) as LocationManager;

            if (locationManager == null)
            {
                Serilog.Log.Error("locationManager is null. Can't continue");
                return string.Empty;
            }

            IList<string> acceptableLocationProviders;

            if (OperatingSystem.IsAndroidVersionAtLeast(34))
            {
                acceptableLocationProviders = locationManager.GetProviders(true);
            }
            else
            {
                Criteria criteriaForLocationService = new()
                {
                    Accuracy = Accuracy.Fine,
                    SpeedRequired = true,
                    SpeedAccuracy = Accuracy.Fine
                };

                acceptableLocationProviders = locationManager.GetProviders(criteriaForLocationService, true);
            }

            //Choose GPS over all other options
            string? provider = acceptableLocationProviders.Where(x => x.Equals("gps")).FirstOrDefault();
            if (provider != null)
            {
                return provider;
            }

            //Else choose first option offered
            if (acceptableLocationProviders.Any())
            {
                Serilog.Log.Debug($"LocationProvider: '{acceptableLocationProviders.First()}'");
                return acceptableLocationProviders.First();
            }

            return string.Empty;
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            if (intent is null || intent.Action is null)
            {
                Serilog.Log.Warning($"OnStartCommand: intent or intent.Action is null ");
                return StartCommandResult.Sticky;
            }

            if (intent.Action.Equals(PrefsFragment.ACTION_START_SERVICE))
            {
                if (isStarted)
                {
                    Serilog.Log.Information($"OnStartCommand: The location service is already running");
                }
                else
                {
                    Serilog.Log.Information($"OnStartCommand: The location service is starting");

                    OnStatusChanged(null, Availability.Available, null);

                    isStarted = true;
                    RegisterForegroundService();
                }
            }
            else if (intent.Action.Equals(PrefsFragment.ACTION_STOP_SERVICE))
            {
                Serilog.Log.Information($"OnStartCommand: The location service is stopping.");

                if (OperatingSystem.IsAndroidVersionAtLeast(24))
                {
                    StopForeground(StopForegroundFlags.Remove);
                } 
                else
                {
                    StopForeground(true);
                }

                StopSelf();
                isStarted = false;
            }

            return StartCommandResult.Sticky;
        }

        public override IBinder? OnBind(Intent? intent)
        {
            // Return null because this is a pure started service. A hybrid service would return a binder that would allow access to the GetFormattedStamp() method.
            return null;
        }

        public override void OnDestroy()
        {
            Serilog.Log.Information($"OnDestroy: The location service is shutting down.");

            //Service is not longer "Started"
            isStarted = false;

            try
            {
                //Stop listing to location updates
                locationManager?.RemoveUpdates(this);

                // Remove the notifications
                NotificationManager? notificationManager = GetSystemService(NotificationService) as NotificationManager;
                notificationManager?.Cancel(PrefsFragment.SERVICE_RUNNING_NOTIFICATION_ID);
                notificationManager?.Cancel(PrefsFragment.NOTIFICATION_ID_HIGH);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"Crashed: " + ex.ToString());
                Crashes.TrackError(ex);
            }

            base.OnDestroy();
        }

        private void RegisterForegroundService()
        {
            if (Resources is null)
            {
                Serilog.Log.Warning($"RegisterForegroundService: resources is null, returning early");
                return;
            }

            NotificationManager? nManager = GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                NotificationChannel nChannel = new(PrefsFragment.NOTIFICATION_CHANNEL_ID, PrefsFragment.channelName, NotificationImportance.Low)
                {
                    LockscreenVisibility = NotificationVisibility.Private,
                };

                nManager?.CreateNotificationChannel(nChannel);
            }

            NotificationCompat.Builder notificationBuilder = new(this, PrefsFragment.NOTIFICATION_CHANNEL_ID);
            Notification notification = notificationBuilder
                .SetOngoing(true)
                .SetSmallIcon(Resource.Drawable.dyno)
                .SetContentText("")
                .SetAutoCancel(true)
                .SetPriority((int)NotificationPriority.Low)
                .SetCategory(Notification.CategoryRecommendation)
                .SetContentIntent(BuildIntentToShowMainActivity())
                .Build();

            // Enlist this instance as a foreground service
            StartForeground(PrefsFragment.SERVICE_RUNNING_NOTIFICATION_ID, notification);

            //Channel for notifications
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                NotificationChannel nChannel = new(PrefsFragment.NOTIFICATION_CHANNEL_ID, PrefsFragment.channelName, NotificationImportance.Low)
                {
                    LockscreenVisibility = NotificationVisibility.Private,
                };
                nChannel.SetSound(null, null);
                nChannel.EnableVibration(false);
                nManager?.CreateNotificationChannel(nChannel);
            }
        }

        /// <summary>
        /// Builds a PendingIntent that will display the main activity of the app. This is used when the 
        /// user taps on the notification; it will take them to the main activity of the app.
        /// </summary>
        /// <returns>The content intent.</returns>
        PendingIntent? BuildIntentToShowMainActivity()
        {
            var notificationIntent = new Intent(this, typeof(MainActivity));
            notificationIntent.SetAction(PrefsFragment.ACTION_MAIN_ACTIVITY);
            notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTask);
            notificationIntent.PutExtra(PrefsFragment.SERVICE_STARTED_KEY, true);

            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                return PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            } 
            else
            {
                return PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent);
            }
        }

        private static string GetCurrentCountry(Android.Locations.Location? cLocation)
        {
            if (cLocation == null)
            {
                Serilog.Log.Error($"cLocation should not be null here");
                return string.Empty;
            }

            GeoLocation? country = new() { Latitude = cLocation.Latitude, Longitude = cLocation.Longitude };
            LocationInfo locationInfo = new CountryReverseGeocodeService().FindCountry(country);

            if (locationInfo == null)
            {
                Serilog.Log.Error($"locationInfo is null. Unable to determine which router database to use");
                return string.Empty;
            }

            var cName = locationInfo.Name.ToLower();
            Serilog.Log.Information($"FindCountry: '{cName}'");
            return cName;
        }

        private static void OpenCountryDataBase(string cName)
        {
            var dbInfo = new FileInfo(FileSystem.AppDataDirectory + "/" + cName + ".db");

            if (dbInfo.Exists == false)
            {
                Serilog.Log.Warning($"RouterDB does NOT exist for '{cName}'. Can't continue until user installs it");
                if (routerDb.HasContracted)
                {
                    try
                    {
                        routerDb.RemoveContracted(profile);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error($"Crashed: " + ex.ToString());
                        Crashes.TrackError(ex);
                    }
                }

                router = null;
                profile = null;

                return;
            }

            try
            {
                Serilog.Log.Information($"RouterDB exists for '{cName}', using it.");
                var stream = new FileInfo(FileSystem.AppDataDirectory + "/" + cName + ".db").OpenRead();
                routerDb = RouterDb.Deserialize(stream, RouterDbProfile.NoCache);
                profile = routerDb.GetSupportedProfile("car");
                router = new Router(routerDb);
            }
            catch
            {
                Serilog.Log.Error($"Crashed using routerdb file for {cName}. Delete");
                new FileInfo(FileSystem.AppDataDirectory + "/" + cName + ".db").Delete();
                return;
            }

            Serilog.Log.Information($"RouterDB initialized for '{cName}'");

            //PBF
            var pbfInfo = new FileInfo(FileSystem.AppDataDirectory + "/" + cName + ".osm.pbf");

            if (pbfInfo.Exists == false)
            {
                Serilog.Log.Warning($"PBF does NOT exist for '{cName}'. Can't continue until user installs it");

                return;
            }

            try
            {
                Serilog.Log.Information($"PBF exists for '{cName}', using it.");
                var stream = new FileInfo(FileSystem.AppDataDirectory + "/" + cName + ".osm.pbf").OpenRead();

            }
            catch
            {
                Serilog.Log.Error($"Crashed using PBF file for {cName}. Delete");
                new FileInfo(FileSystem.AppDataDirectory + "/" + cName + ".osm.pbf").Delete();
                return;
            }

            Serilog.Log.Information($"PBF initialized for '{cName}'");
        }

        private static (string StreetName, string StreetSpeed) GetStreetInformation(Android.Locations.Location? cLocation)
        {
            Serilog.Log.Debug($"Parse cLocation to get streetname and maxspeed");

            if (cLocation == null)
            {
                Serilog.Log.Error($"if cLocation is null, we can't lookup streetname or maxspeed");
                return (string.Empty, string.Empty);
            }

            /*var rp = router.TryResolve(profile, new Itinero.LocalGeo.Coordinate((float)cLocation.Latitude, (float)cLocation.Longitude));
            if (rp == null)
            {
                return (StreetName: "2", StreetSpeed: "2");
            }

            if (rp.IsError == true)
            {
                Serilog.Log.Debug($"ErrorMessage: '{rp.ErrorMessage}' ");
                //return (StreetName: "3", StreetSpeed: "3");
            }
            */

            string sName = string.Empty;
            string sSpeed = string.Empty;
            try
            {
                RouterPoint rp = router.Resolve(profile, new Itinero.LocalGeo.Coordinate((float)cLocation.Latitude, (float)cLocation.Longitude), 10.0f);
                //RouterPoint rp = router.Resolve(profile, new Itinero.LocalGeo.Coordinate(-37.811587f, 144.883007f), 25.0f);
                Itinero.Data.Network.RoutingEdge edge = routerDb.Network.GetEdge(rp.EdgeId);
                IAttributeCollection attributes = routerDb.GetProfileAndMeta(edge.Data.Profile, edge.Data.MetaId);

                attributes.TryGetValue("name", out sName);
                attributes.TryGetValue("maxspeed", out sSpeed);

                Serilog.Log.Debug($"StreetName/Speed: '{sName}', '{sSpeed}'");
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, $"Unable to parse: '{cLocation.Latitude}', '{cLocation.Longitude}'");
            }
            
            return (StreetName: sName, StreetSpeed: sSpeed);
        }

        private void CheckStreetInformation(Android.Locations.Location? cLocation, string sSpeed)
        {
            if (cLocation == null || sSpeed is null)
            {
                Serilog.Log.Error($"null variables. Return");
                return;
            }

            if (cLocation.HasSpeed == false || sSpeed == String.Empty)
            {
                Serilog.Log.Warning($"No Speed information. Nothing todo. Return");
                return;
            }

            //Convert from m/s to km/h
            int carspeed_kmh = (int)(cLocation.Speed * 3.6);
            Serilog.Log.Debug($"Convert car speed from " + cLocation.Speed.ToString() + "m/s to " + carspeed_kmh.ToString() + "km/h");

            //Flying?
            if (carspeed_kmh >= 200)
            {
                Serilog.Log.Warning($"Travelling at excessive speed. Ignoring");
                return;
            }

            //Convert street speed from string to int
            if (Int32.TryParse(sSpeed, out int streetspeed_int) == false)
            {
                Serilog.Log.Error($"Failed to convert streetspeed string to int");
                return;
            }

            //If not speeding, we're done
            int speedmargin = Int32.Parse(Preferences.Get("SpeedGracePercent", PrefsFragment.default_speed_margin.ToString()));
            if (carspeed_kmh <= (int)(streetspeed_int * speedmargin / 100 + streetspeed_int))
            {
                Serilog.Log.Debug($"Not Speeding - Done");
                return;
            }

            Serilog.Log.Debug($"Show High Priority Notification");
            
            NotificationManager? nManager = GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                NotificationChannel nChannel = new(PrefsFragment.NOTIFICATION_CHANNEL_ID_HIGH, PrefsFragment.channelName, NotificationImportance.High)
                {
                    LockscreenVisibility = NotificationVisibility.Private,
                };

                nManager?.CreateNotificationChannel(nChannel);
            }

            NotificationCompat.Builder notificationBuilder = new(this, PrefsFragment.NOTIFICATION_CHANNEL_ID_HIGH);
            Notification notification = notificationBuilder
                .SetAutoCancel(true)
                .SetSmallIcon(Resource.Drawable.dyno)
                .SetContentText(intCounter.ToString() + ": " + Resources?.GetString(Resource.String.str_speeding) + ": " + carspeed_kmh.ToString() + " in a " + sSpeed + " zone")
                .SetPriority((int)NotificationPriority.High)
                .SetCategory(Notification.CategoryRecommendation)
                .SetContentIntent(BuildIntentToShowMainActivity())
                .SetSound(null)
                .Build();

            nManager?.Notify(PrefsFragment.NOTIFICATION_ID_HIGH, notification);

            Serilog.Log.Debug($"Play a sound");
            var soundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            var r = RingtoneManager.GetRingtone(Android.App.Application.Context, soundUri);
            r?.Play();

            nManager?.Cancel(PrefsFragment.NOTIFICATION_ID_HIGH);
        }
    }
}
