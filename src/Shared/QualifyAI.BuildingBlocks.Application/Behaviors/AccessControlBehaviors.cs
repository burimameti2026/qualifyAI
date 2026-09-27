using System.Reflection;
using MediatR;
using QualifyAI.BuildingBlocks.Application.Security;

namespace QualifyAI.BuildingBlocks.Application.Behaviors;

internal static class AccessRequestMetadata
{
    public static AccessControlAttribute? Policy<TRequest>(TRequest request)
        => request?.GetType().GetCustomAttribute<AccessControlAttribute>();

    public static Guid TenantId<TRequest>(TRequest request)
    {
        var property = request?.GetType().GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance);
        if (property?.PropertyType != typeof(Guid))
            throw new InvalidOperationException($"Secured request '{typeof(TRequest).Name}' must expose a public Guid TenantId property.");

        return (Guid)(property.GetValue(request) ?? Guid.Empty);
    }
}

public sealed class TenantValidationBehavior<TRequest, TResponse>(IRequestSecurityContext security)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (AccessRequestMetadata.Policy(request) is null)
            return await next();

        if (!security.IsAuthenticated)
            throw new RequestAccessDeniedException("authentication_required");

        var requestTenantId = AccessRequestMetadata.TenantId(request);
        if (requestTenantId == Guid.Empty || !security.TenantId.HasValue || security.TenantId.Value != requestTenantId)
            throw new RequestAccessDeniedException("tenant_mismatch");

        return await next();
    }
}

public sealed class PermissionAuthorizationBehavior<TRequest, TResponse>(IRequestSecurityContext security)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var policy = AccessRequestMetadata.Policy(request);
        if (policy is not null && !string.IsNullOrWhiteSpace(policy.Permission) && !security.HasPermission(policy.Permission))
            throw new RequestAccessDeniedException($"permission_required:{policy.Permission}");

        return await next();
    }
}

public sealed class LicenseEntitlementBehavior<TRequest, TResponse>(IRequestSecurityContext security)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (AccessRequestMetadata.Policy(request) is null)
            return await next();

        var tenantId = AccessRequestMetadata.TenantId(request);
        var entitlement = await security.GetEntitlementAsync(tenantId, ct);
        if (entitlement is null || !entitlement.IsAccessible)
            throw new RequestAccessDeniedException("license_inactive");

        if (security.LicenseVersion.HasValue && security.LicenseVersion.Value < entitlement.Version)
            throw new RequestAccessDeniedException("token_license_version_stale");

        return await next();
    }
}

public sealed class ModuleEntitlementBehavior<TRequest, TResponse>(IRequestSecurityContext security)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var policy = AccessRequestMetadata.Policy(request);
        if (policy is null || string.IsNullOrWhiteSpace(policy.Module))
            return await next();

        var entitlement = await security.GetEntitlementAsync(AccessRequestMetadata.TenantId(request), ct);
        if (entitlement is null || !entitlement.HasModule(policy.Module))
            throw new RequestAccessDeniedException($"module_required:{policy.Module}");

        return await next();
    }
}
