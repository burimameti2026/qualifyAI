using MediatR;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure;

public sealed class CreateKnowledgeDocumentCommandHandler(IKnowledgeAiRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<CreateKnowledgeDocumentCommand, KnowledgeDocument>
{
    public async Task<KnowledgeDocument> Handle(CreateKnowledgeDocumentCommand command, CancellationToken ct)
    {
        if (!await repository.KnowledgeBaseExistsAsync(command.TenantId, command.Document.KnowledgeBaseId, ct))
            throw new InvalidOperationException("Knowledge base was not found for the current tenant.");
        var document = KnowledgeDocument.Create(command.TenantId, command.Document.KnowledgeBaseId, command.Document.SourceId,
            command.Document.Title, command.Document.Body, command.Document.Published);
        repository.AddKnowledgeDocument(document);
        await unitOfWork.SaveChangesAsync(ct);
        return document;
    }
}

public sealed class UpdateKnowledgeDocumentCommandHandler(IKnowledgeAiRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<UpdateKnowledgeDocumentCommand, KnowledgeDocument?>
{
    public async Task<KnowledgeDocument?> Handle(UpdateKnowledgeDocumentCommand command, CancellationToken ct)
    {
        var document = await repository.GetKnowledgeDocumentAsync(command.TenantId, command.Id, ct);
        if (document is null) return null;
        document.UpdateContent(command.Document.Title, command.Document.Body, command.Document.Published);
        await unitOfWork.SaveChangesAsync(ct);
        return document;
    }
}

public sealed class ReindexKnowledgeDocumentCommandHandler(IKnowledgeAiRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<ReindexKnowledgeDocumentCommand, ReindexResult?>
{
    public async Task<ReindexResult?> Handle(ReindexKnowledgeDocumentCommand command, CancellationToken ct)
    {
        var document = await repository.GetKnowledgeDocumentAsync(command.TenantId, command.Id, ct);
        if (document is null) return null;
        var oldChunks = await repository.ListKnowledgeChunksAsync(command.TenantId, command.Id, ct);
        repository.RemoveKnowledgeChunks(oldChunks);
        var newChunks = document.RebuildChunks();
        repository.AddKnowledgeChunks(newChunks);
        await unitOfWork.SaveChangesAsync(ct);
        return new ReindexResult(document.Id, newChunks.Count, "indexed");
    }
}

public sealed class UpdateKnowledgeGapCommandHandler(IKnowledgeAiRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<UpdateKnowledgeGapCommand, KnowledgeGap?>
{
    public async Task<KnowledgeGap?> Handle(UpdateKnowledgeGapCommand command, CancellationToken ct)
    {
        var gap = await repository.GetKnowledgeGapAsync(command.TenantId, command.Id, ct);
        if (gap is null) return null;
        gap.ChangeStatus(command.Gap.Status);
        await unitOfWork.SaveChangesAsync(ct);
        return gap;
    }
}

public sealed class CreateAiAgentCommandHandler(IKnowledgeAiRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<CreateAiAgentCommand, AiAgent>
{
    public async Task<AiAgent> Handle(CreateAiAgentCommand command, CancellationToken ct)
    {
        await EnsureKnowledgeBaseAsync(command.TenantId, command.Agent.KnowledgeBaseId, ct);
        var agent = AiAgent.Create(command.TenantId, command.Agent.Name, command.Agent.Role, command.Agent.Instructions,
            command.Agent.Tone, command.Agent.Model, command.Agent.LanguagesCsv, command.Agent.Active, command.Agent.KnowledgeBaseId);
        repository.AddAiAgent(agent);
        await unitOfWork.SaveChangesAsync(ct);
        return agent;
    }

    private async Task EnsureKnowledgeBaseAsync(Guid tenantId, Guid? knowledgeBaseId, CancellationToken ct)
    {
        if (knowledgeBaseId.HasValue && !await repository.KnowledgeBaseExistsAsync(tenantId, knowledgeBaseId.Value, ct))
            throw new InvalidOperationException("Knowledge base was not found for the current tenant.");
    }
}

public sealed class UpdateAiAgentCommandHandler(IKnowledgeAiRepository repository, IBusinessUnitOfWork unitOfWork)
    : IRequestHandler<UpdateAiAgentCommand, AiAgent?>
{
    public async Task<AiAgent?> Handle(UpdateAiAgentCommand command, CancellationToken ct)
    {
        var agent = await repository.GetAiAgentAsync(command.TenantId, command.Id, ct);
        if (agent is null) return null;
        if (command.Agent.KnowledgeBaseId.HasValue &&
            !await repository.KnowledgeBaseExistsAsync(command.TenantId, command.Agent.KnowledgeBaseId.Value, ct))
            throw new InvalidOperationException("Knowledge base was not found for the current tenant.");
        agent.UpdateConfiguration(command.Agent.Name, command.Agent.Role, command.Agent.Instructions, command.Agent.Tone,
            command.Agent.Model, command.Agent.LanguagesCsv, command.Agent.Active, command.Agent.KnowledgeBaseId);
        await unitOfWork.SaveChangesAsync(ct);
        return agent;
    }
}
