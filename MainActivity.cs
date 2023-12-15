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
using Itinero;
using Xamarin.Essentials;
using static Xamarin.Essentials.Permissions;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;
using Serilog.Core;
using Serilog.Sink.AppCenter;
using Wibci.CountryReverseGeocode;
using Wibci.CountryReverseGeocode.Models;
using Google.Android.Material.Navigation;
using Mapsui;
using Mapsui.UI.Android;
using Mapsui.Tiling;
using Mapsui.Utilities;
using Mapsui.UI;
using Mapsui.Styles;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Mapsui.Projections;
using Mapsui.Nts;
using NetTopologySuite.Geometries;
using Mapsui.Extensions;
using Mapsui.Widgets.Zoom;


namespace Velociraptor
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
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
        public static TextView? txtcountryname = null;
        public static MapControl? mapControl = null;
        public static Mapsui.Map? map = null;

        //OSM Data
        public static Itinero.RouterDb routerDb = new();
        public static Itinero.Router? router = null;
        public static Itinero.Profiles.Profile? profile = null;

        //Misc
        public static Activity? mContext;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            mContext = this;

            if (Resources is null)
            {
                return;
            }

            string appCenterApiKey = Resources.GetString(Resource.String.Microsoft_App_Center_APIKey);
            AppCenter.Start(appCenterApiKey, typeof(Analytics), typeof(Crashes));

            Serilog.Log.Logger = new LoggerConfiguration()
                .WriteTo.AppCenterSink(null, Serilog.Events.LogEventLevel.Debug, AppCenterTarget.ExceptionsAsCrashes, appCenterApiKey)
                .CreateLogger();

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            Serilog.Log.Debug($"MainActivity - OnCreate()");

            SetContentView(Resource.Layout.activity_main);
            AndroidX.AppCompat.Widget.Toolbar? toolbar = FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);
           
            DrawerLayout? drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            ActionBarDrawerToggle toggle = new(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
            drawer?.AddDrawerListener(toggle);
            toggle.SyncState();

            NavigationView? navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            navigationView?.SetNavigationItemSelectedListener(this);


            //Request Permissions, using xamarin.essentials
            RequestPermission(new LocationWhenInUse());
            //RequestPermission(new LocationAlways());

            //Request Notification Permission, using Android
            if (this.CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
            {
                this.RequestPermissions(new[] { Manifest.Permission.PostNotifications }, 0);
            }

            txtlatitude = FindViewById<TextView>(Resource.Id.txtlatitude);
            txtlong = FindViewById<TextView>(Resource.Id.txtlong);
            txtspeed = FindViewById<TextView>(Resource.Id.txtspeed);
            txtspeeding = FindViewById<TextView>(Resource.Id.txtspeeding);
            txtstreetname = FindViewById<TextView>(Resource.Id.txtstreetname);
            txtspeedlimit = FindViewById<TextView>(Resource.Id.txtspeedlimit);
            txtgpsdatetime = FindViewById<TextView>(Resource.Id.txtgpsdatetime);
            txtlastupdated = FindViewById<TextView>(Resource.Id.txtlastupdated);
            txtcountryname = FindViewById<TextView>(Resource.Id.txtcountryname);
            mapControl = FindViewById<MapControl>(Resource.Id.mapcontrol);

            //Display the map
            /*map = new Mapsui.Map
            {
                CRS = "EPSG:3857",
            };*/

            map = new Mapsui.Map();
            map.CRS = "EPSG:3857";
            map.Layers.Add(OpenStreetMap.CreateTileLayer());
            map.Widgets.Add(new ZoomInOutWidget { MarginX = 10, MarginY = 20 });  //adds the +/- zoom widget
            mapControl.Map = map;

            //var smc = SphericalMercator.FromLonLat(144, -37);
            //MainActivity.mapControl.Map.Home = n => n.CenterOnAndZoomTo(new MPoint(smc.x, smc.y), 16);  //0 zoomed out-19 zoomed in

            Serilog.Log.Debug($"MainActivity - Initilize OSM Provider");
            InitializeOsmProvider();

            //Location Service. Service checks if already running
            Serilog.Log.Debug($"MainActivity - Start LocationService");
            Intent locationServiceIntent = new(this, typeof(LocationForegroundService));
            locationServiceIntent.SetAction(PrefsActivity.ACTION_START_SERVICE);
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                StartForegroundService(locationServiceIntent);
            }
            else
            {
                StartService(locationServiceIntent);
            }
        }

        private static async Task InitializeOsmProvider()
        {
            Serilog.Log.Debug("InitializeOSMProvider() - Start");

            var location = Geolocation.GetLastKnownLocationAsync().Result;
            if (location == null)
            {
                Serilog.Log.Error($"No cached location? - Can't determinte which database to use. Returning");
                return;
            }
            Serilog.Log.Information($"Current location: Latitude: {location.Latitude}, Longitude: {location.Longitude}");

            if (mContext is null)
            {
                Serilog.Log.Error($"mContext is null. Returning");
                return;
            }

            //Clear GUI field
            if (txtcountryname != null)
            {
                txtcountryname.Text = String.Empty;
            }

            var service = new CountryReverseGeocodeService();
            var country = new GeoLocation { Latitude = location.Latitude, Longitude = location.Longitude };
            LocationInfo locationInfo = service.FindCountry(country);

            if (locationInfo == null)
            {
                Serilog.Log.Error($"locationInfo is null. Unable to determine which router database to use");
                return;
            }
            Serilog.Log.Information($"FindCountry: {locationInfo.Name}");
            var countryName = locationInfo.Name.ToLower();

            //RouterDB?
            ShowDialog msg = new(mContext);
            var dbInfo = new FileInfo(FileSystem.AppDataDirectory + "/" + countryName + ".db");
            if (dbInfo.Exists == true)
            {
                Serilog.Log.Information($"RouterDB exists for country, using it.");
                try
                {
                    var stream = new FileInfo(FileSystem.AppDataDirectory + "/" + countryName + ".db").OpenRead();
                    routerDb = RouterDb.Deserialize(stream, RouterDbProfile.NoCache);
                    profile = routerDb.GetSupportedProfile("car");
                    router = new Router(routerDb);

                    if (txtcountryname != null)
                    {
                        txtcountryname.Text = countryName + ".db";
                    }
                }
                catch
                {
                    Serilog.Log.Information($"Crashed using routerdb file for {countryName}. Delete?");

                    if (mContext is null)
                    {
                        Serilog.Log.Information($"mContext is null. Returning");
                        return;
                    }

                    if (await msg.Dialog("Corrupt routerdb", $"Delete '{countryName}.db' road database?", Android.Resource.Attribute.DialogIcon, false, global::ShowDialog.MessageResult.YES, global::ShowDialog.MessageResult.NO) != global::ShowDialog.MessageResult.YES)
                        return;

                    new FileInfo(FileSystem.AppDataDirectory + "/" + countryName + ".db").Delete();
                    await InitializeOsmProvider();
                }

                return;
            }

            if (mContext is null)
            {
                Serilog.Log.Information($"mContext is null. Returning");
                return;
            }

            Serilog.Log.Warning($"RouterDB does not exists for selected country. Need to add routerdb to app folder");
            if (await msg.Dialog("Routerdb not found for region", $"Add '{countryName}.db' road database?", Android.Resource.Attribute.DialogIcon, false, global::ShowDialog.MessageResult.YES, global::ShowDialog.MessageResult.NO) != global::ShowDialog.MessageResult.YES) return;

            if (Android.OS.Environment.DirectoryDownloads is null)
            {
                Serilog.Log.Error($"DirectoryDownloads is null. returning.");
                return;
            }

            //Getting to this point means there is no working router.db file for the location we are inn
            //Open filebrowser and select it. File name must match country name
            await PickAndShow(null);

            Serilog.Log.Debug("InitializeOSMProvider() - End");
            return;
        }

        public override bool OnCreateOptionsMenu(IMenu? menu)
        {
            if (menu == null)
            {
                Serilog.Log.Error("menu is null. Returning");
                return false;
            }

            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                Serilog.Log.Information($"Change to Settings");
                StartActivity(new Intent(this, typeof(PrefsActivity)));

                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        public bool OnNavigationItemSelected(IMenuItem item)
        {
            int id = item.ItemId;

            if (id == Resource.Id.nav_camera)
            {
                _ = PickAndShow(null);
            }

            DrawerLayout? drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            drawer?.CloseDrawer(GravityCompat.Start);

            return true;
        }

        public override void OnBackPressed()
        {
            DrawerLayout? drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            if (drawer == null)
            {
                Serilog.Log.Error("drawer is null. Returning");
                return;
            }

            if (drawer.IsDrawerOpen(GravityCompat.Start))
            {
                drawer.CloseDrawer(GravityCompat.Start);
            }
            else
            {
                if (mContext is null || mContext.Resources is null)
                {
                    return;
                }

                using var alert = new Android.App.AlertDialog.Builder(mContext);
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

                if (!OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    base.OnBackPressed();
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private static PermissionStatus RequestPermission<T>(T permission) where T : BasePermission
        {
            PermissionStatus status = PermissionStatus.Unknown;

            if (mContext is null) 
            {
                Serilog.Log.Debug($"mContext is null. Returning");
                return status;
            }

            try
            {
                status = permission.CheckStatusAsync().Result;
                if (status == PermissionStatus.Denied)
                {
                    status = permission.RequestAsync().Result;
                    if (status == PermissionStatus.Denied)
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

        async static Task PickAndShow(PickOptions? options)
        {
            try
            {
                var result = await FilePicker.PickAsync(options);
                if (result != null)
                {
                    Serilog.Log.Information($"Filenam: {result.FileName}");

                    if (result.FileName.EndsWith("db", StringComparison.OrdinalIgnoreCase))
                    {
                        //Before file copy
                        var filesList = System.IO.Directory.GetFiles(FileSystem.AppDataDirectory);
                        foreach (var file in filesList)
                        {
                            var filename = Path.GetFileName(file);
                            Serilog.Log.Debug(filename);
                        }

                        var strDestFileName = FileSystem.AppDataDirectory + "/" + result.FileName.ToLower();
                        File.Copy(result.FullPath, strDestFileName);

                        //After file copy
                        filesList = System.IO.Directory.GetFiles(FileSystem.AppDataDirectory);
                        foreach (var file in filesList)
                        {
                            var filename = Path.GetFileName(file);
                            Serilog.Log.Debug(filename);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
                Serilog.Log.Error($"The user canceled or something went wrong: " + ex.ToString());
            }

            return;
        }
    }
}