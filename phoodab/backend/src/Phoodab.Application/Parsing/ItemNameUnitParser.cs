using Phoodab.Domain;

namespace Phoodab.Application.Parsing;

public static class ItemNameUnitParser
{
    private static readonly Dictionary<string, Unit[]> TokenMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["piece"] = [Unit.Piece],
        ["pack"] = [Unit.Pack],
        ["box"] = [Unit.Box],
        ["kg"] = [Unit.Kg],
        ["g"] = [Unit.G],
        ["l"] = [Unit.L],
        ["ml"] = [Unit.Ml],
        ["bundle"] = [Unit.Bundle],
        ["roll"] = [Unit.Roll],
        ["pk"] = [Unit.Pack],
        ["pcs"] = [Unit.Piece],
        ["pc"] = [Unit.Piece],
        ["gram"] = [Unit.G],
        ["grams"] = [Unit.G],
        ["liter"] = [Unit.L],
        ["litre"] = [Unit.L],
        ["liters"] = [Unit.L],
        ["litres"] = [Unit.L]
    };

    public static ItemNameUnitParseResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ItemNameUnitParseResult(string.Empty, Unit.Unknown, [
                new UnitParseWarning("UnknownUnit", "Input is empty.")
            ]);
        }

        var trimmed = input.Trim();
        var candidate = ExtractTrailingToken(trimmed, '(', ')') ?? ExtractTrailingToken(trimmed, '[', ']');

        if (candidate is null)
        {
            return new ItemNameUnitParseResult(trimmed, Unit.Unknown, [
                new UnitParseWarning("UnknownUnit", "No supported trailing unit pattern found.")
            ]);
        }

        var (name, token) = candidate.Value;

        if (token.Contains('/') || token.Contains('|') || token.Contains(','))
        {
            return new ItemNameUnitParseResult(name, Unit.Unknown, [
                new UnitParseWarning("AmbiguousUnit", $"Ambiguous unit token '{token}'.")
            ]);
        }

        if (!TokenMap.TryGetValue(token.Trim(), out var units) || units.Length == 0)
        {
            return new ItemNameUnitParseResult(name, Unit.Unknown, [
                new UnitParseWarning("UnknownUnit", $"Unknown unit token '{token}'.")
            ]);
        }

        if (units.Length > 1)
        {
            return new ItemNameUnitParseResult(name, Unit.Unknown, [
                new UnitParseWarning("AmbiguousUnit", $"Ambiguous unit token '{token}'.")
            ]);
        }

        return new ItemNameUnitParseResult(name, units[0], []);
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
