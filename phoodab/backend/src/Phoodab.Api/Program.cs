using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("GetHealth")
    .WithOpenApi();

app.MapGet("/version", () =>
{
    var assembly = Assembly.GetEntryAssembly();
    var version = assembly?.GetName().Version?.ToString() ?? "0.0.0";
    var informationalVersion = assembly?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? version;

    return Results.Ok(new
    {
        version,
        informationalVersion
    });
})
.WithName("GetVersion")
.WithOpenApi();

app.Run();
