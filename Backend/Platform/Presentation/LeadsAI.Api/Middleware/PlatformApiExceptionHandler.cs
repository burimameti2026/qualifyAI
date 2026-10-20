using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LeadsAI.Api;

public sealed class PlatformApiExceptionHandler(ILogger<PlatformApiExceptionHandler> logger) : IExceptionHandler
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
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(y => y.ErrorMessage).Distinct().ToArray())),

            KeyNotFoundException missing => Problem(
                StatusCodes.Status404NotFound,
                "Resource not found",
                missing.Message,
                "resource_not_found"),

            UnauthorizedAccessException denied => Problem(
                StatusCodes.Status403Forbidden,
                "Action not allowed",
                denied.Message,
                "forbidden"),

            ArgumentException invalid => Problem(
                StatusCodes.Status400BadRequest,
                "Invalid request",
                invalid.Message,
                "invalid_request"),

            InvalidOperationException business => Problem(
                StatusCodes.Status409Conflict,
                "Action cannot be completed",
                business.Message,
                "business_rule"),

            _ => null
        };

        if (response is null)
        {
            return false;
        }

        var traceId = context.TraceIdentifier;
        response.Extensions["traceId"] = traceId;

        logger.LogWarning(
            exception,
            "Platform request rejected with status {StatusCode}, code {Code}, traceId {TraceId}.",
            response.Status,
            response.Extensions.TryGetValue("code", out var code) ? code : null,
            traceId);

        context.Response.StatusCode = response.Status ?? StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    private static HttpValidationProblemDetails Validation(IReadOnlyDictionary<string, string[]> errors)
    {
        var response = new HttpValidationProblemDetails(
            errors.ToDictionary(x => x.Key, x => x.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request validation failed",
            Detail = string.Join("; ", errors.Values.SelectMany(x => x))
        };

        response.Extensions["code"] = "validation";
        return response;
    }

    private static ProblemDetails Problem(
        int status,
        string title,
        string detail,
        string code)
    {
        var response = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };

        response.Extensions["code"] = code;
        return response;
    }
}
