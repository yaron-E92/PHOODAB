namespace Phoodab.Domain;

public sealed class InventoryEntry
{
    public Guid Id { get; }
    public Guid ItemDefinitionId { get; }
    public ItemDefinition ItemDefinition { get; }
    public Guid? StorageSlotId { get; }
    public IReadOnlyCollection<InventoryLot> Lots => _lots.AsReadOnly();

    private readonly List<InventoryLot> _lots = new();

    public InventoryEntry(Guid id, ItemDefinition itemDefinition, Guid? storageSlotId = null)
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

    public void AddLot(InventoryLot lot)
    {
        ArgumentNullException.ThrowIfNull(lot);

        if (ItemDefinition.Kind != ItemKind.Consumable)
        {
            throw new InvalidOperationException("Durable items cannot have consumable lots.");
        }

        if (lot.ItemDefinitionId != ItemDefinitionId)
        {
            throw new InvalidOperationException("Lot item definition must match the inventory entry item definition.");
        }

        _lots.Add(lot);
    }
}
