namespace Phoodab.Application;

public sealed record ReplenishmentSuggestionReadModel(
    Guid ItemDefinitionId,
    string ItemName,
    decimal CurrentQuantity,
    decimal UsableCurrentQuantity,
    decimal DesiredQuantity,
    decimal DeficitAmount,
    decimal ExpiringSoonAmount,
    decimal SuggestedPurchaseAmount,
    decimal RequiredAmount,
    string Unit,
    IReadOnlyList<ConsumableEntryReadModel> Entries);
