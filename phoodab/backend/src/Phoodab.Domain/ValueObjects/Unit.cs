namespace Phoodab.Domain;

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

    public Unit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Unit is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
