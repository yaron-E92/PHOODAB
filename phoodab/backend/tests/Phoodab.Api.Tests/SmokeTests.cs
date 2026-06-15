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
    private static string StoreFilePath
    {
        get
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(basePath, "phoodab", "inventory-mvp-store.json");
        }
    }

    private static void ResetStoreFile()
    {
        if (File.Exists(StoreFilePath))
        {
            File.Delete(StoreFilePath);
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
        Assert.That(paths.TryGetProperty("/api/durable-entries", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/durable-entries/{entryId}", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/durable-entries/{entryId}/retire", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/consumable-entries", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/consumable-entries/{entryId}", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/inventory/summary", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/consumable-entries/expiring", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/replenishment/suggestions", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/search", out _), Is.True);
    }

    [Test]
    public async Task Global_search_returns_mixed_labeled_results_and_ignores_blank_queries()
    {
        var blankResponse = await _client.GetAsync("/api/search?q=%20%20");
        Assert.That(blankResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var blankResults = JsonSerializer.Deserialize<JsonElement>(await blankResponse.Content.ReadAsStringAsync());
        Assert.That(blankResults.GetArrayLength(), Is.Zero);

        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new
        {
            name = "Milk",
            kind = ItemKind.Consumable,
            desiredAmount = 2m,
            desiredUnit = "liter"
        });
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());

        var shoppingResponse = await _client.PostAsJsonAsync("/api/shopping-list-items/from-suggestion", new
        {
            itemDefinitionId = item.GetProperty("id").GetGuid(),
            quantity = 1m,
            unit = "liter"
        });
        Assert.That(shoppingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var durableResponse = await _client.PostAsJsonAsync("/api/durable-entries", new
        {
            displayName = "Milk Frother",
            itemType = "Appliance",
            status = "Active",
            currentLocation = "Milk pantry"
        });
        Assert.That(durableResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var searchResponse = await _client.GetAsync("/api/search?q=mILk");
        Assert.That(searchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var results = JsonSerializer.Deserialize<JsonElement>(await searchResponse.Content.ReadAsStringAsync())
            .EnumerateArray().ToList();

        Assert.That(results.Select(result => result.GetProperty("typeLabel").GetString()), Is.SupersetOf(new[]
        {
            "Consumable",
            "Durable Item",
            "Location",
            "Shopping List"
        }));
        Assert.That(results.All(result => !string.IsNullOrWhiteSpace(result.GetProperty("id").GetString())), Is.True);
        Assert.That(results.All(result => !string.IsNullOrWhiteSpace(result.GetProperty("title").GetString())), Is.True);
    }

    [Test]
    public async Task OpenApi_Replenishment_Rules_Expose_Generated_Client_Schemas()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var swagger = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var paths = swagger.GetProperty("paths");
        var schemas = swagger.GetProperty("components").GetProperty("schemas");

        var healthResponseSchema = paths.GetProperty("/health").GetProperty("get")
            .GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");
        var healthSchema = ResolveSchema(schemas, healthResponseSchema);
        AssertSchemaHasProperties(healthSchema, "status");
        AssertSchemaRequiresProperties(healthSchema, "status");

        var versionResponseSchema = paths.GetProperty("/version").GetProperty("get")
            .GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");
        var versionSchema = ResolveSchema(schemas, versionResponseSchema);
        AssertSchemaHasProperties(versionSchema, "version");
        AssertSchemaRequiresProperties(versionSchema, "version");

        var rulesPath = paths.GetProperty("/api/replenishment/rules");
        var rulesResponseSchema = rulesPath.GetProperty("get")
            .GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");
        Assert.That(rulesResponseSchema.GetProperty("type").GetString(), Is.EqualTo("array"));
        var ruleResponseSchema = ResolveSchema(schemas, rulesResponseSchema.GetProperty("items"));
        AssertSchemaHasProperties(ruleResponseSchema,
            "id",
            "itemDefinitionId",
            "desiredAmount",
            "desiredUnit",
            "expiryWarningDays",
            "isDisabled");
        AssertSchemaRequiresProperties(ruleResponseSchema,
            "id",
            "itemDefinitionId",
            "desiredAmount",
            "desiredUnit",
            "expiryWarningDays",
            "isDisabled");

        var rulePatch = paths.GetProperty("/api/replenishment/rules/{ruleId}").GetProperty("patch");
        var patchRequestSchema = rulePatch.GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");
        AssertSchemaHasProperties(ResolveSchema(schemas, patchRequestSchema),
            "desiredAmount",
            "desiredUnit",
            "isDisabled",
            "expiryWarningDays");

        var patchResponseSchema = rulePatch.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");
        ruleResponseSchema = ResolveSchema(schemas, patchResponseSchema);
        AssertSchemaHasProperties(ruleResponseSchema,
            "id",
            "itemDefinitionId",
            "desiredAmount",
            "desiredUnit",
            "expiryWarningDays",
            "isDisabled");
        AssertSchemaRequiresProperties(ruleResponseSchema,
            "id",
            "itemDefinitionId",
            "desiredAmount",
            "desiredUnit",
            "expiryWarningDays",
            "isDisabled");
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
        Assert.That(eggs.GetProperty("entries").EnumerateArray().Single().GetProperty("expiryStatus").GetString(), Is.EqualTo("Urgent"));
        Assert.That(pasta.GetProperty("entries").EnumerateArray().Single().GetProperty("expiryStatus").GetString(), Is.EqualTo("Expired"));
    }

    [Test]
    public async Task Add_consumable_entry_with_quantity_and_expiry_date_is_reflected_in_summary_and_expiring_views()
    {
        var itemId = await CreateConsumableItem("Milk");

        var expiredDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var entryResponse = await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 1m, unit = "liter", expiresOn = expiredDate, storageSlotId = (Guid?)null });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var entry = JsonSerializer.Deserialize<JsonElement>(await entryResponse.Content.ReadAsStringAsync());
        var entryId = entry.GetProperty("id").GetGuid();
        Assert.That(entry.GetProperty("quantity").GetProperty("value").GetDecimal(), Is.EqualTo(1m));
        Assert.That(entry.GetProperty("expiresOn").GetDateTime().Date, Is.EqualTo(expiredDate.ToDateTime(TimeOnly.MinValue).Date));

        var summary = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/inventory/summary")).Content.ReadAsStringAsync())
            .EnumerateArray().Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.That(summary.GetProperty("totalQuantity").GetDecimal(), Is.EqualTo(1m));

        var expiringEntries = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/consumable-entries/expiring")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        var expiringEntry = expiringEntries.Single(x => x.GetProperty("entryId").GetGuid() == entryId);
        Assert.That(expiringEntry.GetProperty("itemName").GetString(), Is.EqualTo("Milk"));
        Assert.That(expiringEntry.GetProperty("expiresInDays").GetInt32(), Is.LessThan(0));
        Assert.That(expiringEntry.GetProperty("expiryStatus").GetString(), Is.EqualTo("Expired"));
    }

    [Test]
    public async Task Consumable_inventory_mvp_flow_returns_replenishment_when_below_and_not_when_sufficient()
    {
        var itemId = await CreateConsumableItem("Beans");

        await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 1m, unit = "can", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });

        var suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        var suggestion = suggestions.Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.That(suggestion.GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(1m));

        await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 1m, unit = "can", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });

        suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        suggestion = suggestions.Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.That(suggestion.GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(0m));
    }

    [Test]
    public async Task Consumable_entries_can_be_listed_and_updated_recalculating_summary()
    {
        var itemId = await CreateConsumableItem("Coffee");
        var storageSlotId = Guid.NewGuid();
        var updatedStorageSlotId = Guid.NewGuid();
        var entryResponse = await _client.PostAsJsonAsync("/api/consumable-entries", new
        {
            itemDefinitionId = itemId,
            quantity = 1m,
            unit = "bag",
            expiresOn = (DateOnly?)null,
            storageSlotId
        });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entryId = JsonSerializer.Deserialize<JsonElement>(await entryResponse.Content.ReadAsStringAsync()).GetProperty("id").GetGuid();

        var entries = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/consumable-entries")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        var entry = entries.Single(x => x.GetProperty("entryId").GetGuid() == entryId);
        Assert.Multiple(() =>
        {
            Assert.That(entry.GetProperty("itemDefinitionId").GetGuid(), Is.EqualTo(itemId));
            Assert.That(entry.GetProperty("itemName").GetString(), Is.EqualTo("Coffee"));
            Assert.That(entry.GetProperty("quantity").GetDecimal(), Is.EqualTo(1m));
            Assert.That(entry.GetProperty("unit").GetString(), Is.EqualTo("bag"));
            Assert.That(entry.GetProperty("expiryStatus").GetString(), Is.EqualTo("Unknown"));
            Assert.That(entry.GetProperty("storageSlotId").GetGuid(), Is.EqualTo(storageSlotId));
        });

        var safeDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14));
        var patchResponse = await _client.PatchAsJsonAsync($"/api/consumable-entries/{entryId}", new
        {
            quantity = 3m,
            unit = "tin",
            expiresOn = safeDate,
            storageSlotId = updatedStorageSlotId
        });
        Assert.That(patchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = JsonSerializer.Deserialize<JsonElement>(await patchResponse.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(updated.GetProperty("quantity").GetDecimal(), Is.EqualTo(3m));
            Assert.That(updated.GetProperty("unit").GetString(), Is.EqualTo("tin"));
            Assert.That(updated.GetProperty("expiryStatus").GetString(), Is.EqualTo("Safe"));
            Assert.That(updated.GetProperty("storageSlotId").GetGuid(), Is.EqualTo(updatedStorageSlotId));
        });

        var summary = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/inventory/summary")).Content.ReadAsStringAsync())
            .EnumerateArray().Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.Multiple(() =>
        {
            Assert.That(summary.GetProperty("totalQuantity").GetDecimal(), Is.EqualTo(3m));
            Assert.That(summary.GetProperty("unit").GetString(), Is.EqualTo("tin"));
            Assert.That(summary.GetProperty("hasMixedUnits").GetBoolean(), Is.False);
        });
    }

    [Test]
    public async Task Inventory_summary_flags_mixed_units_without_misleading_total()
    {
        var itemId = await CreateConsumableItem("Rice");

        await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 1m, unit = "kg", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });
        await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 2m, unit = "bag", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });

        var summary = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/inventory/summary")).Content.ReadAsStringAsync())
            .EnumerateArray().Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.Multiple(() =>
        {
            Assert.That(summary.GetProperty("totalQuantity").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(summary.GetProperty("unit").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(summary.GetProperty("hasMixedUnits").GetBoolean(), Is.True);
            Assert.That(summary.GetProperty("mixedUnitWarning").GetString(), Is.EqualTo("Mixed units cannot be totaled safely."));
        });
    }

    [Test]
    public async Task Replenishment_rule_patch_updates_desired_amount_and_unit_used_by_suggestions()
    {
        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new { name = "Lentils", kind = ItemKind.Consumable, desiredAmount = 2m, desiredUnit = "bag" });
        Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());
        var itemId = item.GetProperty("id").GetGuid();

        var rule = await GetRuleForItem(itemId);
        Assert.Multiple(() =>
        {
            Assert.That(rule.GetProperty("desiredAmount").GetDecimal(), Is.EqualTo(2m));
            Assert.That(rule.GetProperty("desiredUnit").GetString(), Is.EqualTo("bag"));
            Assert.That(rule.GetProperty("expiryWarningDays").GetInt32(), Is.EqualTo(2));
            Assert.That(rule.GetProperty("isDisabled").GetBoolean(), Is.False);
        });

        var patchResponse = await _client.PatchAsJsonAsync($"/api/replenishment/rules/{rule.GetProperty("id").GetGuid()}", new
        {
            desiredAmount = 5m,
            desiredUnit = "pouch"
        });
        Assert.That(patchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        rule = await GetRuleForItem(itemId);
        Assert.Multiple(() =>
        {
            Assert.That(rule.GetProperty("desiredAmount").GetDecimal(), Is.EqualTo(5m));
            Assert.That(rule.GetProperty("desiredUnit").GetString(), Is.EqualTo("pouch"));
        });

        var suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        var suggestion = suggestions.Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.Multiple(() =>
        {
            Assert.That(suggestion.GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(5m));
            Assert.That(suggestion.GetProperty("unit").GetString(), Is.EqualTo("pouch"));
        });
    }

    [Test]
    public async Task Durable_item_definition_does_not_create_replenishment_rule()
    {
        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new { name = "Vacuum", kind = ItemKind.Durable });
        Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());
        var itemId = item.GetProperty("id").GetGuid();

        var rules = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/rules")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();

        Assert.That(rules.Any(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId), Is.False);
    }

    [Test]
    public async Task Durable_entries_can_be_created_listed_updated_viewed_and_retired()
    {
        var purchasedOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-10));
        var warrantyEndsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(1));
        var createResponse = await _client.PostAsJsonAsync("/api/durable-entries", new
        {
            displayName = "Vacuum",
            description = "Cordless cleaner",
            itemType = "Appliance",
            brandManufacturer = "Acme",
            model = "V100",
            serialNumber = "SN-123",
            purchaseDate = purchasedOn,
            purchaseValue = 149.99m,
            warrantyEndsOn,
            status = "Active",
            currentLocation = "Utility closet",
            notes = "Includes wall mount",
            storageSlotId = (Guid?)null
        });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var created = JsonSerializer.Deserialize<JsonElement>(await createResponse.Content.ReadAsStringAsync());
        var entryId = created.GetProperty("id").GetGuid();
        var itemDefinitionId = created.GetProperty("itemDefinitionId").GetGuid();
        Assert.Multiple(() =>
        {
            Assert.That(created.GetProperty("displayName").GetString(), Is.EqualTo("Vacuum"));
            Assert.That(created.GetProperty("itemType").GetString(), Is.EqualTo("Appliance"));
            Assert.That(created.GetProperty("brandManufacturer").GetString(), Is.EqualTo("Acme"));
            Assert.That(created.GetProperty("status").GetString(), Is.EqualTo("Active"));
            Assert.That(created.TryGetProperty("quantity", out _), Is.False);
        });

        var list = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/durable-entries")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        Assert.That(list.Single(x => x.GetProperty("id").GetGuid() == entryId).GetProperty("displayName").GetString(), Is.EqualTo("Vacuum"));

        var getResponse = await _client.GetAsync($"/api/durable-entries/{entryId}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var fetched = JsonSerializer.Deserialize<JsonElement>(await getResponse.Content.ReadAsStringAsync());
        Assert.That(fetched.GetProperty("itemDefinitionId").GetGuid(), Is.EqualTo(itemDefinitionId));

        var updateResponse = await _client.PatchAsJsonAsync($"/api/durable-entries/{entryId}", new
        {
            displayName = "Workshop vacuum",
            description = "Updated description",
            itemType = "Tool",
            brandManufacturer = "Acme",
            model = "V200",
            serialNumber = "SN-456",
            purchaseDate = purchasedOn,
            purchaseValue = 199.50m,
            warrantyEndsOn,
            status = "NeedsRepair",
            currentLocation = "Garage",
            notes = "Broken hose",
            storageSlotId = (Guid?)null
        });
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = JsonSerializer.Deserialize<JsonElement>(await updateResponse.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(updated.GetProperty("displayName").GetString(), Is.EqualTo("Workshop vacuum"));
            Assert.That(updated.GetProperty("status").GetString(), Is.EqualTo("NeedsRepair"));
            Assert.That(updated.GetProperty("currentLocation").GetString(), Is.EqualTo("Garage"));
            Assert.That(updated.GetProperty("purchaseValue").GetDecimal(), Is.EqualTo(199.50m));
        });

        var retireResponse = await _client.PatchAsJsonAsync($"/api/durable-entries/{entryId}/retire", new { notes = "Replaced by newer unit" });
        Assert.That(retireResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var retired = JsonSerializer.Deserialize<JsonElement>(await retireResponse.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(retired.GetProperty("status").GetString(), Is.EqualTo("Retired"));
            Assert.That(retired.GetProperty("notes").GetString(), Is.EqualTo("Replaced by newer unit"));
        });

        var rules = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/rules")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        Assert.That(rules.Any(x => x.GetProperty("itemDefinitionId").GetGuid() == itemDefinitionId), Is.False);
    }

    [Test]
    public async Task Legacy_consumable_rule_with_missing_persisted_fields_is_normalized_to_defaults()
    {
        _client.Dispose();
        _factory.Dispose();
        ResetStoreFile();

        var itemId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        Directory.CreateDirectory(Path.GetDirectoryName(StoreFilePath)!);
        await File.WriteAllTextAsync(StoreFilePath, $$"""
        {
          "itemDefinitions": [
            {
              "id": "{{itemId}}",
              "name": "Flour",
              "kind": 1
            }
          ],
          "durableEntries": [],
          "consumableEntries": [],
          "rules": [
            {
              "id": "{{ruleId}}",
              "itemDefinitionId": "{{itemId}}"
            }
          ],
          "shoppingListItems": []
        }
        """);

        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();

        var rule = await GetRuleForItem(itemId);

        Assert.Multiple(() =>
        {
            Assert.That(rule.GetProperty("desiredAmount").GetDecimal(), Is.EqualTo(2m));
            Assert.That(rule.GetProperty("desiredUnit").GetString(), Is.EqualTo("unit"));
            Assert.That(rule.GetProperty("expiryWarningDays").GetInt32(), Is.EqualTo(2));
            Assert.That(rule.GetProperty("isDisabled").GetBoolean(), Is.False);
        });
    }

    [Test]
    public async Task Legacy_consumable_item_without_rule_is_backfilled_but_durable_item_is_not()
    {
        _client.Dispose();
        _factory.Dispose();
        ResetStoreFile();

        var consumableId = Guid.NewGuid();
        var durableId = Guid.NewGuid();
        Directory.CreateDirectory(Path.GetDirectoryName(StoreFilePath)!);
        await File.WriteAllTextAsync(StoreFilePath, $$"""
        {
          "itemDefinitions": [
            {
              "id": "{{consumableId}}",
              "name": "Sugar",
              "kind": 1
            },
            {
              "id": "{{durableId}}",
              "name": "Vacuum",
              "kind": 0
            }
          ],
          "durableEntries": [],
          "consumableEntries": [
            {
              "id": "{{Guid.NewGuid()}}",
              "itemDefinitionId": "{{consumableId}}",
              "quantity": 1,
              "unit": "unit",
              "expiresOn": null,
              "storageSlotId": null
            }
          ],
          "rules": [],
          "shoppingListItems": []
        }
        """);

        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();

        var rule = await GetRuleForItem(consumableId);
        var rules = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/rules")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rule.GetProperty("desiredAmount").GetDecimal(), Is.EqualTo(2m));
            Assert.That(rule.GetProperty("desiredUnit").GetString(), Is.EqualTo("unit"));
            Assert.That(rule.GetProperty("expiryWarningDays").GetInt32(), Is.EqualTo(2));
            Assert.That(rule.GetProperty("isDisabled").GetBoolean(), Is.False);
            Assert.That(rules.Any(x => x.GetProperty("itemDefinitionId").GetGuid() == durableId), Is.False);
        });
    }

    [Test]
    public async Task Disabled_replenishment_rule_does_not_return_suggestion()
    {
        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new { name = "Crackers", kind = ItemKind.Consumable, desiredAmount = 2m, desiredUnit = "box" });
        Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());
        var itemId = item.GetProperty("id").GetGuid();

        var rule = await GetRuleForItem(itemId);
        var patchResponse = await _client.PatchAsJsonAsync($"/api/replenishment/rules/{rule.GetProperty("id").GetGuid()}", new { isDisabled = true });
        Assert.That(patchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();

        Assert.That(suggestions.Any(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId), Is.False);
    }

    [Test]
    public async Task Expiry_warning_days_controls_backend_expiry_lookahead()
    {
        var itemId = await CreateConsumableItem("Tahini", 2m, "jar");
        var expiresOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(4));
        var entryResponse = await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 1m, unit = "jar", expiresOn, storageSlotId = (Guid?)null });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entry = JsonSerializer.Deserialize<JsonElement>(await entryResponse.Content.ReadAsStringAsync());
        var entryId = entry.GetProperty("id").GetGuid();

        var rule = await GetRuleForItem(itemId);
        var patchResponse = await _client.PatchAsJsonAsync($"/api/replenishment/rules/{rule.GetProperty("id").GetGuid()}", new { expiryWarningDays = 5 });
        Assert.That(patchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        var suggestionEntry = suggestions.Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId)
            .GetProperty("entries").EnumerateArray().Single();

        var expiringEntry = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/consumable-entries/expiring")).Content.ReadAsStringAsync())
            .EnumerateArray().Single(x => x.GetProperty("entryId").GetGuid() == entryId);

        Assert.Multiple(() =>
        {
            Assert.That(suggestionEntry.GetProperty("expiryStatus").GetString(), Is.EqualTo("Urgent"));
            Assert.That(expiringEntry.GetProperty("expiryStatus").GetString(), Is.EqualTo("Urgent"));
        });
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
        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new { name = "Oats", kind = ItemKind.Consumable, desiredAmount = 5m, desiredUnit = "bag" });
        Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());
        var itemId = item.GetProperty("id").GetGuid();

        var rule = await GetRuleForItem(itemId);
        var rulePatchResponse = await _client.PatchAsJsonAsync($"/api/replenishment/rules/{rule.GetProperty("id").GetGuid()}", new { expiryWarningDays = 3 });
        Assert.That(rulePatchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var entryResponse = await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 1m, unit = "bag", expiresOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1)), storageSlotId = (Guid?)null });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        entryResponse = await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 1m, unit = "bag", expiresOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)), storageSlotId = (Guid?)null });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        entryResponse = await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 2m, unit = "bag", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var summary = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/inventory/summary")).Content.ReadAsStringAsync()).EnumerateArray().ToList();
        Assert.That(summary.Any(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId), Is.True);

        var suggestions = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/replenishment/suggestions")).Content.ReadAsStringAsync()).EnumerateArray().ToList();
        var suggestion = suggestions.Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
        Assert.Multiple(() =>
        {
            Assert.That(suggestion.GetProperty("usableCurrentQuantity").GetDecimal(), Is.EqualTo(3m));
            Assert.That(suggestion.GetProperty("desiredQuantity").GetDecimal(), Is.EqualTo(5m));
            Assert.That(suggestion.GetProperty("deficitAmount").GetDecimal(), Is.EqualTo(2m));
            Assert.That(suggestion.GetProperty("expiringSoonAmount").GetDecimal(), Is.EqualTo(1m));
            Assert.That(suggestion.GetProperty("suggestedPurchaseAmount").GetDecimal(), Is.EqualTo(3m));
            Assert.That(suggestion.GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(3m));
        });

        var createShoppingItemResponse = await _client.PostAsJsonAsync("/api/shopping-list-items/from-suggestion", new
        {
            itemDefinitionId = itemId,
            quantity = suggestion.GetProperty("suggestedPurchaseAmount").GetDecimal(),
            unit = suggestion.GetProperty("unit").GetString(),
            deficitAmount = suggestion.GetProperty("deficitAmount").GetDecimal(),
            expiringSoonAmount = suggestion.GetProperty("expiringSoonAmount").GetDecimal(),
            suggestedPurchaseAmount = suggestion.GetProperty("suggestedPurchaseAmount").GetDecimal()
        });
        Assert.That(createShoppingItemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var shoppingItem = JsonSerializer.Deserialize<JsonElement>(await createShoppingItemResponse.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(shoppingItem.GetProperty("quantity").GetDecimal(), Is.EqualTo(3m));
            Assert.That(shoppingItem.GetProperty("sourceDeficitAmount").GetDecimal(), Is.EqualTo(2m));
            Assert.That(shoppingItem.GetProperty("sourceExpiringSoonAmount").GetDecimal(), Is.EqualTo(1m));
            Assert.That(shoppingItem.GetProperty("sourceSuggestedPurchaseAmount").GetDecimal(), Is.EqualTo(3m));
            Assert.That(shoppingItem.GetProperty("status").GetString(), Is.EqualTo("ShoppingList"));
            Assert.That(shoppingItem.GetProperty("stockUpdateNeeded").GetBoolean(), Is.False);
        });

        var patchResponse = await _client.PatchAsJsonAsync($"/api/shopping-list-items/{shoppingItem.GetProperty("id").GetGuid()}", new { status = "InCart" });
        Assert.That(patchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var patched = JsonSerializer.Deserialize<JsonElement>(await patchResponse.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(patched.GetProperty("status").GetString(), Is.EqualTo("InCart"));
            Assert.That(patched.GetProperty("isResolved").GetBoolean(), Is.False);
            Assert.That(patched.GetProperty("isPurchased").GetBoolean(), Is.False);
            Assert.That(patched.GetProperty("stockUpdateNeeded").GetBoolean(), Is.False);
        });

        patchResponse = await _client.PatchAsJsonAsync($"/api/shopping-list-items/{shoppingItem.GetProperty("id").GetGuid()}", new { status = "Bought" });
        Assert.That(patchResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        patched = JsonSerializer.Deserialize<JsonElement>(await patchResponse.Content.ReadAsStringAsync());
        Assert.Multiple(() =>
        {
            Assert.That(patched.GetProperty("status").GetString(), Is.EqualTo("Bought"));
            Assert.That(patched.GetProperty("isResolved").GetBoolean(), Is.True);
            Assert.That(patched.GetProperty("isPurchased").GetBoolean(), Is.True);
            Assert.That(patched.GetProperty("stockUpdateNeeded").GetBoolean(), Is.False);
            Assert.That(patched.GetProperty("nextInventoryAction").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });

        var deleteResponse = await _client.DeleteAsync($"/api/shopping-list-items/{shoppingItem.GetProperty("id").GetGuid()}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var shoppingItems = JsonSerializer.Deserialize<JsonElement>(await (await _client.GetAsync("/api/shopping-list-items")).Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        Assert.That(shoppingItems.Any(x => x.GetProperty("id").GetGuid() == shoppingItem.GetProperty("id").GetGuid()), Is.False);
    }

    private async Task<Guid> CreateConsumableItem(string name, decimal? desiredAmount = null, string? desiredUnit = null)
    {
        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new { name, kind = ItemKind.Consumable, desiredAmount, desiredUnit });
        Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());
        return item.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> GetRuleForItem(Guid itemId)
    {
        var response = await _client.GetAsync("/api/replenishment/rules");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var rules = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync())
            .EnumerateArray().ToList();
        return rules.Single(x => x.GetProperty("itemDefinitionId").GetGuid() == itemId);
    }

    private static JsonElement ResolveSchema(JsonElement schemas, JsonElement schema)
    {
        return schema.TryGetProperty("$ref", out var schemaReference)
            ? schemas.GetProperty(schemaReference.GetString()!.Split('/').Last())
            : schema;
    }

    private static void AssertSchemaHasProperties(JsonElement schema, params string[] properties)
    {
        var schemaProperties = schema.GetProperty("properties");
        Assert.Multiple(() =>
        {
            foreach (var property in properties)
            {
                Assert.That(schemaProperties.TryGetProperty(property, out _), Is.True, $"Missing schema property {property}");
            }
        });
    }

    private static void AssertSchemaRequiresProperties(JsonElement schema, params string[] properties)
    {
        var requiredProperties = schema.GetProperty("required").EnumerateArray()
            .Select(property => property.GetString())
            .ToHashSet();
        Assert.Multiple(() =>
        {
            foreach (var property in properties)
            {
                Assert.That(requiredProperties.Contains(property), Is.True, $"Schema property {property} is not required");
            }
        });
    }
}
