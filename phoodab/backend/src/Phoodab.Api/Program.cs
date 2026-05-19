using Phoodab.Application;
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

builder.Services.AddSingleton<ReplenishmentSuggestionService>();
builder.Services.AddSingleton<IReplenishmentReadData, InMemoryReplenishmentReadData>();

app.MapGet("/replenishment/suggestions", (ReplenishmentSuggestionService suggestionService, IReplenishmentReadData readData) =>
{
    var suggestions = suggestionService.GetSuggestions(readData.GetRules(), readData.GetInventoryEntries());
    return Results.Ok(suggestions);
})
.WithName("GetReplenishmentSuggestions")
.WithOpenApi();

app.Run();

public partial class Program { }
