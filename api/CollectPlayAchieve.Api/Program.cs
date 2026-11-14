using System.Runtime.CompilerServices;
using CollectPlayAchieve.Api.Data;
using CollectPlayAchieve.Api.Endpoints;
using CollectPlayAchieve.Api.Options;
using Microsoft.AspNetCore.Identity;

[assembly: InternalsVisibleTo("CollectPlayAchieve.Api.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddOptions<AppOptions>().BindConfiguration(AppOptions.SectionName);
builder.Services.AddAppDbContext();
builder
    .Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

var app = builder.Build();

await app.MigrateDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapInitEndpoints();

app.UseHttpsRedirection();

await app.RunAsync();
