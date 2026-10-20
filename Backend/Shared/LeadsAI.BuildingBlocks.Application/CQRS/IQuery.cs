using MediatR;
namespace LeadsAI.BuildingBlocks.Application.CQRS;
public interface IQuery<out TResponse> : IRequest<TResponse>;
