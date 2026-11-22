using System.Text.Json;
using System.Text.RegularExpressions;

namespace CollectPlayAchieve.Api.Tests;

internal sealed partial class OpenApiDocumentationValidator(JsonElement root)
{
    private static readonly HashSet<string> Methods =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "get",
        "put",
        "post",
        "delete",
        "options",
        "head",
        "patch",
        "trace",
    ];

    private readonly List<string> _errors = [];

    public static IReadOnlyList<string> Validate(
        JsonElement root,
        IEnumerable<(string Path, string Method)> endpoints,
        IEnumerable<(string Path, string Method, string Name)>? queryParameters = null,
        IEnumerable<(string Path, string Method)>? requestBodies = null
    )
    {
        var validator = new OpenApiDocumentationValidator(root);
        validator.ValidateDocument(endpoints, queryParameters ?? [], requestBodies ?? []);
        return validator._errors;
    }

    private void ValidateDocument(
        IEnumerable<(string Path, string Method)> endpoints,
        IEnumerable<(string Path, string Method, string Name)> queryParameters,
        IEnumerable<(string Path, string Method)> requestBodies
    )
    {
        var expected = endpoints
            .Select(endpoint => Key(endpoint.Path, endpoint.Method))
            .ToHashSet();
        var expectedQueryParameters = queryParameters.ToLookup(
            parameter => Key(parameter.Path, parameter.Method),
            parameter => parameter.Name
        );
        var expectedBodies = requestBodies
            .Select(endpoint => Key(endpoint.Path, endpoint.Method))
            .ToHashSet();
        var documented = new HashSet<string>();
        if (!TryGetObject(root, "paths", "document", out var paths))
        {
            return;
        }

        foreach (var path in paths.EnumerateObject())
        {
            if (path.Value.ValueKind != JsonValueKind.Object)
            {
                Error(path.Name, "path item must be an object");
                continue;
            }

            var inheritedParameters = GetParameters(path.Value, path.Name);
            foreach (
                var member in path
                    .Value.EnumerateObject()
                    .Where(member => Methods.Contains(member.Name))
            )
            {
                var location = $"{member.Name.ToUpperInvariant()} {path.Name}";
                documented.Add(Key(path.Name, member.Name));
                if (member.Value.ValueKind != JsonValueKind.Object)
                {
                    Error(location, "operation must be an object");
                    continue;
                }

                ValidateDocumentationField(member.Value, "summary", location);
                ValidateDocumentationField(member.Value, "description", location);
                ValidateParameters(
                    path.Name,
                    member.Value,
                    inheritedParameters,
                    location,
                    expectedQueryParameters[location]
                );
                if (member.Value.TryGetProperty("requestBody", out var request))
                {
                    request = Resolve(request, $"{location} request body");
                    ValidateContent(request, $"{location} request body", required: true);
                }
                else if (expectedBodies.Contains(location))
                {
                    Error(location, "request body is not documented");
                }

                ValidateResponses(member.Value, location);
            }
        }

        if (documented.Count == 0)
        {
            Error("document", "no operations were documented");
        }

        foreach (var missing in expected.Except(documented).Order())
        {
            Error(missing, "mapped endpoint is missing from the OpenAPI document");
        }

        foreach (var extra in documented.Except(expected).Order())
        {
            Error(extra, "documented operation has no mapped application endpoint");
        }
    }

    private void ValidateParameters(
        string path,
        JsonElement operation,
        Dictionary<(string In, string Name), JsonElement> inherited,
        string location,
        IEnumerable<string> expectedQueryParameters
    )
    {
        var parameters = new Dictionary<(string In, string Name), JsonElement>(inherited);
        foreach (var parameter in GetParameters(operation, location))
        {
            parameters[parameter.Key] = parameter.Value;
        }

        var pathParameters = PathParameter()
            .Matches(path)
            .Select(match => match.Groups[1].Value)
            .ToHashSet();
        foreach (var (key, parameter) in parameters)
        {
            if (key.In is not ("path" or "query"))
            {
                continue;
            }

            var parameterLocation = $"{location} {key.In} parameter '{key.Name}'";
            ValidateDocumentationField(parameter, "description", parameterLocation);
            if (parameter.TryGetProperty("schema", out var schema))
            {
                ValidateSchema(schema, parameterLocation, [], false);
            }
            else if (parameter.TryGetProperty("content", out _))
            {
                ValidateContent(parameter, parameterLocation, required: true);
            }
            else
            {
                Error(parameterLocation, "parameter must have a schema or content");
            }

            if (key.In == "path")
            {
                if (!pathParameters.Contains(key.Name))
                {
                    Error(parameterLocation, "parameter does not occur in the path");
                }

                if (
                    !parameter.TryGetProperty("required", out var required)
                    || required.ValueKind != JsonValueKind.True
                )
                {
                    Error(parameterLocation, "path parameter must be required");
                }
            }
        }

        foreach (var name in pathParameters)
        {
            if (!parameters.ContainsKey(("path", name)))
            {
                Error(location, $"path parameter '{name}' is not documented");
            }
        }
        foreach (var name in expectedQueryParameters)
        {
            if (!parameters.ContainsKey(("query", name)))
            {
                Error(location, $"query parameter '{name}' is not documented");
            }
        }
    }

    private Dictionary<(string In, string Name), JsonElement> GetParameters(
        JsonElement parent,
        string location
    )
    {
        var result = new Dictionary<(string In, string Name), JsonElement>();
        if (!parent.TryGetProperty("parameters", out var parameters))
        {
            return result;
        }

        if (parameters.ValueKind != JsonValueKind.Array)
        {
            Error(location, "parameters must be an array");
            return result;
        }

        foreach (var item in parameters.EnumerateArray())
        {
            var parameter = Resolve(item, $"{location} parameter");
            var name = GetString(parameter, "name");
            var position = GetString(parameter, "in");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(position))
            {
                Error(location, "parameter must have a name and location");
                continue;
            }

            if (!result.TryAdd((position, name), parameter))
            {
                Error(location, $"duplicate {position} parameter '{name}'");
            }
        }

        return result;
    }

    private void ValidateResponses(JsonElement operation, string location)
    {
        if (!TryGetObject(operation, "responses", location, out var responses))
        {
            return;
        }

        var codes = responses.EnumerateObject().Select(response => response.Name).ToArray();
        if (
            !codes.Any(code =>
                code.Length == 3
                && code[0] == '2'
                && char.IsAsciiDigit(code[1])
                && char.IsAsciiDigit(code[2])
            )
        )
        {
            Error(location, "at least one explicit 2xx response is required");
        }

        if (!codes.Contains("500"))
        {
            Error(location, "an explicit 500 response is required");
        }

        foreach (var response in responses.EnumerateObject())
        {
            var responseLocation = $"{location} response {response.Name}";
            var resolved = Resolve(response.Value, responseLocation);
            ValidateDocumentationField(resolved, "description", responseLocation);
            ValidateContent(resolved, responseLocation, required: false);
        }
    }

    private void ValidateContent(JsonElement parent, string location, bool required)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            Error(location, "must be an object");
            return;
        }
        if (!parent.TryGetProperty("content", out var content))
        {
            if (required)
            {
                Error(location, "content is required");
            }
            return;
        }

        if (content.ValueKind != JsonValueKind.Object || !content.EnumerateObject().Any())
        {
            Error(location, "content must contain at least one media type");
            return;
        }

        foreach (var media in content.EnumerateObject())
        {
            var mediaLocation = $"{location} {media.Name}";
            if (
                media.Value.ValueKind != JsonValueKind.Object
                || !media.Value.TryGetProperty("schema", out var schema)
            )
            {
                Error(mediaLocation, "schema is required");
                continue;
            }

            ValidateSchema(schema, mediaLocation, new HashSet<string>(), false);
        }
    }

    private void ValidateSchema(
        JsonElement schema,
        string location,
        HashSet<string> activeReferences,
        bool inheritedDescription
    )
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            Error(location, "schema must be an object");
            return;
        }

        var described =
            inheritedDescription || !string.IsNullOrWhiteSpace(GetString(schema, "description"));
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var pointer =
                reference.ValueKind == JsonValueKind.String ? reference.GetString() : null;
            if (pointer is null)
            {
                Error(location, "reference must be a string");
            }
            else if (activeReferences.Add(pointer))
            {
                var target = ResolvePointer(pointer, location);
                if (target.ValueKind != JsonValueKind.Undefined)
                {
                    ValidateSchema(target, location, activeReferences, described);
                }
                activeReferences.Remove(pointer);
            }
        }

        var hasProperties = schema.TryGetProperty("properties", out var properties);
        var isObject = hasProperties || HasType(schema, "object");
        if (isObject && !described && !HasDescriptionThroughReference(schema, location, []))
        {
            Error(location, "object schema description is required");
        }

        if (hasProperties)
        {
            if (properties.ValueKind != JsonValueKind.Object)
            {
                Error(location, "properties must be an object");
            }
            else
            {
                foreach (var property in properties.EnumerateObject())
                {
                    var propertyLocation = $"{location} property '{property.Name}'";
                    if (!HasDescriptionThroughReference(property.Value, propertyLocation, []))
                    {
                        Error(propertyLocation, "property description is required");
                    }
                    ValidateSchema(property.Value, propertyLocation, activeReferences, false);
                }
            }
        }

        foreach (var name in new[] { "items", "additionalProperties" })
        {
            if (
                schema.TryGetProperty(name, out var nested)
                && nested.ValueKind != JsonValueKind.True
                && nested.ValueKind != JsonValueKind.False
            )
            {
                ValidateSchema(nested, $"{location} {name}", activeReferences, false);
            }
        }

        foreach (var name in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (!schema.TryGetProperty(name, out var alternatives))
            {
                continue;
            }
            if (alternatives.ValueKind != JsonValueKind.Array)
            {
                Error(location, $"{name} must be an array");
                continue;
            }
            var index = 0;
            foreach (var alternative in alternatives.EnumerateArray())
            {
                ValidateSchema(
                    alternative,
                    $"{location} {name}[{index++}]",
                    activeReferences,
                    described
                );
            }
        }
    }

    private bool HasDescriptionThroughReference(
        JsonElement schema,
        string location,
        HashSet<string> visited
    )
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(GetString(schema, "description")))
        {
            return true;
        }
        if (
            schema.TryGetProperty("$ref", out var reference)
            && reference.ValueKind == JsonValueKind.String
        )
        {
            var pointer = reference.GetString()!;
            if (
                visited.Add(pointer)
                && HasDescriptionThroughReference(
                    ResolvePointer(pointer, location),
                    location,
                    visited
                )
            )
            {
                return true;
            }
        }
        foreach (var name in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (
                schema.TryGetProperty(name, out var alternatives)
                && alternatives.ValueKind == JsonValueKind.Array
                && alternatives
                    .EnumerateArray()
                    .Any(alternative =>
                        HasDescriptionThroughReference(alternative, location, visited)
                    )
            )
            {
                return true;
            }
        }
        return false;
    }

    private JsonElement Resolve(JsonElement value, string location)
    {
        var seen = new HashSet<string>();
        while (
            value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("$ref", out var reference)
        )
        {
            if (reference.ValueKind != JsonValueKind.String || !seen.Add(reference.GetString()!))
            {
                Error(location, "invalid or cyclic reference");
                return default;
            }
            value = ResolvePointer(reference.GetString()!, location);
        }
        return value;
    }

    private JsonElement ResolvePointer(string pointer, string location)
    {
        if (!pointer.StartsWith("#/", StringComparison.Ordinal))
        {
            Error(location, $"unsupported reference '{pointer}'");
            return default;
        }
        var value = root;
        foreach (var part in pointer[2..].Split('/'))
        {
            var name = part.Replace("~1", "/").Replace("~0", "~");
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out value))
            {
                Error(location, $"unresolved reference '{pointer}'");
                return default;
            }
        }
        return value;
    }

    private bool TryGetObject(
        JsonElement parent,
        string name,
        string location,
        out JsonElement value
    )
    {
        if (
            parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out value)
            && value.ValueKind == JsonValueKind.Object
        )
        {
            return true;
        }
        value = default;
        Error(location, $"{name} must be an object");
        return false;
    }

    private void ValidateDocumentationField(JsonElement parent, string name, string location)
    {
        if (string.IsNullOrWhiteSpace(GetString(parent, name)))
        {
            Error(location, $"{name} is required");
        }
    }

    private static string? GetString(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool HasType(JsonElement schema, string type) =>
        schema.TryGetProperty("type", out var value)
        && (
            value.ValueKind == JsonValueKind.String && value.GetString() == type
            || value.ValueKind == JsonValueKind.Array
                && value
                    .EnumerateArray()
                    .Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == type)
        );

    private static string Key(string path, string method) => $"{method.ToUpperInvariant()} {path}";

    private void Error(string location, string message) => _errors.Add($"{location}: {message}.");

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex PathParameter();
}
