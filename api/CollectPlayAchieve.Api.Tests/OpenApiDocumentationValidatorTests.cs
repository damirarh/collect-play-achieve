using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Routing.Patterns;

namespace CollectPlayAchieve.Api.Tests;

internal sealed class OpenApiDocumentationValidatorTests
{
    /*language=json*/
    private const string Document = """
        {
          "paths": {
            "/items/{id}": {
              "parameters": [
                { "name": "id", "in": "path", "required": true, "description": "Item ID", "schema": { "type": "string" } }
              ],
              "get": {
                "summary": "Get item",
                "description": "Returns an item.",
                "parameters": [
                  { "name": "filter", "in": "query", "description": "Filter", "schema": { "type": "string" } }
                ],
                "requestBody": {
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "description": "Request",
                        "properties": { "value": { "type": "string", "description": "Value" } }
                      }
                    }
                  }
                },
                "responses": {
                  "200": { "description": "Found", "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Node" } } } },
                  "204": { "description": "No content" },
                  "500": { "description": "Error" }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "Node": {
                "type": "object",
                "description": "A node",
                "properties": {
                  "name": { "type": "string", "description": "Name" },
                  "next": { "description": "Next node", "oneOf": [ { "$ref": "#/components/schemas/Node" }, { "type": "null" } ] },
                  "children": {
                    "type": "array", "description": "Child nodes",
                    "items": { "type": "object", "description": "A child", "properties": {
                      "labels": { "type": "object", "description": "Labels", "additionalProperties": { "type": "array", "items": { "type": "string" } } }
                    } }
                  }
                }
              }
            }
          }
        }
        """;

    [Test]
    public async Task ValidDocument_AllowsRecursiveCollectionsAndResponsesWithNoBody()
    {
        using var document = JsonDocument.Parse(Document);
        var errors = OpenApiDocumentationValidator.Validate(
            document.RootElement,
            [("/items/{id}", "GET")]
        );
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    [Arguments("summary", "GET /items/{id}: summary is required")]
    [Arguments("description", "GET /items/{id}: description is required")]
    [Arguments("path-description", "path parameter 'id': description is required")]
    [Arguments("path-required", "path parameter 'id': path parameter must be required")]
    [Arguments("path-missing", "path parameter 'id' is not documented")]
    [Arguments("query-description", "query parameter 'filter': description is required")]
    [Arguments("query-schema", "query parameter 'filter': parameter must have a schema or content")]
    [Arguments(
        "query-reference",
        "query parameter 'filter': unresolved reference '#/components/schemas/Missing'"
    )]
    [Arguments("success", "at least one explicit 2xx response is required")]
    [Arguments("server-error", "an explicit 500 response is required")]
    [Arguments("response-description", "response 500: description is required")]
    [Arguments("request-schema", "request body application/json: schema is required")]
    [Arguments(
        "request-description",
        "request body application/json: object schema description is required"
    )]
    [Arguments(
        "request-property",
        "request body application/json property 'value': property description is required"
    )]
    [Arguments("response-schema", "response 200 application/json: schema is required")]
    [Arguments(
        "response-property",
        "response 200 application/json property 'name': property description is required"
    )]
    [Arguments("nested-property", "property 'labels': property description is required")]
    [Arguments(
        "map-property",
        "additionalProperties property 'missing': property description is required"
    )]
    [Arguments(
        "composed-property",
        "allOf[0] property 'missing': property description is required"
    )]
    [Arguments(
        "alternative-property",
        "anyOf[0] property 'missing': property description is required"
    )]
    [Arguments("reference-sibling", "property 'sibling': property description is required")]
    [Arguments("missing-reference", "unresolved reference '#/components/schemas/Missing'")]
    [Arguments("external-reference", "unsupported reference 'https://example.test/schema'")]
    public async Task InvalidDocument_ReportsOperationAndMissingDocumentation(
        string mutation,
        string expected
    )
    {
        var document = JsonNode.Parse(Document)!;
        var path = document["paths"]!["/items/{id}"]!;
        var operation = path["get"]!;
        var responses = operation["responses"]!;
        var requestSchema = operation["requestBody"]!["content"]!["application/json"]!["schema"]!;
        var node = document["components"]!["schemas"]!["Node"]!;
        switch (mutation)
        {
            case "summary":
                operation.AsObject().Remove("summary");
                break;
            case "description":
                operation.AsObject().Remove("description");
                break;
            case "path-description":
                path["parameters"]![0]!.AsObject().Remove("description");
                break;
            case "path-required":
                path["parameters"]![0]!["required"] = false;
                break;
            case "path-missing":
                path.AsObject().Remove("parameters");
                break;
            case "query-description":
                operation["parameters"]![0]!.AsObject().Remove("description");
                break;
            case "query-schema":
                operation["parameters"]![0]!.AsObject().Remove("schema");
                break;
            case "query-reference":
                operation["parameters"]![0]!["schema"] = JsonNode.Parse(
                    """{"$ref":"#/components/schemas/Missing"}"""
                );
                break;
            case "success":
                responses.AsObject().Remove("200");
                responses.AsObject().Remove("204");
                break;
            case "server-error":
                responses.AsObject().Remove("500");
                break;
            case "response-description":
                responses["500"]!.AsObject().Remove("description");
                break;
            case "request-schema":
                operation["requestBody"]!["content"]!["application/json"]!
                    .AsObject()
                    .Remove("schema");
                break;
            case "request-description":
                requestSchema.AsObject().Remove("description");
                break;
            case "request-property":
                requestSchema["properties"]!["value"]!.AsObject().Remove("description");
                break;
            case "response-schema":
                responses["200"]!["content"]!["application/json"]!.AsObject().Remove("schema");
                break;
            case "response-property":
                node["properties"]!["name"]!.AsObject().Remove("description");
                break;
            case "nested-property":
                node["properties"]!["children"]!["items"]!["properties"]!["labels"]!
                    .AsObject()
                    .Remove("description");
                break;
            case "map-property":
                node["properties"]!["children"]!["items"]!["properties"]!["labels"]![
                    "additionalProperties"
                ] = JsonNode.Parse(
                    """{"type":"object","description":"Entry","properties":{"missing":{"type":"string"}}}"""
                );
                break;
            case "composed-property":
                node["properties"]!["combined"] = JsonNode.Parse(
                    """{"description":"Combined","allOf":[{"type":"object","properties":{"missing":{"type":"string"}}}]}"""
                );
                break;
            case "alternative-property":
                node["properties"]!["combined"] = JsonNode.Parse(
                    """{"description":"Combined","anyOf":[{"type":"object","properties":{"missing":{"type":"string"}}},{"type":"null"}]}"""
                );
                break;
            case "reference-sibling":
                responses["200"]!["content"]!["application/json"]!["schema"]!["properties"] =
                    JsonNode.Parse("""{"sibling":{"type":"string"}}""");
                break;
            case "missing-reference":
                responses["200"]!["content"]!["application/json"]!["schema"]!["$ref"] =
                    "#/components/schemas/Missing";
                break;
            case "external-reference":
                responses["200"]!["content"]!["application/json"]!["schema"]!["$ref"] =
                    "https://example.test/schema";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        using var parsed = JsonDocument.Parse(document.ToJsonString());
        var errors = OpenApiDocumentationValidator.Validate(
            parsed.RootElement,
            [("/items/{id}", "GET")]
        );
        await Assert.That(string.Join("\n", errors)).Contains(expected);
    }

    [Test]
    public async Task EndpointCoverage_ReportsMissingAndUnexpectedOperations()
    {
        using var document = JsonDocument.Parse(Document);
        var errors = OpenApiDocumentationValidator.Validate(
            document.RootElement,
            [("/other", "POST")]
        );
        var message = string.Join("\n", errors);
        await Assert
            .That(message)
            .Contains("POST /other: mapped endpoint is missing from the OpenAPI document");
        await Assert
            .That(message)
            .Contains("GET /items/{id}: documented operation has no mapped application endpoint");
    }

    [Test]
    public async Task ApiExplorerParameters_RequireQueryAndBodyToBeDocumented()
    {
        var document = JsonNode.Parse(Document)!;
        var operation = document["paths"]!["/items/{id}"]!["get"]!.AsObject();
        operation.Remove("parameters");
        operation.Remove("requestBody");
        using var parsed = JsonDocument.Parse(document.ToJsonString());
        var errors = OpenApiDocumentationValidator.Validate(
            parsed.RootElement,
            [("/items/{id}", "GET")],
            [("/items/{id}", "GET", "filter")],
            [("/items/{id}", "GET")]
        );
        var message = string.Join("\n", errors);
        await Assert
            .That(message)
            .Contains("GET /items/{id}: query parameter 'filter' is not documented");
        await Assert.That(message).Contains("GET /items/{id}: request body is not documented");
    }

    [Test]
    [Arguments("/items/{id:int}", "/items/{id}")]
    [Arguments("/items/{id?}", "/items/{id}")]
    [Arguments("/files/{**path}", "/files/{path}")]
    [Arguments("/v1/items/{name}.{format?}", "/v1/items/{name}.{format}")]
    public async Task RoutePatterns_UseOpenApiParameterSyntax(string route, string expected)
    {
        await Assert.That(RoutePatternFactory.Parse(route).ToOpenApiPath()).IsEqualTo(expected);
    }
}
