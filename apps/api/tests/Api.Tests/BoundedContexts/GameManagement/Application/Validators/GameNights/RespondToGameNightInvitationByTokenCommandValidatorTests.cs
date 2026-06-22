using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Validators.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Unit tests for <see cref="RespondToGameNightInvitationByTokenCommandValidator"/>.
/// Issue #1169 — confirms the optional <c>ResponderDisplayName</c> length cap (≤120)
/// fires the validation rule before the command reaches the MediatR handler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class RespondToGameNightInvitationByTokenCommandValidatorTests
{
    private const string ValidToken = "abcdefghijklmnopqrstuv"; // 22 chars, base62

    private readonly RespondToGameNightInvitationByTokenCommandValidator _validator = new();

    [Fact]
    public void NullDisplayName_Passes()
    {
        var cmd = new RespondToGameNightInvitationByTokenCommand(
            ValidToken, GameNightInvitationStatus.Accepted, ResponderUserId: null,
            ResponderDisplayName: null);

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.ResponderDisplayName);
    }

    [Fact]
    public void EmptyDisplayName_Passes()
    {
        var cmd = new RespondToGameNightInvitationByTokenCommand(
            ValidToken, GameNightInvitationStatus.Accepted, ResponderUserId: null,
            ResponderDisplayName: "");

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.ResponderDisplayName);
    }

    [Fact]
    public void WhitespaceOnlyDisplayName_Passes()
    {
        var cmd = new RespondToGameNightInvitationByTokenCommand(
            ValidToken, GameNightInvitationStatus.Accepted, ResponderUserId: null,
            ResponderDisplayName: "   ");

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.ResponderDisplayName);
    }

    [Fact]
    public void DisplayNameAtMaxLength_Passes()
    {
        var atMax = new string('A', 120);
        var cmd = new RespondToGameNightInvitationByTokenCommand(
            ValidToken, GameNightInvitationStatus.Accepted, ResponderUserId: null,
            ResponderDisplayName: atMax);

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.ResponderDisplayName);
    }

    [Fact]
    public void DisplayNameOverMaxLength_Fails()
    {
        var overMax = new string('A', 121);
        var cmd = new RespondToGameNightInvitationByTokenCommand(
            ValidToken, GameNightInvitationStatus.Accepted, ResponderUserId: null,
            ResponderDisplayName: overMax);

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.ResponderDisplayName);
    }

    [Fact]
    public void ShortDisplayName_Passes()
    {
        var cmd = new RespondToGameNightInvitationByTokenCommand(
            ValidToken, GameNightInvitationStatus.Accepted, ResponderUserId: null,
            ResponderDisplayName: "Marco");

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.ResponderDisplayName);
    }
}
