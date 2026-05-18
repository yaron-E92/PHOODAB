namespace Phoodab.Domain;

public sealed class Room
{
    public Guid Id { get; }
    public Guid HomeId { get; }

    public Room(Guid id, Guid homeId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (homeId == Guid.Empty)
        {
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        }

        Id = id;
        HomeId = homeId;
    }
}
