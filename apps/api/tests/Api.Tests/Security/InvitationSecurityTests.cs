using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.Security;

/// <summary>
/// Security-focused tests for the admin invitation flow.
/// Validates password policy, uniform error responses, XSS prevention, and CSV injection resistance.
/// Issue #124: Admin invitation flow.
/// </summary>
[Trait("Category", TestCategories.Security)]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "124")]
public sealed class InvitationSecurityTests
{
    #region 1. Weak Password Rejected — ActivateInvitedAccountCommandValidator

    [Theory]
    [InlineData("123")]
    [InlineData("password")]
    [InlineData("abcdefgh")]
    [InlineData("12345678")]
    [InlineData("ABCDEFGH")]
    [InlineData("short")]
    [InlineData("")]
    [InlineData("nouppercase1!")]
    [InlineData("NOLOWERCASE1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSpecial1a")]
    public void ActivateInvitedAccountValidator_WeakPassword_ShouldReject(string weakPassword)
    {
        // Arrange
        var validator = new ActivateInvitedAccountCommandValidator();
        var command = new ActivateInvitedAccountCommand("valid-token-abc", weakPassword);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse(
            $"Password '{weakPassword}' should be rejected by the validator");
    }

    [Fact]
    public void ActivateInvitedAccountValidator_StrongPassword_ShouldAccept()
    {
        // Arrange
        var validator = new ActivateInvitedAccountCommandValidator();
        var command = new ActivateInvitedAccountCommand("valid-token-abc", "Str0ng!Pass");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue("A password meeting all rules should be accepted");
    }

    [Fact]
    public void ActivateInvitedAccountValidator_EmptyToken_ShouldReject()
    {
        // Arrange
        var validator = new ActivateInvitedAccountCommandValidator();
        var command = new ActivateInvitedAccountCommand("", "Str0ng!Pass");

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse("Empty token should be rejected");
        result.Errors.Should().Contain(e => e.PropertyName == "Token");
    }

    [Fact]
    public void ActivateInvitedAccountValidator_PasswordExceeds128Chars_ShouldReject()
    {
        // Arrange
        var validator = new ActivateInvitedAccountCommandValidator();
        var longPassword = "Aa1!" + new string('x', 125); // 129 chars total
        var command = new ActivateInvitedAccountCommand("valid-token", longPassword);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse("Password exceeding 128 characters should be rejected");
    }

    #endregion

    #region 2. Uniform Error for Expired/Revoked/NotFound — ValidateInvitationTokenQueryHandler

    [Fact]
    public async Task ValidateToken_NotFound_ReturnsInvalid()
    {
        // Arrange
        var invitationRepo = new Mock<IInvitationTokenRepository>();
        var userRepo = new Mock<IUserRepository>();

        invitationRepo
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        var handler = new ValidateInvitationTokenQueryHandler(invitationRepo.Object, userRepo.Object);

        // Act
        var result = await handler.Handle(new ValidateInvitationTokenQuery("nonexistent-token"), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().Be("invalid");
    }

    [Fact]
    public async Task ValidateToken_Expired_ReturnsInvalid()
    {
        // Arrange
        var invitationRepo = new Mock<IInvitationTokenRepository>();
        var userRepo = new Mock<IUserRepository>();

        var expiredInvitation = CreateInvitationWithStatus(InvitationStatus.Expired, DateTime.UtcNow.AddDays(-1));

        invitationRepo
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredInvitation);

        var handler = new ValidateInvitationTokenQueryHandler(invitationRepo.Object, userRepo.Object);

        // Act
        var result = await handler.Handle(new ValidateInvitationTokenQuery("expired-token"), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().Be("invalid");
    }

    [Fact]
    public async Task ValidateToken_Revoked_ReturnsInvalid()
    {
        // Arrange
        var invitationRepo = new Mock<IInvitationTokenRepository>();
        var userRepo = new Mock<IUserRepository>();

        var revokedInvitation = CreateInvitationWithStatus(InvitationStatus.Revoked, DateTime.UtcNow.AddDays(5));

        invitationRepo
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedInvitation);

        var handler = new ValidateInvitationTokenQueryHandler(invitationRepo.Object, userRepo.Object);

        // Act
        var result = await handler.Handle(new ValidateInvitationTokenQuery("revoked-token"), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().Be("invalid");
    }

    [Fact]
    public async Task ValidateToken_UniformErrorForExpiredRevokedNotFound_AllReturnSameReason()
    {
        // Arrange — Three different failure scenarios must produce the same error reason
        var invitationRepo = new Mock<IInvitationTokenRepository>();
        var userRepo = new Mock<IUserRepository>();
        var handler = new ValidateInvitationTokenQueryHandler(invitationRepo.Object, userRepo.Object);

        // 1. Not found
        invitationRepo
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);
        var notFoundResult = await handler.Handle(new ValidateInvitationTokenQuery("notfound"), CancellationToken.None);

        // 2. Expired
        var expiredInvitation = CreateInvitationWithStatus(InvitationStatus.Expired, DateTime.UtcNow.AddDays(-1));
        invitationRepo
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredInvitation);
        var expiredResult = await handler.Handle(new ValidateInvitationTokenQuery("expired"), CancellationToken.None);

        // 3. Revoked
        var revokedInvitation = CreateInvitationWithStatus(InvitationStatus.Revoked, DateTime.UtcNow.AddDays(5));
        invitationRepo
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedInvitation);
        var revokedResult = await handler.Handle(new ValidateInvitationTokenQuery("revoked"), CancellationToken.None);

        // Assert — All three must produce identical error reason to prevent state enumeration
        notFoundResult.ErrorReason.Should().Be("invalid");
        expiredResult.ErrorReason.Should().Be("invalid");
        revokedResult.ErrorReason.Should().Be("invalid");

        // All three should be indistinguishable to the caller
        notFoundResult.ErrorReason.Should().Be(expiredResult.ErrorReason);
        expiredResult.ErrorReason.Should().Be(revokedResult.ErrorReason);
    }

    [Fact]
    public async Task ValidateToken_Accepted_ReturnsAlreadyUsed_NotInvalid()
    {
        // Arrange — Accepted tokens get a different error to allow redirect to login
        var invitationRepo = new Mock<IInvitationTokenRepository>();
        var userRepo = new Mock<IUserRepository>();

        var acceptedInvitation = CreateInvitationWithStatus(InvitationStatus.Accepted, DateTime.UtcNow.AddDays(5));

        invitationRepo
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(acceptedInvitation);

        var handler = new ValidateInvitationTokenQueryHandler(invitationRepo.Object, userRepo.Object);

        // Act
        var result = await handler.Handle(new ValidateInvitationTokenQuery("accepted-token"), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().Be("already_used",
            "Accepted tokens should return 'already_used' (not 'invalid') to allow login redirect");
    }

    [Fact]
    public async Task ValidateToken_EmptyToken_ReturnsInvalid()
    {
        // Arrange
        var invitationRepo = new Mock<IInvitationTokenRepository>();
        var userRepo = new Mock<IUserRepository>();
        var handler = new ValidateInvitationTokenQueryHandler(invitationRepo.Object, userRepo.Object);

        // Act
        var result = await handler.Handle(new ValidateInvitationTokenQuery(""), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().Be("invalid");
    }

    #endregion

    #region 3. Password Policy Consistency — AcceptInvitation vs ActivateInvitedAccount

    [Theory]
    [InlineData("short1A!")]  // 8 chars - valid for both
    [InlineData("Str0ng!Pass")]
    [InlineData("MyP@ssw0rd")]
    public void PasswordPolicyConsistency_BothValidators_AcceptSameStrongPasswords(string password)
    {
        // Arrange
        var activateValidator = new ActivateInvitedAccountCommandValidator();
        var acceptValidator = new AcceptInvitationCommandValidator();

        var activateCommand = new ActivateInvitedAccountCommand("token", password);
        var acceptCommand = new AcceptInvitationCommand("token", password, password);

        // Act
        var activateResult = activateValidator.Validate(activateCommand);
        var acceptResult = acceptValidator.Validate(acceptCommand);

        // Assert — Both validators should accept the same valid passwords
        activateResult.IsValid.Should().Be(true,
            $"ActivateInvitedAccount should accept '{password}'");
        acceptResult.IsValid.Should().Be(true,
            $"AcceptInvitation should accept '{password}'");
    }

    [Theory]
    [InlineData("123")]        // Too short, no uppercase, no lowercase, no special
    [InlineData("abcdefgh")]   // No uppercase, no digit, no special
    [InlineData("12345678")]   // No uppercase, no lowercase, no special
    [InlineData("short")]      // Too short
    public void PasswordPolicyConsistency_BothValidators_RejectSameWeakPasswords(string password)
    {
        // Arrange
        var activateValidator = new ActivateInvitedAccountCommandValidator();
        var acceptValidator = new AcceptInvitationCommandValidator();

        var activateCommand = new ActivateInvitedAccountCommand("token", password);
        var acceptCommand = new AcceptInvitationCommand("token", password, password);

        // Act
        var activateResult = activateValidator.Validate(activateCommand);
        var acceptResult = acceptValidator.Validate(acceptCommand);

        // Assert — Both validators must reject the same weak passwords
        activateResult.IsValid.Should().BeFalse(
            $"ActivateInvitedAccount should reject '{password}'");
        acceptResult.IsValid.Should().BeFalse(
            $"AcceptInvitation should reject '{password}'");
    }

    [Fact]
    public void PasswordPolicyConsistency_MinLengthIsSame()
    {
        // Arrange — Test boundary: 7 chars should fail, 8 chars with all rules should pass
        var activateValidator = new ActivateInvitedAccountCommandValidator();
        var acceptValidator = new AcceptInvitationCommandValidator();

        var sevenChars = "Aa1!xyz"; // 7 chars
        var eightChars = "Aa1!xyzz"; // 8 chars

        var activate7 = activateValidator.Validate(new ActivateInvitedAccountCommand("t", sevenChars));
        var accept7 = acceptValidator.Validate(new AcceptInvitationCommand("t", sevenChars, sevenChars));

        var activate8 = activateValidator.Validate(new ActivateInvitedAccountCommand("t", eightChars));
        var accept8 = acceptValidator.Validate(new AcceptInvitationCommand("t", eightChars, eightChars));

        // Assert
        activate7.IsValid.Should().BeFalse("7-char password should be rejected by ActivateInvitedAccount");
        accept7.IsValid.Should().BeFalse("7-char password should be rejected by AcceptInvitation");

        activate8.IsValid.Should().BeTrue("8-char password meeting all rules should pass ActivateInvitedAccount");
        accept8.IsValid.Should().BeTrue("8-char password meeting all rules should pass AcceptInvitation");
    }

    [Fact]
    public void PasswordPolicyConsistency_ActivateRequiresSpecialChar_AcceptDoesNot()
    {
        // Document the known difference: ActivateInvitedAccount requires special char, AcceptInvitation does not.
        // This is an intentional policy divergence that should be documented.
        var activateValidator = new ActivateInvitedAccountCommandValidator();
        var acceptValidator = new AcceptInvitationCommandValidator();

        // Password with uppercase, lowercase, digit, but NO special char
        var noSpecialChar = "Abcdefg1";

        var activateResult = activateValidator.Validate(
            new ActivateInvitedAccountCommand("token", noSpecialChar));
        var acceptResult = acceptValidator.Validate(
            new AcceptInvitationCommand("token", noSpecialChar, noSpecialChar));

        // ActivateInvitedAccount requires special character
        activateResult.IsValid.Should().BeFalse(
            "ActivateInvitedAccount requires a special character");
        activateResult.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("special character", StringComparison.OrdinalIgnoreCase));

        // AcceptInvitation does NOT require special character (weaker policy)
        acceptResult.IsValid.Should().BeTrue(
            "AcceptInvitation does not require a special character (known policy divergence)");
    }

    #endregion

    #region 4. XSS in CustomMessage — ProvisionAndInviteUserCommandValidator

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("<svg onload=alert(1)>")]
    public void ProvisionAndInviteValidator_XssInCustomMessage_ValidatorDoesNotRejectButMessageIsLengthConstrained(string xssPayload)
    {
        // The validator constrains CustomMessage to 500 chars max.
        // HTML encoding of the CustomMessage in email templates is the defense-in-depth layer.
        // This test documents that the validator itself does not reject HTML content,
        // so the email service MUST html-encode the custom message.
        var validator = new ProvisionAndInviteUserCommandValidator();
        var command = new ProvisionAndInviteUserCommand(
            Email: "test@example.com",
            DisplayName: "Test User",
            Role: "User",
            Tier: "Free",
            CustomMessage: xssPayload,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: Guid.NewGuid());

        var result = validator.Validate(command);

        // The validator does not reject HTML content itself (length < 500).
        // Defense-in-depth relies on email template encoding.
        // This test documents the current behavior for security review.
        result.Errors.Should().NotContain(e => e.PropertyName == "CustomMessage",
            "Validator currently allows HTML in custom message (defense relies on email template encoding)");
    }

    [Fact]
    public void ProvisionAndInviteValidator_OversizedXssPayload_RejectedByLengthConstraint()
    {
        // Arrange — XSS payload exceeding 500 char limit
        var longXss = "<script>" + new string('x', 500) + "</script>";
        var validator = new ProvisionAndInviteUserCommandValidator();
        var command = new ProvisionAndInviteUserCommand(
            Email: "test@example.com",
            DisplayName: "Test User",
            Role: "User",
            Tier: "Free",
            CustomMessage: longXss,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse("XSS payload exceeding 500 chars should be rejected by length constraint");
        result.Errors.Should().Contain(e =>
            e.PropertyName == "CustomMessage" &&
            e.ErrorMessage.Contains("500", StringComparison.Ordinal));
    }

    #endregion

    #region 5. CSV Injection in DisplayName — ProvisionAndInviteUserCommandValidator

    [Theory]
    [InlineData("=cmd|'/C calc'!A0")]
    [InlineData("+cmd|'/C calc'!A0")]
    [InlineData("-cmd|'/C calc'!A0")]
    [InlineData("@SUM(1+1)*cmd|'/C calc'!A0")]
    [InlineData("=HYPERLINK(\"http://evil.com\")")]
    public void ProvisionAndInviteValidator_CsvInjectionInDisplayName_ValidatorConstrainsLength(string csvPayload)
    {
        // The validator constrains DisplayName to 2-100 chars and requires non-empty.
        // CSV injection payloads that fit within length constraints are accepted by the validator.
        // Defense-in-depth: CSV export code must quote/escape fields.
        var validator = new ProvisionAndInviteUserCommandValidator();
        var command = new ProvisionAndInviteUserCommand(
            Email: "test@example.com",
            DisplayName: csvPayload,
            Role: "User",
            Tier: "Free",
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: Guid.NewGuid());

        var result = validator.Validate(command);

        // Document: CSV injection payloads pass validation if within length constraints.
        // The CSV export layer must handle escaping (prefix with ' or wrap in quotes).
        // This documents the attack surface for security review.
        if (csvPayload.Length >= 2 && csvPayload.Length <= 100)
        {
            result.Errors.Where(e => e.PropertyName == "DisplayName")
                .Should().BeEmpty(
                    $"DisplayName '{csvPayload}' fits length constraints; CSV export must handle escaping");
        }
    }

    [Fact]
    public void ProvisionAndInviteValidator_SingleCharDisplayName_Rejected()
    {
        // Arrange — Single char display name fails minimum length
        var validator = new ProvisionAndInviteUserCommandValidator();
        var command = new ProvisionAndInviteUserCommand(
            Email: "test@example.com",
            DisplayName: "=",
            Role: "User",
            Tier: "Free",
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: Guid.NewGuid());

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse("Single-char display name should fail minimum length rule");
    }

    #endregion

    #region 6. Admin Authorization — Endpoint-Level Gate

    [Fact]
    public void ProvisionAndInviteCommand_RequiresInvitedByUserId_CannotBeEmpty()
    {
        // The handler requires InvitedByUserId (set from admin session in the endpoint).
        // If somehow Guid.Empty is passed, the validator should reject it.
        var validator = new ProvisionAndInviteUserCommandValidator();
        var command = new ProvisionAndInviteUserCommand(
            Email: "test@example.com",
            DisplayName: "Test User",
            Role: "User",
            Tier: "Free",
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse("Empty InvitedByUserId should be rejected");
        result.Errors.Should().Contain(e => e.PropertyName == "InvitedByUserId");
    }

    [Theory]
    [InlineData("InvalidRole")]
    [InlineData("SuperAdmin")]
    [InlineData("Root")]
    public void ProvisionAndInviteValidator_InvalidRole_Rejected(string invalidRole)
    {
        // Only User, Editor, Admin are allowed. SuperAdmin cannot be provisioned via invitation.
        var validator = new ProvisionAndInviteUserCommandValidator();
        var command = new ProvisionAndInviteUserCommand(
            Email: "test@example.com",
            DisplayName: "Test User",
            Role: invalidRole,
            Tier: "Free",
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: Guid.NewGuid());

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse($"Role '{invalidRole}' should be rejected by the validator");
        result.Errors.Should().Contain(e => e.PropertyName == "Role");
    }

    [Theory]
    [InlineData("InvalidTier")]
    [InlineData("Enterprise")]
    [InlineData("")]
    public void ProvisionAndInviteValidator_InvalidTier_Rejected(string invalidTier)
    {
        var validator = new ProvisionAndInviteUserCommandValidator();
        var command = new ProvisionAndInviteUserCommand(
            Email: "test@example.com",
            DisplayName: "Test User",
            Role: "User",
            Tier: invalidTier,
            CustomMessage: null,
            ExpiresInDays: 7,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: Guid.NewGuid());

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse($"Tier '{invalidTier}' should be rejected by the validator");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(31)]
    [InlineData(365)]
    public void ProvisionAndInviteValidator_InvalidExpiresInDays_Rejected(int days)
    {
        var validator = new ProvisionAndInviteUserCommandValidator();
        var command = new ProvisionAndInviteUserCommand(
            Email: "test@example.com",
            DisplayName: "Test User",
            Role: "User",
            Tier: "Free",
            CustomMessage: null,
            ExpiresInDays: days,
            GameSuggestions: new List<GameSuggestionDto>(),
            InvitedByUserId: Guid.NewGuid());

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse($"ExpiresInDays={days} should be rejected (valid range: 1-30)");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates an InvitationToken with the specified status for testing.
    /// Uses the internal RestoreState method to set up the entity.
    /// </summary>
    private static InvitationToken CreateInvitationWithStatus(InvitationStatus status, DateTime expiresAt)
    {
        var invitation = InvitationToken.CreateForHydration(Guid.NewGuid());
        invitation.RestoreState(
            email: "test@example.com",
            role: "User",
            tokenHash: "test-hash-value",
            invitedByUserId: Guid.NewGuid(),
            status: status,
            expiresAt: expiresAt,
            acceptedAt: status == InvitationStatus.Accepted ? DateTime.UtcNow : null,
            acceptedByUserId: status == InvitationStatus.Accepted ? Guid.NewGuid() : null,
            createdAt: DateTime.UtcNow.AddDays(-1),
            revokedAt: status == InvitationStatus.Revoked ? DateTime.UtcNow : null,
            pendingUserId: Guid.NewGuid());

        return invitation;
    }

    #endregion
}
