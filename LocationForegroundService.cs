using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Util;
using Android.Content;
using Android.Runtime;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using Android.Media;
using Itinero.Attributes;
using Itinero.LocalGeo;
using Itinero;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;
using Serilog.Sink.AppCenter;


namespace Velociraptor
{
    [Service]
    public class LocationForegroundService : Service, ILocationListener
    {
        private LocationManager? locationManager = null;
        private string locationProvider = string.Empty;
        private bool isStarted;
        private NotificationManager? nManager;
        private string streetname = string.Empty;
        private string streetspeed = string.Empty;

        public void OnProviderDisabled(string provider)
        {
            throw new NotImplementedException();
        }

        public void OnProviderEnabled(string provider)
        {
            throw new NotImplementedException();
        }

        public void OnStatusChanged(string? provider, [GeneratedEnum] Availability status, Bundle? extras)
        {
            throw new NotImplementedException();
        }
                
        public void OnLocationChanged(Android.Locations.Location location)
        {
            Serilog.Log.Debug("location changed");

            LocationChangedGUI(location);
        }

        private void InitializeLocationManager()
        {
            locationManager = GetSystemService(LocationService) as LocationManager;
            if (locationManager == null)
            {
                Serilog.Log.Error("locationManager is null");
                return;
            }

            Criteria criteriaForLocationService = new()
            {
                Accuracy = Accuracy.Fine,
                SpeedRequired = true,
                SpeedAccuracy = Accuracy.Fine
            };

            IList<string> acceptableLocationProviders = locationManager.GetProviders(criteriaForLocationService, true);
            if (acceptableLocationProviders.Any())
            {
                locationProvider = acceptableLocationProviders.First();
            }
            else
            {
                locationProvider = string.Empty;
            }

            Serilog.Log.Debug("Using " + locationProvider + " as location provider.");
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
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

                    InitializeLocationManager();

                    isStarted = true;

                    Thread serviceThread = new(new ThreadStart(ServiceRunning));
                    serviceThread.Start();

                    RegisterForegroundService();

                    if (locationProvider is not null && locationManager is not null)
                    {
                        Serilog.Log.Warning($"ServiceRunning: Creating callback service for LocationUpdates, every 1000ms and 1m");
                        locationManager.RequestLocationUpdates(locationProvider, 1000, 1, this, Looper.MainLooper); /**/
                    }
                }
            }
            else if (intent.Action.Equals(PrefsActivity.ACTION_STOP_SERVICE))
            {
                Serilog.Log.Information($"OnStartCommand: The location service is stopping.");

                #pragma warning disable CA1422
                #pragma warning disable CA1416

                if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.N)
                {
                    StopForeground(true);
                } else if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.N)
                {
                    StopForeground(StopForegroundFlags.Remove);
                }

                #pragma warning restore CA1422
                #pragma warning restore CA1416

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

            // Remove the notification from the status bar
            NotificationManager? notificationManager = GetSystemService(NotificationService) as NotificationManager;
            notificationManager?.Cancel(PrefsActivity.SERVICE_RUNNING_NOTIFICATION_ID);
            isStarted = false;

            //Stop listing to location updates
            locationManager?.RemoveUpdates(this);

            base.OnDestroy();
        }

        void RegisterForegroundService()
        {
            if (Resources is null)
            {
                Serilog.Log.Warning($"RegisterForegroundService: resources is null, returning early");
                return;
            }

            #pragma warning disable CA1416
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                NotificationChannel nChannel = new(PrefsActivity.NOTIFICATION_CHANNEL_ID, PrefsActivity.channelName, NotificationImportance.Low)
                {
                    LockscreenVisibility = NotificationVisibility.Private,
                };

                nManager = GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
                nManager?.CreateNotificationChannel(nChannel);
            }
            #pragma warning restore CA1416


            NotificationCompat.Builder notificationBuilder = new(this, PrefsActivity.NOTIFICATION_CHANNEL_ID);
                        
            Notification notification = notificationBuilder
                .SetOngoing(true)
                .SetSmallIcon(Resource.Drawable.dragon)
                .SetContentTitle("name")
                .SetContentText("speed")
                .SetPriority(1)
                .SetCategory(Notification.CategoryService)
                .SetContentIntent(BuildIntentToShowMainActivity())
                .Build();
            
            // Enlist this instance of the service as a foreground service
            StartForeground(PrefsActivity.SERVICE_RUNNING_NOTIFICATION_ID, notification);
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

            PendingIntent? pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            return pendingIntent;
        }

        private void ServiceRunning()
        {
            while (isStarted)
            {
                Serilog.Log.Debug($"LocationService loop is running: " + (DateTime.Now).ToString("HH:mm:ss"));
                try
                {
                    if (Resources is not null && nManager is not null)
                    {
                        Serilog.Log.Debug($"Update notification");

                        NotificationCompat.Builder notificationBuilder = new(this, PrefsActivity.NOTIFICATION_CHANNEL_ID);
                        Notification notification = notificationBuilder
                            .SetOngoing(true)
                            .SetSmallIcon(Resource.Drawable.dragon)
                            .SetContentTitle(streetname)
                            .SetContentText(streetspeed + " " + Resources.GetString(Resource.String.str_kmh))
                            .SetPriority(1)
                            .SetCategory(Notification.CategoryService)
                            .SetContentIntent(BuildIntentToShowMainActivity())
                            .Build();

                        nManager.Notify(PrefsActivity.SERVICE_RUNNING_NOTIFICATION_ID, notification);
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error($"Crashed: " + ex.ToString());
                    Crashes.TrackError(ex);
                }
            }
        }

        private void LocationChangedGUI(Android.Locations.Location? currentLocation)
        {
            if ((MainActivity.txtlatitude is null) ||
                (MainActivity.txtlong is null) ||
                (MainActivity.txtspeed is null) ||
                (MainActivity.txtstreetname is null) ||
                (MainActivity.txtspeedlimit is null) ||
                (MainActivity.txtspeeding is null) ||
                (MainActivity.txtgpsdatetime is null) ||
                (MainActivity.txtlastupdated is null))
            {
                Serilog.Log.Error($"LocationChangedGUI - One or more TextView objects are null");
                return;
            }

            if (Resources is null)
            {
                Serilog.Log.Error($"LocationChangedGUI - Resources is null, returning early");
                return;
            }

            if (currentLocation == null)
            {
                Serilog.Log.Warning($"LocationChangedGUI - currentLocation is null, set all TextView fields to N/A");
                MainActivity.mContext?.RunOnUiThread(() =>
                {
                    MainActivity.txtlatitude.Text = Resources.GetString(Resource.String.str_na);
                    MainActivity.txtlong.Text = Resources.GetString(Resource.String.str_na);
                    MainActivity.txtspeed.Text = Resources.GetString(Resource.String.str_na);
                    MainActivity.txtstreetname.Text = Resources.GetString(Resource.String.str_na);
                    MainActivity.txtspeedlimit.Text = Resources.GetString(Resource.String.str_na);
                    MainActivity.txtspeeding.Text = Resources.GetString(Resource.String.str_na);
                    MainActivity.txtgpsdatetime.Text = Resources.GetString(Resource.String.str_na);
                    MainActivity.txtlastupdated.Text = Resources.GetString(Resource.String.str_na);
                });

                return;
            }

            //Update GUI with GPS Information
            Serilog.Log.Debug($"LocationChangedGUI - Update GPS related TextView fields");

            //Convert GPS time in ms since epoch in UTC to local datetime
            DateTime gpslocalDateTime = default;
            try
            {
                TimeZoneInfo systemTimeZone = TimeZoneInfo.Local;
                DateTime gpsUTCDateTime = (DateTimeOffset.FromUnixTimeMilliseconds(currentLocation.Time)).DateTime;
                gpslocalDateTime = TimeZoneInfo.ConvertTimeFromUtc(gpsUTCDateTime, systemTimeZone);
            } 
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
            } 
            finally
            {
                MainActivity.mContext?.RunOnUiThread(() =>
                {
                    MainActivity.txtlatitude.Text = currentLocation.Latitude.ToString();
                    MainActivity.txtlong.Text = currentLocation.Longitude.ToString();
                    MainActivity.txtgpsdatetime.Text = gpslocalDateTime.ToString();
                });
            }

            //Update GUI with OSM data (streetname and street max speed)
            try
            {
                Serilog.Log.Debug($"LocationChangedGUI - Use GPS position to get streetname and maxspeed");
                RouterPoint routerPoint = MainActivity.router.Resolve(MainActivity.profile, new Coordinate((float)currentLocation.Latitude, (float)currentLocation.Longitude));
                //RouterPoint routerPoint = MainActivity.router.Resolve(MainActivity.profile, new Coordinate(-37.81277740408493f, 144.88297495235076f));
                Itinero.Data.Network.RoutingEdge edge = MainActivity.routerDb.Network.GetEdge(routerPoint.EdgeId);
                IAttributeCollection attributes = MainActivity.routerDb.GetProfileAndMeta(edge.Data.Profile, edge.Data.MetaId);

                attributes.TryGetValue("name", out streetname);
                attributes.TryGetValue("maxspeed", out streetspeed);

                if (streetname == String.Empty || streetname is null) 
                {
                    streetname = "Unknown street/road";
                }

                if (streetspeed == String.Empty || streetname is null) 
                {
                    streetspeed = String.Empty;
                } 
                else
                {
                    streetspeed  = streetspeed + " " + Resources.GetString(Resource.String.str_kmh);
                }

                Serilog.Log.Debug($"LocationChangedGUI - Update GUI with streetname and maxspeed");
                MainActivity.mContext?.RunOnUiThread(() =>
                {
                    MainActivity.txtstreetname.Text = streetname;
                    MainActivity.txtspeedlimit.Text = streetspeed;
                    MainActivity.txtlastupdated.Text = (DateTime.Now).ToString("HH:mm:ss");
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"LocationChangedGUI - Crashed: " + ex.ToString());
                Crashes.TrackError(ex);
            }

            //Update GUI and play notification sound
            /**/
            /*
            Serilog.Log.Debug($"LocationChangedGUI - Speeding - Update GUI, play a sound and return");
            MainActivity.txtspeeding.Text = Resources.GetString(Resource.String.str_speeding);
            var soundUri1 = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            var r1 = RingtoneManager.GetRingtone(MainActivity.mContext, soundUri1);
            r1?.Play();
            */


            //GPS Speed?
            if (currentLocation.HasSpeed == false)
            {
                Serilog.Log.Error($"LocationChangedGUI - No Speed information. Update GUI and return");
                MainActivity.mContext?.RunOnUiThread(() =>
                {
                    MainActivity.txtspeed.Text = Resources.GetString(Resource.String.str_na);
                    MainActivity.txtspeeding.Text = String.Empty;
                });

                return;
            }

            //Convert from m/s to km/h
            Serilog.Log.Debug($"LocationChangedGUI - Convert speed from m/s to km/h and update GUI");
            int speed_kmh = (int)(currentLocation.Speed * 3.6);
            MainActivity.mContext?.RunOnUiThread(() =>
            {
                MainActivity.txtspeed.Text = speed_kmh.ToString() + " " + Resources.GetString(Resource.String.str_kmh);
            });


            if (Int32.TryParse(streetspeed, out int streetspeed_int) == false)
            {
                Serilog.Log.Debug($"LocationChangedGUI - Failed to convert streetspeed string to int. Clear GUI and return");
                MainActivity.mContext?.RunOnUiThread(() =>
                {
                    MainActivity.txtspeeding.Text = String.Empty;
                });

                return;
            }

            //If not speeding, we're done
            if (speed_kmh <= streetspeed_int)
            {
                Serilog.Log.Debug($"LocationChangedGUI - Not Speeding - Update GUI and return");
                MainActivity.mContext?.RunOnUiThread(() =>
                {
                    MainActivity.txtspeeding.Text = String.Empty;
                });

                return;
            }

            //Update GUI and play notification sound
            Serilog.Log.Debug($"LocationChangedGUI - Speeding - Update GUI, play a sound and return");
            MainActivity.txtspeeding.Text = Resources.GetString(Resource.String.str_speeding);
            var soundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            var r = RingtoneManager.GetRingtone(MainActivity.mContext, soundUri);
            r?.Play();
        }
    }
}
