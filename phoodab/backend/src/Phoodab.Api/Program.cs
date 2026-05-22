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

app.UseCors("FrontendDev");

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("GetHealth")
    .WithOpenApi();

app.MapGet("/version", () =>
{
    var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    return Results.Ok(new { version });
})
.WithName("GetVersion")
.WithOpenApi();

app.MapPost("/api/item-definitions", (CreateItemDefinitionRequest request, IInventoryMvpStore store) =>
{
    var item = store.CreateItemDefinition(request.Name, request.Kind);
    return Results.Ok(item);
}).WithOpenApi();

app.MapPost("/api/inventory-entries", (CreateInventoryEntryRequest request, IInventoryMvpStore store) =>
{
    var entry = store.CreateInventoryEntry(request.ItemDefinitionId, request.StorageSlotId);
    return entry is null ? Results.NotFound() : Results.Ok(entry);
}).WithOpenApi();

app.MapPost("/api/inventory-lots", (CreateInventoryLotRequest request, IInventoryMvpStore store) =>
{
    var lot = store.AddInventoryLot(request.InventoryEntryId, request.Quantity, request.Unit, request.ExpiresOn, request.StorageSlotId);
    return lot is null ? Results.NotFound() : Results.Ok(lot);
}).WithOpenApi();

app.MapGet("/api/inventory/summary", (IInventoryMvpStore store) => Results.Ok(store.GetSummary())).WithOpenApi();

app.MapGet("/api/inventory/expiring", (IInventoryMvpStore store, IUtcDateProvider utcDateProvider) =>
    Results.Ok(store.GetExpiringLots(utcDateProvider.TodayUtc))).WithOpenApi();

app.MapGet("/api/replenishment/suggestions", (ReplenishmentSuggestionService suggestionService, IInventoryMvpStore store) =>
{
    var suggestions = suggestionService.GetSuggestions(store.GetRules(), store.GetInventoryEntries());
    return Results.Ok(suggestions);
})
.WithName("GetReplenishmentSuggestions")
.WithOpenApi();

app.MapGet("/replenishment/suggestions", (ReplenishmentSuggestionService suggestionService, IInventoryMvpStore store) =>
{
    var suggestions = suggestionService.GetSuggestions(store.GetRules(), store.GetInventoryEntries());
    return Results.Ok(suggestions);
})
.WithName("GetReplenishmentSuggestionsLegacy")
.WithOpenApi();

app.Run();

public sealed record CreateItemDefinitionRequest(string Name, ItemKind Kind);
public sealed record CreateInventoryEntryRequest(Guid ItemDefinitionId, Guid? StorageSlotId);
public sealed record CreateInventoryLotRequest(Guid InventoryEntryId, decimal Quantity, string Unit, DateOnly? ExpiresOn, Guid? StorageSlotId);

public partial class Program { }
