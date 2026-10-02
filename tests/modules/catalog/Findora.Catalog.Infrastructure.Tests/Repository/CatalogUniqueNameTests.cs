using Findora.Catalog.Infrastructure.Persistence;
using Findora.Catalog.Infrastructure.Persistence.Entities;
using Findora.Catalog.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Findora.Catalog.Infrastructure.Tests.Repository;

[Collection("PostgreSQL")]
public sealed class CatalogUniqueNameTests(PostgresFixture database)
{
    [Theory]
    [InlineData("Books", "Books")]
    [InlineData("Books", "books")]
    [InlineData("Books", "  BOOKS  ")]
    [InlineData("Zażółć", "ZAŻÓŁĆ")]
    public async Task Create_DuplicateNameReturnsErrorWithoutTrackingNewRecord(string first, string second)
    {
        var token = TestContext.Current.CancellationToken;
        var prefix = Guid.NewGuid() + "-";
        await using var context = database.CreateContext();
        var repository = new CatalogRepository(context);
        var created = await repository.CreateAsync(prefix + first, token);
        Assert.True(created.IsSuccess);
        context.ChangeTracker.Clear();
        var duplicate = await repository.CreateAsync(" " + prefix + second.Trim() + " ", token);
        Assert.True(duplicate.IsFailure);
        var error = Assert.Single(duplicate.Errors);
        Assert.Equal("Catalog.NameAlreadyExists", error.Code);
        Assert.Equal("name", error.Path);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(first, (await repository.GetByIdAsync(created.Value, token))!.Name[prefix.Length..]);
        Assert.True((await repository.CreateAsync(prefix + "Different", token)).IsSuccess);
    }

    [Fact]
    public async Task Create_ConcurrentDuplicatesReturnOneSuccessAndOneConflict()
    {
        var token = TestContext.Current.CancellationToken;
        var name = "Race-" + Guid.NewGuid();
        var barrier = new SaveBarrier();
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(barrier).Options;
        await using var first = new CatalogDbContext(options);
        await using var second = new CatalogDbContext(options);
        var outcomes = await Task.WhenAll(
            new CatalogRepository(first).CreateAsync(name, token),
            new CatalogRepository(second).CreateAsync(name.ToUpperInvariant(), token));

        Assert.Single(outcomes, result => result.IsSuccess);
        var failure = Assert.Single(outcomes, result => result.IsFailure);
        Assert.Equal("Catalog.NameAlreadyExists", Assert.Single(failure.Errors).Code);
        var failedContext = outcomes[0].IsFailure ? first : second;
        Assert.Empty(failedContext.ChangeTracker.Entries());
        await using var reader = database.CreateContext();
        var id = outcomes.Single(result => result.IsSuccess).Value;
        Assert.True(await reader.Catalogs.AnyAsync(record => record.Id == id, token));
    }

    [Fact]
    public async Task Database_RejectsDuplicateEvenWhenRepositoryIsBypassed()
    {
        var token = TestContext.Current.CancellationToken;
        var name = "Direct-" + Guid.NewGuid();
        await using var context = database.CreateContext();
        context.Catalogs.Add(new CatalogRecord { Id = Guid.CreateVersion7(), Name = name, CreatedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync(token);
        context.Catalogs.Add(new CatalogRecord { Id = Guid.CreateVersion7(), Name = " " + name.ToUpperInvariant() + " ", CreatedAt = DateTimeOffset.UtcNow });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(token));
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ux_catalogs_normalized_name", postgres.ConstraintName);
    }

    private sealed class SaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }
}
