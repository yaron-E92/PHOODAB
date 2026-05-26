using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using Phoodab.Domain;

namespace Phoodab.Api.Tests;

public class SmokeTests
{
    private static void ResetStoreFile()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var filePath = Path.Combine(basePath, "phoodab", "inventory-mvp-store.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        ResetStoreFile();
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Health_Endpoint_Returns_Ok()
    {
        var response = await _client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Version_Endpoint_Returns_Ok()
    {
        var response = await _client.GetAsync("/version");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task OpenApi_Contains_Mvp_Inventory_Endpoints()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var swagger = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var paths = swagger.GetProperty("paths");

        Assert.That(paths.TryGetProperty("/api/item-definitions", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/inventory-entries", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/inventory-lots", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/inventory/summary", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/inventory/expiring", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/replenishment/suggestions", out _), Is.True);
    }

    [Test]
    public async Task Development_seed_data_contains_demo_stock_and_expiry_mix()
    {
        var suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();

        Assert.That(suggestions.Select(x => x.GetProperty("itemName").GetString()), Is.SupersetOf(new[] { "Milk", "Eggs", "Pasta", "Rice" }));

        var milk = suggestions.Single(x => x.GetProperty("itemName").GetString() == "Milk");
        var eggs = suggestions.Single(x => x.GetProperty("itemName").GetString() == "Eggs");
        var pasta = suggestions.Single(x => x.GetProperty("itemName").GetString() == "Pasta");
        var rice = suggestions.Single(x => x.GetProperty("itemName").GetString() == "Rice");

        Assert.That(milk.GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(0m));
        Assert.That(eggs.GetProperty("requiredAmount").GetDecimal(), Is.GreaterThan(0m));
        Assert.That(rice.GetProperty("requiredAmount").GetDecimal(), Is.GreaterThan(0m));
        Assert.That(eggs.GetProperty("lots").EnumerateArray().Single().GetProperty("expiryStatus").GetString(), Is.EqualTo("Urgent"));
        Assert.That(pasta.GetProperty("lots").EnumerateArray().Single().GetProperty("expiryStatus").GetString(), Is.EqualTo("Expired"));
    }

    [Test]
    public async Task Add_lot_with_quantity_and_expiry_date_is_reflected_in_summary_and_expiring_views()
    {
        var (itemId, entryId) = await CreateItemAndEntry("Milk");

        var expiredDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var lotResponse = await _client.PostAsJsonAsync("/api/inventory-lots", new { inventoryEntryId = entryId, quantity = 1m, unit = "liter", expiresOn = expiredDate, storageSlotId = (Guid?)null });
        Assert.That(lotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var lot = JsonSerializer.Deserialize<JsonElement>(await lotResponse.Content.ReadAsStringAsync());
        var lotId = lot.GetProperty("id").GetGuid();
        Assert.That(lot.GetProperty("quantity").GetProperty("value").GetDecimal(), Is.EqualTo(1m));
        Assert.That(lot.GetProperty("expiresOn").GetDateTime().Date, Is.EqualTo(expiredDate.ToDateTime(TimeOnly.MinValue).Date));

        var summary = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/inventory/summary")).Content.ReadAsStringAsync())
            .EnumerateArray().Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.That(summary.GetProperty("totalQuantity").GetDecimal(), Is.EqualTo(1m));

        var expiringLots = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/inventory/expiring")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        var expiringLot = expiringLots.Single(x => x.GetProperty("lotId").GetGuid() == lotId);
        Assert.That(expiringLot.GetProperty("expiresInDays").GetInt32(), Is.LessThan(0));
        Assert.That(expiringLot.GetProperty("expiryStatus").GetString(), Is.EqualTo("Expired"));
    }

    [Test]
    public async Task Consumable_inventory_mvp_flow_returns_replenishment_when_below_and_not_when_sufficient()
    {
        var (itemId, entryId) = await CreateItemAndEntry("Beans");

        await _client.PostAsJsonAsync("/api/inventory-lots", new { inventoryEntryId = entryId, quantity = 1m, unit = "can", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });

        var suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        var suggestion = suggestions.Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.That(suggestion.GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(1m));

        await _client.PostAsJsonAsync("/api/inventory-lots", new { inventoryEntryId = entryId, quantity = 1m, unit = "can", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });

        suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        suggestion = suggestions.Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.That(suggestion.GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(0m));
    }

    [Test]
    public async Task Legacy_replenishment_route_remains_available()
    {
        var response = await _client.GetAsync("/replenishment/suggestions");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Vertical_slice_suggestion_can_create_and_purchase_shopping_list_item()
    {
        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new { name = "Oats", kind = ItemKind.Consumable });
        Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());
        var itemId = item.GetProperty("id").GetGuid();

        var entryResponse = await _client.PostAsJsonAsync("/api/inventory-entries", new { itemDefinitionId = itemId, storageSlotId = (Guid?)null });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entry = JsonSerializer.Deserialize<JsonElement>(await entryResponse.Content.ReadAsStringAsync());
        var entryId = entry.GetProperty("id").GetGuid();

        var lotResponse = await _client.PostAsJsonAsync("/api/inventory-lots", new { inventoryEntryId = entryId, quantity = 1m, unit = "bag", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });
        Assert.That(lotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var summary = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/inventory/summary")).Content.ReadAsStringAsync()).EnumerateArray().ToList();
        Assert.That(summary.Any(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId), Is.True);

        var suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync()).EnumerateArray().ToList();
        var suggestion = suggestions.Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);

        var createShoppingItemResponse = await _client.PostAsJsonAsync("/api/shopping-list-items/from-suggestion", new
        {
            itemDefinitionId = itemId,
            quantity = suggestion.GetProperty("requiredAmount").GetDecimal(),
            unit = suggestion.GetProperty("unit").GetString()
        });
        Assert.That(createShoppingItemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var shoppingItem = JsonSerializer.Deserialize<JsonElement>(await createShoppingItemResponse.Content.ReadAsStringAsync());

        var patchResponse = await _client.PatchAsJsonAsync($"/api/shopping-list-items/{shoppingItem.GetProperty("id").GetGuid()}", new { isResolved = true, isPurchased = true });
        Assert.That(patchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var patched = JsonSerializer.Deserialize<JsonElement>(await patchResponse.Content.ReadAsStringAsync());
        Assert.That(patched.GetProperty("isResolved").GetBoolean(), Is.True);
        Assert.That(patched.GetProperty("isPurchased").GetBoolean(), Is.True);
    }

    private async Task<(Guid itemId, Guid entryId)> CreateItemAndEntry(string name)
    {
        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new { name, kind = ItemKind.Consumable });
        Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());
        var itemId = item.GetProperty("id").GetGuid();

        var entryResponse = await _client.PostAsJsonAsync("/api/inventory-entries", new { itemDefinitionId = itemId, storageSlotId = (Guid?)null });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entry = JsonSerializer.Deserialize<JsonElement>(await entryResponse.Content.ReadAsStringAsync());

        return (itemId, entry.GetProperty("id").GetGuid());
    }
}
