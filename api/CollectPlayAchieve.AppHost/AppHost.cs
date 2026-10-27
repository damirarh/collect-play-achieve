var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Api>("api").WithExternalHttpEndpoints();

#pragma warning disable ASPIREJAVASCRIPT001
builder
    .AddViteApp("web", "../../web")
    .WithPnpm()
    .PublishAsPackageScript()
    .WithExternalHttpEndpoints();
#pragma warning restore ASPIREJAVASCRIPT001

builder.Build().Run();
