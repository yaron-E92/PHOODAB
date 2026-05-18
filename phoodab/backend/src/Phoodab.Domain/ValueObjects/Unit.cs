namespace Phoodab.Domain;

public readonly record struct Unit
{
    public static readonly Unit Piece = new("Piece");
    public static readonly Unit Pack = new("Pack");
    public static readonly Unit Box = new("Box");
    public static readonly Unit Kg = new("Kg");
    public static readonly Unit G = new("G");
    public static readonly Unit L = new("L");
    public static readonly Unit Ml = new("Ml");
    public static readonly Unit Bundle = new("Bundle");
    public static readonly Unit Roll = new("Roll");
    public static readonly Unit Unknown = new("Unknown");

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
