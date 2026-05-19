using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuelRecords.Scan.Data.Models;
using DuelRecords.Scan.Data.Services;
using DuelRecords.Scan.Pages;

namespace DuelRecords.Scan.ViewModels;

public partial class CollectionViewModel(DuelRecordsApiService apiService) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCards))]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowError))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCards))]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    public partial List<Card> Cards { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowError))]
    [NotifyPropertyChangedFor(nameof(ShowCards))]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    public partial string? ErrorMessage { get; set; }

    public bool ShowCards => !IsLoading && ErrorMessage is null && Cards.Count > 0;
    public bool ShowEmpty => !IsLoading && ErrorMessage is null && Cards.Count == 0;
    public bool ShowError => !IsLoading && ErrorMessage is not null;

    [RelayCommand]
    private async Task LoadCardsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Cards = await apiService.GetCardsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao carregar coleção: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private static async Task GoToScanAsync()
        => await Shell.Current.GoToAsync(nameof(ScanPage));
}
