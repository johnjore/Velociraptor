using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using OsmSharp;
using OsmSharp.Geo;
using OsmSharp.Streams;

namespace Velociraptor
{
    internal class DataOSMPBF
    {
        private static string pbf_StreetName = string.Empty;
        private static string pbf_StreetSpeed = string.Empty;

        private static (string pbf_StreetName, string pbf_StreetSpeed) GetStreetInformation_pbf(Android.Locations.Location? cLocation)
        {
            string sName = string.Empty;
            string sSpeed = string.Empty;
            var bbox = Misc.GetBoundingBox(cLocation, 100);

            if (bbox == null)
            {
                return (pbf_StreetName: string.Empty, pbf_StreetSpeed: string.Empty);
            }

            if (bbox.MinPoint == null || bbox.MaxPoint == null)
            {
                return (pbf_StreetName: string.Empty, pbf_StreetSpeed: string.Empty);
            }

            using (var fileStream = File.OpenRead(FileSystem.AppDataDirectory + "/" + /*countryName*/ "luxembourg" + ".osm.pbf"))
            {
                var source = new PBFOsmStreamSource(fileStream);
                //var region = source.FilterBox(bbox.MaxPoint.Latitude, bbox.MaxPoint.Longitude, bbox.MinPoint.Latitude, bbox.MinPoint.Longitude);
                var region = source.FilterBox(6.242969810172371f, 49.71720151392213f, 6.249192535136989f, 49.71520366157044f);
                var filtered = region.Where(x => x.Type == OsmSharp.OsmGeoType.Way || x.Type == OsmSharp.OsmGeoType.Node);
                var features = filtered.ToFeatureSource();

                var items = features.ToList();
                var ItemCount = items.Count;

                var lineStrings = items.Where(x => x.Geometry.GeometryType == "LineString").ToList();
                var lineStringCount = lineStrings.Count;

                var completeSource = region.ToComplete();
                //var filtered = from osmGeo in completeSource where osmGeo.Type == OsmGeoType.Way select osmGeo;

                /*var filtered = from osmGeo in progress
                               where osmGeo.Type == OsmSharp.OsmGeoType.Node ||
                                     (osmGeo.Type == OsmSharp.OsmGeoType.Way && osmGeo.Tags != null && osmGeo.Tags.Contains("power", "line"))
                               select osmGeo;
                */
                Serilog.Log.Debug(filtered.Count().ToString());
                Serilog.Log.Debug("ArfArf");
                Serilog.Log.Debug("ArfArf");

                //var filtered2 = from osmGeo in progress where osmGeo.Type == OsmGeoType.Way select osmGeo;


                /*                var filtered2 = from osmGeo in progress
                                                where osmGeo.Type == OsmSharp.OsmGeoType.Node ||
                                                      (osmGeo.Type == OsmSharp.OsmGeoType.Way && osmGeo.Tags != null) // && osmGeo.Tags.Contains("highway", "residential"))
                                                select osmGeo;
                */

                foreach (var osmGeo in filtered)
                {
                    Serilog.Log.Debug($"'{osmGeo}'");
                }
                Serilog.Log.Debug($"Done");


                /*df_osm.loc[df_osm.tagkey == 'highway', ['id', 'tagvalue']].merge(
                    df_osm.loc[df_osm.tagkey == 'name', ['id', 'tagvalue']],
                    on = 'id', suffixes = ['_kind', '_name'])*/

                /*var features = filtered2.ToFeatureSource();

                var lineStrings = from feature in features
                                  where feature.Geometry is LineString
                                  select feature;
                */
                /*var featureCollection = new FeatureCollection();
                foreach (var feature in lineStrings)
                {
                    featureCollection.Add(feature);
                    //Serilog.Log.Debug($"'{feature.ToString()}'");
                }
                */
                Serilog.Log.Debug($"Done");
                //var json = ToJson(featureCollection);
            }


            return (pbf_StreetName: sName, pbf_StreetSpeed: sSpeed);
        }
    }
}
