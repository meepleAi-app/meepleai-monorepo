using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.GameManagement.TestHelpers;

/// <summary>
/// Builder for creating Game test instances with sensible defaults.
/// </summary>
internal class GameBuilder
{
    private Guid _id = Guid.NewGuid();
    private GameTitle _title = new("Catan");
    private Publisher? _publisher;
    private YearPublished? _yearPublished;
    private PlayerCount? _playerCount;
    private PlayTime? _playTime;
    private int? _bggId;
    private string? _bggMetadata;

    public GameBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public GameBuilder WithTitle(string title)
    {
        _title = new GameTitle(title);
        return this;
    }

    public GameBuilder WithTitle(GameTitle title)
    {
        _title = title;
        return this;
    }

    public GameBuilder WithPublisher(string publisherName)
    {
        _publisher = new Publisher(publisherName);
        return this;
    }

    public GameBuilder WithPublisher(Publisher publisher)
    {
        _publisher = publisher;
        return this;
    }

    public GameBuilder WithYearPublished(int year)
    {
        _yearPublished = new YearPublished(year);
        return this;
    }

    public GameBuilder WithPlayerCount(int min, int max)
    {
        _playerCount = new PlayerCount(min, max);
        return this;
    }

    public GameBuilder WithPlayTime(int minMinutes, int maxMinutes)
    {
        _playTime = new PlayTime(minMinutes, maxMinutes);
        return this;
    }

    public GameBuilder WithBggLink(int bggId, string? metadata = null)
    {
        _bggId = bggId;
        _bggMetadata = metadata;
        return this;
    }

    /// <summary>
    /// Creates a fully detailed game (all properties set).
    /// </summary>
    public GameBuilder WithAllDetails()
    {
        return this
            .WithPublisher("Days of Wonder")
            .WithYearPublished(2004)
            .WithPlayerCount(2, 5)
            .WithPlayTime(45, 60)
            .WithBggLink(9209, "{\"rating\": 7.4}");
    }

    /// <summary>
    /// Builds the Game instance.
    /// </summary>
    public Game Build()
    {
        var game = new Game(_id, _title, _publisher, _yearPublished, _playerCount, _playTime);

        if (_bggId.HasValue)
        {
            game.LinkToBgg(_bggId.Value, _bggMetadata);
        }

        return game;
    }

    /// <summary>
    /// Implicit conversion to Game for convenience.
    /// </summary>
    public static implicit operator Game(GameBuilder builder) => builder.Build();
}

