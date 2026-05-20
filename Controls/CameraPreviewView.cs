namespace DuelRecords.Scan.Controls;

public class CameraPreviewView : View
{
    public static readonly BindableProperty ScanEnabledProperty =
        BindableProperty.Create(nameof(ScanEnabled), typeof(bool), typeof(CameraPreviewView), true);

    public bool ScanEnabled
    {
        get => (bool)GetValue(ScanEnabledProperty);
        set => SetValue(ScanEnabledProperty, value);
    }

    public event EventHandler<string>? SetCodeDetected;
    public event EventHandler<string>? CardNameDetected;

    internal void RaiseSetCodeDetected(string setCode)
        => SetCodeDetected?.Invoke(this, setCode);

    internal void RaiseCardNameDetected(string name)
        => CardNameDetected?.Invoke(this, name);

    // Chamado após permissão de câmera ser concedida para (re)iniciar o stream
    public void TryStart()
    {
#if ANDROID
        if (Handler is DuelRecords.Scan.Platforms.Android.CameraPreviewHandler h)
            h.TryOpenCamera();
#endif
    }
}
