namespace Phoodab.Domain;

public abstract class InventoryEntry
{
    public Guid Id { get; }
    public Guid ItemDefinitionId { get; }
    public ItemDefinition ItemDefinition { get; }
    public Guid? StorageSlotId { get; }
    public Guid? LocationId => StorageSlotId;
    public abstract ItemKind Kind { get; }

    protected InventoryEntry(Guid id, ItemDefinition itemDefinition, Guid? storageSlotId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (storageSlotId == Guid.Empty)
        {
            throw new ArgumentException("StorageSlotId cannot be empty when provided.", nameof(storageSlotId));
        }

        ItemDefinition = itemDefinition ?? throw new ArgumentNullException(nameof(itemDefinition));
        Id = id;
        ItemDefinitionId = itemDefinition.Id;
        StorageSlotId = storageSlotId;
    }
}
