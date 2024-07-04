using Android.Content;
using Android.OS;
using Mapsui;
using NetTopologySuite.Operation.Distance3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

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

        public async static Task PickAndShow(PickOptions? options, string file_extention)
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
