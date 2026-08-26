using MediatR;
using QualifyAI.Knowledge.Application.KnowledgeBases.Queries.GetById;
namespace QualifyAI.Knowledge.Api.Endpoints.KnowledgeBases;
public static class GetKnowledgeBaseEndpoint
{
    public static IEndpointRouteBuilder MapGetKnowledgeBase(this IEndpointRouteBuilder app)
    {
        app.MapGet("/knowledgebases/{id:guid}", async (Guid id, Guid tenantId, ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetKnowledgeBaseByIdQuery(tenantId,id), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });
        return app;
    }
}
