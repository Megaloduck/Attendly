using System;
using System.Threading.Tasks;
using Android.App;
using Android.Gms.Tasks;
using Attendly.Services;
using Xamarin.Google.MLKit.Vision.CodeScanner;
using Xamarin.Google.MLKit.Vision.Barcode.Common;

namespace Attendly.Android;

/// <summary>
/// Wraps Google's ML Kit Code Scanner (com.google.android.gms:play-services-mlkit-barcode-scanning).
/// This launches Google's own prebuilt full-screen scanning UI (camera preview, focus, torch
/// button) entirely outside Avalonia's visual tree, so no custom camera-preview control is
/// needed here at all - sidesteps the fact that Avalonia itself has no camera control
/// (AvaloniaUI/Avalonia#12956, still open) by never embedding a live camera feed in the app's
/// own UI in the first place.
///
/// ONE THING I COULDN'T VERIFY WITHOUT COMPILING: the exact C# namespace the NuGet package's
/// binding generator used (the two `using`s above). If they don't resolve, open the
/// Xamarin.GooglePlayServices.MLKit.BarcodeScanning package under Dependencies in Solution
/// Explorer and let IntelliSense's namespace autocomplete show the real path when you start
/// typing "GmsBarcodeScanning" - the class/method names below should still be correct either way.
/// </summary>
public class AndroidQrScanner : IQrScanner
{
    private readonly Activity _activity;

    public AndroidQrScanner(Activity activity)
    {
        _activity = activity;
    }

    public bool IsSupported => true;

    public Task<string?> ScanAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        var options = new GmsBarcodeScannerOptions.Builder()
            .SetBarcodeFormats(Barcode.FormatQrCode)
            .Build();

        var scanner = GmsBarcodeScanning.GetClient(_activity, options);
        var task = scanner.StartScan();

        task.AddOnSuccessListener(new SuccessListener(barcode => tcs.TrySetResult(barcode?.RawValue)));
        task.AddOnFailureListener(new FailureListener(_ => tcs.TrySetResult(null)));
        task.AddOnCanceledListener(new CanceledListener(() => tcs.TrySetResult(null)));

        return tcs.Task;
    }

    private sealed class SuccessListener : Java.Lang.Object, IOnSuccessListener
    {
        private readonly Action<Barcode?> _onSuccess;
        public SuccessListener(Action<Barcode?> onSuccess) => _onSuccess = onSuccess;
        public void OnSuccess(Java.Lang.Object? result) => _onSuccess(result as Barcode);
    }

    private sealed class FailureListener : Java.Lang.Object, IOnFailureListener
    {
        private readonly Action<Java.Lang.Exception> _onFailure;
        public FailureListener(Action<Java.Lang.Exception> onFailure) => _onFailure = onFailure;
        public void OnFailure(Java.Lang.Exception e) => _onFailure(e);
    }

    private sealed class CanceledListener : Java.Lang.Object, IOnCanceledListener
    {
        private readonly Action _onCanceled;
        public CanceledListener(Action onCanceled) => _onCanceled = onCanceled;
        public void OnCanceled() => _onCanceled();
    }
}