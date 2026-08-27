using MediatR;
using QualifyAI.Identity.Domain.AccessControl;

namespace QualifyAI.Identity.Application.AccessControl;

public sealed record ListRolesQuery(Guid? TenantId, bool IncludePlatform = false) : IRequest<IReadOnlyList<AccessRoleDto>>;
public sealed record ListPermissionCatalogQuery() : IRequest<IReadOnlyList<PermissionDefinitionDto>>;
public sealed record ListSecurityAuditQuery(Guid? TenantId, int Take = 250) : IRequest<IReadOnlyList<SecurityAuditDto>>;
public sealed record GetClientPermissionsQuery(Guid ClientApplicationId) : IRequest<IReadOnlyList<string>>;

public sealed record CreateRoleCommand(
    Guid? TenantId,
    string Name,
    string Description,
    AccessRoleScope Scope,
    bool IsSystem,
    Guid? ActorUserId) : IRequest<AccessRoleDto>;

public sealed record SetRolePermissionsCommand(
    Guid RoleId,
    IReadOnlyCollection<string> Permissions,
    Guid? ActorUserId) : IRequest;

public sealed record SetClientPermissionsCommand(
    Guid ClientApplicationId,
    IReadOnlyCollection<string> Permissions,
    Guid? ActorUserId) : IRequest;

public sealed class ListRolesQueryHandler(IAccessControlRepository repository)
    : IRequestHandler<ListRolesQuery, IReadOnlyList<AccessRoleDto>>
{
    public Task<IReadOnlyList<AccessRoleDto>> Handle(ListRolesQuery request, CancellationToken ct)
        => repository.ListRolesAsync(request.TenantId, request.IncludePlatform, ct);
}

public sealed class ListPermissionCatalogQueryHandler(IAccessControlRepository repository)
    : IRequestHandler<ListPermissionCatalogQuery, IReadOnlyList<PermissionDefinitionDto>>
{
    public Task<IReadOnlyList<PermissionDefinitionDto>> Handle(ListPermissionCatalogQuery request, CancellationToken ct)
        => repository.ListPermissionsAsync(ct);
}

public sealed class ListSecurityAuditQueryHandler(IAccessControlRepository repository)
    : IRequestHandler<ListSecurityAuditQuery, IReadOnlyList<SecurityAuditDto>>
{
    public Task<IReadOnlyList<SecurityAuditDto>> Handle(ListSecurityAuditQuery request, CancellationToken ct)
        => repository.ListAuditAsync(request.TenantId, Math.Clamp(request.Take, 1, 1000), ct);
}

public sealed class GetClientPermissionsQueryHandler(IAccessControlRepository repository)
    : IRequestHandler<GetClientPermissionsQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(GetClientPermissionsQuery request, CancellationToken ct)
        => repository.GetClientPermissionsAsync(request.ClientApplicationId, ct);
}

public sealed class CreateRoleCommandHandler(IAccessControlRepository repository)
    : IRequestHandler<CreateRoleCommand, AccessRoleDto>
{
    public Task<AccessRoleDto> Handle(CreateRoleCommand request, CancellationToken ct)
        => repository.CreateRoleAsync(request.TenantId, request.Name, request.Description, request.Scope, request.IsSystem, request.ActorUserId, ct);
}

public sealed class SetRolePermissionsCommandHandler(IAccessControlRepository repository)
    : IRequestHandler<SetRolePermissionsCommand>
{
    public async Task Handle(SetRolePermissionsCommand request, CancellationToken ct)
        => await repository.SetRolePermissionsAsync(request.RoleId, request.Permissions, request.ActorUserId, ct);
}

public sealed class SetClientPermissionsCommandHandler(IAccessControlRepository repository)
    : IRequestHandler<SetClientPermissionsCommand>
{
    public async Task Handle(SetClientPermissionsCommand request, CancellationToken ct)
        => await repository.SetClientPermissionsAsync(request.ClientApplicationId, request.Permissions, request.ActorUserId, ct);
}
