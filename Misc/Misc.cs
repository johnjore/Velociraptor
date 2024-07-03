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
    }
}
