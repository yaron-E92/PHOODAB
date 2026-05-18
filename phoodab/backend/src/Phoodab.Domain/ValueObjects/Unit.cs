namespace Phoodab.Domain;

public readonly record struct Unit
{
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
