using System.Runtime.CompilerServices;
using CollectPlayAchieve.Api.Data;
using CollectPlayAchieve.Api.Options;

[assembly: InternalsVisibleTo("CollectPlayAchieve.Api.Tests")]

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddOptions<AppOptions>().BindConfiguration(AppOptions.SectionName);
builder.Services.AddAppDbContext();

var app = builder.Build();

await app.MigrateDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

await app.RunAsync();
