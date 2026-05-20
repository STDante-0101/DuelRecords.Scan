using DuelRecords.Scan.Controls;
using DuelRecords.Scan.Data.Services;
using DuelRecords.Scan.Pages;
using DuelRecords.Scan.ViewModels;
using Microsoft.Extensions.Logging;
#if ANDROID
using DuelRecords.Scan.Platforms.Android;
#endif

namespace DuelRecords.Scan;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureMauiHandlers(handlers =>
			{
#if ANDROID
				handlers.AddHandler<CameraPreviewView, CameraPreviewHandler>();
#endif
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddHttpClient<DuelRecordsApiService>(client =>
		{
			client.BaseAddress = new Uri(ApiConfig.BaseUrl);
			client.Timeout = TimeSpan.FromSeconds(10);
		});

		builder.Services.AddTransient<CollectionViewModel>();
		builder.Services.AddTransient<CollectionPage>();
		builder.Services.AddTransient<ScanPage>();
		builder.Services.AddTransient<ScanViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
