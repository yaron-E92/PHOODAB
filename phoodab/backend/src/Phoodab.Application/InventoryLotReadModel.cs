namespace Phoodab.Application;

public sealed record InventoryLotReadModel(
    Guid LotId,
    decimal Quantity,
    string Unit,
    DateOnly? ExpiresOn,
    int? ExpiresInDays,
    string ExpiryStatus);
