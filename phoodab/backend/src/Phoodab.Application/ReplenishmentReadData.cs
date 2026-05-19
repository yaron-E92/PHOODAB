using Phoodab.Domain;

namespace Phoodab.Application;

public interface IReplenishmentReadData
{
    IReadOnlyList<ReplenishmentRule> GetRules();
    IReadOnlyList<InventoryEntry> GetInventoryEntries();
}

public sealed class InMemoryReplenishmentReadData : IReplenishmentReadData
{
    public IReadOnlyList<ReplenishmentRule> GetRules()
    {
        var milkId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var beansId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var riceId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        return new List<ReplenishmentRule>
        {
            new(Guid.NewGuid(), milkId, Quantity.From(2), new Unit("liter")),
            new(Guid.NewGuid(), beansId, Quantity.From(2), new Unit("can")),
            new(Guid.NewGuid(), riceId, Quantity.From(4), new Unit("kg")),
            new(Guid.NewGuid(), Guid.NewGuid(), Quantity.From(5), new Unit("pack"), isHidden: true),
            new(Guid.NewGuid(), Guid.NewGuid(), Quantity.From(5), new Unit("pack"), isDisabled: true)
        };
    }

    public IReadOnlyList<InventoryEntry> GetInventoryEntries()
    {
        var milk = new ItemDefinition(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Milk", ItemKind.Consumable);
        var beans = new ItemDefinition(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Beans", ItemKind.Consumable);
        var rice = new ItemDefinition(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Rice", ItemKind.Consumable);

        var milkEntry = new InventoryEntry(Guid.NewGuid(), milk);
        milkEntry.AddLot(new InventoryLot(Guid.NewGuid(), milk.Id, Quantity.From(1), new Unit("liter"), null));

        var beansEntry = new InventoryEntry(Guid.NewGuid(), beans);
        beansEntry.AddLot(new InventoryLot(Guid.NewGuid(), beans.Id, Quantity.From(2), new Unit("can"), null));

        var riceEntry = new InventoryEntry(Guid.NewGuid(), rice);
        riceEntry.AddLot(new InventoryLot(Guid.NewGuid(), rice.Id, Quantity.From(1), new Unit("kg"), null));
        riceEntry.AddLot(new InventoryLot(Guid.NewGuid(), rice.Id, Quantity.From(1), new Unit("bag"), null));

        return new List<InventoryEntry> { milkEntry, beansEntry, riceEntry };
    }
}
