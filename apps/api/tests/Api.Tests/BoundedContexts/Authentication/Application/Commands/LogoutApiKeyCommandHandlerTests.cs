using Api.BoundedContexts.Authentication.Application.Commands;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Unit tests for LogoutApiKeyCommandHandler (Issue #2643).
/// Tests API key logout acknowledgement.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2643")]
public class LogoutApiKeyCommandHandlerTests
{
    private readonly LogoutApiKeyCommandHandler _handler;

    public LogoutApiKeyCommandHandlerTests()
    {
        _handler = new LogoutApiKeyCommandHandler();
    }

    [Fact]
    public async Task Handle_ReturnsAcknowledgement()
    {
        // Arrange
        var command = new LogoutApiKeyCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("acknowledged", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var command = new LogoutApiKeyCommand();
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _handler.Handle(command, cts.Token);

        // Assert
        Assert.NotNull(result);
    }
}
