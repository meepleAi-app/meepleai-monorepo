using System;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

[Trait("Category", TestCategories.Unit)]
public sealed class TermsAcceptanceTests
{
    [Fact]
    public void Create_WithValidInput_SetsAllFields()
    {
        var userId = Guid.NewGuid();

        var acceptance = TermsAcceptance.Create(
            userId, "2026-07-15", TermsAcceptanceContext.Registration, "1.2.3.4", "UA/1.0");

        acceptance.Id.Should().NotBe(Guid.Empty);
        acceptance.UserId.Should().Be(userId);
        acceptance.TermsVersion.Should().Be("2026-07-15");
        acceptance.Context.Should().Be(TermsAcceptanceContext.Registration);
        acceptance.IpAddress.Should().Be("1.2.3.4");
        acceptance.UserAgent.Should().Be("UA/1.0");
        acceptance.AcceptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        acceptance.CreatedAt.Should().Be(acceptance.AcceptedAt);
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        var act = () => TermsAcceptance.Create(
            Guid.Empty, "2026-07-15", TermsAcceptanceContext.Registration);
        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankVersion_Throws(string? version)
    {
        var act = () => TermsAcceptance.Create(
            Guid.NewGuid(), version!, TermsAcceptanceContext.ReConsent);
        act.Should().Throw<ArgumentException>().WithParameterName("termsVersion");
    }

    [Fact]
    public void Create_AllowsNullAuditFields()
    {
        var acceptance = TermsAcceptance.Create(
            Guid.NewGuid(), "2026-07-15", TermsAcceptanceContext.ReConsent);
        acceptance.IpAddress.Should().BeNull();
        acceptance.UserAgent.Should().BeNull();
    }
}
