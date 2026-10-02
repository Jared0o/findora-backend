using Findora.Catalog.Core.Commands.CreateCatalog;
using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using FluentValidation;
using FluentValidation.Results;

namespace Findora.Catalog.Core.Tests.Commands.CreateCatalog;

public sealed class CreateCatalogCommandHandlerTests
{
    [Fact]
    public void Constructor_RejectsNullValidator()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CreateCatalogCommandHandler(new RecordingCatalogRepository(), null!));
    }

    [Fact]
    public async Task ExecuteAsync_AwaitsAsyncValidationAndMapsAllErrorsWithoutWriting()
    {
        var repository = new RecordingCatalogRepository();
        var observedToken = CancellationToken.None;
        var validator = new InlineValidator<CreateCatalogCommand>();
        validator.RuleFor(command => command.Name).CustomAsync(async (_, context, token) =>
        {
            await Task.Yield();
            observedToken = token;
            context.AddFailure(new ValidationFailure("name", "Name is not allowed.")
            {
                ErrorCode = "Catalog.NameNotAllowed"
            });
            context.AddFailure(new ValidationFailure("name", "Name is reserved.")
            {
                ErrorCode = "Catalog.NameReserved"
            });
        });
        var handler = new CreateCatalogCommandHandler(repository, validator);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var result = await handler.ExecuteAsync(new CreateCatalogCommand("Products"), source.Token);

        Assert.True(result.IsFailure);
        Assert.Collection(result.Errors,
            error => Assert.Equal(new Error("Catalog.NameNotAllowed", "Name is not allowed.", "name"), error),
            error => Assert.Equal(new Error("Catalog.NameReserved", "Name is reserved.", "name"), error));
        Assert.Equal(source.Token, observedToken);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_StopsWhenAsyncValidationIsCancelled()
    {
        var repository = new RecordingCatalogRepository();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var validator = new InlineValidator<CreateCatalogCommand>();
        validator.RuleFor(command => command.Name).CustomAsync(async (_, _, token) =>
        {
            await source.CancelAsync();
            token.ThrowIfCancellationRequested();
        });
        var handler = new CreateCatalogCommandHandler(repository, validator);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.ExecuteAsync(new CreateCatalogCommand("Products"), source.Token));

        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public void Constructor_RejectsNullRepository()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateCatalogCommandHandler(null!, new CreateCatalogCommandValidator()));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsNullCommandWithoutWriting()
    {
        var repository = new RecordingCatalogRepository();
        var handler = new CreateCatalogCommandHandler(repository, new CreateCatalogCommandValidator());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.ExecuteAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal(0, repository.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public async Task ExecuteAsync_RejectsMissingNameWithoutWriting(string? name)
    {
        var repository = new RecordingCatalogRepository();
        var handler = new CreateCatalogCommandHandler(repository, new CreateCatalogCommandValidator());

        var result = await handler.ExecuteAsync(new CreateCatalogCommand(name!), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Catalog.NameRequired", error.Code);
        Assert.Equal("name", error.Path);
        Assert.Equal(0, repository.Calls);
    }

    [Theory]
    [InlineData("Products", "Products")]
    [InlineData("  Products  ", "Products")]
    [InlineData("\tProduct catalog\r\n", "Product catalog")]
    public async Task ExecuteAsync_CreatesCatalogWithTrimmedName(string suppliedName, string expectedName)
    {
        var repository = new RecordingCatalogRepository();
        var handler = new CreateCatalogCommandHandler(repository, new CreateCatalogCommandValidator());

        var result = await handler.ExecuteAsync(new CreateCatalogCommand(suppliedName), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.Calls);
        Assert.Equal(expectedName, repository.Name);
        Assert.Same(repository.Result, result);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsRepositoryErrors()
    {
        var expected = Result<Guid>.Failure([
            new Error("Catalog.NameConflict", "Name already exists.", "name"),
            new Error("Catalog.LimitReached", "Catalog limit reached.")
        ]);
        var repository = new RecordingCatalogRepository { Result = expected };
        var handler = new CreateCatalogCommandHandler(repository, new CreateCatalogCommandValidator());

        var result = await handler.ExecuteAsync(new CreateCatalogCommand("Products"), TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var repository = new RecordingCatalogRepository();
        var handler = new CreateCatalogCommandHandler(repository, new CreateCatalogCommandValidator());

        await handler.ExecuteAsync(new CreateCatalogCommand("Products"), source.Token);

        Assert.Equal(source.Token, repository.Token);
    }

    [Fact]
    public async Task ExecuteAsync_StopsBeforeWritingWhenCancelled()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var repository = new RecordingCatalogRepository();
        var handler = new CreateCatalogCommandHandler(repository, new CreateCatalogCommandValidator());

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.ExecuteAsync(new CreateCatalogCommand("Products"), source.Token));

        Assert.Equal(source.Token, exception.CancellationToken);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesUnexpectedRepositoryFailure()
    {
        var expected = new InvalidOperationException("Storage unavailable.");
        var repository = new RecordingCatalogRepository { Exception = expected };
        var handler = new CreateCatalogCommandHandler(repository, new CreateCatalogCommandValidator());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(new CreateCatalogCommand("Products"), TestContext.Current.CancellationToken));

        Assert.Same(expected, exception);
    }

    private sealed class RecordingCatalogRepository : ICatalogRepository
    {
        public Result<Guid> Result { get; init; } = Result<Guid>.Success(Guid.CreateVersion7());
        public Exception? Exception { get; init; }
        public int Calls { get; private set; }
        public string? Name { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken = default)
        {
            Calls++;
            Name = name;
            Token = cancellationToken;
            return Exception is null ? Task.FromResult(Result) : Task.FromException<Result<Guid>>(Exception);
        }
    }
}
