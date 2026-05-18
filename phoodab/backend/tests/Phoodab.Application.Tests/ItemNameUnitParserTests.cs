using Phoodab.Application.Parsing;
using Phoodab.Domain;

namespace Phoodab.Application.Tests;

public class ItemNameUnitParserTests
{
    [TestCase("Apples (kg)", "Apples", "Kg")]
    [TestCase("Dates [kg]", "Dates", "Kg")]
    [TestCase("Spring onions [bundle]", "Spring onions", "Bundle")]
    [TestCase("Milk (ML)", "Milk", "Ml")]
    public void Parse_Recognizes_Bracket_And_Parenthesis_Units_Case_Insensitively(string input, string expectedName, string expectedUnit)
    {
        var result = ItemNameUnitParser.Parse(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo(expectedName));
            Assert.That(result.Unit.Value, Is.EqualTo(expectedUnit));
            Assert.That(result.Unit.Kind, Is.Not.EqualTo(UnitEnum.Unknown));
            Assert.That(result.Warnings, Is.Empty);
        });
    }

    [Test]
    public void Parse_Returns_Unknown_With_Warning_When_Unit_Token_Is_Unknown()
    {
        var result = ItemNameUnitParser.Parse("Tea [scoop]");

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("Tea"));
            Assert.That(result.Unit, Is.EqualTo(Unit.Unknown));
            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Warnings[0].Code, Is.EqualTo("UnknownUnit"));
        });
    }

    [Test]
    public void Parse_Returns_Unknown_With_Warning_When_Unit_Token_Is_Ambiguous()
    {
        var result = ItemNameUnitParser.Parse("Tomatoes [kg/g]");

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("Tomatoes"));
            Assert.That(result.Unit, Is.EqualTo(Unit.Unknown));
            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Warnings[0].Code, Is.EqualTo("AmbiguousUnit"));
        });
    }

    [Test]
    public void Parse_Returns_Unknown_With_Warning_When_No_Trailing_Pattern_Exists()
    {
        var result = ItemNameUnitParser.Parse("Bananas kg");

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("Bananas kg"));
            Assert.That(result.Unit, Is.EqualTo(Unit.Unknown));
            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Warnings[0].Code, Is.EqualTo("UnknownUnit"));
        });
    }
}
