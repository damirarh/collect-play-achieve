using System.Runtime.CompilerServices;
using CollectPlayAchieve.Api.Data;
using CollectPlayAchieve.Api.Features.Auth;
using CollectPlayAchieve.Api.Infrastructure;
using CollectPlayAchieve.Api.Options;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

[assembly: InternalsVisibleTo("CollectPlayAchieve.Api.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

builder.Services.AddOptions<AppOptions>().BindConfiguration(AppOptions.SectionName);
builder.Services.AddAppDbContext();
builder
    .Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.Scan(scan =>
    scan.FromAssemblyOf<Program>()
        .AddClasses(
            classes =>
                classes
                    .AssignableTo(typeof(IQueryHandler<,>))
                    .Where(type => type.DeclaringType != typeof(ValidationDecorator)),
            publicOnly: false
        )
        .AsImplementedInterfaces()
        .WithScopedLifetime()
        .AddClasses(
            classes =>
                classes
                    .AssignableTo(typeof(ICommandHandler<>))
                    .Where(type => type.DeclaringType != typeof(ValidationDecorator)),
            publicOnly: false
        )
        .AsImplementedInterfaces()
        .WithScopedLifetime()
        .AddClasses(
            classes =>
                classes
                    .AssignableTo(typeof(ICommandHandler<,>))
                    .Where(type => type.DeclaringType != typeof(ValidationDecorator)),
            publicOnly: false
        )
        .AsImplementedInterfaces()
        .WithScopedLifetime()
        .AddClasses(classes => classes.AssignableTo(typeof(IValidator<>)), publicOnly: false)
        .AsImplementedInterfaces()
        .WithScopedLifetime()
);

builder.Services.TryDecorate(
    typeof(ICommandHandler<>),
    typeof(ValidationDecorator.CommandBaseHandler<>)
);
builder.Services.TryDecorate(
    typeof(ICommandHandler<,>),
    typeof(ValidationDecorator.CommandHandler<,>)
);
builder.Services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationDecorator.QueryHandler<,>));

var app = builder.Build();

await app.MigrateDatabaseAsync();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapInitEndpoints();

app.UseHttpsRedirection();

await app.RunAsync();
