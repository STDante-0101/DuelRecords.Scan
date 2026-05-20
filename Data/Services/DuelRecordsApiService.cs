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

    public async Task<YgoCardSuggestion?> BuscarPorSetCodeAsync(string setCode)
    {
        // Primeira tentativa com o código normalizado
        var card = await BuscarPorCodigoAsync(setCode);
        if (card is not null) return card;

        // OCR confunde dígito 0 com letra O no prefixo (ex: CHO1 → CH01)
        // Se o prefixo contiver O, tenta novamente com O→0
        var dash = setCode.IndexOf('-');
        if (dash > 0 && setCode[..dash].Contains('O'))
        {
            var prefixCorrigido = setCode[..dash].Replace('O', '0') + setCode[dash..];
            card = await BuscarPorCodigoAsync(prefixCorrigido);
        }
        return card;
    }

    private async Task<YgoCardSuggestion?> BuscarPorCodigoAsync(string codigo)
    {
        var response = await httpClient.GetAsync(
            $"api/ygo/cards/buscar-por-setcode?codigo={Uri.EscapeDataString(codigo)}");
        if (!response.IsSuccessStatusCode) return null;
        var results = await response.Content.ReadFromJsonAsync<List<YgoCardSuggestion>>();
        return results?.FirstOrDefault();
    }

    public async Task<Card?> AddToCollectionAsync(int ygoId, string? setCode = null, string? setRarity = null)
    {
        var details = await GetCardDetailsAsync(ygoId);
        if (details is null) return null;

        var response = await httpClient.PostAsJsonAsync("api/Cards", new
        {
            nome       = details.Name,
            tipo       = MapTipo(details.Type),
            atributo   = MapAtributo(details.Attribute),
            nivel      = details.Level,
            ataque     = details.Attack,
            defesa     = details.Defense,
            tipoDeck   = MapTipoDeck(details.Type),
            raridade   = setRarity,
            quantidade = 1,
            colecao    = setCode,
            descricao  = details.Description,
            imagemUrl  = details.ImageUrl
        });

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Card>()
            : null;
    }

    private static string MapTipo(string? type) => type switch
    {
        "Effect Monster"           => "Efeito",
        "Normal Monster"           => "Monstro",
        "Fusion Monster"           => "Fusão",
        "Synchro Monster"          => "Sincro",
        "Xyz Monster"              => "Xyz",
        "Link Monster"             => "Link",
        "Ritual Monster"           => "Ritual",
        "Pendulum Effect Monster"  => "Pêndulo",
        "Spell Card"               => "Magia",
        "Trap Card"                => "Armadilha",
        _                          => type ?? "Monstro"
    };

    private static string? MapAtributo(string? attr) => attr switch
    {
        "DARK"   => "Trevas",
        "LIGHT"  => "Luz",
        "EARTH"  => "Terra",
        "WATER"  => "Água",
        "FIRE"   => "Fogo",
        "WIND"   => "Vento",
        "DIVINE" => "Divino",
        _        => attr
    };

    private static string MapTipoDeck(string? type) => type switch
    {
        "Fusion Monster"  => "Fusão",
        "Synchro Monster" => "Sincro",
        "Xyz Monster"     => "Xyz",
        "Link Monster"    => "Link",
        _                 => "Main"
    };
}
