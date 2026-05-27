namespace Phoodab.Domain;

public sealed class ConsumableEntry : InventoryEntry
{
    public override ItemKind Kind => ItemKind.Consumable;
    public Quantity Quantity { get; }
    public Unit Unit { get; }
    public DateOnly? ExpiresOn { get; }

    public ConsumableEntry(Guid id, ItemDefinition itemDefinition, Quantity quantity, Unit unit, DateOnly? expiresOn, Guid? storageSlotId = null)
        : base(id, itemDefinition, storageSlotId)
    {
        if (itemDefinition.Kind != ItemKind.Consumable)
        {
            throw new InvalidOperationException("Consumable entries require a consumable item definition.");
        }

        Quantity = quantity;
        Unit = unit;
        ExpiresOn = expiresOn;
    }
}
