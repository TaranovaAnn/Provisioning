namespace Shared.Contracts;

public enum PlanType
{
    Standard = 0,
    Enterprise = 1
}

public record RequestCreated(
    Guid RequestId,
    string CustomerEmail,
    PlanType Plan,
    string Region,
    DateTimeOffset CreatedAt);

public record ApprovalRequested(
    Guid ApprovalId,
    Guid RequestId,
    string CustomerEmail,
    string Region,
    DateTimeOffset RequestedAt);

public record ApprovalDecided(
    Guid ApprovalId,
    Guid RequestId,
    bool Approved,
    string DecidedBy,
    string? Comment,
    DateTimeOffset DecidedAt);

public record ProvisionCompleted(
    Guid RequestId,
    string CustomerEmail,
    PlanType Plan,
    string Region,
    DateTimeOffset CompletedAt);

public record ProvisionFailed(
    Guid RequestId,
    string CustomerEmail,
    string Reason,
    DateTimeOffset FailedAt);

public record RequestStatusChanged(
    Guid RequestId,
    string Status,
    DateTimeOffset ChangedAt);
