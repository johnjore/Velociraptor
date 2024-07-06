using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wibci.CountryReverseGeocode.Models;
using Wibci.CountryReverseGeocode;
using Xamarin.Essentials;

namespace Velociraptor
{
    internal class InitializeLocationData
    {
        public static async Task InitializeOsmProvider()
        {
            Serilog.Log.Debug("InitializeOSMProvider() - Start");

            var cLocation = Geolocation.GetLastKnownLocationAsync().Result;
            if (cLocation == null)
            {
                Serilog.Log.Error($"No cached location? - Can't determine which database to use. Returning");
                return;
            }
            Serilog.Log.Information($"Current location: Latitude: {cLocation.Latitude}, Longitude: {cLocation.Longitude}");

            if (Platform.CurrentActivity is null)
            {
                Serilog.Log.Error($"mContext is null. Returning");
                return;
            }

            if (UpdateScreen.txtcountryname is null)
            {
                Serilog.Log.Error($"txtcountryname is null. Returning");
                return;
            }

            //Clear GUI field
            UpdateScreen.txtcountryname.Text = String.Empty;

            var service = new CountryReverseGeocodeService();
            var gLocation = new GeoLocation { Latitude = cLocation.Latitude, Longitude = cLocation.Longitude };
            LocationInfo locationInfo = service.FindCountry(gLocation);

            if (locationInfo == null)
            {
                Serilog.Log.Error($"locationInfo is null. Unable to determine which router database to use");
                return;
            }

            Serilog.Log.Information($"FindCountry: '{locationInfo.Name}'");
            var countryName = locationInfo.Name.ToLower();

            //RouterDB exists?
            var dbInfo = new FileInfo(FileSystem.AppDataDirectory + "/" + countryName + ".db");
            if (dbInfo.Exists == false)
            {
                Serilog.Log.Warning($"RouterDB does not exists for selected country. Need to add routerdb to app folder");
                ShowDialog msg = new(Platform.CurrentActivity);
                if (await msg.Dialog("Routerdb not found for region", $"Add '{countryName}.db' road database?", Android.Resource.Attribute.DialogIcon, false, global::ShowDialog.MessageResult.YES, global::ShowDialog.MessageResult.NO) != global::ShowDialog.MessageResult.YES) return;

                if (Android.OS.Environment.DirectoryDownloads is null)
                {
                    Serilog.Log.Error($"DirectoryDownloads is null. returning.");
                    return;
                }

                //Getting to this point means there is no working router.db file for the location we are inn
                //Open filebrowser and select it. File name must match country name
                await Misc.PickAndShow(null, "db");
            }

            //PBF exists?
            var pbfInfo = new FileInfo(FileSystem.AppDataDirectory + "/" + countryName + ".osm.pbf");
            if (pbfInfo.Exists == false)
            {
                Serilog.Log.Warning($"PBF does not exists for selected country. Need to add PBF to app folder");
                ShowDialog msg = new(Platform.CurrentActivity);
                if (await msg.Dialog("PBF not found for region", $"Add '{countryName}.osm.pbf'?", Android.Resource.Attribute.DialogIcon, false, global::ShowDialog.MessageResult.YES, global::ShowDialog.MessageResult.NO) != global::ShowDialog.MessageResult.YES) return;

                if (Android.OS.Environment.DirectoryDownloads is null)
                {
                    Serilog.Log.Error($"DirectoryDownloads is null. returning.");
                    return;
                }

                //Getting to this point means there is no working PBF file for the location we are inn
                //Open filebrowser and select it. File name must match country name
                await Misc.PickAndShow(null, "osm.pbf");
            }

            Serilog.Log.Debug("InitializeOSMProvider() - End");
            return;
        }

    }
}
