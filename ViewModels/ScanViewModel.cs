using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuelRecords.Scan.Data.Models;
using DuelRecords.Scan.Data.Services;

namespace DuelRecords.Scan.ViewModels;

public partial class ScanViewModel(DuelRecordsApiService api) : ObservableObject
{
    private string? _failedSetCode; // set code que já retornou 400 — pula na próxima detecção

    // ── Estado da câmera / scan ──────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowResumeButton))]
    public partial bool ScanEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Aponte a câmera para o código set da carta.";

    // ── Código detectado ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSetCode))]
    public partial string? DetectedSetCode { get; set; }

    public bool HasSetCode => DetectedSetCode is not null;

    // ── Resultado da busca ───────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFoundCard))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial YgoCardSuggestion? FoundCard { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial bool IsLookingUp { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial bool IsAdding { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSuccess))]
    public partial string? SuccessMessage { get; set; }

    public bool HasFoundCard    => FoundCard is not null;
    public bool CanConfirm      => HasFoundCard && !IsAdding && !IsLookingUp;
    public bool ShowResumeButton => !ScanEnabled && !IsLookingUp && !IsAdding;
    public bool ShowSuccess      => SuccessMessage is not null;

    // ── Chamado pelo code-behind quando a câmera detecta um nome de carta ───

    public async Task CardNameDetectedAsync(string cardName)
    {
        if (cardName == DetectedSetCode && (IsLookingUp || HasFoundCard)) return;

        ScanEnabled = false;
        DetectedSetCode = cardName;
        FoundCard = null;
        SuccessMessage = null;
        StatusMessage = $"Carta detectada: {cardName}";

        try
        {
            IsLookingUp = true;
            StatusMessage = $"Buscando \"{cardName}\"...";

            var results = await api.SearchCardsAsync(cardName);
            var card = results.FirstOrDefault();

            if (card is null)
            {
                StatusMessage = $"Carta não encontrada para \"{cardName}\".";
                ScanEnabled = true;
                return;
            }

            FoundCard = card;
            StatusMessage = "Carta encontrada! Confirme para adicionar à coleção.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro na busca: {ex.Message}";
            ScanEnabled = true;
        }
        finally
        {
            IsLookingUp = false;
        }
    }

    // ── Chamado pelo code-behind quando a câmera detecta um código ───────────

    public async Task SetCodeDetectedAsync(string setCode)
    {
        if (setCode == _failedSetCode) return; // já tentou esse código e falhou — deixa detectar por nome
        if (setCode == DetectedSetCode && (IsLookingUp || HasFoundCard)) return;

        ScanEnabled = false;
        DetectedSetCode = setCode;
        FoundCard = null;
        SuccessMessage = null;
        StatusMessage = $"Código detectado: {setCode}";

        try
        {
            IsLookingUp = true;
            StatusMessage = $"Buscando carta para {setCode}...";

            var card = await api.BuscarPorSetCodeAsync(setCode);

            if (card is null)
            {
                _failedSetCode = setCode; // marca para não tentar de novo
                StatusMessage = $"Código {setCode} não encontrado — buscando por nome...";
                ScanEnabled = true;
                return;
            }

            _failedSetCode = null;
            FoundCard = card;
            StatusMessage = "Carta encontrada! Confirme para adicionar à coleção.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro na busca: {ex.Message}";
            ScanEnabled = true;
        }
        finally
        {
            IsLookingUp = false;
        }
    }

    // ── Confirmar adição à coleção ───────────────────────────────────────────

    [RelayCommand]
    private async Task ConfirmAddAsync()
    {
        if (FoundCard is null) return;

        try
        {
            IsAdding = true;
            StatusMessage = "Adicionando à coleção...";

            var added = await api.AddToCollectionAsync(FoundCard.YgoId, FoundCard.SetCode, FoundCard.SetRarity);
            if (added is null)
            {
                StatusMessage = "Erro ao adicionar. Tente novamente.";
                return;
            }

            SuccessMessage = $"{added.Nome} adicionada ao estoque!";
            StatusMessage = "Aponte para outra carta para continuar.";

            await Task.Delay(1500);
            ResetScanState();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
        }
        finally
        {
            IsAdding = false;
        }
    }

    // ── Voltar a escanear sem confirmar ──────────────────────────────────────

    [RelayCommand]
    private void ResumeScan()
    {
        ResetScanState();
        StatusMessage = "Aponte a câmera para o código set da carta.";
    }

    private void ResetScanState()
    {
        DetectedSetCode = null;
        FoundCard = null;
        SuccessMessage = null;
        _failedSetCode = null;
        ScanEnabled = true;
    }
}
