using Android.Locations;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using System.Collections.Generic;
using System.Linq;
using Itinero;
using Itinero.Attributes;
using Itinero.LocalGeo;
using Xamarin.Essentials;
using Serilog;

namespace Velociraptor
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity, ILocationListener
    {
        // Debugging
        public string? TAG { get; private set; }
        private const bool Debug = true;

        // GUI
        TextView? txtlatitude = null;
        TextView? txtlong = null;
        TextView? txtspeed = null;
        TextView? txtstreetname = null;
        TextView? txtspeedlimit = null;
        TextView? txtspeeding = null;
        Android.Locations.Location? currentLocation = null;
        LocationManager? locationManager = null;
        string locationProvider = string.Empty;

        //OSM Data
        Itinero.RouterDb routerDb = new();
        Itinero.Router? router = null;
        Itinero.Profiles.Profile? profile = null;

        //Misc
        private static Activity? mContext;
        /*
        readonly string[] permissions =
        {
            Android.Manifest.Permission.AccessNetworkState,
            Android.Manifest.Permission.AccessFineLocation,
            Android.Manifest.Permission.AccessBackgroundLocation,
            Android.Manifest.Permission.ControlLocationUpdates,
            Android.Manifest.Permission.Internet,            
        };*/

        public void OnLocationChanged(Android.Locations.Location location)
        {
            currentLocation = location;

            if ((txtlatitude is null) || (txtlong is null) || (txtspeed is null) || (txtstreetname is null) || (txtspeedlimit is null) || (txtspeeding is null))
                return;

            if (Resources is null)
                return;

            if (currentLocation == null)
            {
                txtlatitude.Text = Resources.GetString(Resource.String.str_na);
                txtlong.Text = Resources.GetString(Resource.String.str_na);
                txtspeed.Text = Resources.GetString(Resource.String.str_na);
                txtstreetname.Text = Resources.GetString(Resource.String.str_na);
                txtspeedlimit.Text = Resources.GetString(Resource.String.str_na);
                txtspeeding.Text = Resources.GetString(Resource.String.str_na);
            
                return;
            }

            //Update GUI with GPS Information
            txtlatitude.Text = currentLocation.Latitude.ToString();
            txtlong.Text = currentLocation.Longitude.ToString();
            
            //Update GUI with OSM data (streetname and street max speed)
            string streetspeed = string.Empty;
            try
            {
                RouterPoint routerPoint = router.Resolve(profile, new Coordinate((float)currentLocation.Latitude, (float)currentLocation.Longitude));
                //RouterPoint routerPoint = router.Resolve(profile, new Coordinate(-37.81277740408493f, 144.88297495235076f));
                Itinero.Data.Network.RoutingEdge edge = routerDb.Network.GetEdge(routerPoint.EdgeId);
                IAttributeCollection attributes = routerDb.GetProfileAndMeta(edge.Data.Profile, edge.Data.MetaId);

                attributes.TryGetValue("name", out string streetname);
                attributes.TryGetValue("maxspeed", out streetspeed);

                txtstreetname.Text = streetname;
                txtspeedlimit.Text = streetspeed;
            }
            catch
            {
                txtstreetname.Text = Resources.GetString(Resource.String.str_na);
                txtspeedlimit.Text = Resources.GetString(Resource.String.str_na);
                txtspeeding.Text = Resources.GetString(Resource.String.str_na);
            }

            //GPS Speed?
            if (location.HasSpeed == false)
            {
                txtspeed.Text = Resources.GetString(Resource.String.str_na);
                txtspeeding.Text = String.Empty;
                return;
            }

            //Convert from m/s to km/h
            int speed_kmh = (int)(location.Speed * 3.6);
            txtspeed.Text = speed_kmh.ToString() + " " + Resources.GetString(Resource.String.str_kmh);


            if (Int32.TryParse(streetspeed, out int streetspeed_int) == false)
            {
                txtspeeding.Text = String.Empty;
                return;
            }

            //If not speeding, we're done
            if (speed_kmh < streetspeed_int)
            {
                txtspeeding.Text = String.Empty;
                return;
            }

            //Update GUI and play notification sound
            txtspeeding.Text = Resources.GetString(Resource.String.str_speeding);
            var soundUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            var r = RingtoneManager.GetRingtone(mContext, soundUri);
            r?.Play();           
        }

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

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            if (Debug)
                Android.Util.Log.Error(TAG, "+!++ ON CREATE ++!+");

            /*  foreach (Android.Manifest.Permission permission in permissions)
            {
                var status = 
                while (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(this, permission) != (int)Android.Content.PM.Permission.Granted)
                {
                    RequestPermissions(new[] { permission }, 42)
                }
            }*/

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            txtlatitude = FindViewById<TextView>(Resource.Id.txtlatitude);
            txtlong = FindViewById<TextView>(Resource.Id.txtlong);
            txtspeed = FindViewById<TextView>(Resource.Id.txtspeed);
            txtspeeding = FindViewById<TextView>(Resource.Id.txtspeeding);
            txtstreetname = FindViewById<TextView>(Resource.Id.txtstreetname);
            txtspeedlimit = FindViewById<TextView>(Resource.Id.txtspeedlimit);

            mContext = this;

            InitializeLocationManager();
            InitializeOsmProvider();
        }

        private void InitializeLocationManager()
        {
            locationManager = GetSystemService(LocationService) as LocationManager;
            if (locationManager == null)
            {
                Android.Util.Log.Error(TAG, "locationManager is null");
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
            } else {
                locationProvider = string.Empty;
            }

            Android.Util.Log.Debug(TAG, "Using " + locationProvider + ".");
        }

        private void InitializeOsmProvider()
        {
            Android.Util.Log.Debug(TAG, "InitializeOSMProvider() - Start");

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
                Android.Util.Log.Debug(TAG, filename);
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
                Android.Util.Log.Debug(TAG, filename);
            }
            */

            var filename = FileSystem.AppDataDirectory + "/" + "routerdb";
            var stream = new FileInfo(filename).OpenRead();
            routerDb = RouterDb.Deserialize(stream, RouterDbProfile.NoCache);
            profile = routerDb.GetSupportedProfile("car");
            router = new Router(routerDb);

            Android.Util.Log.Debug(TAG, "InitializeOSMProvider() - End");
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (locationProvider is not null && locationManager is not null)
            {
                locationManager.RequestLocationUpdates(locationProvider, 0, 0, this);
            }            
        }

        protected override void OnPause()
        {
            base.OnPause();
            if (locationManager is null)
            {
                return;
            }

            locationManager.RemoveUpdates(this);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
