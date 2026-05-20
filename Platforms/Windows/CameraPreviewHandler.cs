using DuelRecords.Scan.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Text.RegularExpressions;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Ocr;

namespace DuelRecords.Scan.Platforms.Windows;

// Handler Windows: usa MediaFrameReader para preview contínuo + Windows.Media.Ocr para set code.
public partial class CameraPreviewHandler : ViewHandler<CameraPreviewView, Microsoft.UI.Xaml.Controls.Image>
{
    public static readonly IPropertyMapper<CameraPreviewView, CameraPreviewHandler> Mapper =
        new PropertyMapper<CameraPreviewView, CameraPreviewHandler>(ViewMapper)
        {
            [nameof(CameraPreviewView.ScanEnabled)] = MapScanEnabled,
        };

    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private CancellationTokenSource? _scanCts;
    private volatile bool _scanEnabled = true;
    private bool _cameraReady;
    private SoftwareBitmap? _latestBitmap;
    private readonly object _bitmapLock = new();
    private OcrEngine? _ocrEngine;
    private volatile bool _previewPending;

    [GeneratedRegex(@"\b[A-Z0-9]{2,6}-[A-Z]{2}\d{3,4}\b")]
    private static partial Regex SetCodeRegex();

    public CameraPreviewHandler() : base(Mapper) { }

    // ── View nativa ──────────────────────────────────────────────────────────────

    protected override Microsoft.UI.Xaml.Controls.Image CreatePlatformView()
        => new Microsoft.UI.Xaml.Controls.Image { Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill };

    protected override void ConnectHandler(Microsoft.UI.Xaml.Controls.Image platformView)
    {
        base.ConnectHandler(platformView);
        _ = InitCameraAsync();
    }

    protected override void DisconnectHandler(Microsoft.UI.Xaml.Controls.Image platformView)
    {
        StopScanLoop();
        _ = CleanupCameraAsync();
        base.DisconnectHandler(platformView);
    }

    // ── Property mapping ─────────────────────────────────────────────────────────

    private static void MapScanEnabled(CameraPreviewHandler handler, CameraPreviewView view)
    {
        handler._scanEnabled = view.ScanEnabled;
        if (view.ScanEnabled)
            handler.StartScanLoop();
    }

    public void TryOpenCamera()
    {
        if (!_cameraReady)
            _ = InitCameraAsync();
    }

    // ── Inicialização ─────────────────────────────────────────────────────────────

    private async Task InitCameraAsync()
    {
        try
        {
            _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
                      ?? OcrEngine.TryCreateFromUserProfileLanguages();

            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video
            });

            // Prefere VideoPreview (menor resolução, ideal para display); cai em VideoRecord se não existir
            var frameSource = _mediaCapture.FrameSources.Values
                .FirstOrDefault(s => s.Info.MediaStreamType == global::Windows.Media.Capture.MediaStreamType.VideoPreview)
                ?? _mediaCapture.FrameSources.Values
                .FirstOrDefault(s => s.Info.MediaStreamType == global::Windows.Media.Capture.MediaStreamType.VideoRecord);

            if (frameSource is null)
            {
                SetStatus("Nenhuma fonte de vídeo encontrada na webcam.");
                return;
            }

            // Pede Bgra8; o OS converte automaticamente se a câmera usar outro formato
            _frameReader = await _mediaCapture.CreateFrameReaderAsync(
                frameSource,
                global::Windows.Media.MediaProperties.MediaEncodingSubtypes.Bgra8);

            _frameReader.FrameArrived += OnFrameArrived;
            await _frameReader.StartAsync();

            _cameraReady = true;
            StartScanLoop();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WinCam] Init error: {ex.Message}");
            SetStatus($"Webcam não disponível: {ex.Message}");
        }
    }

    // ── Recebimento de frames ─────────────────────────────────────────────────────

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        using var frameRef = sender.TryAcquireLatestFrame();
        var raw = frameRef?.VideoMediaFrame?.SoftwareBitmap;
        if (raw is null) return;

        // Converte para Bgra8 Premultiplied (exigido por WriteableBitmap e OcrEngine)
        var frame = SoftwareBitmap.Convert(raw, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        // Guarda frame mais recente para o loop de OCR
        SoftwareBitmap? old;
        lock (_bitmapLock)
        {
            old = _latestBitmap;
            _latestBitmap = SoftwareBitmap.Convert(frame, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
        old?.Dispose();

        // Atualiza preview na UI thread; throttle para não enfileirar centenas de updates
        if (!_previewPending)
        {
            _previewPending = true;
            _ = UpdatePreviewAsync(frame);
        }
        else
        {
            frame.Dispose();
        }
    }

    private async Task UpdatePreviewAsync(SoftwareBitmap bitmap)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                var wbm = new WriteableBitmap(bitmap.PixelWidth, bitmap.PixelHeight);
                bitmap.CopyToBuffer(wbm.PixelBuffer);
                PlatformView.Source = wbm;
            }
            catch { }
            finally
            {
                bitmap.Dispose();
                _previewPending = false;
            }
        });
    }

    // ── Loop de OCR ──────────────────────────────────────────────────────────────

    private void StartScanLoop()
    {
        if (_scanCts is not null) return;
        _scanCts = new CancellationTokenSource();
        _ = ScanLoopAsync(_scanCts.Token);
    }

    private void StopScanLoop()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
    }

    private async Task ScanLoopAsync(CancellationToken ct)
    {
        if (_ocrEngine is null) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, ct).ConfigureAwait(false);
                if (!_scanEnabled) continue;

                // Copia o frame atual para não bloquear o lock durante o OCR
                SoftwareBitmap? snapshot;
                lock (_bitmapLock)
                {
                    snapshot = _latestBitmap is not null
                        ? SoftwareBitmap.Convert(_latestBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
                        : null;
                }

                if (snapshot is null) continue;

                using (snapshot)
                {
                    var ocrResult = await _ocrEngine.RecognizeAsync(snapshot).AsTask(ct);
                    var text = string.Join(" ", ocrResult.Lines.Select(l => l.Text));

                    if (string.IsNullOrWhiteSpace(text)) continue;

                    System.Diagnostics.Debug.WriteLine($"[WinCam] OCR: {text[..Math.Min(100, text.Length)]}");

                    var match = SetCodeRegex().Match(text);
                    if (match.Success)
                    {
                        var setCode = match.Value;
                        System.Diagnostics.Debug.WriteLine($"[WinCam] Set code: {setCode}");
                        MainThread.BeginInvokeOnMainThread(()
                            => VirtualView?.RaiseSetCodeDetected(setCode));
                        await Task.Delay(3000, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WinCam] Scan error: {ex.Message}");
            }
        }
    }

    // ── Limpeza ───────────────────────────────────────────────────────────────────

    private async Task CleanupCameraAsync()
    {
        if (_frameReader is not null)
        {
            _frameReader.FrameArrived -= OnFrameArrived;
            await _frameReader.StopAsync();
            _frameReader.Dispose();
            _frameReader = null;
        }
        _mediaCapture?.Dispose();
        _mediaCapture = null;
        lock (_bitmapLock)
        {
            _latestBitmap?.Dispose();
            _latestBitmap = null;
        }
        _cameraReady = false;
    }

    private void SetStatus(string msg) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (VirtualView?.BindingContext is ViewModels.ScanViewModel vm)
                vm.StatusMessage = msg;
        });
}
