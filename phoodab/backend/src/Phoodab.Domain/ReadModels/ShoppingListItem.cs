namespace Phoodab.Domain;

public sealed record ShoppingListItem(Guid ShoppingListId, Guid ItemDefinitionId, Quantity Quantity, Unit Unit);
