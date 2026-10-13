using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (5g-a). Il passo che i tre percorsi di risposta eseguono prima di cercare: chi chiede può
/// interrogare questo gioco? È lo stesso controllo di prima, in un punto solo. La quota per piano
/// (5g-b) si aggiunge qui.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4168")]
public sealed class AnswerPreconditionsTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Game = Guid.NewGuid();

    private readonly Mock<IRagAccessService> _access = new();
    private readonly AnswerPreconditions _sut;

    public AnswerPreconditionsTests()
    {
        _sut = new AnswerPreconditions(_access.Object);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AUserWhoMayQueryTheGame_Passes()
    {
        _access.Setup(a => a.CanAccessRagAsync(User, Game, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.EnsureCanAskAboutGameAsync(User, "User", Game, Ct);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AUserWhoMayNot_IsRefused_NamingTheGame()
    {
        _access.Setup(a => a.CanAccessRagAsync(User, Game, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => _sut.EnsureCanAskAboutGameAsync(User, "User", Game, Ct);

        (await act.Should().ThrowAsync<RagOwnershipRequiredException>()).Which.GameId.Should().Be(Game);
    }

    /// <summary>
    /// Il ruolo arriva dalla sessione come testo. Decide il passaggio degli amministratori, quindi
    /// va letto senza badare alle maiuscole, e un ruolo sconosciuto vale come utente: nel dubbio
    /// si controlla, non si lascia passare.
    /// </summary>
    [Theory]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("admin", UserRole.Admin)]
    [InlineData("SuperAdmin", UserRole.SuperAdmin)]
    [InlineData("Editor", UserRole.Editor)]
    [InlineData("User", UserRole.User)]
    [InlineData("qualcosa-che-non-esiste", UserRole.User)]
    [InlineData("", UserRole.User)]
    [InlineData(null, UserRole.User)]
    public async Task TheRoleOfTheSession_IsTheRoleTheGateSees(string? roleFromTheSession, UserRole expected)
    {
        _access.Setup(a => a.CanAccessRagAsync(User, Game, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.EnsureCanAskAboutGameAsync(User, roleFromTheSession, Game, Ct);

        _access.Verify(a => a.CanAccessRagAsync(User, Game, expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheGamesOpenToAUser_AreTheOnesTheAccessServiceLists()
    {
        var games = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _access.Setup(a => a.GetAccessibleGameIdsAsync(User, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(games);

        var open = await _sut.GamesOpenToAsync(User, UserRole.User, Ct);

        open.Should().Equal(games);
    }
}
