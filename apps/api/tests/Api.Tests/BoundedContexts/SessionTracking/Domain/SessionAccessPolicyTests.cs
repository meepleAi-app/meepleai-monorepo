using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// #4883: the one rule behind <c>Session.IsAccessibleBy</c> and the query handlers that read the persisted
/// row. Owner and registered participants only; an empty id and a guest never match.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SessionAccessPolicyTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Player = Guid.NewGuid();

    [Fact]
    public void Owner_IsAccessible_EvenWithNoParticipants()
    {
        SessionAccessPolicy.IsAccessible(Owner, [], Owner).Should().BeTrue();
    }

    [Fact]
    public void RegisteredParticipant_IsAccessible()
    {
        SessionAccessPolicy.IsAccessible(Owner, [Owner, Player], Player).Should().BeTrue();
    }

    [Fact]
    public void Stranger_IsNotAccessible()
    {
        SessionAccessPolicy.IsAccessible(Owner, [Owner, Player], Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void EmptyId_IsNotAccessible_EvenWhenAGuestParticipantHasNoUser()
    {
        // A guest is a participant with no user (null): an empty caller id must not be read as "that guest".
        SessionAccessPolicy.IsAccessible(Owner, [Owner, null], Guid.Empty).Should().BeFalse();
    }

    [Fact]
    public void GuestWithNoUser_DoesNotGrantAccessToAnyone()
    {
        SessionAccessPolicy.IsAccessible(Owner, [null, null], Guid.NewGuid()).Should().BeFalse();
    }
}
