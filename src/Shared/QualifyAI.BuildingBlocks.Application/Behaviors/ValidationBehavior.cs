using FluentValidation;
using MediatR;
namespace QualifyAI.BuildingBlocks.Application.Behaviors;
public sealed class ValidationBehavior<TRequest,TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest,TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct))))
                .SelectMany(x => x.Errors).Where(x => x is not null).ToArray();
            if (failures.Length > 0) throw new ValidationException(failures);
        }
        return await next();
    }
}
