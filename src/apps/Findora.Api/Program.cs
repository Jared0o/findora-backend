using Findora.Catalog.Api;
using Findora.Shated.Infrastructure.Modules;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddModule<CatalogModule>(builder.Configuration);

var app = builder.Build();
await app.Services.InitializeModulesAsync(app.Lifetime.ApplicationStopping);
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Findora API"));
}

app.MapGet("/", () => TypedResults.Ok(new { Name = "findora-api" }))
    .WithName("Findora.Info")
    .WithTags("Findora");

app.MapModules();

await app.RunAsync();
