
using Android.App;
using Xamarin.Essentials;

public class ShowDialog
{
    public enum MessageResult
    {
        NONE = 0,
        OK = 1,
        CANCEL = 2,
        ABORT = 3,
        RETRY = 4,
        IGNORE = 5,
        YES = 6,
        NO = 7
    }

    readonly Activity mcontext;
    public ShowDialog(Activity activity) : base()
    {
        this.mcontext = activity;
    }

    public Task<MessageResult> Dialog(string Title, string Message, int IconAttribute = Android.Resource.Attribute.AlertDialogIcon, bool SetCancelable = false, MessageResult PositiveButton = MessageResult.OK, MessageResult NegativeButton = MessageResult.NONE, MessageResult NeutralButton = MessageResult.NONE)
    {
        var tcs = new TaskCompletionSource<MessageResult>();

        var builder = new AlertDialog.Builder(mcontext);
        builder.SetIconAttribute(IconAttribute);
        builder.SetTitle(Title);
        builder.SetMessage(Message);
        builder.SetCancelable(SetCancelable);

        if (!OperatingSystem.IsAndroidVersionAtLeast(23) )
        {
            builder.SetInverseBackgroundForced(false);
        }

        builder.SetPositiveButton((PositiveButton != MessageResult.NONE) ? PositiveButton.ToString() : string.Empty, (senderAlert, args) =>
        {
            tcs.SetResult(PositiveButton);
        });
        builder.SetNegativeButton((NegativeButton != MessageResult.NONE) ? NegativeButton.ToString() : string.Empty, delegate
        {
            tcs.SetResult(NegativeButton);
        });
        builder.SetNeutralButton((NeutralButton != MessageResult.NONE) ? NeutralButton.ToString() : string.Empty, delegate
        {
            tcs.SetResult(NeutralButton);
        });

        MainThread.BeginInvokeOnMainThread(() =>
        {
            builder.Show();
        });

        return tcs.Task;
    }
}
