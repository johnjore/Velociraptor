using Android.App;
using Android.Locations;
using Android.OS;
using Android.Preferences;
using System;
using System.Text;

namespace Velociraptor
{
    [Activity(Label = "preferences")]
    public class PrefsActivity : PreferenceActivity
    {
        //Location Service
        public const int SERVICE_RUNNING_NOTIFICATION_ID = 10005;
        public const string NOTIFICATION_CHANNEL_ID = "no.jore.velociraptor.service";
        public const string channelName = "App Service1";
        public const int NOTIFICATION_ID_HIGH = 10006;
        public const string NOTIFICATION_CHANNEL_ID_HIGH = "no.jore.velociraptor.high6";
        public const string channelName_high = "High priority notifications6";
        public const string SERVICE_STARTED_KEY = "has_service_been_started";
        public const string ACTION_START_SERVICE = "velociraptor.action.START_SERVICE";
        public const string ACTION_STOP_SERVICE = "velociraptor.action.STOP_SERVICE";
        public const string ACTION_MAIN_ACTIVITY = "velociraptor.action.MAIN_ACTIVITY";

        //Map
        public const string LocationLayerName = "Location";

        public const int default_speed_margin = 3; //% of margin before speeding alarm

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                /**///Obsoleted in API 29
                AddPreferencesFromResource(Resource.Xml.Preferences);
            }
            else if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                AddPreferencesFromResource(Resource.Xml.Preferences);
            }
        }
    }
}