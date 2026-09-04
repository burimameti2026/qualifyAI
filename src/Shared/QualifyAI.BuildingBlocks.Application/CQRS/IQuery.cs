using MediatR;
namespace QualifyAI.BuildingBlocks.Application.CQRS;
public interface IQuery<out TResponse> : IRequest<TResponse>;
