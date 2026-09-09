using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Portfolio.TaskFlowApi.Core.Exceptions;

namespace Portfolio.TaskFlowApi.Api.ErrorHandling;

internal sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            DomainValidationException domainException =>
                (StatusCodes.Status400BadRequest, "Validation failed", domainException.Message),
            InvalidCredentialsException credentialsException =>
                (StatusCodes.Status401Unauthorized, "Authentication failed", credentialsException.Message),
            CurrentUserUnavailableException currentUserException =>
                (StatusCodes.Status401Unauthorized, "Authentication failed", currentUserException.Message),
            DuplicateEmailException duplicateEmailException =>
                (StatusCodes.Status409Conflict, "Email already registered", duplicateEmailException.Message),
            BadHttpRequestException badRequestException =>
                (StatusCodes.Status400BadRequest, "Invalid request", badRequestException.Message),
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "Update conflict", "The resource was changed or removed by another operation."),
            DbUpdateException =>
                (StatusCodes.Status409Conflict, "Database conflict", "The request conflicts with the current database state."),
            _ =>
                (StatusCodes.Status500InternalServerError, "Unexpected server error", "An unexpected error occurred."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, httpContext.Request.Method, httpContext.Request.Path, exception);
        }
        else
        {
            LogRequestFailure(logger, status, exception);
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problemDetails, cancellationToken: cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing {Method} {Path}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        string method,
        string path,
        Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request failed with status {StatusCode}")]
    private static partial void LogRequestFailure(ILogger logger, int statusCode, Exception exception);
}
