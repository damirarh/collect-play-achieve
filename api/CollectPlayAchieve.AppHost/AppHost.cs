using Aspire.Hosting.Yarp.Transforms;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Api>("api").WithExternalHttpEndpoints();

#pragma warning disable ASPIREJAVASCRIPT001
var web = builder
    .AddViteApp("web", "../../web")
    .WithPnpm()
    .PublishAsPackageScript()
    .WithExternalHttpEndpoints();
#pragma warning restore ASPIREJAVASCRIPT001

var gateway = builder
    .AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute(web);
        yarp.AddRoute("/api/{**catch-all}", api).WithTransformPathRemovePrefix("/api");
    });

if (builder.Configuration.GetValue<bool>("Sandbox:Enabled"))
{
    gateway.WithHostPort(8080).WithEndpoint("http", endpoint => endpoint.TargetHost = "0.0.0.0");
}

builder.Build().Run();
