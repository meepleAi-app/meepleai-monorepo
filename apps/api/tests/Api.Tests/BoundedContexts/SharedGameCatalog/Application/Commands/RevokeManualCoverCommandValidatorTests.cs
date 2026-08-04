using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 3a-3 — identity guards for the manual-cover revoke. GameId (route) and
/// AdminId (session) must be non-empty; the "nothing to revoke" case is a 204 in the handler,
/// not a validation error.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class RevokeManualCoverCommandValidatorTests
{
    private static readonly Guid GameId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly RevokeManualCoverCommandValidator _validator = new();

    private static RevokeManualCoverCommand Valid() => new(GameId, AdminId);

    [Fact]
    public void Passes_ForAValidCommand()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Fails_WhenGameIdEmpty()
    {
        _validator.TestValidate(Valid() with { GameId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void Fails_WhenAdminIdEmpty()
    {
        _validator.TestValidate(Valid() with { AdminId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.AdminId);
    }
}
