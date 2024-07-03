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
using System.Text.Json;
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
using Mapsui.Extensions;
using Mapsui.Widgets.Zoom;
using NetTopologySuite.Geometries;
using TelegramSink;
using Velociraptor.Models;



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
            string telegramApiKey = Resources.GetString(Resource.String.Telegram_APIKey);
            string telegramChatId = Resources.GetString(Resource.String.Telegram_ChatId);
            AppCenter.Start(appCenterApiKey, typeof(Analytics), typeof(Crashes));

            Serilog.Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProperty(Serilog.Core.Constants.SourceContextPropertyName, "velociraptor")
                .WriteTo.AppCenterSink(null, Serilog.Events.LogEventLevel.Warning, AppCenterTarget.ExceptionsAsCrashes, appCenterApiKey)
                .WriteTo.AndroidLog()
                //.WriteTo.TeleSink(telegramApiKey: telegramApiKey, telegramChatId: telegramChatId)
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
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                if (this.CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
                {
                    this.RequestPermissions([Manifest.Permission.PostNotifications], 0);
                }
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

            /*
            //Display the map
            if (mapControl != null)
            {
                var map = new Mapsui.Map
                {
                    CRS = "EPSG:3857", //https://epsg.io/3857
                };
                map.Layers.Add(OpenStreetMap.CreateTileLayer());

                mapControl.Map = map;
                mapControl.Map.Navigator.RotationLock = true;
            }
            */

            Serilog.Log.Debug($"MainActivity - Initilize OSM Provider");
            _ = InitializeOsmProvider();

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

            /*
            double lat = -37.81076991109956;
            double lon = 144.88298804322875;
            string AzureMapsAPIKey = Resources.GetString(Resource.String.AzureMapsAPIKey);
            string searchURL = $"https://atlas.microsoft.com/search/address/reverse/json?api-version=1.0&query={lat},{lon}&subscription-key={AzureMapsAPIKey}&returnSpeedLimit=true&radius=25&returnRoadUse=false&returnMatchType=false";
                        
            var client = new HttpClient();
            HttpResponseMessage response = client.GetAsync(searchURL).Result;
            HttpContent responseContent = response.Content;

            string output = string.Empty;
            using (var reader = new StreamReader(responseContent.ReadAsStreamAsync().Result))
            {
                output = reader.ReadToEndAsync().Result;
                Console.WriteLine(output);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            AzureMapData? azureMapData = JsonSerializer.Deserialize<AzureMapData>(output, options);
            var azureSpeedLimits = azureMapData?.Addresses?.Select(x => x.Address?.SpeedLimit).ToArray();
            if (azureSpeedLimits?.Length > 0)
            {
                var b = azureSpeedLimits?.First()?.Replace("KPH", "");
                Console.WriteLine(b);
            }
            */
        }

        private static async Task InitializeOsmProvider()
        {
            Serilog.Log.Debug("InitializeOSMProvider() - Start");

            var cLocation = Geolocation.GetLastKnownLocationAsync().Result;
            if (cLocation == null)
            {
                Serilog.Log.Error($"No cached location? - Can't determine which database to use. Returning");
                return;
            }
            Serilog.Log.Information($"Current location: Latitude: {cLocation.Latitude}, Longitude: {cLocation.Longitude}");

            if (mContext is null)
            {
                Serilog.Log.Error($"mContext is null. Returning");
                return;
            }

            if (txtcountryname is null)
            {
                Serilog.Log.Error($"txtcountryname is null. Returning");
                return;
            }

            //Clear GUI field
            txtcountryname.Text = String.Empty;

            var service = new CountryReverseGeocodeService();
            var gLocation = new GeoLocation { Latitude = cLocation.Latitude, Longitude = cLocation.Longitude };
            LocationInfo locationInfo = service.FindCountry(gLocation);

            if (locationInfo == null)
            {
                Serilog.Log.Error($"locationInfo is null. Unable to determine which router database to use");
                return;
            }

            if (mContext is null)
            {
                Serilog.Log.Information($"mContext is null. Returning");
                return;
            }

            Serilog.Log.Information($"FindCountry: '{locationInfo.Name}'");
            var countryName = locationInfo.Name.ToLower();

            //RouterDB exists?
            var dbInfo = new FileInfo(FileSystem.AppDataDirectory + "/" + countryName + ".db");
            if (dbInfo.Exists == false)
            {
                Serilog.Log.Warning($"RouterDB does not exists for selected country. Need to add routerdb to app folder");
                ShowDialog msg = new(mContext);
                if (await msg.Dialog("Routerdb not found for region", $"Add '{countryName}.db' road database?", Android.Resource.Attribute.DialogIcon, false, global::ShowDialog.MessageResult.YES, global::ShowDialog.MessageResult.NO) != global::ShowDialog.MessageResult.YES) return;

                if (Android.OS.Environment.DirectoryDownloads is null)
                {
                    Serilog.Log.Error($"DirectoryDownloads is null. returning.");
                    return;
                }

                //Getting to this point means there is no working router.db file for the location we are inn
                //Open filebrowser and select it. File name must match country name
                await PickAndShow(null, "db");
            }

            //PBF exists?
            var pbfInfo = new FileInfo(FileSystem.AppDataDirectory + "/" + countryName + ".osm.pbf");
            if (pbfInfo.Exists == false)
            {
                Serilog.Log.Warning($"PBF does not exists for selected country. Need to add PBF to app folder");
                ShowDialog msg = new(mContext);
                if (await msg.Dialog("PBF not found for region", $"Add '{countryName}.osm.pbf'?", Android.Resource.Attribute.DialogIcon, false, global::ShowDialog.MessageResult.YES, global::ShowDialog.MessageResult.NO) != global::ShowDialog.MessageResult.YES) return;

                if (Android.OS.Environment.DirectoryDownloads is null)
                {
                    Serilog.Log.Error($"DirectoryDownloads is null. returning.");
                    return;
                }

                //Getting to this point means there is no working PBF file for the location we are inn
                //Open filebrowser and select it. File name must match country name
                await PickAndShow(null, "osm.pbf");
            }

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

            if (id == Resource.Id.nav_import_db)
            {
                _ = PickAndShow(null, "db");
            }

            if (id == Resource.Id.nav_import_pbf)
            {
                _ = PickAndShow(null, "osm.pbf");
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

                    Serilog.Log.CloseAndFlush();
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
            
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            }
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

        async static Task PickAndShow(PickOptions? options, string file_extention)
        {
            try
            {
                var result = await FilePicker.PickAsync(options);
                if (result != null)
                {
                    Serilog.Log.Information($"Filename: '{result.FileName}'");

                    if (result.FileName.EndsWith(file_extention, StringComparison.OrdinalIgnoreCase))
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