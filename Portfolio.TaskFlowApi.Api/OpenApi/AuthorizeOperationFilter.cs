using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Portfolio.TaskFlowApi.Api.OpenApi;

internal sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controllerAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? [];
        var actionAttributes = context.MethodInfo.GetCustomAttributes(true);

        if (controllerAttributes.OfType<AllowAnonymousAttribute>().Any() ||
            actionAttributes.OfType<AllowAnonymousAttribute>().Any())
        {
            return;
        }

        if (!controllerAttributes.OfType<AuthorizeAttribute>().Any() &&
            !actionAttributes.OfType<AuthorizeAttribute>().Any())
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                }] = Array.Empty<string>(),
            },
        ];

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Authentication required" });
    }
}
