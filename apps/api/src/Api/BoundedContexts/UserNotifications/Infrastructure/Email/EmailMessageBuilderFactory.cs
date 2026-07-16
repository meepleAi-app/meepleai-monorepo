using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Email;

/// <summary>
/// Resolves the correct <see cref="IEmailMessageBuilder"/> for a given <see cref="NotificationType"/>.
/// Falls back to <see cref="GenericEmailBuilder"/> for unhandled types.
/// Mirrors <c>SlackMessageBuilderFactory</c> so richer per-payload-type email builders can be added
/// later without touching the processor job (issue #3026).
/// </summary>
internal sealed class EmailMessageBuilderFactory
{
    private readonly IEnumerable<IEmailMessageBuilder> _builders;
    private readonly GenericEmailBuilder _fallback;

    public EmailMessageBuilderFactory(IEnumerable<IEmailMessageBuilder> builders, GenericEmailBuilder fallback)
    {
        _builders = builders;
        _fallback = fallback;
    }

    public IEmailMessageBuilder GetBuilder(NotificationType type)
    {
        return _builders.FirstOrDefault(b => b.CanHandle(type)) ?? _fallback;
    }
}
