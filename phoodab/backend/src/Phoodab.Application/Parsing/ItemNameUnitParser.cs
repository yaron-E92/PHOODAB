using Phoodab.Domain;

namespace Phoodab.Application.Parsing;

public static class ItemNameUnitParser
{
    private const string UnknownUnitCode = "UnknownUnit";
    private const string AmbiguousUnitCode = "AmbiguousUnit";
    private const string EmptyInputMessage = "Input is empty.";
    private const string MissingPatternMessage = "No supported trailing unit pattern found.";
    private const string AmbiguousUnitMessageTemplate = "Ambiguous unit token '{0}'.";
    private const string UnknownUnitMessageTemplate = "Unknown unit token '{0}'.";

    private const char ParenthesisOpen = '(';
    private const char ParenthesisClose = ')';
    private const char BracketOpen = '[';
    private const char BracketClose = ']';
    private const char AmbiguousSlash = '/';
    private const char AmbiguousPipe = '|';
    private const char AmbiguousComma = ',';

    private static readonly Dictionary<string, Unit> TokenMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["piece"] = Unit.Piece,
        ["pack"] = Unit.Pack,
        ["box"] = Unit.Box,
        ["kg"] = Unit.Kg,
        ["g"] = Unit.G,
        ["l"] = Unit.L,
        ["ml"] = Unit.Ml,
        ["bundle"] = Unit.Bundle,
        ["roll"] = Unit.Roll,
        ["pk"] = Unit.Pack,
        ["pcs"] = Unit.Piece,
        ["pc"] = Unit.Piece,
        ["gram"] = Unit.G,
        ["grams"] = Unit.G,
        ["liter"] = Unit.L,
        ["litre"] = Unit.L,
        ["liters"] = Unit.L,
        ["litres"] = Unit.L
    };

    public static ItemNameUnitParseResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ItemNameUnitParseResult(string.Empty, Unit.Unknown, [new UnitParseWarning(UnknownUnitCode, EmptyInputMessage)]);
        }

        var trimmed = input.Trim();
        var candidate = ExtractTrailingToken(trimmed, ParenthesisOpen, ParenthesisClose)
            ?? ExtractTrailingToken(trimmed, BracketOpen, BracketClose);

        if (candidate is null)
        {
            return new ItemNameUnitParseResult(trimmed, Unit.Unknown, [new UnitParseWarning(UnknownUnitCode, MissingPatternMessage)]);
        }

        var (name, token) = candidate.Value;

        if (token.Contains(AmbiguousSlash) || token.Contains(AmbiguousPipe) || token.Contains(AmbiguousComma))
        {
            return new ItemNameUnitParseResult(name, Unit.Unknown, [new UnitParseWarning(AmbiguousUnitCode, string.Format(AmbiguousUnitMessageTemplate, token))]);
        }

        if (!TokenMap.TryGetValue(token.Trim(), out var unit))
        {
            return new ItemNameUnitParseResult(name, Unit.Unknown, [new UnitParseWarning(UnknownUnitCode, string.Format(UnknownUnitMessageTemplate, token))]);
        }

        return new ItemNameUnitParseResult(name, unit, []);
    }

    private static (string Name, string Token)? ExtractTrailingToken(string value, char open, char close)
    {
        if (!value.EndsWith(close))
        {
            return null;
        }

        var openIndex = value.LastIndexOf(open);
        if (openIndex <= 0)
        {
            return null;
        }

        var token = value.Substring(openIndex + 1, value.Length - openIndex - 2).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var name = value[..openIndex].TrimEnd();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return (name, token);
    }
}

public sealed record ItemNameUnitParseResult(string Name, Unit Unit, IReadOnlyList<UnitParseWarning> Warnings);

public sealed record UnitParseWarning(string Code, string Message);
