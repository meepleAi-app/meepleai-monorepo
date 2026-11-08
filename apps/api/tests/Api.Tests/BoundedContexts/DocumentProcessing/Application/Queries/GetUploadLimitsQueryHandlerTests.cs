using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #4449: the limit the web's upload zones show is the caller's effective one — the smaller of the
/// single PDF limit and their plan's PDF size, which is a commercial cap under it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class GetUploadLimitsQueryHandlerTests
{
    private const long Limit = 104_857_600; // 100 MiB

    private static async Task<UploadLimitsDto> LimitsFor(TierLimits plan)
    {
        var tiers = new Mock<ITierEnforcementService>();
        tiers.Setup(t => t.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        var handler = new GetUploadLimitsQueryHandler(tiers.Object, Options.Create(new PdfProcessingOptions()));

        return await handler.Handle(new GetUploadLimitsQuery(Guid.NewGuid()), CancellationToken.None);
    }

    [Fact]
    public async Task AdminsAndEditors_GetTheSingleLimit_NotTheirPlansFiveHundredMegabytes()
    {
        (await LimitsFor(TierLimits.Unlimited)).MaxFileSizeBytes.Should().Be(Limit);
    }

    [Fact]
    public async Task APlanBelowTheLimit_KeepsItsOwnCap()
    {
        (await LimitsFor(TierLimits.FreeTier)).MaxFileSizeBytes.Should().Be(50L * 1024 * 1024);
    }

    [Fact]
    public async Task APlanAboveTheLimit_IsCappedByIt()
    {
        var oldPremium = TierLimits.PremiumTier with { MaxPdfSizeBytes = 200L * 1024 * 1024 };

        (await LimitsFor(oldPremium)).MaxFileSizeBytes.Should().Be(Limit);
    }

    [Fact]
    public async Task ConcurrentChunkedUploads_AreTheChunkedInitLimit()
    {
        (await LimitsFor(TierLimits.FreeTier)).MaxConcurrentChunkedUploads.Should().Be(3);
    }
}
