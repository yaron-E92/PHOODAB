using System;
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
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
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
    public async Task Consumable_inventory_mvp_flow_returns_summary_expiry_and_replenishment()
    {
        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new { name = "Milk", kind = ItemKind.Consumable });
        Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());
        var itemId = item.GetProperty("id").GetGuid();

        var entryResponse = await _client.PostAsJsonAsync("/api/inventory-entries", new { itemDefinitionId = itemId, storageSlotId = (Guid?)null });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entry = JsonSerializer.Deserialize<JsonElement>(await entryResponse.Content.ReadAsStringAsync());
        var entryId = entry.GetProperty("id").GetGuid();

        var expiredDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var lotResponse = await _client.PostAsJsonAsync("/api/inventory-lots", new { inventoryEntryId = entryId, quantity = 1m, unit = "liter", expiresOn = expiredDate, storageSlotId = (Guid?)null });
        Assert.That(lotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var summary = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/inventory/summary")).Content.ReadAsStringAsync())
            .EnumerateArray().Single();
        Assert.That(summary.GetProperty("totalQuantity").GetDecimal(), Is.EqualTo(1m));

        var expiringLots = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/inventory/expiring")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        Assert.That(expiringLots.Count, Is.EqualTo(1));
        Assert.That(expiringLots[0].GetProperty("expiresInDays").GetInt32(), Is.LessThan(0));
        Assert.That(expiringLots[0].GetProperty("expiryStatus").GetString(), Is.EqualTo("Expired"));

        var suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        Assert.That(suggestions.Count, Is.EqualTo(1));
        Assert.That(suggestions[0].GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(1m));

        await _client.PostAsJsonAsync("/api/inventory-lots", new { inventoryEntryId = entryId, quantity = 3m, unit = "liter", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });

        suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        Assert.That(suggestions[0].GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(0m));
    }
}
