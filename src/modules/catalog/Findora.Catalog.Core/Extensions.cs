using System.Reflection;
using Findora.Catalog.Core.Queries.GetCatalog;
using Findora.Catalog.Core.Commands.CreateCatalog;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Findora.Catalog.Core;

public static class Extensions
{
    public static IServiceCollection AddCatalogCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), ServiceLifetime.Transient);
        services.TryAddScoped<CreateCatalogCommandHandler>();
        services.TryAddScoped<GetCatalogQueryHandler>();
        return services;
    }
}