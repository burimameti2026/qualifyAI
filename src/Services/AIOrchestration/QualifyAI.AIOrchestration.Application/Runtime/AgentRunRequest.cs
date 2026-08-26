namespace QualifyAI.AIOrchestration.Application.Runtime;
public sealed record AgentRunRequest(Guid TenantId, Guid AgentId, Guid ConversationId, string UserMessage);
