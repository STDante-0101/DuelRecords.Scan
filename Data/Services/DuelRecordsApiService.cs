using DuelRecords.Scan.Data.Models;
using System.Net.Http.Json;

namespace DuelRecords.Scan.Data.Services;

public class DuelRecordsApiService(HttpClient httpClient)
{
    public async Task<List<Card>> GetCardsAsync()
        => await httpClient.GetFromJsonAsync<List<Card>>("api/cards") ?? [];

    public async Task<List<YgoCardSuggestion>> SearchCardsAsync(string nome)
        => await httpClient.GetFromJsonAsync<List<YgoCardSuggestion>>(
               $"api/ygo/cards/buscar?nome={Uri.EscapeDataString(nome)}") ?? [];

    public async Task<YgoCardDetails?> GetCardDetailsAsync(int ygoId)
        => await httpClient.GetFromJsonAsync<YgoCardDetails>($"api/ygo/cards/{ygoId}");

    public async Task<Card?> AddToCollectionAsync(int ygoId)
    {
        var response = await httpClient.PostAsJsonAsync($"api/scan/cards/from-ygo/{ygoId}", new { });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Card>()
            : null;
    }
}
