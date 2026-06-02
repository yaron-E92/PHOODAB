namespace Phoodab.Domain;

public enum DurableItemStatus
{
    Active = 0,
    NeedsRepair = 1,
    LoanedOut = 2,
    Stored = 3,
    Retired = 4,
    Lost = 5
}

public sealed class DurableEntry : InventoryEntry
{
    public override ItemKind Kind => ItemKind.Durable;
    public string DisplayName => ItemDefinition.Name;
    public string? Description { get; }
    public string? ItemType { get; }
    public string? BrandManufacturer { get; }
    public string? Model { get; }
    public string? SerialNumber { get; }
    public DateOnly? PurchaseDate { get; }
    public decimal? PurchaseValue { get; }
    public DateOnly? WarrantyEndsOn { get; }
    public DurableItemStatus Status { get; }
    public string? CurrentLocation { get; }
    public string? Notes { get; }

    public DurableEntry(
        Guid id,
        ItemDefinition itemDefinition,
        Guid? storageSlotId = null,
        string? description = null,
        string? itemType = null,
        string? brandManufacturer = null,
        string? model = null,
        string? serialNumber = null,
        DateOnly? purchaseDate = null,
        decimal? purchaseValue = null,
        DateOnly? warrantyEndsOn = null,
        DurableItemStatus status = DurableItemStatus.Active,
        string? currentLocation = null,
        string? notes = null)
        : base(id, itemDefinition, storageSlotId)
    {
        if (itemDefinition.Kind != ItemKind.Durable)
        {
            throw new InvalidOperationException("Durable entries require a durable item definition.");
        }

        if (purchaseValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(purchaseValue), "Purchase value cannot be negative.");
        }

        Description = NormalizeOptionalText(description);
        ItemType = NormalizeOptionalText(itemType);
        BrandManufacturer = NormalizeOptionalText(brandManufacturer);
        Model = NormalizeOptionalText(model);
        SerialNumber = NormalizeOptionalText(serialNumber);
        PurchaseDate = purchaseDate;
        PurchaseValue = purchaseValue;
        WarrantyEndsOn = warrantyEndsOn;
        Status = status;
        CurrentLocation = NormalizeOptionalText(currentLocation);
        Notes = NormalizeOptionalText(notes);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
