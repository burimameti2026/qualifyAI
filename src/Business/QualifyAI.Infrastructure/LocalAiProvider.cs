using QualifyAI.Application;
namespace QualifyAI.Infrastructure;
public sealed class LocalAiProvider:IAiProvider { public Task<string> CompleteAsync(string system,string user,CancellationToken ct=default)=>Task.FromResult($"I can help with that. Based on the configured knowledge, you said: {user}"); }
