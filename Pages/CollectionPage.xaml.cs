using DuelRecords.Scan.ViewModels;

namespace DuelRecords.Scan.Pages;

public partial class CollectionPage : ContentPage
{
    public CollectionPage(CollectionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CollectionViewModel vm)
            vm.LoadCardsCommand.Execute(null);
    }
}
