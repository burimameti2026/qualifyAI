using QualifyAI.Domain;
namespace QualifyAI.Application;
public record CurrentTenant(Guid Id, string Slug);
public interface ITenantContext { CurrentTenant? Current { get; } void Set(CurrentTenant tenant); }
public interface IPasswordService { string Hash(string value); bool Verify(string hash, string value); }
public interface ITokenService { string Create(AppUser user, Tenant tenant, IEnumerable<string> roles, IEnumerable<string> permissions); }
public interface IAiProvider { Task<string> CompleteAsync(string system, string user, CancellationToken ct=default); }
public record KnowledgeHit(Guid DocumentId,string Title,string Text,double Score);
public interface IKnowledgeRetriever { Task<IReadOnlyList<KnowledgeHit>> SearchAsync(Guid tenantId,string query,int take=5,CancellationToken ct=default); }
public record AiToolContext(Guid TenantId, Guid? UserId, Guid? ConversationId);
public record AiToolResult(bool Success,string Json,string? Error=null);
public interface IAiTool { string Name { get; } Task<AiToolResult> ExecuteAsync(AiToolContext context,string inputJson,CancellationToken ct=default); }
public interface IAiToolRegistry { IEnumerable<string> Names { get; } IAiTool? Resolve(string name); }
public interface IIntegrationProvider { string Provider { get; } Task<bool> TestAsync(string settingsJson,CancellationToken ct=default); Task<string> PushAsync(string entityType,string payloadJson,CancellationToken ct=default); }
public interface IIntegrationRegistry { IEnumerable<string> Providers { get; } IIntegrationProvider? Resolve(string provider); }
