using Api.BoundedContexts.Administration.Application.Commands.Resources;
using FluentAssertions;
using FluentValidation.TestHelper;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.ResourcesTests;

/// <summary>
/// Integration tests for clear cache command.
/// Issue #3695: Resources Monitoring - Clear cache action
/// </summary>
[Collection("Sequential")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Administration")]
[Trait("Epic", "3685")]
public class ClearCacheCommandTests : IAsyncLifetime
{
    private RedisContainer? _redis;
    private IConnectionMultiplexer? _connection;

    public async ValueTask InitializeAsync()
    {
        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redis.StartAsync().ConfigureAwait(false);

        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString())
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        if (_redis != null)
        {
            await _redis.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Handle_WithConfirmation_ClearsCacheSuccessfully()
    {
        // Arrange
        var handler = new ClearCacheCommandHandler(_connection!);
        var command = new ClearCacheCommand(Confirmed: true);
        var db = _connection!.GetDatabase();

        // Add test data
        await db.StringSetAsync("test:key1", "value1");
        await db.StringSetAsync("test:key2", "value2");

        // Verify data exists
        var valueBefore = await db.StringGetAsync("test:key1");
        valueBefore.Should().Be("value1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        // Verify cache is cleared
        var valueAfter = await db.StringGetAsync("test:key1");
        valueAfter.IsNull.Should().BeTrue("cache should be cleared");
    }

    [Fact]
    public async Task Handle_WithoutConfirmation_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new ClearCacheCommandHandler(_connection!);
        var command = new ClearCacheCommand(Confirmed: false);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Confirmation is required*");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ClearCacheCommandHandler(_connection!);

        // Act
        var act = () => handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRedis_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ClearCacheCommandHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validator_WithoutConfirmation_HasValidationError()
    {
        // Arrange
        var validator = new ClearCacheCommandValidator();
        var command = new ClearCacheCommand(Confirmed: false);

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Confirmed)
            .WithErrorMessage("*Confirmation is required*");
    }

    [Fact]
    public void Validator_WithConfirmation_PassesValidation()
    {
        // Arrange
        var validator = new ClearCacheCommandValidator();
        var command = new ClearCacheCommand(Confirmed: true);

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Confirmed);
    }
}