# Test Data Builders

**Reference Guide** for creating test data across MeepleAI bounded contexts.

---

## Overview

Test data builders provide consistent, reusable factory methods for creating domain entities in tests. Each bounded context should have builders for its core entities.

---

## Quick Reference

| Bounded Context | Builder Location | Key Entities |
|-----------------|------------------|--------------|
| Authentication | `TestHelpers/AuthenticationBuilders.cs` | User, Session |
| GameManagement | `TestHelpers/GameManagementBuilders.cs` | Game, GameSession |
| UserLibrary | `TestHelpers/UserLibraryBuilders.cs` | UserLibraryEntry |
| SharedGameCatalog | `TestHelpers/SharedGameCatalogBuilders.cs` | SharedGame, ShareRequest, Badge |
| UserNotifications | `TestHelpers/NotificationBuilders.cs` | Notification |

---

## Builder Patterns

### Simple Factory Method

For entities with few properties:

```csharp
public static class AuthenticationBuilders
{
    public static User CreateUser(
        Guid? id = null,
        string? email = null,
        string? username = null,
        bool emailConfirmed = true)
    {
        return new User(
            id: id ?? Guid.NewGuid(),
            email: email ?? $"user_{Guid.NewGuid():N}@test.com",
            username: username ?? $"testuser_{Guid.NewGuid():N}",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
            emailConfirmed: emailConfirmed,
            createdAt: DateTime.UtcNow);
    }

    public static Session CreateSession(
        Guid? id = null,
        Guid? userId = null,
        bool isActive = true)
    {
        return new Session(
            id: id ?? Guid.NewGuid(),
            userId: userId ?? Guid.NewGuid(),
            token: Guid.NewGuid().ToString("N"),
            expiresAt: DateTime.UtcNow.AddHours(24),
            isActive: isActive,
            createdAt: DateTime.UtcNow);
    }
}
```

### Fluent Builder Pattern

For entities with many properties or complex setup:

```csharp
public class GameBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _title = "Test Game";
    private int _minPlayers = 2;
    private int _maxPlayers = 4;
    private int _playingTimeMinutes = 60;
    private int _minAge = 10;
    private decimal _complexityRating = 2.5m;
    private decimal _averageRating = 7.5m;
    private string _imageUrl = "https://example.com/image.jpg";
    private GameStatus _status = GameStatus.Published;
    private Guid _createdBy = Guid.NewGuid();

    public GameBuilder WithId(Guid id) { _id = id; return this; }
    public GameBuilder WithTitle(string title) { _title = title; return this; }
    public GameBuilder WithPlayers(int min, int max) { _minPlayers = min; _maxPlayers = max; return this; }
    public GameBuilder WithPlayingTime(int minutes) { _playingTimeMinutes = minutes; return this; }
    public GameBuilder WithMinAge(int age) { _minAge = age; return this; }
    public GameBuilder WithComplexity(decimal rating) { _complexityRating = rating; return this; }
    public GameBuilder WithRating(decimal rating) { _averageRating = rating; return this; }
    public GameBuilder WithStatus(GameStatus status) { _status = status; return this; }
    public GameBuilder WithCreatedBy(Guid userId) { _createdBy = userId; return this; }
    public GameBuilder AsDraft() { _status = GameStatus.Draft; return this; }
    public GameBuilder AsPublished() { _status = GameStatus.Published; return this; }

    public Game Build()
    {
        return new Game(
            id: _id,
            title: _title,
            yearPublished: 2020,
            description: "Test game description",
            minPlayers: _minPlayers,
            maxPlayers: _maxPlayers,
            playingTimeMinutes: _playingTimeMinutes,
            minAge: _minAge,
            complexityRating: _complexityRating,
            averageRating: _averageRating,
            imageUrl: _imageUrl,
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            status: _status,
            createdBy: _createdBy,
            modifiedBy: null,
            createdAt: DateTime.UtcNow.AddDays(-30),
            modifiedAt: null,
            isDeleted: false,
            bggId: 12345);
    }

    public static GameBuilder Create() => new();
}

// Usage:
var game = GameBuilder.Create()
    .WithTitle("Catan")
    .WithPlayers(3, 4)
    .WithComplexity(2.3m)
    .AsPublished()
    .Build();
```

---

## Bounded Context Builders

### Authentication

```csharp
public static class AuthenticationBuilders
{
    public static User CreateUser(
        Guid? id = null,
        string? email = null,
        string? username = null,
        UserRole role = UserRole.User,
        bool emailConfirmed = true,
        bool twoFactorEnabled = false)
    {
        var userId = id ?? Guid.NewGuid();
        return new User(
            id: userId,
            email: email ?? $"user_{userId:N}@test.com",
            username: username ?? $"testuser_{userId:N[..8]}",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
            role: role,
            emailConfirmed: emailConfirmed,
            twoFactorEnabled: twoFactorEnabled,
            createdAt: DateTime.UtcNow);
    }

    public static User CreateAdmin(Guid? id = null, string? email = null)
        => CreateUser(id, email, role: UserRole.Admin);

    public static Session CreateSession(Guid? userId = null, TimeSpan? expiresIn = null)
    {
        return new Session(
            id: Guid.NewGuid(),
            userId: userId ?? Guid.NewGuid(),
            token: Guid.NewGuid().ToString("N"),
            refreshToken: Guid.NewGuid().ToString("N"),
            expiresAt: DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromHours(24)),
            isActive: true,
            createdAt: DateTime.UtcNow);
    }

    public static Session CreateExpiredSession(Guid? userId = null)
        => CreateSession(userId, TimeSpan.FromHours(-1));
}
```

### GameManagement

```csharp
public static class GameManagementBuilders
{
    public static Game CreateGame(
        Guid? id = null,
        string? title = null,
        int minPlayers = 2,
        int maxPlayers = 4,
        GameStatus status = GameStatus.Published,
        Guid? createdBy = null)
    {
        var gameId = id ?? Guid.NewGuid();
        return new Game(
            id: gameId,
            title: title ?? $"Test Game {gameId:N[..8]}",
            yearPublished: 2020,
            description: "A test game for unit testing",
            minPlayers: minPlayers,
            maxPlayers: maxPlayers,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            status: status,
            createdBy: createdBy ?? Guid.NewGuid(),
            modifiedBy: null,
            createdAt: DateTime.UtcNow.AddDays(-30),
            modifiedAt: null,
            isDeleted: false,
            bggId: null);
    }

    public static GameSession CreateGameSession(
        Guid? id = null,
        Guid? gameId = null,
        Guid? userId = null,
        GameSessionStatus status = GameSessionStatus.InProgress)
    {
        return new GameSession(
            id: id ?? Guid.NewGuid(),
            gameId: gameId ?? Guid.NewGuid(),
            userId: userId ?? Guid.NewGuid(),
            status: status,
            startedAt: DateTime.UtcNow,
            endedAt: status == GameSessionStatus.Completed ? DateTime.UtcNow.AddHours(1) : null);
    }
}
```

### UserLibrary

```csharp
public static class UserLibraryBuilders
{
    public static UserLibraryEntry CreateEntry(
        Guid? id = null,
        Guid? userId = null,
        Guid? gameId = null,
        string? notes = null,
        bool isFavorite = false)
    {
        var entry = new UserLibraryEntry(
            id: id ?? Guid.NewGuid(),
            userId: userId ?? Guid.NewGuid(),
            gameId: gameId ?? Guid.NewGuid());

        if (notes != null)
            entry.UpdateNotes(LibraryNotes.FromNullable(notes));

        if (isFavorite)
            entry.MarkAsFavorite();

        return entry;
    }

    public static UserLibraryEntry CreateFavoriteEntry(Guid? userId = null, Guid? gameId = null)
        => CreateEntry(userId: userId, gameId: gameId, isFavorite: true);
}
```

### SharedGameCatalog

```csharp
public static class SharedGameCatalogBuilders
{
    public static SharedGame CreateSharedGame(
        Guid? id = null,
        string? title = null,
        int yearPublished = 2020,
        GameStatus status = GameStatus.Published)
    {
        var gameId = id ?? Guid.NewGuid();
        return new SharedGame(
            id: gameId,
            title: title ?? $"Shared Game {gameId:N[..8]}",
            yearPublished: yearPublished,
            description: "A shared game from the catalog",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            status: status,
            createdBy: Guid.NewGuid(),
            modifiedBy: null,
            createdAt: DateTime.UtcNow.AddDays(-30),
            modifiedAt: null,
            isDeleted: false,
            bggId: 12345);
    }

    public static ShareRequest CreateShareRequest(
        Guid? id = null,
        Guid? gameId = null,
        Guid? requesterId = null,
        ShareRequestStatus status = ShareRequestStatus.Pending)
    {
        return new ShareRequest(
            id: id ?? Guid.NewGuid(),
            gameId: gameId ?? Guid.NewGuid(),
            requesterId: requesterId ?? Guid.NewGuid(),
            status: status,
            createdAt: DateTime.UtcNow);
    }

    public static Badge CreateBadge(
        Guid? id = null,
        string? name = null,
        BadgeType type = BadgeType.Contributor)
    {
        var badgeId = id ?? Guid.NewGuid();
        return new Badge(
            id: badgeId,
            name: name ?? $"Test Badge {badgeId:N[..8]}",
            description: "A test badge",
            type: type,
            iconUrl: "https://example.com/badge.png",
            createdAt: DateTime.UtcNow);
    }
}
```

### UserNotifications

```csharp
public static class NotificationBuilders
{
    public static Notification CreateNotification(
        Guid? id = null,
        Guid? userId = null,
        NotificationType type = NotificationType.Info,
        NotificationSeverity severity = NotificationSeverity.Normal,
        string? title = null,
        string? message = null,
        bool isRead = false)
    {
        var notificationId = id ?? Guid.NewGuid();
        var notification = new Notification(
            id: notificationId,
            userId: userId ?? Guid.NewGuid(),
            type: type,
            severity: severity,
            title: title ?? "Test Notification",
            message: message ?? "This is a test notification message.",
            createdAt: DateTime.UtcNow);

        if (isRead)
            notification.MarkAsRead();

        return notification;
    }

    public static Notification CreateUnreadNotification(Guid? userId = null)
        => CreateNotification(userId: userId, isRead: false);

    public static Notification CreateReadNotification(Guid? userId = null)
        => CreateNotification(userId: userId, isRead: true);

    public static Notification CreateUrgentNotification(Guid? userId = null, string? message = null)
        => CreateNotification(
            userId: userId,
            type: NotificationType.Alert,
            severity: NotificationSeverity.High,
            title: "Urgent",
            message: message);
}
```

---

## Usage in Tests

### Unit Tests

```csharp
[Fact]
public async Task Handle_WithValidCommand_ReturnsEntry()
{
    // Arrange
    var userId = Guid.NewGuid();
    var gameId = Guid.NewGuid();
    var entry = UserLibraryBuilders.CreateEntry(userId: userId, gameId: gameId);
    var sharedGame = SharedGameCatalogBuilders.CreateSharedGame(id: gameId);

    _mockLibraryRepository
        .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(entry);

    _mockSharedGameRepository
        .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(sharedGame);

    // Act & Assert
    var result = await _handler.Handle(new GetEntryQuery(userId, gameId), CancellationToken.None);
    result.Should().NotBeNull();
}
```

### Integration Tests

```csharp
[Fact]
public async Task GetByIdAsync_ExistingGame_ReturnsGame()
{
    // Arrange
    var game = GameManagementBuilders.CreateGame(title: "Integration Test Game");
    await _repository.AddAsync(game, CancellationToken.None);
    await _dbContext.SaveChangesAsync();
    _dbContext.ChangeTracker.Clear();

    // Act
    var result = await _repository.GetByIdAsync(game.Id, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result!.Title.Should().Be("Integration Test Game");
}
```

### Seeding Multiple Related Entities

```csharp
[Fact]
public async Task GetUserLibrary_WithMultipleEntries_ReturnsAll()
{
    // Arrange
    var userId = Guid.NewGuid();

    // Create related entities
    var games = Enumerable.Range(1, 5)
        .Select(i => SharedGameCatalogBuilders.CreateSharedGame(title: $"Game {i}"))
        .ToList();

    foreach (var game in games)
    {
        await _sharedGameRepository.AddAsync(game, CancellationToken.None);
    }

    var entries = games.Select(g =>
        UserLibraryBuilders.CreateEntry(userId: userId, gameId: g.Id))
        .ToList();

    foreach (var entry in entries)
    {
        await _libraryRepository.AddAsync(entry, CancellationToken.None);
    }

    await _dbContext.SaveChangesAsync();
    _dbContext.ChangeTracker.Clear();

    // Act
    var result = await _libraryRepository.GetByUserIdAsync(userId, CancellationToken.None);

    // Assert
    result.Should().HaveCount(5);
}
```

---

## Best Practices

### DO

- Use unique identifiers (GUIDs) by default to prevent collisions
- Provide sensible defaults for all optional parameters
- Create convenience methods for common scenarios (e.g., `CreateAdmin()`, `CreateExpiredSession()`)
- Keep builders stateless when possible
- Include timestamps that make sense for the entity lifecycle

### DON'T

- Don't share mutable builder instances between tests
- Don't hardcode IDs unless specifically testing ID-based behavior
- Don't create entities with invalid state (violating invariants)
- Don't over-engineer builders for simple entities

---

**Last Updated**: 2026-01-27
**Maintainer**: Backend Team
