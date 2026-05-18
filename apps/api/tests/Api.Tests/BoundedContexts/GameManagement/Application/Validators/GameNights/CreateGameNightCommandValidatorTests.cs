using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Validators.GameNights;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Unit tests for <see cref="CreateGameNightCommandValidator"/>.
/// Issue #950 (W1-PR1): extend command with InvitedEmails + combined invitee limit.
/// Covers spec §12b BE-7 (combined limit) + RFC 5321 email format + length cap.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreateGameNightCommandValidatorTests
{
    private readonly CreateGameNightCommandValidator _validator = new();

    private static CreateGameNightCommand MakeCommand(
        List<Guid>? invitedUserIds = null,
        List<string>? invitedEmails = null)
    {
        return new CreateGameNightCommand(
            UserId: Guid.NewGuid(),
            Title: "Test Night",
            ScheduledAt: DateTimeOffset.UtcNow.AddHours(24),
            Description: null,
            Location: null,
            MaxPlayers: null,
            GameIds: null,
            InvitedUserIds: invitedUserIds,
            InvitedEmails: invitedEmails);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Combined invitee limit (BE-7): InvitedUserIds.Count + InvitedEmails.Count ≤ 49
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_With25UserIds_24Emails_TotalAt49_Passes()
    {
        var userIds = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToList();
        var emails = Enumerable.Range(0, 24).Select(i => $"user{i}@example.com").ToList();
        var cmd = MakeCommand(userIds, emails);

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.InvitedEmails);
        result.ShouldNotHaveValidationErrorFor(x => x.InvitedUserIds);
    }

    [Fact]
    public void Validate_With40UserIds_10Emails_TotalAt50_Fails()
    {
        var userIds = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToList();
        var emails = Enumerable.Range(0, 10).Select(i => $"user{i}@example.com").ToList();
        var cmd = MakeCommand(userIds, emails);

        var result = _validator.TestValidate(cmd);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_With49UserIds_NoEmails_Passes()
    {
        var userIds = Enumerable.Range(0, 49).Select(_ => Guid.NewGuid()).ToList();
        var cmd = MakeCommand(userIds, null);

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.InvitedUserIds);
    }

    [Fact]
    public void Validate_WithNoUserIds_49Emails_Passes()
    {
        var emails = Enumerable.Range(0, 49).Select(i => $"user{i}@example.com").ToList();
        var cmd = MakeCommand(null, emails);

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.InvitedEmails);
    }

    [Fact]
    public void Validate_WithNoUserIds_50Emails_Fails()
    {
        var emails = Enumerable.Range(0, 50).Select(i => $"user{i}@example.com").ToList();
        var cmd = MakeCommand(null, emails);

        var result = _validator.TestValidate(cmd);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_With50UserIds_NoEmails_Fails()
    {
        // Pre-existing rule: InvitedUserIds.Count ≤ 49.
        // This regression test ensures the legacy rule still fires when no emails are provided.
        var userIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
        var cmd = MakeCommand(userIds, null);

        var result = _validator.TestValidate(cmd);

        Assert.False(result.IsValid);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Email format (RFC 5321 — basic format gate)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithInvalidEmail_NoAtSymbol_Fails()
    {
        var cmd = MakeCommand(null, new List<string> { "no-at-sign" });

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.InvitedEmails);
    }

    [Fact]
    public void Validate_WithEmptyEmail_Fails()
    {
        var cmd = MakeCommand(null, new List<string> { "" });

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.InvitedEmails);
    }

    [Fact]
    public void Validate_WithWhitespaceEmail_Fails()
    {
        var cmd = MakeCommand(null, new List<string> { "   " });

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.InvitedEmails);
    }

    [Fact]
    public void Validate_WithValidEmails_Passes()
    {
        var cmd = MakeCommand(null, new List<string>
        {
            "alice@example.com",
            "bob.smith+tag@example.co.uk",
            "carol_2024@sub.example.org",
        });

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.InvitedEmails);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Email length (RFC 5321 caps at 254 in practice; spec §7b.4 picks 200)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithEmailTooLong_Fails()
    {
        var longLocal = new string('a', 195);
        var tooLong = $"{longLocal}@example.com"; // 195 + 12 = 207 > 200
        var cmd = MakeCommand(null, new List<string> { tooLong });

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.InvitedEmails);
    }

    [Fact]
    public void Validate_WithEmailAt200Chars_Passes()
    {
        var localLen = 200 - "@example.com".Length;
        var atLimit = $"{new string('a', localLen)}@example.com";
        Assert.Equal(200, atLimit.Length);
        var cmd = MakeCommand(null, new List<string> { atLimit });

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.InvitedEmails);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Deduplication (case-insensitive + whitespace-trim) — code review feedback PR #1289
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithExactDuplicateEmails_Fails()
    {
        var cmd = MakeCommand(null, new List<string> { "alice@example.com", "alice@example.com" });

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.InvitedEmails);
    }

    [Fact]
    public void Validate_WithCaseInsensitiveDuplicateEmails_Fails()
    {
        var cmd = MakeCommand(null, new List<string> { "Alice@Example.com", "alice@example.com" });

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.InvitedEmails);
    }

    [Fact]
    public void Validate_WithUniqueEmailsDifferingByCase_Passes()
    {
        // Sanity: only true duplicates after normalization trigger the rule.
        var cmd = MakeCommand(null, new List<string> { "alice@example.com", "BOB@example.com" });

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.InvitedEmails);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Backward compatibility: null/empty InvitedEmails preserves existing behavior
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithNullInvitedEmails_Passes()
    {
        var cmd = MakeCommand(null, null);

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.InvitedEmails);
    }

    [Fact]
    public void Validate_WithEmptyInvitedEmails_Passes()
    {
        var cmd = MakeCommand(null, new List<string>());

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(x => x.InvitedEmails);
    }
}
