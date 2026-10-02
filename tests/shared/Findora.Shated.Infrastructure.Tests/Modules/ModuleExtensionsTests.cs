using Findora.Shared.Abstraction.Modules;
using Findora.Shated.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Findora.Shated.Infrastructure.Tests.Modules;

public sealed class ModuleExtensionsTests
{
    [Fact]
    public void AddModule_RegistersServicesWithProvidedConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Modules:Value"] = "configured" }).Build();
        var services = new ServiceCollection();

        var returned = services.AddModule<TestModule>(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Same(services, returned);
        Assert.Equal("configured", provider.GetRequiredService<ModuleService>().Value);
        Assert.IsType<TestModule>(Assert.Single(provider.GetServices<IModule>()));
    }

    [Fact]
    public void AddModule_DoesNotRegisterSameModuleTwice()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddModule<TestModule>(configuration);
        services.AddModule<TestModule>(configuration);

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IModule));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ModuleService));
    }

    [Fact]
    public void MapModules_MapsEveryRegisteredModuleInRegistrationOrder()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton(new List<string>());
        services.AddRouting();
        services.AddModule<TestModule>(configuration);
        services.AddModule<SecondTestModule>(configuration);
        using var provider = services.BuildServiceProvider();
        var endpoints = new TestEndpointRouteBuilder(provider);

        var returned = endpoints.MapModules();

        Assert.Same(endpoints, returned);
        Assert.Equal(["first", "second"], provider.GetRequiredService<List<string>>());
    }

    [Fact]
    public void AddModule_RejectsNullServices()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ModuleExtensions.AddModule<TestModule>(null!, new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void AddModule_RejectsNullConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddModule<TestModule>(null!));
    }

    [Fact]
    public void MapModules_RejectsNullEndpoints()
    {
        Assert.Throws<ArgumentNullException>(() => ModuleExtensions.MapModules(null!));
    }

    [Fact]
    public void MapModules_AllowsHostWithoutModules()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var endpoints = new TestEndpointRouteBuilder(provider);

        Assert.Same(endpoints, endpoints.MapModules());
    }

    public sealed class TestModule : IModule
    {
        public string Name => "first";
        public string RoutePrefix => "/api/first";

        public void RegisterServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(new ModuleService(configuration["Modules:Value"]));
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.ServiceProvider.GetRequiredService<List<string>>().Add("first");
        }
    }

    public sealed class SecondTestModule : IModule
    {
        public string Name => "second";
        public string RoutePrefix => "/api/second";

        public void RegisterServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.ServiceProvider.GetRequiredService<List<string>>().Add("second");
        }
    }

    private sealed record ModuleService(string? Value);

    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }
}
