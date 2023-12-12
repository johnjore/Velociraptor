using Android.App;
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
        public const int SERVICE_RUNNING_NOTIFICATION_ID = 10001;
        public const string NOTIFICATION_CHANNEL_ID = "no.jore.velociraptor";
        public const string channelName = "Velociraptor service";
        public const string SERVICE_STARTED_KEY = "has_service_been_started";
        public const string ACTION_START_SERVICE = "velociraptor.action.START_SERVICE";
        public const string ACTION_STOP_SERVICE = "velociraptor.action.STOP_SERVICE";
        public const string ACTION_MAIN_ACTIVITY = "velociraptor.action.MAIN_ACTIVITY";

        public const int default_speed_margin = 3; //% of margin before speeding alarm

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            AddPreferencesFromResource(Resource.Xml.Preferences);
        }
    }
}