using DuelRecords.Scan.Data.Models;
using DuelRecords.Scan.ViewModels;

namespace DuelRecords.Scan.Pages;

public partial class CollectionPage : ContentPage
{
    public CollectionPage(CollectionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // Cada toque num item da CollectionView chama SelectCardCommand com a carta tocada
        CardsCollectionView.SelectionChanged += (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is Card card)
                vm.SelectCardCommand.Execute(card);

            // Reseta a seleção visual para permitir tocar na mesma carta novamente
            CardsCollectionView.SelectedItem = null;
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CollectionViewModel vm)
            vm.LoadCardsCommand.Execute(null);
    }
}
