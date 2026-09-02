using System.ComponentModel.DataAnnotations;
using Phoodab.Application;
using Phoodab.Domain;
using Phoodab.Infrastructure.Eventing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddPhoodabEventing();

builder.Services.AddSingleton<IUtcDateProvider, SystemUtcDateProvider>();
builder.Services.AddSingleton<ReplenishmentSuggestionService>();
builder.Services.AddSingleton<IInventoryMvpStore, FileInventoryMvpStore>();

var app = builder.Build();

var demoDataMode = app.Configuration["DemoData:Mode"];
if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(demoDataMode))
{
    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<IInventoryMvpStore>();
    var utcDateProvider = scope.ServiceProvider.GetRequiredService<IUtcDateProvider>();
    if (demoDataMode.Equals("Seed", StringComparison.OrdinalIgnoreCase))
    {
        store.EnsureDevelopmentSeedData(utcDateProvider.TodayUtc);
    }
    else if (demoDataMode.Equals("Reset", StringComparison.OrdinalIgnoreCase))
    {
        store.ResetDevelopmentSeedData(utcDateProvider.TodayUtc);
    }
    else
    {
        throw new InvalidOperationException("DemoData:Mode must be either 'Seed' or 'Reset'.");
    }
}

app.UseCors("FrontendDev");

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
    .WithName("GetHealth")
    .Produces<HealthResponse>(StatusCodes.Status200OK)
    .WithOpenApi();

app.MapGet("/version", () =>
{
    var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    return Results.Ok(new VersionResponse(version));
})
.WithName("GetVersion")
.Produces<VersionResponse>(StatusCodes.Status200OK)
.WithOpenApi();

app.MapPost("/api/item-definitions", (CreateItemDefinitionRequest request, IInventoryMvpStore store) =>
{
    var item = store.CreateItemDefinition(request.Name, request.Kind, request.DesiredAmount, request.DesiredUnit);
    return Results.Ok(item);
}).WithOpenApi();

app.MapPost("/api/durable-entries", (CreateDurableEntryRequest request, IInventoryMvpStore store) =>
{
    if (!TryParseDurableStatus(request.Status, out var status))
    {
        return Results.BadRequest(new ErrorResponse("Unsupported durable item status."));
    }

    try
    {
        var locationId = request.LocationId ?? request.StorageSlotId;
        if (request.ItemDefinitionId.HasValue && string.IsNullOrWhiteSpace(request.DisplayName))
        {
            // Backward-compatible path for creating an entry from an existing durable item definition.
            var existingEntry = store.CreateDurableEntry(request.ItemDefinitionId.Value, locationId);
            return existingEntry is null
                ? Results.BadRequest(new ErrorResponse("Item definition must reference an existing durable item definition."))
                : Results.Ok(existingEntry);
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.BadRequest(new ErrorResponse("Display name is required."));
        }

        var entry = store.CreateDurableItem(
            request.DisplayName,
            request.Description,
            request.ItemType,
            request.BrandManufacturer,
            request.Model,
            request.SerialNumber,
            request.PurchaseDate,
            request.PurchaseValue,
            request.WarrantyEndsOn,
            status ?? DurableItemStatus.Active,
            request.CurrentLocation,
            request.Notes,
            locationId);
        return Results.Ok(entry);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
}).Produces<DurableItemReadModel>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.WithOpenApi();

app.MapGet("/api/durable-entries", (IInventoryMvpStore store) => Results.Ok(store.GetDurableEntries()))
.Produces<IEnumerable<DurableItemReadModel>>(StatusCodes.Status200OK)
.WithOpenApi();

app.MapGet("/api/durable-entries/{entryId:guid}", (Guid entryId, IInventoryMvpStore store) =>
{
    var entry = store.GetDurableEntry(entryId);
    return entry is null ? Results.NotFound() : Results.Ok(entry);
})
.Produces<DurableItemReadModel>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi();

app.MapPatch("/api/durable-entries/{entryId:guid}", (Guid entryId, UpdateDurableEntryRequest request, IInventoryMvpStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.DisplayName))
    {
        return Results.BadRequest(new ErrorResponse("Display name is required."));
    }

    if (!TryParseDurableStatus(request.Status, out var status))
    {
        return Results.BadRequest(new ErrorResponse("Unsupported durable item status."));
    }

    try
    {
        var entry = store.UpdateDurableEntry(
            entryId,
            request.DisplayName,
            request.Description,
            request.ItemType,
            request.BrandManufacturer,
            request.Model,
            request.SerialNumber,
            request.PurchaseDate,
            request.PurchaseValue,
            request.WarrantyEndsOn,
            status ?? DurableItemStatus.Active,
            request.CurrentLocation,
            request.Notes,
            request.LocationId ?? request.StorageSlotId);
        return entry is null ? Results.NotFound() : Results.Ok(entry);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
})
.Produces<DurableItemReadModel>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi();

app.MapPatch("/api/durable-entries/{entryId:guid}/retire", (Guid entryId, RetireDurableEntryRequest request, IInventoryMvpStore store) =>
{
    var entry = store.RetireDurableEntry(entryId, request.Notes);
    return entry is null ? Results.NotFound() : Results.Ok(entry);
})
.Produces<DurableItemReadModel>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi();

app.MapPost("/api/consumable-entries", (CreateConsumableEntryRequest request, IInventoryMvpStore store) =>
{
    try
    {
        var entry = store.AddConsumableEntry(request.ItemDefinitionId, request.Quantity, request.Unit, request.ExpiresOn, request.LocationId ?? request.StorageSlotId);
        return entry is null ? Results.NotFound() : Results.Ok(entry);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
})
.Produces<ConsumableEntry>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi();

app.MapGet("/api/consumable-entries", (IInventoryMvpStore store, IUtcDateProvider utcDateProvider) =>
    Results.Ok(store.GetConsumableEntryReadModels(utcDateProvider.TodayUtc)))
.Produces<IEnumerable<ConsumableEntryReadModel>>(StatusCodes.Status200OK)
.WithOpenApi();

app.MapPatch("/api/consumable-entries/{entryId:guid}", (Guid entryId, UpdateConsumableEntryRequest request, IInventoryMvpStore store, IUtcDateProvider utcDateProvider) =>
{
    try
    {
        var updated = store.UpdateConsumableEntry(entryId, request.Quantity, request.Unit, request.ExpiresOn, request.LocationId ?? request.StorageSlotId, utcDateProvider.TodayUtc);
        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
})
.Produces<ConsumableEntryReadModel>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi();

app.MapGet("/api/inventory/summary", (IInventoryMvpStore store) => Results.Ok(store.GetSummary())).WithOpenApi();

app.MapGet("/api/consumable-entries/expiring", (IInventoryMvpStore store, IUtcDateProvider utcDateProvider) =>
    Results.Ok(store.GetExpiringConsumableEntries(utcDateProvider.TodayUtc))).WithOpenApi();

app.MapGet("/api/replenishment/suggestions", (ReplenishmentSuggestionService suggestionService, IInventoryMvpStore store) =>
{
    var suggestions = suggestionService.GetSuggestions(store.GetRules(), store.GetConsumableEntries());
    return Results.Ok(suggestions);
})
.WithName("GetReplenishmentSuggestions")
.WithOpenApi();

app.MapGet("/api/replenishment/rules", (IInventoryMvpStore store) =>
{
    var rules = store.GetRules().Select(ToReplenishmentRuleResponse);
    return Results.Ok(rules);
})
.Produces<IEnumerable<ReplenishmentRuleResponse>>(StatusCodes.Status200OK)
.WithOpenApi();

app.MapPatch("/api/replenishment/rules/{ruleId:guid}", (Guid ruleId, UpdateReplenishmentRuleRequest request, IInventoryMvpStore store) =>
{
    var updated = store.UpdateRule(ruleId, request.DesiredAmount, request.DesiredUnit, request.IsDisabled, request.ExpiryWarningDays);
    if (updated is null) return Results.NotFound();
    return Results.Ok(ToReplenishmentRuleResponse(updated));
})
.Produces<ReplenishmentRuleResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi();

app.MapPost("/api/shopping-list-items/from-suggestion", (CreateShoppingListItemFromSuggestionRequest request, IInventoryMvpStore store) =>
{
    var item = store.CreateOrUpdateShoppingListItemFromSuggestion(
        request.ItemDefinitionId,
        request.Quantity,
        request.Unit,
        request.DeficitAmount,
        request.ExpiringSoonAmount,
        request.SuggestedPurchaseAmount);
    return Results.Ok(item);
}).WithOpenApi();

app.MapPatch("/api/shopping-list-items/{shoppingListItemId:guid}", (Guid shoppingListItemId, UpdateShoppingListItemStatusRequest request, IInventoryMvpStore store) =>
{
    var item = store.UpdateShoppingListItemStatus(shoppingListItemId, request.IsResolved, request.IsPurchased, request.Status);
    return item is null ? Results.NotFound() : Results.Ok(item);
}).WithOpenApi();

app.MapDelete("/api/shopping-list-items/{shoppingListItemId:guid}", (Guid shoppingListItemId, IInventoryMvpStore store) =>
    store.DeleteShoppingListItem(shoppingListItemId) ? Results.NoContent() : Results.NotFound())
.WithOpenApi();

app.MapGet("/api/shopping-list-items", (IInventoryMvpStore store) => Results.Ok(store.GetShoppingListItems())).WithOpenApi();

app.MapGet("/api/locations", (bool? includeArchived, IInventoryMvpStore store) =>
    Results.Ok(store.GetLocations(includeArchived ?? false)))
    .Produces<IEnumerable<LocationReadModel>>(StatusCodes.Status200OK)
    .WithOpenApi();

app.MapGet("/api/locations/tree", (IInventoryMvpStore store) => Results.Ok(store.GetLocationTree()))
    .Produces<IEnumerable<LocationTreeNodeReadModel>>(StatusCodes.Status200OK)
    .WithOpenApi();

app.MapGet("/api/locations/{locationId:guid}", (Guid locationId, IInventoryMvpStore store, IUtcDateProvider utcDateProvider) =>
{
    var detail = store.GetLocationDetail(locationId, utcDateProvider.TodayUtc);
    return detail is null ? Results.NotFound() : Results.Ok(detail);
})
.Produces<LocationDetailReadModel>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi();

app.MapPost("/api/locations", (CreateLocationRequest request, IInventoryMvpStore store) =>
{
    if (!TryParseLocationType(request.Type, out var type))
    {
        return Results.BadRequest(new ErrorResponse("Unsupported location type."));
    }

    try
    {
        return Results.Ok(store.CreateLocation(request.Name, type, request.ParentLocationId, request.Description, request.SortOrder));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
})
.Produces<LocationReadModel>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.WithOpenApi();

app.MapPatch("/api/locations/{locationId:guid}", (Guid locationId, UpdateLocationRequest request, IInventoryMvpStore store) =>
{
    if (!TryParseLocationType(request.Type, out var type))
    {
        return Results.BadRequest(new ErrorResponse("Unsupported location type."));
    }

    try
    {
        var updated = store.UpdateLocation(locationId, request.Name, type, request.ParentLocationId, request.Description, request.SortOrder);
        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
})
.Produces<LocationReadModel>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi();

app.MapDelete("/api/locations/{locationId:guid}", (Guid locationId, IInventoryMvpStore store) =>
    store.ArchiveLocation(locationId) switch
    {
        LocationArchiveResult.Archived => Results.NoContent(),
        LocationArchiveResult.NotFound => Results.NotFound(),
        LocationArchiveResult.HasChildren => Results.Conflict(new ErrorResponse("Location cannot be archived while it has active child locations.")),
        LocationArchiveResult.HasItems => Results.Conflict(new ErrorResponse("Location cannot be archived while it contains active inventory items.")),
        _ => Results.Problem("Unexpected location archive result.", statusCode: StatusCodes.Status500InternalServerError)
    })
    .Produces(StatusCodes.Status204NoContent)
    .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status500InternalServerError)
    .WithOpenApi();

app.MapGet("/api/search", (string? q, IInventoryMvpStore store) => Results.Ok(store.Search(q ?? string.Empty)))
    .Produces<IEnumerable<GlobalSearchResultReadModel>>(StatusCodes.Status200OK)
    .WithOpenApi();

app.MapGet("/replenishment/suggestions", (ReplenishmentSuggestionService suggestionService, IInventoryMvpStore store) =>
{
    var suggestions = suggestionService.GetSuggestions(store.GetRules(), store.GetConsumableEntries());
    return Results.Ok(suggestions);
})
.WithName("GetReplenishmentSuggestionsLegacy")
.WithOpenApi();

static ReplenishmentRuleResponse ToReplenishmentRuleResponse(ReplenishmentRule rule)
{
    return new ReplenishmentRuleResponse(
        rule.Id,
        rule.ItemDefinitionId,
        rule.TargetAmount.Value,
        rule.Unit.Value,
        rule.ExpiryWarningDays,
        rule.IsDisabled);
}

static bool TryParseDurableStatus(string? status, out DurableItemStatus? parsed)
{
    parsed = null;
    if (string.IsNullOrWhiteSpace(status))
    {
        return true;
    }

    if (Enum.TryParse<DurableItemStatus>(status.Trim(), ignoreCase: true, out var value) &&
        Enum.IsDefined(typeof(DurableItemStatus), value))
    {
        parsed = value;
        return true;
    }

    return false;
}

static bool TryParseLocationType(string? type, out LocationType parsed)
{
    return Enum.TryParse(type?.Trim(), ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
}

app.Run();

public sealed record ErrorResponse([property: Required] string Message);
public sealed record HealthResponse([property: Required] string Status);
public sealed record VersionResponse([property: Required] string Version);
public sealed record CreateItemDefinitionRequest(string Name, ItemKind Kind, decimal? DesiredAmount, string? DesiredUnit);
public sealed record CreateDurableEntryRequest(
    Guid? ItemDefinitionId,
    string? DisplayName,
    string? Description,
    string? ItemType,
    string? BrandManufacturer,
    string? Model,
    string? SerialNumber,
    DateOnly? PurchaseDate,
    decimal? PurchaseValue,
    DateOnly? WarrantyEndsOn,
    string? Status,
    string? CurrentLocation,
    string? Notes,
    Guid? StorageSlotId,
    Guid? LocationId);
public sealed record UpdateDurableEntryRequest(
    string DisplayName,
    string? Description,
    string? ItemType,
    string? BrandManufacturer,
    string? Model,
    string? SerialNumber,
    DateOnly? PurchaseDate,
    decimal? PurchaseValue,
    DateOnly? WarrantyEndsOn,
    string? Status,
    string? CurrentLocation,
    string? Notes,
    Guid? StorageSlotId,
    Guid? LocationId);
public sealed record RetireDurableEntryRequest(string? Notes);
public sealed record CreateConsumableEntryRequest(Guid ItemDefinitionId, decimal Quantity, string Unit, DateOnly? ExpiresOn, Guid? StorageSlotId, Guid? LocationId);
public sealed record UpdateConsumableEntryRequest(decimal Quantity, string Unit, DateOnly? ExpiresOn, Guid? StorageSlotId, Guid? LocationId);
public sealed record CreateLocationRequest(string Name, string Type, Guid? ParentLocationId, string? Description, int? SortOrder);
public sealed record UpdateLocationRequest(string Name, string Type, Guid? ParentLocationId, string? Description, int? SortOrder);
public sealed record CreateShoppingListItemFromSuggestionRequest(Guid ItemDefinitionId, decimal Quantity, string Unit, decimal? DeficitAmount, decimal? ExpiringSoonAmount, decimal? SuggestedPurchaseAmount);
public sealed record UpdateShoppingListItemStatusRequest(bool? IsResolved, bool? IsPurchased, string? Status);
public sealed record UpdateReplenishmentRuleRequest(decimal? DesiredAmount, string? DesiredUnit, bool? IsDisabled, int? ExpiryWarningDays);
public sealed record ReplenishmentRuleResponse(
    [property: Required] Guid Id,
    [property: Required] Guid ItemDefinitionId,
    [property: Required] decimal DesiredAmount,
    [property: Required] string DesiredUnit,
    [property: Required] int ExpiryWarningDays,
    [property: Required] bool IsDisabled);

public partial class Program { }
