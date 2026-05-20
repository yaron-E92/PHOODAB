using Phoodab.Application;
using Phoodab.Domain;

namespace Phoodab.Application.Tests;

public class ReplenishmentSuggestionServiceTests
{
    private static readonly DateOnly Today = new(2026, 05, 20);

    [Test]
    public void Understocked_item_returns_positive_required_amount()
    {
        var item = new ItemDefinition(Guid.NewGuid(), "Milk", ItemKind.Consumable);
        var entry = new InventoryEntry(Guid.NewGuid(), item);
        entry.AddLot(new InventoryLot(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("liter"), null));
        var rule = new ReplenishmentRule(Guid.NewGuid(), item.Id, Quantity.From(3), new Unit("liter"));

        var result = CreateService().GetSuggestions(new[] { rule }, new[] { entry });

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

        var suggestions = CreateService().GetSuggestions(
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

        var suggestions = CreateService().GetSuggestions(
            new[]
            {
                new ReplenishmentRule(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("cup"), isHidden: true),
                new ReplenishmentRule(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("cup"), isDisabled: true)
            },
            new[] { entry });

        Assert.That(suggestions, Is.Empty);
    }

    [Test]
    public void Required_amount_formula_uses_max_zero_of_desired_minus_current()
    {
        var rule = new ReplenishmentRule(Guid.NewGuid(), Guid.NewGuid(), Quantity.From(5), new Unit("liter"));

        Assert.That(rule.GetRequiredAmount(Quantity.From(2)).Value, Is.EqualTo(3));
        Assert.That(rule.GetRequiredAmount(Quantity.From(5)).Value, Is.EqualTo(0));
        Assert.That(rule.GetRequiredAmount(Quantity.From(7)).Value, Is.EqualTo(0));
    }

    [TestCase(-1, "Expired")]
    [TestCase(0, "Urgent")]
    [TestCase(2, "Urgent")]
    [TestCase(3, "Soon")]
    [TestCase(7, "Soon")]
    [TestCase(8, "Safe")]
    public void Lot_expiry_status_uses_mvp_boundaries(int offsetDays, string expectedStatus)
    {
        var item = new ItemDefinition(Guid.NewGuid(), "Cheese", ItemKind.Consumable);
        var entry = new InventoryEntry(Guid.NewGuid(), item);
        entry.AddLot(new InventoryLot(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("pack"), Today.AddDays(offsetDays)));
        var rule = new ReplenishmentRule(Guid.NewGuid(), item.Id, Quantity.From(2), new Unit("pack"));

        var result = CreateService().GetSuggestions(new[] { rule }, new[] { entry });

        Assert.That(result.Single().Lots.Single().ExpiryStatus, Is.EqualTo(expectedStatus));
    }

    [Test]
    public void Lot_without_expiry_date_is_unknown()
    {
        var item = new ItemDefinition(Guid.NewGuid(), "Eggs", ItemKind.Consumable);
        var entry = new InventoryEntry(Guid.NewGuid(), item);
        entry.AddLot(new InventoryLot(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("dozen"), null));
        var rule = new ReplenishmentRule(Guid.NewGuid(), item.Id, Quantity.From(2), new Unit("dozen"));

        var lot = CreateService().GetSuggestions(new[] { rule }, new[] { entry }).Single().Lots.Single();

        Assert.That(lot.ExpiresInDays, Is.Null);
        Assert.That(lot.ExpiryStatus, Is.EqualTo("Unknown"));
    }

    [Test]
    public void Multiple_lots_for_same_item_have_independent_expiry_status()
    {
        var item = new ItemDefinition(Guid.NewGuid(), "Yogurt", ItemKind.Consumable);
        var entry = new InventoryEntry(Guid.NewGuid(), item);
        entry.AddLot(new InventoryLot(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("cup"), Today.AddDays(1)));
        entry.AddLot(new InventoryLot(Guid.NewGuid(), item.Id, Quantity.From(1), new Unit("cup"), Today.AddDays(10)));
        var rule = new ReplenishmentRule(Guid.NewGuid(), item.Id, Quantity.From(3), new Unit("cup"));

        var lots = CreateService().GetSuggestions(new[] { rule }, new[] { entry }).Single().Lots;

        Assert.That(lots.Select(l => l.ExpiryStatus), Is.EquivalentTo(new[] { "Urgent", "Safe" }));
    }

    private static ReplenishmentSuggestionService CreateService(DateOnly? today = null)
        => new(new FakeUtcDateProvider(today ?? Today), new InventoryLotExpiryCalculator());

    private sealed class FakeUtcDateProvider(DateOnly today) : IUtcDateProvider
    {
        public DateOnly TodayUtc => today;
    }

}
