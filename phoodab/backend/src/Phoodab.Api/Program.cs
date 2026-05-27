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

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<IInventoryMvpStore>();
    var utcDateProvider = scope.ServiceProvider.GetRequiredService<IUtcDateProvider>();
    store.EnsureDevelopmentSeedData(utcDateProvider.TodayUtc);
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
    var entry = store.CreateDurableEntry(request.ItemDefinitionId, request.StorageSlotId);
    return entry is null ? Results.NotFound() : Results.Ok(entry);
}).WithOpenApi();

app.MapPost("/api/consumable-entries", (CreateConsumableEntryRequest request, IInventoryMvpStore store) =>
{
    var entry = store.AddConsumableEntry(request.ItemDefinitionId, request.Quantity, request.Unit, request.ExpiresOn, request.StorageSlotId);
    return entry is null ? Results.NotFound() : Results.Ok(entry);
}).WithOpenApi();

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
    var item = store.CreateOrUpdateShoppingListItemFromSuggestion(request.ItemDefinitionId, request.Quantity, request.Unit);
    return Results.Ok(item);
}).WithOpenApi();

app.MapPatch("/api/shopping-list-items/{shoppingListItemId:guid}", (Guid shoppingListItemId, UpdateShoppingListItemStatusRequest request, IInventoryMvpStore store) =>
{
    var item = store.UpdateShoppingListItemStatus(shoppingListItemId, request.IsResolved, request.IsPurchased);
    return item is null ? Results.NotFound() : Results.Ok(item);
}).WithOpenApi();

app.MapGet("/api/shopping-list-items", (IInventoryMvpStore store) => Results.Ok(store.GetShoppingListItems())).WithOpenApi();

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

app.Run();

public sealed record HealthResponse([property: Required] string Status);
public sealed record VersionResponse([property: Required] string Version);
public sealed record CreateItemDefinitionRequest(string Name, ItemKind Kind, decimal? DesiredAmount, string? DesiredUnit);
public sealed record CreateDurableEntryRequest(Guid ItemDefinitionId, Guid? StorageSlotId);
public sealed record CreateConsumableEntryRequest(Guid ItemDefinitionId, decimal Quantity, string Unit, DateOnly? ExpiresOn, Guid? StorageSlotId);
public sealed record CreateShoppingListItemFromSuggestionRequest(Guid ItemDefinitionId, decimal Quantity, string Unit);
public sealed record UpdateShoppingListItemStatusRequest(bool? IsResolved, bool? IsPurchased);
public sealed record UpdateReplenishmentRuleRequest(decimal? DesiredAmount, string? DesiredUnit, bool? IsDisabled, int? ExpiryWarningDays);
public sealed record ReplenishmentRuleResponse(
    [property: Required] Guid Id,
    [property: Required] Guid ItemDefinitionId,
    [property: Required] decimal DesiredAmount,
    [property: Required] string DesiredUnit,
    [property: Required] int ExpiryWarningDays,
    [property: Required] bool IsDisabled);

public partial class Program { }
