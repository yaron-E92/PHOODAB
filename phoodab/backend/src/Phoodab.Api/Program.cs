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
    var suggestions = suggestionService.GetSuggestions(Array.Empty<ReplenishmentRule>(), Array.Empty<InventoryEntry>());
    return Results.Ok(suggestions);
})
.WithName("GetReplenishmentSuggestions")
.WithOpenApi();

app.Run();

public partial class Program { }
