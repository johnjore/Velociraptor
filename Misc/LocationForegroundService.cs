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
using Itinero.Profiles;
using Itinero.Attributes;
using Itinero.LocalGeo;
using Itinero;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;
using Serilog.Sink.AppCenter;
using Xamarin.Essentials;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Rendering;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI;
using Mapsui.UI.Android;
using Mapsui.Utilities;
using Mapsui.Widgets;
using Random = System.Random;
using Wibci.CountryReverseGeocode.Models;
using Wibci.CountryReverseGeocode;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using OsmSharp;
using OsmSharp.Geo;
using OsmSharp.Streams;
using static Velociraptor.Misc;
using SkiaSharp;


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
        private static Android.Locations.Location? currentLocation; /**///Remove me

        //PBF
        private static string pbf_StreetName = string.Empty;
        private static string pbf_StreetSpeed = string.Empty;


        //Debug
        private string strLastUpdate = string.Empty;
        private static readonly Random rand = new();
        private static int intCounter = 0;

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
                var intTimer = 3000;
                var intDistance = 0;
                Serilog.Log.Debug($"ServiceRunning: Creating callback service for LocationUpdates, every " + intTimer.ToString() + "s and " + intDistance.ToString() + "m");
                locationManager.RequestLocationUpdates(lProvider, intTimer, intDistance, this, Looper.MainLooper);
            }
        }

        public void OnLocationChanged(Android.Locations.Location location)
        {
            /**///Remove this global variable
            currentLocation = location;
            Task.Run(() => ProcessLocationData(location, ));
        }

        private Task? ProcessLocationData(Android.Locations.Location? cLocation)
        {
            if (cLocation is null)
            {
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

            //Lookup street and speed information
            (streetName, streetSpeed) = GetStreetInformation(cLocation);
            if (streetSpeed != null && streetSpeed != string.Empty)
            {
                CheckStreetInformation(cLocation, streetSpeed);
            }

            //Lookup street and speed information, PBF Version
            //(pbf_StreetName, pbf_StreetSpeed) = GetStreetInformation_pbf(cLocation);

            intCounter -= 1;
            strLastUpdate = streetName?.ToString() + "," + streetSpeed?.ToString() + ", " + pbf_StreetName?.ToString() + ", " + pbf_StreetSpeed?.ToString() + ", " + cLocation.Latitude.ToString() + ", " + cLocation.Longitude.ToString() + "," + DateTime.Now.ToString("mm:ss") + "," + rand.Next(0, 9) + "," + intCounter.ToString();

            return Task.CompletedTask;
        }

        private static (string pbf_StreetName, string pbf_StreetSpeed) GetStreetInformation_pbf(Android.Locations.Location? cLocation)
        {
            string sName = string.Empty;
            string sSpeed = string.Empty;

            var bbox = Misc.GetBoundingBox(cLocation, 100);

            if (bbox == null)
            {
                return (pbf_StreetName: string.Empty, pbf_StreetSpeed: string.Empty);
            }

            if (bbox.MinPoint == null || bbox.MaxPoint == null)
            {
                return (pbf_StreetName: string.Empty, pbf_StreetSpeed: string.Empty);
            }

            using (var fileStream = File.OpenRead(FileSystem.AppDataDirectory + "/" + /*countryName*/ "luxembourg" + ".osm.pbf"))
            {
                var source = new PBFOsmStreamSource(fileStream);
                //var region = source.FilterBox(bbox.MaxPoint.Latitude, bbox.MaxPoint.Longitude, bbox.MinPoint.Latitude, bbox.MinPoint.Longitude);
                var region = source.FilterBox(6.242969810172371f, 49.71720151392213f, 6.249192535136989f, 49.71520366157044f);
                var filtered = region.Where(x => x.Type == OsmSharp.OsmGeoType.Way || x.Type == OsmSharp.OsmGeoType.Node);
                var features = filtered.ToFeatureSource();

                var items = features.ToList();
                var ItemCount = items.Count();

                var lineStrings = items.Where(x => x.Geometry.GeometryType == "LineString").ToList();
                var lineStringCount = lineStrings.Count();
                
                var completeSource = region.ToComplete();
                //var filtered = from osmGeo in completeSource where osmGeo.Type == OsmGeoType.Way select osmGeo;

                /*var filtered = from osmGeo in progress
                               where osmGeo.Type == OsmSharp.OsmGeoType.Node ||
                                     (osmGeo.Type == OsmSharp.OsmGeoType.Way && osmGeo.Tags != null && osmGeo.Tags.Contains("power", "line"))
                               select osmGeo;
                */
                Serilog.Log.Debug(filtered.Count().ToString());
                Serilog.Log.Debug("ArfArf");
                Serilog.Log.Debug("ArfArf");

                //var filtered2 = from osmGeo in progress where osmGeo.Type == OsmGeoType.Way select osmGeo;


                /*                var filtered2 = from osmGeo in progress
                                                where osmGeo.Type == OsmSharp.OsmGeoType.Node ||
                                                      (osmGeo.Type == OsmSharp.OsmGeoType.Way && osmGeo.Tags != null) // && osmGeo.Tags.Contains("highway", "residential"))
                                                select osmGeo;
                */

                foreach (var osmGeo in filtered)
                {
                    Serilog.Log.Debug($"'{osmGeo.ToString()}'");
                }
                Serilog.Log.Debug($"Done");


                /*df_osm.loc[df_osm.tagkey == 'highway', ['id', 'tagvalue']].merge(
                    df_osm.loc[df_osm.tagkey == 'name', ['id', 'tagvalue']],
                    on = 'id', suffixes = ['_kind', '_name'])*/

                /*var features = filtered2.ToFeatureSource();

                var lineStrings = from feature in features
                                  where feature.Geometry is LineString
                                  select feature;
                */
                /*var featureCollection = new FeatureCollection();
                foreach (var feature in lineStrings)
                {
                    featureCollection.Add(feature);
                    //Serilog.Log.Debug($"'{feature.ToString()}'");
                }
                */
                Serilog.Log.Debug($"Done");
                //var json = ToJson(featureCollection);
            }



            return (pbf_StreetName: sName, pbf_StreetSpeed: sSpeed);
        }


        public string InitializeLocationManager()
        {
            locationManager = GetSystemService(LocationService) as LocationManager;

            if (locationManager == null)
            {
                Serilog.Log.Error("locationManager is null. Can't continue");
                return string.Empty;
            }

            Criteria criteriaForLocationService = new()
            {
                Accuracy = Accuracy.Fine,
                SpeedRequired = true,
                SpeedAccuracy = Accuracy.Fine
            };

            IList<string> acceptableLocationProviders = locationManager.GetProviders(criteriaForLocationService, true);

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
            SetDozeOptimization(Platform.CurrentActivity, intent);

            if (intent is null || intent.Action is null)
            {
                Serilog.Log.Warning($"OnStartCommand: intent or intent.Action is null ");
                return StartCommandResult.Sticky;
            }

            if (intent.Action.Equals(PrefsActivity.ACTION_START_SERVICE))
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
                    Thread serviceThread = new(new ThreadStart(ServiceRunning));
                    serviceThread.Start();
                    RegisterForegroundService();
                }
            }
            else if (intent.Action.Equals(PrefsActivity.ACTION_STOP_SERVICE))
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
                notificationManager?.Cancel(PrefsActivity.SERVICE_RUNNING_NOTIFICATION_ID);
                notificationManager?.Cancel(PrefsActivity.NOTIFICATION_ID_HIGH);
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
                NotificationChannel nChannel = new(PrefsActivity.NOTIFICATION_CHANNEL_ID, PrefsActivity.channelName, NotificationImportance.Low)
                {
                    LockscreenVisibility = NotificationVisibility.Private,
                };

                nManager?.CreateNotificationChannel(nChannel);
            }

            NotificationCompat.Builder notificationBuilder = new(this, PrefsActivity.NOTIFICATION_CHANNEL_ID);
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
            StartForeground(PrefsActivity.SERVICE_RUNNING_NOTIFICATION_ID, notification);

            //Channel for high importance notifications
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                NotificationChannel nChannel = new(PrefsActivity.NOTIFICATION_CHANNEL_ID_HIGH, PrefsActivity.channelName_high, NotificationImportance.High)
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
            notificationIntent.SetAction(PrefsActivity.ACTION_MAIN_ACTIVITY);
            notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTask);
            notificationIntent.PutExtra(PrefsActivity.SERVICE_STARTED_KEY, true);

            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                return PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            } 
            else
            {
                return PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent);
            }
        }

        private void ServiceRunning()
        {
            while (isStarted)
            {
                Serilog.Log.Debug($"LocationService loop is running: " + (DateTime.Now).ToString("mm:ss"));
                try
                {
                    string strSpeed = String.Empty;
                    if (streetSpeed != String.Empty && streetSpeed != null)
                    {
                        strSpeed = streetSpeed + " " + Resources?.GetString(Resource.String.str_kmh);
                    }

                    NotificationCompat.Builder notificationBuilder = new(this, PrefsActivity.NOTIFICATION_CHANNEL_ID);
                    Notification notification = notificationBuilder
                        .SetOngoing(true)
                        .SetSmallIcon(Resource.Drawable.dyno)
                        .SetContentText(streetName + "," + streetSpeed + "," + strLastUpdate)
                        .SetPriority((int)NotificationPriority.Default)
                        .SetCategory(Notification.CategoryRecommendation)
                        .SetContentIntent(BuildIntentToShowMainActivity())
                        .Build();

                    NotificationManager? nManager = GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
                    nManager?.Notify(PrefsActivity.SERVICE_RUNNING_NOTIFICATION_ID, notification);

                    UpdateGUI();

                    Thread.Sleep(1000*3);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error($"Crashed: " + ex.ToString());
                    Crashes.TrackError(ex);
                }
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
                RouterPoint rp = router.Resolve(profile, new Itinero.LocalGeo.Coordinate((float)cLocation.Latitude, (float)cLocation.Longitude), 25.0f);
                //RouterPoint rp = router.Resolve(profile, new Itinero.LocalGeo.Coordinate(-37.811587f, 144.883007f), 25.0f);
                Itinero.Data.Network.RoutingEdge edge = routerDb.Network.GetEdge(rp.EdgeId);
                IAttributeCollection attributes = routerDb.GetProfileAndMeta(edge.Data.Profile, edge.Data.MetaId);

                attributes.TryGetValue("name", out sName);
                attributes.TryGetValue("maxspeed", out sSpeed);

                Serilog.Log.Debug($"StreetName/Speed: '{sName}', '{sSpeed}'");
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug($"Unable to parse: '{cLocation.Latitude}', '{cLocation.Longitude}', {ex.ToString()}");
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
            Serilog.Log.Debug($"Convert car speed from '" + cLocation.Speed.ToString() + "'m/s to " + carspeed_kmh.ToString() + "km/h: '");

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
            int speedmargin = Int32.Parse(Preferences.Get("SpeedGracePercent", PrefsActivity.default_speed_margin.ToString()));
            if (carspeed_kmh <= (int)(streetspeed_int * speedmargin / 100 + streetspeed_int))
            {
                Serilog.Log.Debug($"Not Speeding - Done");
                return;
            }
            Serilog.Log.Debug($"Show High Priority Notification");
            NotificationCompat.Builder notificationBuilder = new(this, PrefsActivity.NOTIFICATION_CHANNEL_ID_HIGH);
            Notification notification = notificationBuilder
                .SetAutoCancel(true)
                .SetSmallIcon(Resource.Drawable.dyno)
                .SetContentText(intCounter.ToString() + ", " + Resources?.GetString(Resource.String.str_speeding) + ", " + sSpeed + " / " + carspeed_kmh.ToString())
                .SetPriority((int)NotificationPriority.High)
                .SetCategory(Notification.CategoryRecommendation)
                .SetContentIntent(BuildIntentToShowMainActivity())
                .SetSound(null)
                .Build();

            NotificationManager? nManager = GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
            nManager?.Notify(PrefsActivity.NOTIFICATION_ID_HIGH, notification);

            Serilog.Log.Debug($"Play a sound");
            var soundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            var r = RingtoneManager.GetRingtone(Android.App.Application.Context, soundUri);
            r?.Play();

            nManager?.Cancel(PrefsActivity.NOTIFICATION_ID_HIGH);
        }

        private void UpdateGUI()
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

            if (Resources is null)
            {
                Serilog.Log.Error($"UpdateGUI - Resources is null, returning early");
                return;
            }

            if (currentLocation == null)
            {
                Serilog.Log.Warning($"UpdateGUI - currentLocation is null, set all TextView fields to N/A");
                MainActivity.txtlatitude.Text = Resources.GetString(Resource.String.str_na);
                MainActivity.txtlong.Text = Resources.GetString(Resource.String.str_na);
                MainActivity.txtspeed.Text = Resources.GetString(Resource.String.str_na);
                MainActivity.txtstreetname.Text = Resources.GetString(Resource.String.str_na);
                MainActivity.txtspeedlimit.Text = Resources.GetString(Resource.String.str_na);
                MainActivity.txtspeeding.Text = Resources.GetString(Resource.String.str_na);
                MainActivity.txtgpsdatetime.Text = Resources.GetString(Resource.String.str_na);
                return;
            }

            //Updated
            MainActivity.txtlastupdated.Text = (DateTime.Now).ToString("HH:mm:ss");

            //GPS Information
            Serilog.Log.Debug($"UpdateGUI - Update GPS related TextView fields");
            MainActivity.txtlatitude.Text = currentLocation.Latitude.ToString("0.00000");
            MainActivity.txtlong.Text = currentLocation.Longitude.ToString("0.00000");

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
                DateTime gpsUTCDateTime = DateTimeOffset.FromUnixTimeMilliseconds(currentLocation.Time).DateTime;
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
            if (streetName == String.Empty || streetName is null)
            {
                MainActivity.txtstreetname.Text = "Unknown street/road";
            } 
            else
            {
                MainActivity.txtstreetname.Text = streetName;
            }

            if (streetSpeed == String.Empty || streetSpeed is null)
            {
                MainActivity.txtspeedlimit.Text = Resources.GetString(Resource.String.str_na);
            }
            else
            {
                MainActivity.txtspeedlimit.Text = streetSpeed + " " + Resources.GetString(Resource.String.str_kmh);
            }

            //GPS Speed?
            if (currentLocation.HasSpeed == false)
            {
                Serilog.Log.Debug($"UpdateGUI - No Speed information. Update GUI and return");
                MainActivity.txtspeed.Text = Resources.GetString(Resource.String.str_na);
                MainActivity.txtspeeding.Text = String.Empty;

                return;
            }

            int carspeed_kmh = (int)(currentLocation.Speed * 3.6);
            MainActivity.txtspeed.Text = carspeed_kmh.ToString() + " " + Resources.GetString(Resource.String.str_kmh);

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
                MainActivity.txtspeeding.Text = Resources.GetString(Resource.String.str_speeding);
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
