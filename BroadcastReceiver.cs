using System;
using System.Text;
using Android.App;
using Android.Content;

namespace Velociraptor
{
    [BroadcastReceiver(Enabled = true, Label = "Boot Notification") ]
    public class BootBroadcastReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (context is null || intent.Action is null)
            {
                Serilog.Log.Warning($"BootBroadcastReceiver - context or intent.Action is null - returning");
                return;
            }

            if (intent.Action.Equals(Intent.ActionBootCompleted))
            {
                Serilog.Log.Debug($"BootBroadcastReceiver - ActionBootcompleted - Starting locationService");
                Intent locationServiceIntent = new(context, typeof(LocationForegroundService));
                locationServiceIntent.SetAction(PrefsActivity.ACTION_START_SERVICE);
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
                {
#pragma warning disable CA1416
                    context.StartForegroundService(locationServiceIntent);
#pragma warning restore CA1416
                }
                else
                {
                    context.StartService(locationServiceIntent);
                }
            }
        }
    }
}