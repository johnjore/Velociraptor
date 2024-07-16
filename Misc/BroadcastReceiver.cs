using System;
using System.Text;
using Android.App;
using Android.Content;

namespace Velociraptor
{
    [BroadcastReceiver(Exported = true, Enabled = true, Label = "BootBroadcastReceiver")]
    [IntentFilter([Intent.ActionBootCompleted])]
    public class BootBroadcastReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            Serilog.Log.Information($"BootBroadcastReceiver - OnReceive() Starting");

            if (context is null || intent is null || intent.Action is null)
            {
                Serilog.Log.Warning($"BootBroadcastReceiver - context or intent.Action is null - returning");
                return;
            }

            if (intent.Action.Equals(Intent.ActionBootCompleted))
            {
                Serilog.Log.Debug($"BootBroadcastReceiver - ActionBootcompleted - Starting locationService");
                Intent locationServiceIntent = new(context, typeof(LocationForegroundService));
                locationServiceIntent.SetAction(Fragments.Fragment_Preferences.ACTION_START_SERVICE);
                if (OperatingSystem.IsAndroidVersionAtLeast(26))
                {
                    context.StartForegroundService(locationServiceIntent);
                }
                else
                {
                    context.StartService(locationServiceIntent);
                }
            }
        }
    }
}