using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Resolves the correct ISlackMessageBuilder for a given NotificationType.
/// Falls back to GenericSlackBuilder for unhandled types.
/// </summary>
internal sealed class SlackMessageBuilderFactory
{
    private readonly IEnumerable<ISlackMessageBuilder> _builders;
    private readonly GenericSlackBuilder _fallback;

    public SlackMessageBuilderFactory(IEnumerable<ISlackMessageBuilder> builders, GenericSlackBuilder fallback)
    {
        _builders = builders;
        _fallback = fallback;
    }

    public ISlackMessageBuilder GetBuilder(NotificationType type)
    {
        return _builders.FirstOrDefault(b => b.CanHandle(type)) ?? _fallback;
    }
}
