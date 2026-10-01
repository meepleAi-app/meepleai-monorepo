using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Tests for ChatSessionTierLimits. Issue #4913 introduced the type without tests; #3994
/// added them while fixing the role set, because the defect it carried — superadmin falling
/// through to the per-tier lookup — was invisible precisely for lack of coverage.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ChatSessionTierLimitsTests
{
    [Theory]
    [InlineData("anonymous", 10)]
    [InlineData("free", 10)]
    [InlineData("Free", 10)]
    [InlineData("user", 100)]
    [InlineData("normal", 100)]
    [InlineData("premium", 1000)]
    [InlineData("pro", 1000)]
    public void GetLimit_RegularUser_ReturnsTierLimit(string tier, int expected)
    {
        ChatSessionTierLimits.GetLimit(tier, "user").Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("platinum")]
    public void GetLimit_UnknownTier_ReturnsDefault(string? tier)
    {
        ChatSessionTierLimits.GetLimit(tier, "user").Should().Be(ChatSessionTierLimits.DefaultLimit);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("Editor")]
    [InlineData("editor")]
    [InlineData("SuperAdmin")]
    [InlineData("superadmin")]
    public void GetLimit_UnrestrictedRole_ReturnsZero(string role)
    {
        // 0 e' il valore sentinella per «illimitato», non «nessuna sessione consentita».
        ChatSessionTierLimits.GetLimit("free", role).Should().Be(0);
    }

    /// <summary>
    /// #3994: la stessa coppia che colpiva AgentTierLimits. L'account di bootstrap e'
    /// `Role.SuperAdmin` con il `UserTier.Free` di default, quindi era lui a ricevere il
    /// limite del tier gratuito su una decisione pensata per non applicarsi agli admin.
    /// </summary>
    [Fact]
    public void GetLimit_SuperAdminOnFreeTier_IsUnlimited()
    {
        var result = ChatSessionTierLimits.GetLimit("free", "superadmin");

        result.Should().Be(0);
        result.Should().NotBe(ChatSessionTierLimits.MaxSessionsPerTier["free"]);
    }

    [Theory]
    [InlineData("user", false)]
    [InlineData("User", false)]
    // Creator e' escluso per scelta: HasPermission(Role.Editor) e' falso per creator.
    [InlineData("creator", false)]
    [InlineData("editor", true)]
    [InlineData("admin", true)]
    [InlineData("superadmin", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    // Un ruolo ignoto fallisce chiuso: paga il limite del tier.
    [InlineData("wizard", false)]
    public void IsAdminOrEditor_MatchesHasPermissionEditor(string? role, bool expected)
    {
        ChatSessionTierLimits.IsAdminOrEditor(role).Should().Be(expected);
    }

    /// <summary>
    /// I due tipi di limite decidono lo stesso insieme di ruoli. Erano due enumerazioni di
    /// letterali indipendenti e sono divergute dal terzo omonimo
    /// (`ClaimsPrincipalExtensions.IsAdminOrEditor`, che includeva superadmin): questo test
    /// lega le due implementazioni, cosi' una sola non puo' tornare a deviare in silenzio.
    /// </summary>
    [Theory]
    [InlineData("user")]
    [InlineData("creator")]
    [InlineData("editor")]
    [InlineData("admin")]
    [InlineData("superadmin")]
    [InlineData(null)]
    [InlineData("wizard")]
    public void IsAdminOrEditor_AgreesWithAgentTierLimits(string? role)
    {
        ChatSessionTierLimits.IsAdminOrEditor(role)
            .Should().Be(AgentTierLimits.IsAdminOrEditor(role));
    }
}
