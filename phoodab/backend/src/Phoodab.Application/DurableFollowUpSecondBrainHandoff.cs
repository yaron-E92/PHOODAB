using Phoodab.Domain;

namespace Phoodab.Application;

public enum DurableFollowUpCandidateType
{
    Repair = 0,
    WarrantyCheck = 1,
    Service = 2,
    Clean = 3,
    ReplacePart = 4,
    InspectCondition = 5
}

public enum DurableFollowUpUrgencyHint
{
    Unspecified = 0,
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

public sealed record DurableFollowUpSecondBrainHandoff(
    Guid DurableEntryId,
    Guid ItemDefinitionId,
    string DisplayName,
    DurableItemStatus CurrentStatus,
    DurableFollowUpCandidateType FollowUpType,
    DateOnly? DueOn = null,
    DurableFollowUpUrgencyHint UrgencyHint = DurableFollowUpUrgencyHint.Unspecified,
    string? Notes = null);
