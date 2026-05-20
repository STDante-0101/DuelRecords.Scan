using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Gms.Extensions;
using Android.Widget;
using DuelRecords.Scan.Controls;
using Microsoft.Maui.Handlers;
using System.Text.RegularExpressions;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;

namespace DuelRecords.Scan.Platforms.Android;

// Arquitetura Camera2 correta:
//   Câmera → CaptureSession → TextureView  (preview para o usuário)
//                           → ImageReader  (frames brutos para o OCR)
// GetBitmap() do TextureView retorna branco porque o driver escreve em buffer
// de hardware inacessível — o ImageReader resolve isso.
public partial class CameraPreviewHandler : ViewHandler<CameraPreviewView, FrameLayout>
{
    private const string Tag = "DuelScan";
    private const int BufW = 1920;
    private const int BufH = 1080;

    public static readonly IPropertyMapper<CameraPreviewView, CameraPreviewHandler> Mapper =
        new PropertyMapper<CameraPreviewView, CameraPreviewHandler>(ViewMapper)
        {
            [nameof(CameraPreviewView.ScanEnabled)] = MapScanEnabled,
        };

    private TextureView? _textureView;
    private CameraDevice? _camera;
    private CameraCaptureSession? _session;
    private ImageReader? _imageReader;
    private Surface? _analysisSurface;
    private HandlerThread? _bgThread;
    private global::Android.OS.Handler? _bgHandler;
    private CancellationTokenSource? _scanCts;
    private volatile bool _scanEnabled = true;
    private volatile bool _cameraOpenRequested;
    private int _sensorOrientation = 90;

    // OCR confunde O↔0 — aceita O na parte numérica (CHO1-PTO09 → CHO1-PT009)
    [GeneratedRegex(@"\b[A-Z0-9]{2,6}-[A-Z]{2}[0-9O]{3,4}\b")]
    private static partial Regex SetCodeRegex();

    // Nome de carta: 2-4 palavras ALL-CAPS consecutivas em qualquer posição do texto
    // Usa espaço literal (não \s) para não cruzar linhas do OCR
    // Primeira palavra ≥5 chars para filtrar ruído (ATK, DEF, CTRL, etc.)
    [GeneratedRegex(@"([A-ZÁÀÃÂÉÊÍÓÔÕÚÇ]{5,}(?: [A-ZÁÀÃÂÉÊÍÓÔÕÚÇ]{3,}){1,3})")]
    private static partial Regex CardNameStartRegex();

    public CameraPreviewHandler() : base(Mapper) { }

    // ── View nativa ─────────────────────────────────────────────────────────────

    protected override FrameLayout CreatePlatformView()
    {
        Log.Debug(Tag, "CreatePlatformView");
        var frame = new FrameLayout(Context!);
        _textureView = new TextureView(Context!);
        _textureView.SurfaceTextureListener = new SurfaceListener(this);
        frame.AddView(_textureView, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));
        return frame;
    }

    protected override void ConnectHandler(FrameLayout platformView)
    {
        Log.Debug(Tag, "ConnectHandler");
        base.ConnectHandler(platformView);
        StartBackgroundThread();
    }

    protected override void DisconnectHandler(FrameLayout platformView)
    {
        Log.Debug(Tag, "DisconnectHandler");
        StopScanLoop();
        CloseCamera();
        StopBackgroundThread();
        base.DisconnectHandler(platformView);
    }

    // ── Property mapping ────────────────────────────────────────────────────────

    private static void MapScanEnabled(CameraPreviewHandler handler, CameraPreviewView view)
    {
        handler._scanEnabled = view.ScanEnabled;
        if (view.ScanEnabled)
            handler.StartScanLoop();
    }

    // ── Background thread ────────────────────────────────────────────────────────

    private void StartBackgroundThread()
    {
        _bgThread = new HandlerThread("CameraBackground");
        _bgThread.Start();
        _bgHandler = new global::Android.OS.Handler(_bgThread.Looper!);
        Log.Debug(Tag, "Background thread started");
    }

    private void StopBackgroundThread()
    {
        _bgThread?.QuitSafely();
        try { _bgThread?.Join(); } catch { }
        _bgThread = null;
        _bgHandler = null;
    }

    // ── Abertura da câmera ───────────────────────────────────────────────────────

    internal void OnSurfaceAvailable()
    {
        Log.Debug(Tag, "OnSurfaceAvailable — camera=" + (_camera != null ? "open" : "null"));
        if (_camera is not null)
        {
            var texture = _textureView?.SurfaceTexture;
            if (texture is not null)
                SetupPreviewSession(texture);
            else
                Log.Error(Tag, "SurfaceTexture still null in OnSurfaceAvailable!");
        }
        else
        {
            TryOpenCamera();
        }
    }

    public void TryOpenCamera()
    {
        Log.Debug(Tag, "TryOpenCamera");
        if (_cameraOpenRequested) return;
        _cameraOpenRequested = true;
        OpenCamera();
    }

    private void OpenCamera()
    {
        if (Context is null) return;

#pragma warning disable CA1416
        var permission = Context!.CheckSelfPermission(global::Android.Manifest.Permission.Camera);
#pragma warning restore CA1416
        Log.Debug(Tag, "Camera permission: " + permission);
        if (permission != global::Android.Content.PM.Permission.Granted)
        {
            Log.Warn(Tag, "Camera permission NOT granted");
            _cameraOpenRequested = false;
            return;
        }

        try
        {
            var manager = (CameraManager)Context.GetSystemService(global::Android.Content.Context.CameraService)!;
            var ids = manager.GetCameraIdList();
            Log.Debug(Tag, $"Found {ids?.Length ?? 0} cameras");

            string? cameraId = null;
            foreach (var id in ids ?? [])
            {
                var chars = manager.GetCameraCharacteristics(id);
                var facing = chars.Get(CameraCharacteristics.LensFacing);
                if (facing is Java.Lang.Integer i && i.IntValue() == 1)
                {
                    cameraId = id;
                    var ori = chars.Get(CameraCharacteristics.SensorOrientation) as Java.Lang.Integer;
                    _sensorOrientation = ori?.IntValue() ?? 90;
                    Log.Debug(Tag, $"Back camera id={cameraId} sensorOrientation={_sensorOrientation}");
                    break;
                }
            }
            cameraId ??= ids?.FirstOrDefault();
            if (cameraId is null) { Log.Error(Tag, "No camera found"); return; }

            Log.Debug(Tag, $"Opening camera {cameraId}");
            manager.OpenCamera(cameraId, new CameraStateCallback(this), _bgHandler);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"OpenCamera exception: {ex.GetType().Name} — {ex.Message}");
            _cameraOpenRequested = false;
        }
    }

    // ── Callbacks ────────────────────────────────────────────────────────────────

    internal void OnCameraOpened(CameraDevice camera)
    {
        Log.Debug(Tag, "OnCameraOpened");
        _camera = camera;
        var texture = _textureView?.SurfaceTexture;
        if (texture is null)
        {
            Log.Warn(Tag, "SurfaceTexture null — aguardando OnSurfaceTextureAvailable");
            return;
        }
        SetupPreviewSession(texture);
    }

    private void SetupPreviewSession(SurfaceTexture texture)
    {
        if (_camera is null) return;

        texture.SetDefaultBufferSize(BufW, BufH);
        var previewSurface = new Surface(texture);

        // ImageReader: segundo output para OCR — acesso direto aos pixels do sensor
        _imageReader?.Close();
        _imageReader = ImageReader.NewInstance(BufW, BufH, ImageFormatType.Yuv420888, 2);
        _analysisSurface = _imageReader.Surface;

        Log.Debug(Tag, $"CreateCaptureSession preview+analysis {BufW}x{BufH}");
        try
        {
            _camera.CreateCaptureSession(
                new List<Surface> { previewSurface, _analysisSurface! },
                new SessionStateCallback(this, previewSurface),
                _bgHandler);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"CreateCaptureSession exception: {ex.Message}");
        }
    }

    internal void OnSessionReady(CameraCaptureSession session, Surface previewSurface)
    {
        Log.Debug(Tag, "OnSessionReady");
        _session = session;
        try
        {
            var req = _camera!.CreateCaptureRequest(CameraTemplate.Preview);
            req.AddTarget(previewSurface);
            req.AddTarget(_analysisSurface!);
            req.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
            req.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);
            session.SetRepeatingRequest(req.Build(), null, _bgHandler);
            Log.Debug(Tag, "SetRepeatingRequest OK");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"SetRepeatingRequest exception: {ex.Message}");
            return;
        }
        StartScanLoop();
    }

    // ── Loop de OCR (via ImageReader) ─────────────────────────────────────────────

    private void StartScanLoop()
    {
        if (_scanCts is not null) return;
        Log.Debug(Tag, "StartScanLoop");
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
        using var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
        Log.Debug(Tag, "ScanLoop started");

        while (!ct.IsCancellationRequested)
        {
            global::Android.Media.Image? mediaImage = null;
            try
            {
                await Task.Delay(900, ct).ConfigureAwait(false);
                if (!_scanEnabled || ct.IsCancellationRequested) continue;

                mediaImage = _imageReader?.AcquireLatestImage();
                if (mediaImage is null) { Log.Debug(Tag, "ImageReader: no frame yet"); continue; }

                Log.Debug(Tag, $"OCR image {mediaImage.Width}x{mediaImage.Height} rot={_sensorOrientation}");

                string? text = null;

                // Passe 1: crop no canto inferior-direito do sensor raw = inferior-esquerdo da carta em portrait
                // sensorOrientation=90: portrait bottom → raw right (x alto); portrait left → raw bottom (y alto)
                try
                {
                    int fw = mediaImage.Width, fh = mediaImage.Height;
                    var nv21 = Yuv420888ToNv21(mediaImage);
                    var yuvImg = new YuvImage(nv21, ImageFormatType.Nv21, fw, fh, null);
                    int cx = (int)(fw * 0.68f), cy = (int)(fh * 0.40f);
                    using var ms = new MemoryStream();
                    yuvImg.CompressToJpeg(new global::Android.Graphics.Rect(cx, cy, fw, fh), 95, ms);
                    var jpegBytes = ms.ToArray();
                    using var bmp = BitmapFactory.DecodeByteArray(jpegBytes, 0, jpegBytes.Length);
                    if (bmp is not null)
                    {
                        using var cropInput = InputImage.FromBitmap(bmp, _sensorOrientation);
                        var r = await recognizer.Process(cropInput).AsAsync<Text>().ConfigureAwait(false);
                        text = r?.GetText();
                        if (!string.IsNullOrWhiteSpace(text))
                            Log.Debug(Tag, $"OCR crop: {text.Replace('\n', ' ')[..Math.Min(300, text.Length)]}");
                    }
                }
                catch (Exception ex) { Log.Warn(Tag, $"Crop OCR: {ex.Message}"); }

                // Passe 2: imagem completa se o crop não encontrou set code
                if (!SetCodeRegex().IsMatch(text ?? ""))
                {
                    using var fullInput = InputImage.FromMediaImage(mediaImage, _sensorOrientation);
                    var fr = await recognizer.Process(fullInput).AsAsync<Text>().ConfigureAwait(false);
                    var ft = fr?.GetText();
                    if (!string.IsNullOrWhiteSpace(ft))
                    {
                        Log.Debug(Tag, $"OCR full: {ft.Replace('\n', ' ')[..Math.Min(300, ft.Length)]}");
                        text = ft; // sempre usa o full — serve tanto para set code quanto para nome
                    }
                }

                var match = SetCodeRegex().Match(text ?? "");
                if (match.Success)
                {
                    var setCode = NormalizeSetCode(match.Value);
                    Log.Debug(Tag, $"Set code found: {match.Value} → normalized: {setCode}");
                    MainThread.BeginInvokeOnMainThread(() => VirtualView?.RaiseSetCodeDetected(setCode));
                    await Task.Delay(3000, ct).ConfigureAwait(false);
                }
                else if (!string.IsNullOrWhiteSpace(text))
                {
                    // Fallback: extrai nome da carta (palavras ALL-CAPS no início do texto OCR)
                    var nameMatch = CardNameStartRegex().Match(text);
                    if (nameMatch.Success && nameMatch.Value.Contains(' '))
                    {
                        var name = nameMatch.Value.Trim();
                        Log.Debug(Tag, $"Card name detected: {name}");
                        MainThread.BeginInvokeOnMainThread(() => VirtualView?.RaiseCardNameDetected(name));
                        await Task.Delay(3000, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (System.OperationCanceledException) { break; }
            catch (Exception ex) { Log.Error(Tag, $"ScanLoop error: {ex.GetType().Name} — {ex.Message}"); }
            finally { mediaImage?.Close(); }
        }
        Log.Debug(Tag, "ScanLoop ended");
    }

    // Normaliza o set code: troca idioma por EN e corrige O→0 no sufixo numérico (ruído OCR)
    private static string NormalizeSetCode(string raw)
    {
        var dash = raw.IndexOf('-');
        if (dash < 0 || dash + 3 > raw.Length) return raw;
        var prefix = raw[..dash];
        var num    = raw[(dash + 3)..].Replace('O', '0');
        return $"{prefix}-EN{num}";
    }

    // Converte YUV_420_888 (Camera2) para NV21 (YuvImage) copiando os planos corretamente
    private static byte[] Yuv420888ToNv21(global::Android.Media.Image image)
    {
        int w = image.Width, h = image.Height;
        var planes = image.GetPlanes();
        var nv21 = new byte[w * h * 3 / 2];

        var yBuf = planes[0].Buffer!;
        int yStride = planes[0].RowStride;
        for (int row = 0; row < h; row++)
        {
            yBuf.Position(row * yStride);
            yBuf.Get(nv21, row * w, w);
        }

        var vBuf = planes[2].Buffer!;
        var uBuf = planes[1].Buffer!;
        int uvStride   = planes[2].RowStride;
        int uvPixStride = planes[2].PixelStride;
        int uvBase = w * h;

        if (uvPixStride == 2)
        {
            // Semi-planar (comum em Android): plano V já contém VU intercalado → cópia direta por linha
            // Math.Min protege contra o quirk do último chunk ter 1 byte a menos
            for (int row = 0; row < h / 2; row++)
            {
                vBuf.Position(row * uvStride);
                int toRead = Math.Min(w, vBuf.Remaining());
                vBuf.Get(nv21, uvBase + row * w, toRead);
            }
        }
        else
        {
            // Planar: intercala V e U byte a byte
            for (int row = 0; row < h / 2; row++)
            {
                for (int col = 0; col < w / 2; col++)
                {
                    int src = row * uvStride + col * uvPixStride;
                    int dst = uvBase + row * w + col * 2;
                    nv21[dst]     = (byte)vBuf.Get(src);
                    nv21[dst + 1] = (byte)uBuf.Get(src);
                }
            }
        }
        return nv21;
    }

    // ── Fechamento ───────────────────────────────────────────────────────────────

    private void CloseCamera()
    {
        Log.Debug(Tag, "CloseCamera");
        _cameraOpenRequested = false;
        try { _session?.StopRepeating(); } catch { }
        _session?.Close();
        _session = null;
        _camera?.Close();
        _camera = null;
        _imageReader?.Close();
        _imageReader = null;
        _analysisSurface = null;
    }
}

// ── State callbacks ───────────────────────────────────────────────────────────

internal class CameraStateCallback(CameraPreviewHandler handler) : CameraDevice.StateCallback
{
    public override void OnOpened(CameraDevice camera) => handler.OnCameraOpened(camera);
    public override void OnDisconnected(CameraDevice camera) { Log.Warn("DuelScan", "Camera disconnected"); camera.Close(); }
    public override void OnError(CameraDevice camera, CameraError error) { Log.Error("DuelScan", $"Camera error: {error}"); camera.Close(); }
}

internal class SessionStateCallback(CameraPreviewHandler handler, Surface previewSurface)
    : CameraCaptureSession.StateCallback
{
    public override void OnConfigured(CameraCaptureSession session) => handler.OnSessionReady(session, previewSurface);
    public override void OnConfigureFailed(CameraCaptureSession session) => Log.Error("DuelScan", "CaptureSession configure FAILED");
}

internal class SurfaceListener(CameraPreviewHandler handler)
    : Java.Lang.Object, TextureView.ISurfaceTextureListener
{
    public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
    {
        Log.Debug("DuelScan", $"OnSurfaceTextureAvailable {width}x{height}");
        handler.OnSurfaceAvailable();
    }
    public bool OnSurfaceTextureDestroyed(SurfaceTexture surface) { Log.Debug("DuelScan", "OnSurfaceTextureDestroyed"); return true; }
    public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) { }
    public void OnSurfaceTextureUpdated(SurfaceTexture surface) { }
}
