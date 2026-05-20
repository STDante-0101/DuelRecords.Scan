using Android.App;
using Android.Runtime;

namespace DuelRecords.Scan;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		// Captura exceções Java/Android ANTES do .NET inicializar
		AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
		{
			args.Handled = true;
			EscreverLog($"CRASH JAVA: {args.Exception}");
		};

		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
			EscreverLog($"CRASH DOTNET: {args.ExceptionObject}");
	}

	protected override MauiApp CreateMauiApp()
	{
		EscreverLog("CreateMauiApp: iniciando...");
		try
		{
			var app = MauiProgram.CreateMauiApp();
			EscreverLog("CreateMauiApp: OK");
			return app;
		}
		catch (Exception ex)
		{
			EscreverLog($"CreateMauiApp FALHOU: {ex}");
			throw;
		}
	}

	// Grava em /sdcard/duelrecords_log.txt — legível pelo app Meus Arquivos
	private static void EscreverLog(string mensagem)
	{
		try
		{
			var caminho = Path.Combine(
				Android.OS.Environment.ExternalStorageDirectory!.AbsolutePath,
				"duelrecords_log.txt");
			File.AppendAllText(caminho, $"{DateTime.Now:HH:mm:ss.fff} {mensagem}{Environment.NewLine}");
		}
		catch { /* se falhar, ignora — não queremos crash dentro do handler de crash */ }
	}
}
