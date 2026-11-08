using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application;

/// <summary>
/// #4846. The unsubscribe key of an invitation is written in the emails to its address and nowhere else: the
/// organizer receives the invitation back from the API, and whoever holds the key can silence that address for
/// every organizer. So no response shape of the game night invitations, and no event the outbox carries, may have a
/// property for it — a field added by a later change would otherwise hand it to exactly the people it is kept from.
/// </summary>
/// <remarks>
/// It reads the shapes by namespace, not from a list of five types: a new DTO next to these is covered without
/// anybody remembering to add it here.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4846")]
public sealed class GameNightUnsubscribeKeyNeverLeavesTests
{
    private const string DtoNamespace = "Api.BoundedContexts.GameManagement.Application.DTOs.GameNights";
    private const string EventNamespace = "Api.BoundedContexts.GameManagement.Domain.Events";

    private static IReadOnlyList<Type> ShapesThatLeaveTheAggregate()
    {
        var assembly = typeof(GameNightInvitationDto).Assembly;
        return assembly.GetTypes()
            .Where(t => string.Equals(t.Namespace, DtoNamespace, StringComparison.Ordinal)
                || (string.Equals(t.Namespace, EventNamespace, StringComparison.Ordinal)
                    && t.Name.StartsWith("GameNight", StringComparison.Ordinal)))
            .ToList();
    }

    [Fact]
    public void TheScan_ReallyCoversTheInvitationShapesItIsAbout()
    {
        var names = ShapesThatLeaveTheAggregate().Select(t => t.Name).ToList();

        names.Should().Contain([
            nameof(GameNightInvitationDto),
            nameof(PublicGameNightInvitationDto),
            nameof(GameNightGuestInvitationDto),
            "GameNightInvitationCreatedEvent",
            "GameNightInvitationRespondedEvent",
        ]);
    }

    [Fact]
    public void NoResponseShapeAndNoEvent_HasAPropertyForTheUnsubscribeKey()
    {
        var offenders = ShapesThatLeaveTheAggregate()
            .SelectMany(t => t.GetProperties().Select(p => $"{t.Name}.{p.Name}"))
            .Where(name => name.Contains("Unsubscribe", StringComparison.OrdinalIgnoreCase))
            .ToList();

        offenders.Should().BeEmpty("the key is written in the emails to the address and in no response or event");
    }
}
