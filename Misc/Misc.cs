using Android.Content;
using Android.OS;
using Mapsui;
using NetTopologySuite.Operation.Distance3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Velociraptor {
    internal class Misc
    {
        public class MapPoint
        {
            public float Longitude { get; set; }
            public float Latitude { get; set; }
        }

        public class BoundingBox
        {
            public MapPoint? MinPoint { get; set; }
            public MapPoint? MaxPoint { get; set; }
        }

        public static BoundingBox? GetBoundingBox(Android.Locations.Location? cLocation, int distance_m)
        {
            if (cLocation == null)
            {
                return null;
            }

            var lat_min = cLocation.Latitude - (0.000009 * distance_m);
            var lat_max = cLocation.Latitude + (0.000009 * distance_m);
            var lon_min = cLocation.Longitude - (0.000009 * distance_m);
            var lon_max = cLocation.Longitude + (0.000009 * distance_m);

            return new BoundingBox
            {
                MinPoint = new MapPoint { Latitude = (float)lat_min, Longitude = (float)lon_max },
                MaxPoint = new MapPoint { Latitude = (float)lat_max, Longitude = (float)lon_min }
            };
        }

        public static void SetDozeOptimization(Activity mContext, Intent? BatteryOptimizationsIntent)
        {
            if (mContext == null || BatteryOptimizationsIntent == null)
            {
                return;
            }
            /*
            //https://social.msdn.microsoft.com/Forums/en-US/895f0759-e05d-4747-b72b-e16a2e8dbcf9/developing-a-location-background-service?forum=xamarinforms
            if (Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
            {
                PowerManager? pm = (PowerManager)mContext?.GetSystemService(Context.PowerService);
                //if (pm != null && !pm.IsIgnoringBatteryOptimizations(mContext?.PackageName))
                {
                    BatteryOptimizationsIntent.AddFlags(ActivityFlags.NewTask);
                    BatteryOptimizationsIntent.SetAction(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                    BatteryOptimizationsIntent.SetData(Android.Net.Uri.Parse("package:" + mContext.PackageName));
                    mContext.StartActivity(BatteryOptimizationsIntent);
                }
            }*/
        }

        /*
        public void ClearDozeOptimization(Intent BatteryOptimizationsIntent)
        {
            //https://social.msdn.microsoft.com/Forums/en-US/895f0759-e05d-4747-b72b-e16a2e8dbcf9/developing-a-location-background-service?forum=xamarinforms
            if (null != BatteryOptimizationsIntent && Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
            {
                BatteryOptimizationsIntent.ReplaceExtras(new Bundle());
                BatteryOptimizationsIntent.SetAction("");
                BatteryOptimizationsIntent.SetData(null);
                BatteryOptimizationsIntent.SetFlags(0);
            }
        }
        */
    }
}
