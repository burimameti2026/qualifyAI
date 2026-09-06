using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Identity.Application;

namespace QualifyAI.Identity.Api;

public sealed class IdentityApiExceptionHandler(ILogger<IdentityApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var response = exception switch
        {
            ValidationException validation => Validation(
                validation.Errors
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.PropertyName) ? "request" : x.PropertyName)
                    .ToDictionary(x => x.Key, x => x.Select(y => y.ErrorMessage).Distinct().ToArray())),
            IdentityValidationException validation => Validation(validation.Errors),
            IdentityConflictException conflict => Problem(
                StatusCodes.Status409Conflict, "Identity operation conflict", conflict.Message),
            KeyNotFoundException missing => Problem(
                StatusCodes.Status404NotFound, "Identity resource not found", missing.Message),
            ArgumentException invalid => Problem(
                StatusCodes.Status400BadRequest, "Invalid identity request", invalid.Message),
            _ => null
        };

        if (response is null) return false;

        logger.LogWarning(exception, "Identity request rejected with status {StatusCode}.", response.Status);
        context.Response.StatusCode = response.Status ?? StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    private static HttpValidationProblemDetails Validation(IReadOnlyDictionary<string, string[]> errors)
        => new(errors.ToDictionary(x => x.Key, x => x.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Identity request validation failed",
            Detail = string.Join("; ", errors.Values.SelectMany(x => x))
        };

    private static ProblemDetails Problem(int status, string title, string detail)
        => new() { Status = status, Title = title, Detail = detail };
}
