namespace Phoodab.Domain;

public sealed class InventoryLot
{
    public Guid Id { get; }
    public Guid ItemDefinitionId { get; }
    public Guid? StorageSlotId { get; }
    public Quantity Quantity { get; }
    public Unit Unit { get; }
    public DateOnly? ExpiresOn { get; }

    public InventoryLot(Guid id, Guid itemDefinitionId, Quantity quantity, Unit unit, DateOnly? expiresOn, Guid? storageSlotId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (itemDefinitionId == Guid.Empty)
        {
            throw new ArgumentException("Item definition id is required.", nameof(itemDefinitionId));
        }

        if (storageSlotId == Guid.Empty)
        {
            throw new ArgumentException("StorageSlotId cannot be empty when provided.", nameof(storageSlotId));
        }

        Id = id;
        ItemDefinitionId = itemDefinitionId;
        StorageSlotId = storageSlotId;
        Quantity = quantity;
        Unit = unit;
        ExpiresOn = expiresOn;
    }
}
