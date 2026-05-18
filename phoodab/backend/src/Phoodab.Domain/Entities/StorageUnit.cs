namespace Phoodab.Domain;

public sealed class StorageUnit
{
    public Guid Id { get; }
    public Guid RoomId { get; }

    public StorageUnit(Guid id, Guid roomId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("RoomId is required.", nameof(roomId));
        }

        Id = id;
        RoomId = roomId;
    }
}
