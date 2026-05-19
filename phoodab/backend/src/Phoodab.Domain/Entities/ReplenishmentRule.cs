namespace Phoodab.Domain;

public sealed class ReplenishmentRule
{
    public Guid Id { get; }
    public Guid ItemDefinitionId { get; }
    public Quantity TargetAmount { get; }
    public Unit Unit { get; }
    public int ExpiryWarningDays { get; }
    public bool IsHidden { get; }
    public bool IsDisabled { get; }

    public ReplenishmentRule(
        Guid id,
        Guid itemDefinitionId,
        Quantity targetAmount,
        Unit unit,
        int expiryWarningDays = 0,
        bool isHidden = false,
        bool isDisabled = false)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (itemDefinitionId == Guid.Empty)
        {
            throw new ArgumentException("Item definition id is required.", nameof(itemDefinitionId));
        }

        if (expiryWarningDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expiryWarningDays), "Expiry warning days cannot be negative.");
        }

        Id = id;
        ItemDefinitionId = itemDefinitionId;
        TargetAmount = targetAmount;
        Unit = unit;
        ExpiryWarningDays = expiryWarningDays;
        IsHidden = isHidden;
        IsDisabled = isDisabled;
    }

    public Quantity GetRequiredAmount(Quantity currentAmount)
    {
        var required = TargetAmount.Value - currentAmount.Value;
        return Quantity.From(required <= 0 ? 0 : required);
    }

    public bool IsExpiryWarning(DateOnly? expiresOn, DateOnly today)
    {
        if (expiresOn is null)
        {
            return false;
        }

        return expiresOn.Value.DayNumber - today.DayNumber <= ExpiryWarningDays;
    }
}
