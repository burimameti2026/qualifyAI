using MediatR;
using QualifyAI.Knowledge.Application.KnowledgeBases.Commands.Create;
namespace QualifyAI.Knowledge.Api.Endpoints.KnowledgeBases;
public static class CreateKnowledgeBaseEndpoint
{
    public static IEndpointRouteBuilder MapCreateKnowledgeBase(this IEndpointRouteBuilder app)
    {
        app.MapPost("/knowledgebases", async (CreateKnowledgeBaseRequest request, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateKnowledgeBaseCommand(request.TenantId, request.Name), ct);
            return Results.Created($"/knowledgebases/{id}", new { id });
        });
        return app;
    }
}
public sealed record CreateKnowledgeBaseRequest(Guid TenantId, string Name);
