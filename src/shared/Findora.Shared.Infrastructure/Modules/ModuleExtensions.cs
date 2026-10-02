using Findora.Shared.Abstraction.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Findora.Shated.Infrastructure.Modules;

public static class ModuleExtensions
{
    public static IServiceCollection AddModule<TModule>(this IServiceCollection services, IConfiguration configuration)
        where TModule : class, IModule, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IModule)
            && descriptor.ImplementationInstance is TModule))
        {
            return services;
        }

        var module = new TModule();
        module.RegisterServices(services, configuration);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static async Task InitializeModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var module in services.GetServices<IModule>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = services.CreateAsyncScope();
            await module.InitializeAsync(scope.ServiceProvider, cancellationToken);
        }
    }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        foreach (var module in endpoints.ServiceProvider.GetServices<IModule>())
        {
            var group = endpoints.MapGroup(module.RoutePrefix).WithTags(module.Name);
            group.MapGet("/", () => TypedResults.Ok(new ModuleInfo(module.Name)))
                .WithName(module.Name + ".Info")
                .WithSummary("Information about the " + module.Name + " module.");
            module.MapEndpoints(group);
        }

        return endpoints;
    }
}
