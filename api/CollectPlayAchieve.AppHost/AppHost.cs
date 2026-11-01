using Aspire.Hosting.Yarp.Transforms;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Api>("api").WithExternalHttpEndpoints();

#pragma warning disable ASPIREJAVASCRIPT001
var web = builder
    .AddViteApp("web", "../../web")
    .WithPnpm()
    .PublishAsPackageScript()
    .WithExternalHttpEndpoints();
#pragma warning restore ASPIREJAVASCRIPT001

builder
    .AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute(web);
        yarp.AddRoute("/api/{**catch-all}", api).WithTransformPathRemovePrefix("/api");
    });

builder.Build().Run();
