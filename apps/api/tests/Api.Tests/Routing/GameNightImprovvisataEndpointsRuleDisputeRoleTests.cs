using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.Routing;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// #4751 — the role the dispute endpoint hands to the command, because the RAG ownership gate (ADR-096) lets
/// administrators through and nobody else. The handler of the Arbitro is tested with the role it is given;
/// this is where the role comes from. A role lost here (a null passed instead) would send every administrator
/// to the no-rulebook branch, and would not fail any other test.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4751")]
public sealed class GameNightImprovvisataEndpointsRuleDisputeRoleTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid CallerId = Guid.NewGuid();

    private static UserDto User(string role, Guid? id = null)
        => new(
            id ?? CallerId, "user@example.test", "Alice", role, "free", DateTime.UtcNow,
            IsTwoFactorEnabled: false, TwoFactorEnabledAt: null, Level: 1, ExperiencePoints: 0);

    private static DefaultHttpContext ContextWithSession(Principal principal, bool valid = true)
    {
        var context = new DefaultHttpContext();
        context.Items[nameof(SessionStatusDto)] = new SessionStatusDto(valid, principal, null, null);
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, CallerId.ToString())], "test"));
        return context;
    }

    private static DefaultHttpContext ContextWithoutSession()
    {
        // API-key authentication: a user is authenticated, and there is no session item
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, CallerId.ToString())], "api-key"));
        return context;
    }

    // ─── ResolveCallerRole ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    [InlineData("Editor")]
    [InlineData("User")]
    public void ResolveCallerRole_IsTheRoleOfTheSession(string role)
    {
        var context = ContextWithSession(new Principal(User(role), Actor: null));

        GameNightImprovvisataEndpoints.ResolveCallerRole(context).Should().Be(role);
    }

    [Fact]
    public void ResolveCallerRole_WhenImpersonating_IsTheRoleOfTheRealActor()
    {
        // Authorization reads EffectiveActor (the admin who impersonates), as every other check does
        var context = ContextWithSession(new Principal(User("User"), Actor: User("SuperAdmin", Guid.NewGuid())));

        GameNightImprovvisataEndpoints.ResolveCallerRole(context).Should().Be("SuperAdmin");
    }

    [Fact]
    public void ResolveCallerRole_WithoutASessionItem_IsNull_SoTheGateTreatsTheCallerAsAPlainUser()
    {
        GameNightImprovvisataEndpoints.ResolveCallerRole(ContextWithoutSession()).Should().BeNull();
    }

    [Fact]
    public void ResolveCallerRole_WithAnInvalidSession_IsNull()
    {
        var context = ContextWithSession(new Principal(User("Admin"), Actor: null), valid: false);

        GameNightImprovvisataEndpoints.ResolveCallerRole(context).Should().BeNull();
    }

    // ─── The command the endpoint builds ──────────────────────────────────────

    private static async Task<SubmitRuleDisputeCommand> CommandBuiltFor(HttpContext context)
    {
        SubmitRuleDisputeCommand? sent = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<SubmitRuleDisputeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<RuleDisputeResponse>, CancellationToken>((command, _) => sent = (SubmitRuleDisputeCommand)command)
            .ReturnsAsync(new RuleDisputeResponse(Guid.NewGuid(), "verdetto", new List<string>(), null));

        await GameNightImprovvisataEndpoints.HandleSubmitRuleDispute(
            SessionId,
            new GameNightImprovvisataEndpoints.SubmitRuleDisputeRequest("Il ladro si sposta subito?", "Alice"),
            mediator.Object,
            context,
            CancellationToken.None);

        sent.Should().NotBeNull();
        return sent!;
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("User")]
    public async Task TheEndpoint_PassesTheRoleOfTheSessionToTheCommand(string role)
    {
        var command = await CommandBuiltFor(ContextWithSession(new Principal(User(role), Actor: null)));

        command.CallerRole.Should().Be(role);
        command.CallerUserId.Should().Be(CallerId);
        command.SessionId.Should().Be(SessionId);
        command.Description.Should().Be("Il ladro si sposta subito?");
        command.RaisedByPlayerName.Should().Be("Alice");
    }

    [Fact]
    public async Task TheEndpoint_WithoutASession_PassesNoRole_ButStillTheCaller()
    {
        var command = await CommandBuiltFor(ContextWithoutSession());

        command.CallerRole.Should().BeNull();
        command.CallerUserId.Should().Be(CallerId);
    }
}
