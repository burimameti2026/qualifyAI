using QualifyAI.BuildingBlocks.Messaging;

namespace QualifyAI.Contracts.Identity;

public sealed record TenantCreatedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid TenantId,
    string TenantSlug,
    string TenantName,
    string ContactEmail)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, EventId);

public sealed record TenantStatusChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid TenantId,
    string TenantSlug,
    string Status)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, EventId);

public sealed record TenantLicenseChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid TenantId,
    Guid LicenseId,
    string Plan,
    string Status,
    int MaxUsers,
    DateTime StartsAtUtc,
    DateTime? ExpiresAtUtc,
    long Version,
    IReadOnlyCollection<string> Modules)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, EventId)
{
    public string TenantSlug
    {
        get;
        set;
    }
}

public sealed record UserAccessChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid TenantId,
    Guid UserId,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, EventId);
