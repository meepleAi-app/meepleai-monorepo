using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for GDPR Art. 20 data portability.
/// Aggregates all user data from across bounded contexts into a portable JSON structure.
/// </summary>
internal sealed class ExportUserDataCommandHandler
    : IQueryHandler<ExportUserDataCommand, ExportUserDataResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserAiConsentRepository _aiConsentRepository;
    private readonly IConversationMemoryRepository _conversationMemoryRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ILogger<ExportUserDataCommandHandler> _logger;

    public ExportUserDataCommandHandler(
        IUserRepository userRepository,
        IUserLibraryRepository libraryRepository,
        IChatThreadRepository chatThreadRepository,
        INotificationRepository notificationRepository,
        IUserAiConsentRepository aiConsentRepository,
        IConversationMemoryRepository conversationMemoryRepository,
        IApiKeyRepository apiKeyRepository,
        ILogger<ExportUserDataCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _aiConsentRepository = aiConsentRepository ?? throw new ArgumentNullException(nameof(aiConsentRepository));
        _conversationMemoryRepository = conversationMemoryRepository ?? throw new ArgumentNullException(nameof(conversationMemoryRepository));
        _apiKeyRepository = apiKeyRepository ?? throw new ArgumentNullException(nameof(apiKeyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExportUserDataResult> Handle(
        ExportUserDataCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new NotFoundException("User", command.UserId.ToString());

        // Gather data from all bounded contexts in parallel
        var libraryTask = _libraryRepository.GetUserGamesAsync(command.UserId, cancellationToken: cancellationToken);
        var chatThreadsTask = _chatThreadRepository.FindByUserIdAsync(command.UserId, cancellationToken);
        var notificationsTask = _notificationRepository.GetByUserIdAsync(command.UserId, unreadOnly: false, limit: 10000, cancellationToken: cancellationToken);
        var aiConsentTask = _aiConsentRepository.GetByUserIdAsync(command.UserId, cancellationToken);
        var memoryCountTask = _conversationMemoryRepository.CountByUserIdAsync(command.UserId, cancellationToken);
        var apiKeysTask = _apiKeyRepository.GetByUserIdAsync(command.UserId, cancellationToken);

        await Task.WhenAll(libraryTask, chatThreadsTask, notificationsTask, aiConsentTask, memoryCountTask, apiKeysTask).ConfigureAwait(false);

        var library = await libraryTask.ConfigureAwait(false);
        var chatThreads = await chatThreadsTask.ConfigureAwait(false);
        var notifications = await notificationsTask.ConfigureAwait(false);
        var aiConsent = await aiConsentTask.ConfigureAwait(false);
        var memoryCount = await memoryCountTask.ConfigureAwait(false);
        var apiKeys = await apiKeysTask.ConfigureAwait(false);

        // Map to export DTOs
        var profile = new ExportedUserProfile(
            Id: user.Id,
            Email: user.Email.ToString(),
            DisplayName: user.DisplayName,
            Role: user.Role.ToString(),
            Tier: user.Tier.ToString(),
            CreatedAt: user.CreatedAt,
            EmailVerified: user.EmailVerified,
            Has2FAEnabled: user.TotpSecret is not null);

        var preferences = new ExportedUserPreferences(
            Language: user.Language,
            EmailNotifications: user.EmailNotifications,
            Theme: user.Theme,
            DataRetentionDays: user.DataRetentionDays);

        var exportedLibrary = library.Select(entry => new ExportedLibraryEntry(
            GameId: entry.GameId,
            GameTitle: entry.GameId.ToString(), // Game title requires cross-context join
            State: entry.CurrentState.ToString(),
            IsFavorite: entry.IsFavorite,
            AddedAt: entry.AddedAt)).ToList();

        var exportedThreads = chatThreads.Select(thread => new ExportedChatThread(
            ThreadId: thread.Id,
            GameTitle: thread.Title,
            MessageCount: thread.MessageCount,
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt)).ToList();

        var exportedNotifications = notifications.Select(n => new ExportedNotification(
            Id: n.Id,
            Type: n.Type.ToString(),
            Title: n.Title,
            IsRead: n.IsRead,
            CreatedAt: n.CreatedAt)).ToList();

        ExportedAiConsent? exportedConsent = aiConsent is not null
            ? new ExportedAiConsent(
                ConsentGiven: aiConsent.ConsentedToAiProcessing,
                ConsentedAt: aiConsent.ConsentedAt,
                RevokedAt: null)
            : null;

        var summary = new ExportedDataSummary(
            TotalLibraryGames: library.Count,
            TotalChatThreads: chatThreads.Count,
            TotalNotifications: notifications.Count,
            TotalConversationMemories: memoryCount,
            TotalApiKeys: apiKeys.Count);

        _logger.LogInformation(
            "GDPR Art.20: Data export completed for UserId={UserId}. Library={Library}, Threads={Threads}, Notifications={Notifications}",
            command.UserId, library.Count, chatThreads.Count, notifications.Count);

        return new ExportUserDataResult(
            Profile: profile,
            Preferences: preferences,
            Library: exportedLibrary,
            ChatThreads: exportedThreads,
            Notifications: exportedNotifications,
            AiConsent: exportedConsent,
            Summary: summary,
            ExportedAt: DateTime.UtcNow);
    }
}
