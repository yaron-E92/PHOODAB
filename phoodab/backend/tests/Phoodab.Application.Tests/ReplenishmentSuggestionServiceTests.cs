using Phoodab.Application;
using Phoodab.Domain;

namespace Phoodab.Application.Tests;

public class ReplenishmentSuggestionServiceTests
{
    [Test]
    public void Understocked_item_returns_positive_required_amount()
    {
        var item = new ItemDefinition(Guid.NewGuid(), "Milk", ItemKind.Consumable);
        var entry = new InventoryEntry(Guid.NewGuid(), item);
        entry.AddLot(new InventoryLot(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("liter"), null));
        var rule = new ReplenishmentRule(Guid.NewGuid(), item.Id, Quantity.From(3), new Unit("liter"));

        var result = new ReplenishmentSuggestionService().GetSuggestions(new[] { rule }, new[] { entry });

        Assert.That(result.Single().RequiredAmount, Is.EqualTo(2));
    }

    [Test]
    public void Fully_stocked_and_overstocked_items_return_zero_required_amount()
    {
        var fullItem = new ItemDefinition(Guid.NewGuid(), "Beans", ItemKind.Consumable);
        var fullEntry = new InventoryEntry(Guid.NewGuid(), fullItem);
        fullEntry.AddLot(new InventoryLot(Guid.NewGuid(), fullItem.Id, Quantity.From(2), new Unit("can"), null));

        var overItem = new ItemDefinition(Guid.NewGuid(), "Rice", ItemKind.Consumable);
        var overEntry = new InventoryEntry(Guid.NewGuid(), overItem);
        overEntry.AddLot(new InventoryLot(Guid.NewGuid(), overItem.Id, Quantity.From(5), new Unit("kg"), null));

        var suggestions = new ReplenishmentSuggestionService().GetSuggestions(
            new[]
            {
                new ReplenishmentRule(Guid.NewGuid(), fullItem.Id, Quantity.From(2), new Unit("can")),
                new ReplenishmentRule(Guid.NewGuid(), overItem.Id, Quantity.From(3), new Unit("kg"))
            },
            new[] { fullEntry, overEntry });

        Assert.That(suggestions.Select(s => s.RequiredAmount), Is.EqualTo(new[] { 0m, 0m }));
    }

    [Test]
    public void Hidden_or_disabled_rules_do_not_produce_suggestions()
    {
        var item = new ItemDefinition(Guid.NewGuid(), "Yogurt", ItemKind.Consumable);
        var entry = new InventoryEntry(Guid.NewGuid(), item);

        var suggestions = new ReplenishmentSuggestionService().GetSuggestions(
            new[]
            {
                new ReplenishmentRule(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("cup"), isHidden: true),
                new ReplenishmentRule(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("cup"), isDisabled: true)
            },
            new[] { entry });

        Assert.That(suggestions, Is.Empty);
    }
}
