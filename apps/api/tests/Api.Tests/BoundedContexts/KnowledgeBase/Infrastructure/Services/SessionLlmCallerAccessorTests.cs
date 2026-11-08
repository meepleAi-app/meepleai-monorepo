using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// #4164 (ADR-095 fetta 1b): il chiamante di una chiamata LLM letto dalla sessione della richiesta.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SessionLlmCallerAccessorTests
{
    private static readonly UserDto Player = User(Guid.Parse("11111111-2222-4333-8444-555555555555"), "user");
    private static readonly UserDto Admin = User(Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"), "admin");

    private static UserDto User(Guid id, string role) =>
        new(id, $"{role}@example.test", role, role, "free", DateTime.UtcNow, false, null, 1, 0);

    private static SessionLlmCallerAccessor AccessorFor(object? sessionItem, bool withHttpContext = true)
    {
        var accessor = new HttpContextAccessor();
        if (withHttpContext)
        {
            var context = new DefaultHttpContext();
            if (sessionItem is not null)
            {
                context.Items[nameof(SessionStatusDto)] = sessionItem;
            }

            accessor.HttpContext = context;
        }

        return new SessionLlmCallerAccessor(accessor);
    }

    private static SessionStatusDto Session(Principal? principal, bool isValid = true) =>
        new(isValid, principal, DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

    [Fact]
    public void AuthenticatedRequest_IsTheSubject()
    {
        var caller = AccessorFor(Session(new Principal(Player, Actor: null))).Current;

        caller.Should().NotBeNull();
        caller!.UserId.Should().Be(Player.Id);
        caller.RoleName.Should().Be("user");
        caller.Tier.Should().Be(LlmUserTier.User);
    }

    [Fact]
    public void Impersonation_ChargesTheImpersonatedUser_NotTheAdmin()
    {
        // Il costo alimenta il budget: per le chiavi di quota il contratto di Principal
        // prescrive il soggetto, non EffectiveActor.
        var caller = AccessorFor(Session(new Principal(Player, Actor: Admin))).Current;

        caller!.UserId.Should().Be(Player.Id);
    }

    [Fact]
    public void AdminRequest_MapsToTheAdminTier()
    {
        AccessorFor(Session(new Principal(Admin, Actor: null))).Current!.Tier.Should().Be(LlmUserTier.Admin);
    }

    [Fact]
    public void InvalidSession_HasNoCaller()
    {
        AccessorFor(Session(new Principal(Player, Actor: null), isValid: false)).Current.Should().BeNull();
    }

    [Fact]
    public void AnonymousRequest_HasNoCaller()
    {
        AccessorFor(sessionItem: null).Current.Should().BeNull();
    }

    [Fact]
    public void OutsideARequest_HasNoCaller()
    {
        AccessorFor(sessionItem: null, withHttpContext: false).Current.Should().BeNull();
    }
}
