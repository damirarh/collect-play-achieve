using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CollectPlayAchieve.Api.Tests;

internal sealed class OpenApiTests : ApiTestBase
{
    [Test]
    public async Task DevelopmentDocument_DescribesEveryApplicationOperation()
    {
        await using var factory = Factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development")
        );
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var routes = factory
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText != "/openapi/{documentName}.json")
            .ToArray();
        var unconstrainedRoutes = routes
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Count is null or 0
            )
            .Select(endpoint => endpoint.RoutePattern.RawText);
        await Assert.That(unconstrainedRoutes).IsEmpty(); // All routes should have HTTP method constraints.
        var endpoints = routes.SelectMany(endpoint =>
            (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Select(
                method => (Path: endpoint.RoutePattern.ToOpenApiPath(), Method: method)
            )
        );
        var descriptions = factory
            .Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items)
            .Where(description =>
                description.HttpMethod is not null && description.RelativePath is not null
            )
            .ToArray();
        var queryParameters = descriptions.SelectMany(description =>
            description
                .ParameterDescriptions.Where(parameter => parameter.Source == BindingSource.Query)
                .Select(parameter =>
                    (
                        Path: "/" + description.RelativePath,
                        Method: description.HttpMethod!,
                        parameter.Name
                    )
                )
        );
        var requestBodies = descriptions
            .Where(description =>
                description.ParameterDescriptions.Any(parameter =>
                    parameter.Source == BindingSource.Body || parameter.Source == BindingSource.Form
                )
            )
            .Select(description =>
                (Path: "/" + description.RelativePath, Method: description.HttpMethod!)
            );
        var errors = OpenApiDocumentationValidator.Validate(
            document.RootElement,
            endpoints,
            queryParameters,
            requestBodies
        );
        await Assert.That(errors).IsEmpty();
        // await Assert.That(string.Join(Environment.NewLine, errors)).IsEqualTo("");
    }

    [Test]
    public async Task Production_DoesNotExposeOpenApi()
    {
        await using var factory = Factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production")
        );
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
