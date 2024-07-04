using Android.Content;
using Android.OS;
using Itinero;
using System;
using System.Runtime.CompilerServices;
using Xamarin.Essentials;


namespace Velociraptor
{
    internal class BatteryOptimization
    {
        /// <summary>
        /// Opt out of BatteryOptimization
        /// </summary>
        public static bool SetDozeOptimization(Activity? activity)
        {
            if (activity == null)
            {
                return false;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                Serilog.Log.Debug($"Request disabling BatteryOptimizations");
                var intent = new Intent();
                intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.BroughtToFront);
                PowerManager ? pm = (PowerManager?)activity.GetSystemService(Android.Content.Context.PowerService);
                if (pm != null && pm.IsIgnoringBatteryOptimizations(activity.PackageName))
                {
                    //For future reference - Fine tune BatteryOptimization
                    //intent.SetAction(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
                    //activity.StartActivity(intent);
                }
                else
                {
                    //intent.SetAction(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                    //intent.SetAction(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
                    
                    intent.SetAction(Android.Provider.Settings.ExtraBatterySaverModeEnabled);
                    intent.SetData(Android.Net.Uri.Parse("package:" + activity.PackageName));
                    try
                    {
                        activity.StartActivity(intent);
                    }
                    catch (Exception ex)
                    {
                        /**////Not working. Fix me!
                        Serilog.Log.Error(ex, "Crashed on opening battery optimization settings screen");
                        return false;
                    }
                }

                return true;
            }
            else
            {
                Serilog.Log.Debug($"BatteryOptimizations Not Supported - Requires API 23 or above");
                return false;
            }
        }
    }
}
