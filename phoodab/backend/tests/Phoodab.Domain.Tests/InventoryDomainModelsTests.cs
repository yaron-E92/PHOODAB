using NUnit.Framework;
using System;

namespace Phoodab.Domain.Tests;

public class InventoryDomainModelsTests
{

    [Test]
    public void Durable_and_consumable_entries_can_hold_nullable_storage_slot()
    {
        var slotId = Guid.NewGuid();
        var durable = new ItemDefinition(Guid.NewGuid(), "Vacuum", ItemKind.Durable);
        var consumable = new ItemDefinition(Guid.NewGuid(), "Yogurt", ItemKind.Consumable);

        var durableEntryWithSlot = new DurableEntry(Guid.NewGuid(), durable, slotId);
        var durableEntryWithoutSlot = new DurableEntry(Guid.NewGuid(), durable);
        var consumableEntryWithSlot = new ConsumableEntry(Guid.NewGuid(), consumable, Quantity.From(1), new Unit("cup"), DateOnly.FromDateTime(DateTime.UtcNow), slotId);
        var consumableEntryWithoutSlot = new ConsumableEntry(Guid.NewGuid(), consumable, Quantity.From(1), new Unit("cup"), null);

        Assert.Multiple(() =>
        {
            Assert.That(durableEntryWithSlot.StorageSlotId, Is.EqualTo(slotId));
            Assert.That(durableEntryWithoutSlot.StorageSlotId, Is.Null);
            Assert.That(consumableEntryWithSlot.StorageSlotId, Is.EqualTo(slotId));
            Assert.That(consumableEntryWithoutSlot.StorageSlotId, Is.Null);
        });
    }

    [Test]
    public void Empty_storage_slot_id_is_rejected_when_provided()
    {
        var durable = new ItemDefinition(Guid.NewGuid(), "Mop", ItemKind.Durable);
        var consumable = new ItemDefinition(Guid.NewGuid(), "Beans", ItemKind.Consumable);

        Assert.Throws<ArgumentException>(() => new DurableEntry(Guid.NewGuid(), durable, Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ConsumableEntry(Guid.NewGuid(), consumable, Quantity.From(1), new Unit("kg"), null, Guid.Empty));
    }

    [Test]
    public void Entry_subclasses_expose_their_item_kind()
    {
        var durable = new ItemDefinition(Guid.NewGuid(), "Broom", ItemKind.Durable);
        var consumable = new ItemDefinition(Guid.NewGuid(), "Milk", ItemKind.Consumable);

        InventoryEntry durableEntry = new DurableEntry(Guid.NewGuid(), durable);
        InventoryEntry consumableEntry = new ConsumableEntry(Guid.NewGuid(), consumable, Quantity.From(1), new Unit("liter"), DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Multiple(() =>
        {
            Assert.That(durableEntry.Kind, Is.EqualTo(ItemKind.Durable));
            Assert.That(consumableEntry.Kind, Is.EqualTo(ItemKind.Consumable));
        });
    }

    [Test]
    public void Entry_subclasses_reject_wrong_item_kind()
    {
        var durable = new ItemDefinition(Guid.NewGuid(), "Vacuum", ItemKind.Durable);
        var consumable = new ItemDefinition(Guid.NewGuid(), "Milk", ItemKind.Consumable);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => new DurableEntry(Guid.NewGuid(), consumable));
            Assert.Throws<InvalidOperationException>(() => new ConsumableEntry(Guid.NewGuid(), durable, Quantity.From(1), new Unit("piece"), null));
        });
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
