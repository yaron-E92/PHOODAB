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
        Assert.That(paths.TryGetProperty("/api/consumable-entries", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/inventory/summary", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/consumable-entries/expiring", out _), Is.True);
        Assert.That(paths.TryGetProperty("/api/replenishment/suggestions", out _), Is.True);
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
        var itemResponse = await _client.PostAsJsonAsync("/api/item-definitions", new { name = "Oats", kind = ItemKind.Consumable });
        Assert.That(itemResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var item = JsonSerializer.Deserialize<JsonElement>(await itemResponse.Content.ReadAsStringAsync());
        var itemId = item.GetProperty("id").GetGuid();

        var entryResponse = await _client.PostAsJsonAsync("/api/consumable-entries", new { itemDefinitionId = itemId, quantity = 1m, unit = "bag", expiresOn = (DateOnly?)null, storageSlotId = (Guid?)null });
        Assert.That(entryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

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
