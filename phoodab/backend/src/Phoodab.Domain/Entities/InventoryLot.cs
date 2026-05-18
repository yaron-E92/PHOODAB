namespace Phoodab.Domain;

public sealed class InventoryLot
{
    public Guid Id { get; }
    public Guid ItemDefinitionId { get; }
    public Quantity Quantity { get; }
    public Unit Unit { get; }
    public DateOnly? ExpiresOn { get; }

    public InventoryLot(Guid id, Guid itemDefinitionId, Quantity quantity, Unit unit, DateOnly? expiresOn)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (itemDefinitionId == Guid.Empty)
        {
            throw new ArgumentException("Item definition id is required.", nameof(itemDefinitionId));
        }

        Id = id;
        ItemDefinitionId = itemDefinitionId;
        Quantity = quantity;
        Unit = unit;
        ExpiresOn = expiresOn;
    }
}
