using Android;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Locations;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using AndroidX.Fragment.App;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Navigation;
using Google.Android.Material.Snackbar;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;
using Serilog.Core;
using Serilog.Sink.AppCenter;
using TelegramSink;
using Xamarin.Essentials;

namespace Velociraptor
{
    [Activity(Name = "no.johnjore.velociraptor.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        // Debugging
        public static string? TAG { get; private set; }

        protected override async void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

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
                .Enrich.WithProperty(Serilog.Core.Constants.SourceContextPropertyName, "@string/app_name")
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

            Serilog.Log.Debug($"Request all application permissions");
            await AppPermissions.RequestAppPermissions(this);

            Serilog.Log.Debug($"Notify user if location permission does not allow background collection");
            await AppPermissions.LocationPermissionNotification(this);

            Serilog.Log.Debug($"MainActivity - Initilize OSM Provider");
            _ = InitializeLocationData.InitializeOsmProvider();

            //Location Service. Service checks if already running
            Serilog.Log.Debug($"MainActivity - Start LocationService");
            Intent locationServiceIntent = new(this, typeof(LocationForegroundService));
            locationServiceIntent.SetAction(PrefsFragment.ACTION_START_SERVICE);
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                StartForegroundService(locationServiceIntent);
            }
            else
            {
                StartService(locationServiceIntent);
            }

            //Disable battery optimization
            BatteryOptimization.SetDozeOptimization(this);
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
                SetContentView(Resource.Layout.preferences);
                var FragmentsTransaction = SupportFragmentManager.BeginTransaction();
                FragmentsTransaction.Replace(Resource.Id.preference_container, new PrefsFragment());
                FragmentsTransaction.Commit();

                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        public bool OnNavigationItemSelected(IMenuItem item)
        {
            int id = item.ItemId;

            if (id == Resource.Id.nav_import_db)
            {
                _ = Misc.PickAndShow(null, "db");
            }

            if (id == Resource.Id.nav_import_pbf)
            {
                _ = Misc.PickAndShow(null, "osm.pbf");
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
                //If in "Settings", jump back to MainActivity. Fix when MainActivity uses Fragments
                StartActivity(new Intent(this, typeof(MainActivity)));

                Serilog.Log.Error("drawer is null. Returning");
                return;
            }

            if (drawer.IsDrawerOpen(GravityCompat.Start))
            {
                drawer.CloseDrawer(GravityCompat.Start);
            }
            else
            {
                using var alert = new Android.App.AlertDialog.Builder(Platform.CurrentActivity);
                alert.SetTitle(Platform.CurrentActivity?.Resources?.GetString(Resource.String.ExitTitle));
                alert.SetMessage(Platform.CurrentActivity?.Resources?.GetString(Resource.String.ExitPrompt));
                alert.SetPositiveButton(Resource.String.Yes, (sender, args) => {
                    //Location Service
                    Intent locationServiceIntent = new(this, typeof(LocationForegroundService));
                    locationServiceIntent.SetAction(PrefsFragment.ACTION_STOP_SERVICE);
                    StopService(locationServiceIntent);

                    Serilog.Log.CloseAndFlush();
                    Platform.CurrentActivity?.FinishAffinity();
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
    }
}