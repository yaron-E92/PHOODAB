namespace Phoodab.Domain;

public enum UnitEnum
{
    Piece,
    Pack,
    Box,
    Kg,
    G,
    L,
    Ml,
    Bundle,
    Roll,
    Unknown
}

public readonly record struct Unit
{
    public const string PieceValue = "Piece";
    public const string PackValue = "Pack";
    public const string BoxValue = "Box";
    public const string KgValue = "Kg";
    public const string GValue = "G";
    public const string LValue = "L";
    public const string MlValue = "Ml";
    public const string BundleValue = "Bundle";
    public const string RollValue = "Roll";
    public const string UnknownValue = "Unknown";

    public static readonly Unit Piece = new(PieceValue);
    public static readonly Unit Pack = new(PackValue);
    public static readonly Unit Box = new(BoxValue);
    public static readonly Unit Kg = new(KgValue);
    public static readonly Unit G = new(GValue);
    public static readonly Unit L = new(LValue);
    public static readonly Unit Ml = new(MlValue);
    public static readonly Unit Bundle = new(BundleValue);
    public static readonly Unit Roll = new(RollValue);
    public static readonly Unit Unknown = new(UnknownValue);

    public string Value { get; }

    public UnitEnum Kind => Value.ToLowerInvariant() switch
    {
        "piece" => UnitEnum.Piece,
        "pack" => UnitEnum.Pack,
        "box" => UnitEnum.Box,
        "kg" => UnitEnum.Kg,
        "g" => UnitEnum.G,
        "l" => UnitEnum.L,
        "ml" => UnitEnum.Ml,
        "bundle" => UnitEnum.Bundle,
        "roll" => UnitEnum.Roll,
        _ => UnitEnum.Unknown
    };

    public Unit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Unit is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public static Unit From(UnitEnum unit) => unit switch
    {
        UnitEnum.Piece => Piece,
        UnitEnum.Pack => Pack,
        UnitEnum.Box => Box,
        UnitEnum.Kg => Kg,
        UnitEnum.G => G,
        UnitEnum.L => L,
        UnitEnum.Ml => Ml,
        UnitEnum.Bundle => Bundle,
        UnitEnum.Roll => Roll,
        _ => Unknown
    };

    public override string ToString() => Value;
}
