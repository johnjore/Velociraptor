using Android.App;
using Android.Locations;
using Android.OS;
using Android.Preferences;
using Android.Views;
using AndroidX;
using AndroidX.Core.App;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using AndroidX.Preference;
using AndroidX.AppCompat.App;
using System;
using System.Text;

namespace Velociraptor
{
    [Activity(Label = "settings")]
    public class Fragment_Preferences : PreferenceFragmentCompat
    {
        //Misc
        public readonly static string rootPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        public const string Fragment_Map = "Fragment_Map";
        public const string Fragment_Text = "Fragment_Text";
        public const string Fragment_Settings = "Fragment_Settings";

        //Map
        public const string CacheDB = "CacheDB.mbtiles";    //Database to store offline tiles
        public const int MinZoom = 0;                       //MinZoom level to use
        public const int MaxZoom = 16;                      //MaxZoom level to use
        public const int OfflineMaxAge = 90;                //Don't refresh tiles until this threashhold in days        
        public const string LocationLayerName = "Location";

        //Foreground Service
        public const int SERVICE_RUNNING_NOTIFICATION_ID = 10005;
        public const string NOTIFICATION_CHANNEL_ID = "no.jore.velociraptor.service";
        public const string channelName = "App Service";
        public const int NOTIFICATION_ID_HIGH = 10006;
        public const string NOTIFICATION_CHANNEL_ID_HIGH = "no.jore.velociraptor.high";
        public const string channelName_high = "High priority notifications";
        public const string SERVICE_STARTED_KEY = "has_service_been_started";
        public const string ACTION_START_SERVICE = "velociraptor.action.START_SERVICE";
        public const string ACTION_STOP_SERVICE = "velociraptor.action.STOP_SERVICE";
        public const string ACTION_MAIN_ACTIVITY = "velociraptor.action.MAIN_ACTIVITY";

        //Location Service
        public const int intTimer = 2000;   //How often to get new location
        public const int intDistance = 0;   //Minimum distance for new location

        //Speeding
        public const int default_speed_margin = 3; //% of margin before speeding alarm

        public override void OnCreatePreferences(Bundle? savedInstanceState, string? rootKey)
        {
            SetPreferencesFromResource(Resource.Xml.Preferences, rootKey);
        }
    }
}
