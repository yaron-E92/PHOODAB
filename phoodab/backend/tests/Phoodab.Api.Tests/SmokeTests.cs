using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

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
    public async Task Replenishment_Suggestions_Endpoint_Returns_Ok_With_Expected_Shape_And_Values()
    {
        var response = await _client.GetAsync("/replenishment/suggestions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);

        Assert.That(json.ValueKind, Is.EqualTo(JsonValueKind.Array));

        var suggestions = json.EnumerateArray().ToList();
        Assert.That(suggestions.Count, Is.EqualTo(2));

        var milk = suggestions.Single(s => s.GetProperty("itemName").GetString() == "Milk");
        Assert.That(milk.GetProperty("itemDefinitionId").GetString(), Is.Not.Null.And.Not.Empty);
        Assert.That(milk.GetProperty("currentQuantity").GetDecimal(), Is.EqualTo(1));
        Assert.That(milk.GetProperty("desiredQuantity").GetDecimal(), Is.EqualTo(2));
        Assert.That(milk.GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(1));
        Assert.That(milk.GetProperty("unit").GetString(), Is.EqualTo("liter"));

        var beans = suggestions.Single(s => s.GetProperty("itemName").GetString() == "Beans");
        Assert.That(beans.GetProperty("itemDefinitionId").GetString(), Is.Not.Null.And.Not.Empty);
        Assert.That(beans.GetProperty("currentQuantity").GetDecimal(), Is.EqualTo(2));
        Assert.That(beans.GetProperty("desiredQuantity").GetDecimal(), Is.EqualTo(2));
        Assert.That(beans.GetProperty("requiredAmount").GetDecimal(), Is.EqualTo(0));
        Assert.That(beans.GetProperty("unit").GetString(), Is.EqualTo("can"));
    }
}
