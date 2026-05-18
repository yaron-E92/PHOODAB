namespace Phoodab.Domain;

public enum ItemKind
{
    Durable = 0,
    Consumable = 1
}

public readonly record struct Quantity(decimal Value)
{
    public static Quantity From(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Quantity cannot be negative.");
        }

        return new Quantity(value);
    }
}

public readonly record struct Unit
{
    public string Value { get; }

    public Unit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Unit is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

public sealed class ItemDefinition
{
    public Guid Id { get; }
    public string Name { get; }
    public ItemKind Kind { get; }

    public ItemDefinition(Guid id, string name, ItemKind kind)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        Kind = kind;
    }
}

public sealed class InventoryEntry
{
    public Guid Id { get; }
    public Guid ItemDefinitionId { get; }
    public ItemDefinition ItemDefinition { get; }
    public IReadOnlyCollection<InventoryLot> Lots => _lots.AsReadOnly();

    private readonly List<InventoryLot> _lots = new();

    public InventoryEntry(Guid id, ItemDefinition itemDefinition)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        ItemDefinition = itemDefinition ?? throw new ArgumentNullException(nameof(itemDefinition));
        Id = id;
        ItemDefinitionId = itemDefinition.Id;
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

public sealed class ReplenishmentRule
{
    public Guid Id { get; }
    public Guid ItemDefinitionId { get; }
    public Quantity TargetAmount { get; }
    public Unit Unit { get; }
    public int ExpiryWarningDays { get; }

    public ReplenishmentRule(Guid id, Guid itemDefinitionId, Quantity targetAmount, Unit unit, int expiryWarningDays = 0)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (itemDefinitionId == Guid.Empty)
        {
            throw new ArgumentException("Item definition id is required.", nameof(itemDefinitionId));
        }

        if (expiryWarningDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expiryWarningDays), "Expiry warning days cannot be negative.");
        }

        Id = id;
        ItemDefinitionId = itemDefinitionId;
        TargetAmount = targetAmount;
        Unit = unit;
        ExpiryWarningDays = expiryWarningDays;
    }

    public Quantity GetRequiredAmount(Quantity currentAmount)
    {
        var required = TargetAmount.Value - currentAmount.Value;
        return Quantity.From(required <= 0 ? 0 : required);
    }

    public bool IsExpiryWarning(DateOnly? expiresOn, DateOnly today)
    {
        if (expiresOn is null)
        {
            return false;
        }

        return expiresOn.Value.DayNumber - today.DayNumber <= ExpiryWarningDays;
    }
}

public sealed record ShoppingListItem(Guid ShoppingListId, Guid ItemDefinitionId, Quantity Quantity, Unit Unit);

public sealed class Home
{
    public Guid Id { get; }

    public Home(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        Id = id;
    }
}

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

public sealed class ShoppingList
{
    public Guid Id { get; }
    public Guid HomeId { get; }

    public ShoppingList(Guid id, Guid homeId)
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
