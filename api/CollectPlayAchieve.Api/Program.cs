using System.Runtime.CompilerServices;
using CollectPlayAchieve.Api.Data;
using CollectPlayAchieve.Api.Infrastructure;
using CollectPlayAchieve.Api.Options;
using Microsoft.AspNetCore.Identity;

[assembly: InternalsVisibleTo("CollectPlayAchieve.Api.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
    options.AddSchemaTransformer<ProblemDetailsSchemaTransformer>()
);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

builder.Services.AddOptions<AppOptions>().BindConfiguration(AppOptions.SectionName);
builder.Services.AddAppDbContext();
builder
    .Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.Scan(scan =>
    scan.FromAssemblyOf<Program>().AddHandlerClasses().AddValidatorClasses().AddEndpointClasses()
);

builder.Services.AddValidationDecorators();

var app = builder.Build();

await app.MigrateDatabaseAsync();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapEndpoints();

app.UseHttpsRedirection();

await app.RunAsync();
