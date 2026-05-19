using DuelRecords.Scan.Data.Services;
using DuelRecords.Scan.Pages;
using DuelRecords.Scan.ViewModels;
using Microsoft.Extensions.Logging;

namespace DuelRecords.Scan;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Emulador Android: 10.0.2.2 = localhost do computador
		// Celular físico na mesma rede: troque pelo IP do seu PC, ex: http://192.168.1.100:5001/
		builder.Services.AddHttpClient<DuelRecordsApiService>(client =>
		{
			client.BaseAddress = new Uri("http://10.0.2.2:5001/");
		});

		builder.Services.AddTransient<CollectionViewModel>();
		builder.Services.AddTransient<CollectionPage>();
		builder.Services.AddTransient<ScanPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
