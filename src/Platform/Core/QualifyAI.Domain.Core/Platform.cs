namespace QualifyAI.Domain;
public class Tenant : Entity { public string Name { get; set; }=""; public string Slug { get; set; }=""; public bool IsActive { get; set; }=true; public string PlanCode { get; set; }="starter"; }
public class AppUser : TenantEntity { public string Email { get; set; }=""; public string DisplayName { get; set; }=""; public string PasswordHash { get; set; }=""; public bool IsActive { get; set; }=true; }
public class Role : TenantEntity { public string Name { get; set; }=""; public string Description { get; set; }=""; }
public class Permission : Entity { public string Code { get; set; }=""; public string Description { get; set; }=""; }
public class UserRole : TenantEntity { public Guid UserId { get; set; } public Guid RoleId { get; set; } }
public class RolePermission : TenantEntity { public Guid RoleId { get; set; } public Guid PermissionId { get; set; } }
public class AuditLog : TenantEntity { public Guid? UserId { get; set; } public string Action { get; set; }=""; public string EntityType { get; set; }=""; public string EntityId { get; set; }=""; public string DataJson { get; set; }="{}"; }
public class ApiKey : TenantEntity { public string Name { get; set; }=""; public string KeyHash { get; set; }=""; public DateTime? ExpiresAtUtc { get; set; } public bool Revoked { get; set; } }
public class Notification : TenantEntity { public Guid? UserId { get; set; } public string Title { get; set; }=""; public string Body { get; set; }=""; public bool IsRead { get; set; }
    public string Message { get; set; }="";
    public string Type { get; set; }="";
}
public class TenantSetting : TenantEntity { public string Key { get; set; }=""; public string Value { get; set; }=""; }
