namespace Phoodab.Application;

public sealed record ReplenishmentSuggestionReadModel(
    Guid ItemDefinitionId,
    string ItemName,
    decimal CurrentQuantity,
    decimal DesiredQuantity,
    decimal RequiredAmount,
    string Unit,
    IReadOnlyList<InventoryLotReadModel> Lots);
