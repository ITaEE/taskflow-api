using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Portfolio.TaskFlowApi.Api.Authentication;

internal sealed class JwtBearerProblemDetailsEvents : JwtBearerEvents
{
    public override Task Challenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        return WriteProblemDetailsAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            "Authentication required",
            "A valid bearer access token is required.");
    }

    public override Task Forbidden(ForbiddenContext context) =>
        WriteProblemDetailsAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "Access forbidden",
            "The authenticated user is not permitted to perform this operation.");

    private static Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        return JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
            },
            cancellationToken: httpContext.RequestAborted);
    }
}
