using Android.Locations;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using System.Collections.Generic;
using System.Linq;
using Itinero;
using Xamarin.Essentials;
using static Xamarin.Essentials.Permissions;
using Android.Content;
using Android.Content.PM;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;
using Serilog.Core;
using Serilog.Sink.AppCenter;

namespace Velociraptor
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity
    {
        // Debugging
        public static string? TAG { get; private set; }

        // GUI
        public static TextView? txtlatitude = null;
        public static TextView? txtlong = null;
        public static TextView? txtspeed = null;
        public static TextView? txtstreetname = null;
        public static TextView? txtspeedlimit = null;
        public static TextView? txtspeeding = null;
        public static TextView? txtgpsdatetime = null;
        public static TextView? txtlastupdated = null;

        //OSM Data
        public static Itinero.RouterDb routerDb = new();
        public static Itinero.Router? router = null;
        public static Itinero.Profiles.Profile? profile = null;

        //Misc
        public static Activity? mContext;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Resources is null)
            {
                return;
            }

            mContext = this;

            string appCenterApiKey = Resources.GetString(Resource.String.Microsoft_App_Center_APIKey);
            AppCenter.Start(appCenterApiKey, typeof(Analytics), typeof(Crashes));

            Serilog.Log.Logger = new LoggerConfiguration()
                .WriteTo.AppCenterSink(null, Serilog.Events.LogEventLevel.Debug, AppCenterTarget.ExceptionsAsCrashes, appCenterApiKey)
                .CreateLogger();

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            Serilog.Log.Debug($"MainActivity - OnCreate()");

            //Request Permissions
            RequestPermission(new LocationAlways());

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            txtlatitude = FindViewById<TextView>(Resource.Id.txtlatitude);
            txtlong = FindViewById<TextView>(Resource.Id.txtlong);
            txtspeed = FindViewById<TextView>(Resource.Id.txtspeed);
            txtspeeding = FindViewById<TextView>(Resource.Id.txtspeeding);
            txtstreetname = FindViewById<TextView>(Resource.Id.txtstreetname);
            txtspeedlimit = FindViewById<TextView>(Resource.Id.txtspeedlimit);
            txtgpsdatetime = FindViewById<TextView>(Resource.Id.txtgpsdatetime);
            txtlastupdated = FindViewById<TextView>(Resource.Id.txtlastupdated);
            
            //Location Service. Service checks if already running
            Serilog.Log.Debug($"MainActivity - Start LocationService");
            Intent locationServiceIntent = new(this, typeof(LocationForegroundService));
            locationServiceIntent.SetAction(PrefsActivity.ACTION_START_SERVICE);
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                #pragma warning disable CA1416
                StartForegroundService(locationServiceIntent);
                #pragma warning restore CA1416
            }
            else
            {
                StartService(locationServiceIntent);
            }

            Serilog.Log.Debug($"MainActivity - Initilize OSM Provider");
            InitializeOsmProvider();
        }

        public override void OnBackPressed()
        {
            using var alert = new AlertDialog.Builder(mContext);

            if (mContext is not null && mContext.Resources is not null)
            {
                alert.SetTitle(mContext.Resources.GetString(Resource.String.ExitTitle));
                alert.SetMessage(mContext.Resources.GetString(Resource.String.ExitPrompt));
                alert.SetPositiveButton(Resource.String.Yes, (sender, args) => {
                    //Location Service
                    Intent locationServiceIntent = new(this, typeof(LocationForegroundService));
                    locationServiceIntent.SetAction(PrefsActivity.ACTION_STOP_SERVICE);
                    StopService(locationServiceIntent);

                    mContext.FinishAffinity(); 
                });
                alert.SetNegativeButton(Resource.String.No, (sender, args) => { });

                var dialog = alert.Create();
                dialog?.Show();
            }           
        }

        private static void InitializeOsmProvider()
        {
            Serilog.Log.Debug("InitializeOSMProvider() - Start");

            //Convert pbf to routerdb format
            /*using (var stream = new FileInfo(@"c:\data\australia-latest.osm.pbf").OpenRead())
            {
                routerDb.LoadOsmData(stream, Vehicle.Car); // create the network for cars only.
            }*/

            /*using (var stream = new FileInfo(@"c:\data\routerdb").Open(FileMode.Create))
            {
                routerDb.Serialize(stream);
            }*/


            //Download routerdb from URL
            /*
            var folder = FileSystem.AppDataDirectory;
            var filesList = System.IO.Directory.GetFiles(folder);
            foreach (var file in filesList)
            {
                var filename = Path.GetFileName(file);
                Serilog.Log.Debug(filename);
            }
            */

            /*
            byte[] data = null;
            for (int i = 0; i < 10; i++)
            {
                var url = "https://192.168.1.62/bestofpictures/routerdb";
                data = DownloadImageAsync(url);
                Thread.Sleep(10000);
            }
            System.IO.File.WriteAllBytes(folder + "/routerdb", data);
            filesList = System.IO.Directory.GetFiles(folder);
            foreach (var file in filesList)
            {
                var filename = Path.GetFileName(file);
                SeriLog.Log.Debug(filename);
            }
            */

            var filename = FileSystem.AppDataDirectory + "/" + "routerdb";
            var stream = new FileInfo(filename).OpenRead();
            routerDb = RouterDb.Deserialize(stream, RouterDbProfile.NoCache);
            profile = routerDb.GetSupportedProfile("car");
            router = new Router(routerDb);

            Serilog.Log.Debug("InitializeOSMProvider() - End");
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private static PermissionStatus RequestPermission<T>(T permission) where T : BasePermission
        {
            PermissionStatus status = PermissionStatus.Unknown;

            try
            {
                status = permission.CheckStatusAsync().Result;
                if (status == PermissionStatus.Denied)
                {
                    status = permission.RequestAsync().Result;
                    if (status == PermissionStatus.Denied && mContext != null)
                    {
                        mContext.FinishAffinity();
                    }
                }
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
                Serilog.Log.Error($"Permission not declared? " + ex.ToString());
            }

            return status;
        }

    }
}
