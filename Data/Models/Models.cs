namespace DuelRecords.Scan.Data.Models;

public record Card(
    int Id,
    string Nome,
    string Tipo,
    string? Atributo,
    int? Nivel,
    int? Ataque,
    int? Defesa,
    string TipoDeck,
    string Raridade,
    int Quantidade,
    string Colecao,
    string? Descricao,
    string? ImagemUrl,
    string DataCadastro
);

public record YgoCardSuggestion(
    int YgoId,
    string Name,
    string? Type,
    string? Attribute,
    int? Level,
    int? Attack,
    int? Defense,
    string? ImageUrlSmall,
    string? SetCode = null,
    string? SetRarity = null
);

public record YgoCardDetails(
    int YgoId,
    string Name,
    string Type,
    string? FrameType,
    string? Description,
    string? Race,
    string? Attribute,
    string? Archetype,
    int? Attack,
    int? Defense,
    int? Level,
    string? HumanReadableCardType,
    string? ImageUrl,
    string? ImageUrlSmall,
    string? ImageUrlCropped
);
