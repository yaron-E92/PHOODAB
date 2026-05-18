namespace Phoodab.Domain;

public sealed class ItemDefinition
{
    public Guid Id { get; }
    public string Name { get; }
    public ItemKind Kind { get; }

    public ItemDefinition(Guid id, string name, ItemKind kind)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        Kind = kind;
    }
}
