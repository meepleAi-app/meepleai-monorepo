using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// #4812. The password reset email is written in the language of the user it is for — the person asking for
/// the reset, who is the account's owner — so the service has to hand the stored language to the email.
/// The integration tests of this service need a database server; this one runs on an in-memory context.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4812")]
public sealed class PasswordResetServiceLanguageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    public async Task The_reset_email_goes_out_in_the_language_of_the_user_it_is_for(string language)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.Users.Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "marco@example.test",
            DisplayName = "Marco",
            Role = "User",
            Language = language,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);

        var email = new Mock<IEmailService>();
        var rateLimit = new Mock<IRateLimitService>();
        rateLimit
            .Setup(r => r.CheckRateLimitAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 3, 0));
        var service = new PasswordResetService(
            db,
            email.Object,
            rateLimit.Object,
            new Mock<IPasswordHashingService>().Object,
            NullLogger<PasswordResetService>.Instance);

        (await service.RequestPasswordResetAsync("marco@example.test", Ct)).Should().BeTrue();

        email.Verify(
            e => e.SendPasswordResetEmailAsync(
                "marco@example.test", "Marco", It.IsAny<string>(), language, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
