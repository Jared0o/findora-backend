using Findora.Shared.Abstraction.Modules;
using Findora.Shated.Infrastructure.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Findora.Shated.Infrastructure.Tests.Modules;

public sealed class ModuleInitializationTests
{
    [Fact]
    public async Task InitializeModules_AwaitsModulesInOrderWithSeparateDisposedScopes()
    {
        var resources = new List<ScopedResource>();
        var calls = new List<string>();
        var token = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddScoped<ScopedResource>();
        services.AddSingleton<IModule>(new InitializingModule(async (provider, receivedToken) =>
        {
            Assert.Equal(token, receivedToken);
            resources.Add(provider.GetRequiredService<ScopedResource>());
            await Task.Yield();
            calls.Add("first");
        }));
        services.AddSingleton<IModule>(new InitializingModule((provider, _) =>
        {
            Assert.Equal(["first"], calls);
            Assert.True(resources[0].Disposed);
            resources.Add(provider.GetRequiredService<ScopedResource>());
            calls.Add("second");
            return Task.CompletedTask;
        }));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await provider.InitializeModulesAsync(token);

        Assert.Equal(["first", "second"], calls);
        Assert.NotSame(resources[0], resources[1]);
        Assert.All(resources, resource => Assert.True(resource.Disposed));
    }

    [Fact]
    public async Task InitializeModules_PropagatesFailureDisposesScopeAndStopsLaterModules()
    {
        ScopedResource? resource = null;
        var laterModuleCalled = false;
        var failure = new InvalidOperationException("Migration failed.");
        var services = new ServiceCollection();
        services.AddScoped<ScopedResource>();
        services.AddSingleton<IModule>(new InitializingModule((provider, _) =>
        {
            resource = provider.GetRequiredService<ScopedResource>();
            return Task.FromException(failure);
        }));
        services.AddSingleton<IModule>(new InitializingModule((_, _) =>
        {
            laterModuleCalled = true;
            return Task.CompletedTask;
        }));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.InitializeModulesAsync(TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.True(resource!.Disposed);
        Assert.False(laterModuleCalled);
    }

    [Fact]
    public async Task InitializeModules_DoesNotInitializeWhenCancelled()
    {
        var called = false;
        var services = new ServiceCollection();
        services.AddSingleton<IModule>(new InitializingModule((_, _) =>
        {
            called = true;
            return Task.CompletedTask;
        }));
        await using var provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.InitializeModulesAsync(cancellation.Token));

        Assert.False(called);
    }

    private sealed class ScopedResource : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InitializingModule(Func<IServiceProvider, CancellationToken, Task> initialize) : IModule
    {
        public string Name => "test";
        public string RoutePrefix => "/test";
        public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }
        public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
        public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
            => initialize(services, cancellationToken);
    }
}
