using MediatR;
namespace LeadsAI.BuildingBlocks.Application.CQRS;
public interface ICommand : IRequest;
public interface ICommand<out TResponse> : IRequest<TResponse>;
