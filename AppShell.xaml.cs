using DuelRecords.Scan.Pages;

namespace DuelRecords.Scan;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(ScanPage), typeof(ScanPage));
	}
}
