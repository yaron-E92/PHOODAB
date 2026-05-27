namespace Phoodab.Domain;

public sealed class DurableEntry : InventoryEntry
{
    public override ItemKind Kind => ItemKind.Durable;

    public DurableEntry(Guid id, ItemDefinition itemDefinition, Guid? storageSlotId = null)
        : base(id, itemDefinition, storageSlotId)
    {
        if (itemDefinition.Kind != ItemKind.Durable)
        {
            throw new InvalidOperationException("Durable entries require a durable item definition.");
        }
    }
}
