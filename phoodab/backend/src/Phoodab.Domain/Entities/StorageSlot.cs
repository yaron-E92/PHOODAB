namespace Phoodab.Domain;

public sealed class StorageSlot
{
    public Guid Id { get; }
    public Guid StorageUnitId { get; }

    public StorageSlot(Guid id, Guid storageUnitId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (storageUnitId == Guid.Empty)
        {
            throw new ArgumentException("StorageUnitId is required.", nameof(storageUnitId));
        }

        Id = id;
        StorageUnitId = storageUnitId;
    }
}
