using Phoodab.Application;
using Phoodab.Domain;
using Phoodab.Infrastructure.Eventing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPhoodabEventing();

var app = builder.Build();

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


var suggestionService = new ReplenishmentSuggestionService();

app.MapGet("/replenishment/suggestions", () =>
{
    var milk = new ItemDefinition(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Milk", ItemKind.Consumable);
    var beans = new ItemDefinition(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Beans", ItemKind.Consumable);

    var milkEntry = new InventoryEntry(Guid.NewGuid(), milk);
    milkEntry.AddLot(new InventoryLot(Guid.NewGuid(), milk.Id, Quantity.From(1), new Unit("liter"), null));

    var beansEntry = new InventoryEntry(Guid.NewGuid(), beans);
    beansEntry.AddLot(new InventoryLot(Guid.NewGuid(), beans.Id, Quantity.From(3), new Unit("can"), null));

    var rules = new[]
    {
        new ReplenishmentRule(Guid.NewGuid(), milk.Id, Quantity.From(2), new Unit("liter")),
        new ReplenishmentRule(Guid.NewGuid(), beans.Id, Quantity.From(2), new Unit("can"))
    };

    var suggestions = suggestionService.GetSuggestions(rules, new[] { milkEntry, beansEntry });
    return Results.Ok(suggestions);
})
.WithName("GetReplenishmentSuggestions")
.WithOpenApi();

app.Run();

public partial class Program { }
