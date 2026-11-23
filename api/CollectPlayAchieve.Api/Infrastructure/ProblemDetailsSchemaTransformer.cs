using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CollectPlayAchieve.Api.Infrastructure;

internal sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            context.JsonTypeInfo.Type != typeof(ProblemDetails)
            && context.JsonTypeInfo.Type != typeof(HttpValidationProblemDetails)
        )
        {
            return Task.CompletedTask;
        }

        schema.Description = "Details of an application error.";
        if (schema.Properties is not null)
        {
            SetDescription(schema, "type", "URI identifying the type of problem.");
            SetDescription(schema, "title", "Application error code.");
            SetDescription(schema, "status", "HTTP status code.");
            SetDescription(schema, "detail", "Human-readable description of the error.");
            SetDescription(schema, "instance", "URI identifying this occurrence, when available.");
            SetDescription(schema, "errors", "Validation messages grouped by field name.");
            schema.Properties["traceId"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Trace identifier associated with the request.",
            };
        }

        return Task.CompletedTask;
    }

    private static void SetDescription(OpenApiSchema schema, string name, string description)
    {
        if (
            schema.Properties?.TryGetValue(name, out var property) == true
            && property is OpenApiSchema value
        )
        {
            value.Description = description;
        }
    }
}
