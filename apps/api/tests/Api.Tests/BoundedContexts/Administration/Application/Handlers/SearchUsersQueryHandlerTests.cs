using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.BoundedContexts.Administration.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for SearchUsersQueryHandler.
/// Autocomplete searches by display name with a two-character minimum. #4727: the email is never
/// matched partially and never returned whole — a term with an "@" is a lookup of exactly that address.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class SearchUsersQueryHandlerTests
{
    private readonly Mock<IUserProfileRepository> _userProfileRepositoryMock;
    private readonly Mock<ILogger<SearchUsersQueryHandler>> _loggerMock;
    private readonly SearchUsersQueryHandler _handler;

    public SearchUsersQueryHandlerTests()
    {
        _userProfileRepositoryMock = new Mock<IUserProfileRepository>();
        _loggerMock = new Mock<ILogger<SearchUsersQueryHandler>>();
        _handler = new SearchUsersQueryHandler(
            _userProfileRepositoryMock.Object,
            _loggerMock.Object);
    }

    private void VerifyNothingWasSearched()
    {
        _userProfileRepositoryMock.Verify(
            r => r.SearchByDisplayNameAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userProfileRepositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidQuery_ReturnsMatchingUsers()
    {
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithEmail("john@example.com").WithDisplayName("John Doe").Build(),
        };
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("john", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "john", MaxResults: 10), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].DisplayName.Should().Be("John Doe");
        _userProfileRepositoryMock.Verify(
            r => r.SearchByDisplayNameAsync("john", It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleMatches_ReturnsAllMatches()
    {
        var users = new List<UserProfile>
        {
            new UserProfileBuilder().WithEmail("admin1@example.com").WithDisplayName("Admin One").WithRole("admin").Build(),
            new UserProfileBuilder().WithEmail("admin2@example.com").WithDisplayName("Admin Two").WithRole("admin").Build(),
            new UserProfileBuilder().WithEmail("admin3@example.com").WithDisplayName("Admin Three").WithRole("admin").Build(),
        };
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("admin", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "admin", MaxResults: 10), TestContext.Current.CancellationToken);

        result.Count.Should().Be(3);
        result.Should().AllSatisfy(r => r.DisplayName.Should().Contain("Admin"));
    }

    [Fact]
    public async Task Handle_WithMaxResults_PassesTheLimitToTheRepository()
    {
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("user", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>
            {
                new UserProfileBuilder().WithDisplayName("User 1").Build(),
                new UserProfileBuilder().WithDisplayName("User 2").Build(),
            });

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "user", MaxResults: 2), TestContext.Current.CancellationToken);

        result.Count.Should().Be(2);
        _userProfileRepositoryMock.Verify(
            r => r.SearchByDisplayNameAsync("user", 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData(" a ")]
    public async Task Handle_WithEmptyOrTooShortQuery_ReturnsEmptyWithoutSearching(string term)
    {
        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: term, MaxResults: 10), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        VerifyNothingWasSearched();
    }

    [Fact]
    public async Task Handle_WithNullQuery_ReturnsEmptyWithoutSearching()
    {
        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: null!, MaxResults: 10), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        VerifyNothingWasSearched();
    }

    [Fact]
    public async Task Handle_WithTwoCharacterQuery_CallsRepository()
    {
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("ab", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());

        await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "ab", MaxResults: 10), TestContext.Current.CancellationToken);

        _userProfileRepositoryMock.Verify(
            r => r.SearchByDisplayNameAsync("ab", It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TrimsTheTermBeforeSearching()
    {
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("anna", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile> { new UserProfileBuilder().WithDisplayName("Anna").Build() });

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "  anna ", MaxResults: 10), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WithNoMatches_ReturnsEmptyList()
    {
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("nonexistent", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "nonexistent", MaxResults: 10), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RepositoryThrowsException_ReturnsEmptyListAndLogsError()
    {
        var exception = new Exception("Database error");
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("error", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "error", MaxResults: 10), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error searching users")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("test", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());
        using var cts = new CancellationTokenSource();

        await _handler.Handle(new SearchUsersQuery(SearchQuery: "test", MaxResults: 10), cts.Token);

        _userProfileRepositoryMock.Verify(
            r => r.SearchByDisplayNameAsync("test", It.IsAny<int?>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithResults_LogsResultCount()
    {
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("test", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile> { new UserProfileBuilder().Build(), new UserProfileBuilder().Build() });

        await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "test", MaxResults: 10), TestContext.Current.CancellationToken);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Found 2 users")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ---- #4727: the email is not a way to list the accounts ----

    [Fact]
    public async Task Handle_NameSearch_ReturnsTheEmailMaskedNotWhole()
    {
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("carla", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>
            {
                new UserProfileBuilder().WithEmail("carla.bianchi@example.com").WithDisplayName("Carla").Build(),
            });

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "carla", MaxResults: 10), TestContext.Current.CancellationToken);

        result[0].Email.Should().Be("c***i@example.com");
        result[0].Email.Should().NotContain("carla.bianchi");
    }

    [Fact]
    public async Task Handle_WholeEmail_LooksUpThatAddressAndNeverSearchesNames()
    {
        var carla = new UserProfileBuilder().WithEmail("carla@example.com").WithDisplayName("Carla").Build();
        _userProfileRepositoryMock
            .Setup(r => r.GetByEmailAsync("carla@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(carla);

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "carla@example.com", MaxResults: 10), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].DisplayName.Should().Be("Carla");
        result[0].Email.Should().Be("c***a@example.com");
        _userProfileRepositoryMock.Verify(
            r => r.SearchByDisplayNameAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WholeEmailOfNobody_ReturnsEmpty()
    {
        _userProfileRepositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "nessuno@example.com", MaxResults: 10), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PartialEmailFragment_IsANameSearchSoItCannotListAddresses()
    {
        // "gmail" has no "@": it goes to the name search only, which does not look at the email column.
        _userProfileRepositoryMock
            .Setup(r => r.SearchByDisplayNameAsync("gmail", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "gmail", MaxResults: 10), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _userProfileRepositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserWithoutDisplayName_IsShownByTheMaskedAddressNotAnEmptyName()
    {
        // The client schema requires a non-empty name; an exact-email hit on such a user used to fail the whole parse.
        _userProfileRepositoryMock
            .Setup(r => r.GetByEmailAsync("senza.nome@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileBuilder().WithEmail("senza.nome@example.com").WithDisplayName(null).Build());

        var result = await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "senza.nome@example.com", MaxResults: 10), TestContext.Current.CancellationToken);

        result[0].DisplayName.Should().Be("s***e@example.com");
        result[0].DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_WholeEmail_IsNotWrittenToTheLogs()
    {
        // For an email lookup the term is somebody's address.
        _userProfileRepositoryMock
            .Setup(r => r.GetByEmailAsync("carla@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfileBuilder().WithEmail("carla@example.com").Build());

        await _handler.Handle(
            new SearchUsersQuery(SearchQuery: "carla@example.com", MaxResults: 10), TestContext.Current.CancellationToken);

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("carla@example.com")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
