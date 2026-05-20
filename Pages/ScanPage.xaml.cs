using DuelRecords.Scan.ViewModels;

namespace DuelRecords.Scan.Pages;

public partial class ScanPage : ContentPage
{
    public ScanPage(ScanViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        CameraPreview.SetCodeDetected  += async (_, code) => await vm.SetCodeDetectedAsync(code);
        CameraPreview.CardNameDetected += async (_, name) => await vm.CardNameDetectedAsync(name);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Solicita permissão de câmera (Android requer em runtime a partir do API 23)
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status == PermissionStatus.Granted)
            CameraPreview.TryStart();
        else if (BindingContext is ScanViewModel vm)
            vm.StatusMessage = "Permissão de câmera negada. Habilite nas configurações do app.";
    }
}
