namespace Phoodab.Domain;

public readonly record struct Quantity(decimal Value)
{
    public static Quantity From(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Quantity cannot be negative.");
        }

        return new Quantity(value);
    }
}
