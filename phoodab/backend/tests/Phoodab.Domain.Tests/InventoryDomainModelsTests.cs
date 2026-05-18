using Phoodab.Domain;

namespace Phoodab.Domain.Tests;

public class InventoryDomainModelsTests
{
    [Test]
    public void Durable_item_cannot_accept_lots()
    {
        var item = new ItemDefinition(Guid.NewGuid(), "Broom", ItemKind.Durable);
        var entry = new InventoryEntry(Guid.NewGuid(), item);
        var lot = new InventoryLot(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("piece"), DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Throws<InvalidOperationException>(() => entry.AddLot(lot));
    }

    [Test]
    public void Consumable_item_can_have_multiple_lots()
    {
        var item = new ItemDefinition(Guid.NewGuid(), "Milk", ItemKind.Consumable);
        var entry = new InventoryEntry(Guid.NewGuid(), item);

        entry.AddLot(new InventoryLot(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("liter"), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2)));
        entry.AddLot(new InventoryLot(Guid.NewGuid(), item.Id, Quantity.From(2), new Unit("liter"), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(4)));

        Assert.That(entry.Lots, Has.Count.EqualTo(2));
    }

    [Test]
    public void Quantity_and_unit_are_required()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Quantity.From(-1));
        Assert.Throws<ArgumentException>(() => new Unit("  "));
    }

    [Test]
    public void Required_amount_and_expiry_warning_are_computed_from_current_values()
    {
        var itemId = Guid.NewGuid();
        var rule = new ReplenishmentRule(Guid.NewGuid(), itemId, Quantity.From(10), new Unit("liter"), expiryWarningDays: 3);

        var required = rule.GetRequiredAmount(Quantity.From(7));
        var warning = rule.IsExpiryWarning(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2), DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.That(required.Value, Is.EqualTo(3));
        Assert.That(warning, Is.True);
    }

    [Test]
    public void Structural_entities_keep_basic_relationship_shape()
    {
        var home = new Home(Guid.NewGuid());
        var room = new Room(Guid.NewGuid(), home.Id);
        var storageUnit = new StorageUnit(Guid.NewGuid(), room.Id);
        var storageSlot = new StorageSlot(Guid.NewGuid(), storageUnit.Id);
        var shoppingList = new ShoppingList(Guid.NewGuid(), home.Id);

        Assert.Multiple(() =>
        {
            Assert.That(room.HomeId, Is.EqualTo(home.Id));
            Assert.That(storageUnit.RoomId, Is.EqualTo(room.Id));
            Assert.That(storageSlot.StorageUnitId, Is.EqualTo(storageUnit.Id));
            Assert.That(shoppingList.HomeId, Is.EqualTo(home.Id));
        });
    }
}
