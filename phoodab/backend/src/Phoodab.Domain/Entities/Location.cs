namespace Phoodab.Domain;

public enum LocationType
{
    House = 0,
    Room = 1,
    StorageUnit = 2,
    StorageSlot = 3
}

public sealed class Location
{
    public Guid Id { get; }
    public string Name { get; }
    public LocationType Type { get; }
    public Guid? ParentLocationId { get; }
    public string? Description { get; }
    public int? SortOrder { get; }
    public bool IsArchived { get; }

    public Location(
        Guid id,
        string name,
        LocationType type,
        Guid? parentLocationId = null,
        LocationType? parentType = null,
        string? description = null,
        int? sortOrder = null,
        bool isArchived = false)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Location name is required.", nameof(name));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException("Unsupported location type.", nameof(type));
        }

        if (parentLocationId == Guid.Empty)
        {
            throw new ArgumentException("Parent location id cannot be empty when provided.", nameof(parentLocationId));
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        ValidateHierarchy(type, parentLocationId, parentType);

        Id = id;
        Name = name.Trim();
        Type = type;
        ParentLocationId = parentLocationId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SortOrder = sortOrder;
        IsArchived = isArchived;
    }

    public static void ValidateHierarchy(LocationType type, Guid? parentLocationId, LocationType? parentType)
    {
        if (type == LocationType.House)
        {
            if (parentLocationId is not null || parentType is not null)
            {
                throw new InvalidOperationException("House locations cannot have a parent.");
            }

            return;
        }

        if (parentLocationId is null || parentType is null)
        {
            throw new InvalidOperationException($"{type} locations require a parent.");
        }

        var expectedParent = type switch
        {
            LocationType.Room => LocationType.House,
            LocationType.StorageUnit => LocationType.Room,
            LocationType.StorageSlot => LocationType.StorageUnit,
            _ => throw new ArgumentException("Unsupported location type.", nameof(type))
        };

        if (parentType != expectedParent)
        {
            throw new InvalidOperationException($"{type} locations must have a {expectedParent} parent.");
        }
    }
}
