namespace Phoodab.Domain;

public sealed class Home
{
    public Guid Id { get; }

    public Home(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        Id = id;
    }
}
