using QualifyAI.BuildingBlocks.Messaging;

namespace QualifyAI.Contracts.Identity;

public sealed record TenantCreatedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid TenantId,
    string TenantSlug,
    string TenantName,
    string ContactEmail)
    : IntegrationEvent(EventId, OccurredAtUtc);

public sealed record TenantStatusChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid TenantId,
    string TenantSlug,
    string Status)
    : IntegrationEvent(EventId, OccurredAtUtc);

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
    : IntegrationEvent(EventId, OccurredAtUtc);

public sealed record UserAccessChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid TenantId,
    Guid UserId,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions)
    : IntegrationEvent(EventId, OccurredAtUtc);
