using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Velociraptor.Models;

namespace Velociraptor
{
    internal class Azure
    {
        public static void AzureStreetSpeed() 
        {
            double lat = -37.81076991109956;
            double lon = 144.88298804322875;
            string? AzureMapsAPIKey = Platform.AppContext.Resources?.GetString(Resource.String.AzureMapsAPIKey);
            string searchURL = $"https://atlas.microsoft.com/search/address/reverse/json?api-version=1.0&query={lat}," +
                $"{lon}&subscription-key={AzureMapsAPIKey}&returnSpeedLimit=true&radius=25&returnRoadUse=false&returnMatchType=false";

            var client = new HttpClient();
            HttpResponseMessage response = client.GetAsync(searchURL).Result;
            HttpContent responseContent = response.Content;

            string output = string.Empty;
            using (var reader = new StreamReader(responseContent.ReadAsStreamAsync().Result))
            {
                output = reader.ReadToEndAsync().Result;
                Console.WriteLine(output);
            }

            JsonSerializerOptions jsonSerializerOptions = new()
            {
                PropertyNameCaseInsensitive = true
            };
            var options = jsonSerializerOptions;

            AzureMapData? azureMapData = JsonSerializer.Deserialize<AzureMapData>(output, options);
            var azureSpeedLimits = azureMapData?.Addresses?.Select(x => x.Address?.SpeedLimit).ToArray();
            if (azureSpeedLimits?.Length > 0)
            {
                var b = azureSpeedLimits?.First()?.Replace("KPH", "");
                Console.WriteLine(b);
            }
        }
    }
}
