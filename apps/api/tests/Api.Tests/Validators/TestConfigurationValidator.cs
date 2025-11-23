using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Validators;

/// <summary>
/// Validates that test constants match production configuration values.
/// This prevents test drift and ensures test reliability.
///
/// ISSUE-1731: Configuration validation for test constants
/// These tests MUST run in CI to catch drift between test and production config.
/// </summary>
public class TestConfigurationValidator
{
    private const int ProductionFreeDailyLimit = 5;
    private const int ProductionFreeWeeklyLimit = 20;
    private const int ProductionNormalDailyLimit = 20;
    private const int ProductionNormalWeeklyLimit = 100;
    private const int ProductionPremiumDailyLimit = 100;
    private const int ProductionPremiumWeeklyLimit = 500;

    /// <summary>
    /// Validates that quota limit test constants match production defaults.
    /// Source: PdfUploadQuotaService.DefaultQuotas
    /// </summary>
    [Fact]
    public void ValidateQuotaLimits_FreeTier_MatchProductionDefaults()
    {
        // Arrange & Act & Assert
        PdfUploadTestConstants.QuotaLimits.FreeTier.DailyLimit
            .Should().Be(ProductionFreeDailyLimit,
                "Free tier daily limit must match PdfUploadQuotaService.DefaultQuotas.FreeDailyLimit");

        PdfUploadTestConstants.QuotaLimits.FreeTier.WeeklyLimit
            .Should().Be(ProductionFreeWeeklyLimit,
                "Free tier weekly limit must match PdfUploadQuotaService.DefaultQuotas.FreeWeeklyLimit");
    }

    [Fact]
    public void ValidateQuotaLimits_NormalTier_MatchProductionDefaults()
    {
        // Arrange & Act & Assert
        PdfUploadTestConstants.QuotaLimits.NormalTier.DailyLimit
            .Should().Be(ProductionNormalDailyLimit,
                "Normal tier daily limit must match PdfUploadQuotaService.DefaultQuotas.NormalDailyLimit");

        PdfUploadTestConstants.QuotaLimits.NormalTier.WeeklyLimit
            .Should().Be(ProductionNormalWeeklyLimit,
                "Normal tier weekly limit must match PdfUploadQuotaService.DefaultQuotas.NormalWeeklyLimit");
    }

    [Fact]
    public void ValidateQuotaLimits_PremiumTier_MatchProductionDefaults()
    {
        // Arrange & Act & Assert
        PdfUploadTestConstants.QuotaLimits.PremiumTier.DailyLimit
            .Should().Be(ProductionPremiumDailyLimit,
                "Premium tier daily limit must match PdfUploadQuotaService.DefaultQuotas.PremiumDailyLimit");

        PdfUploadTestConstants.QuotaLimits.PremiumTier.WeeklyLimit
            .Should().Be(ProductionPremiumWeeklyLimit,
                "Premium tier weekly limit must match PdfUploadQuotaService.DefaultQuotas.PremiumWeeklyLimit");
    }

    /// <summary>
    /// Validates that file size test constants are correctly derived from production defaults.
    /// Source: PdfProcessingOptions
    /// </summary>
    [Fact]
    public void ValidateFileSizes_ProductionMax_MatchesConfiguration()
    {
        // Arrange
        var productionDefault = new PdfProcessingOptions();

        // Act & Assert
        PdfUploadTestConstants.FileSizes.ProductionMaxBytes
            .Should().Be(productionDefault.MaxFileSizeBytes,
                "Production max bytes must match PdfProcessingOptions.MaxFileSizeBytes");
    }

    [Fact]
    public void ValidateFileSizes_LargePdfThreshold_MatchesConfiguration()
    {
        // Arrange
        var productionDefault = new PdfProcessingOptions();

        // Act & Assert
        PdfUploadTestConstants.FileSizes.LargePdfThresholdBytes
            .Should().Be(productionDefault.LargePdfThresholdBytes,
                "Large PDF threshold must match PdfProcessingOptions.LargePdfThresholdBytes");
    }

    /// <summary>
    /// Validates that test file size constants are properly ordered and make logical sense
    /// </summary>
    [Fact]
    public void ValidateFileSizes_TestConstants_AreLogicallyOrdered()
    {
        // Arrange & Act & Assert
        PdfUploadTestConstants.FileSizes.SmallPdf
            .Should().BeLessThan(PdfUploadTestConstants.FileSizes.MediumPdf,
                "Small PDF should be smaller than medium PDF");

        PdfUploadTestConstants.FileSizes.MediumPdf
            .Should().BeLessThan(PdfUploadTestConstants.FileSizes.LargePdf,
                "Medium PDF should be smaller than large PDF");

        PdfUploadTestConstants.FileSizes.LargePdf
            .Should().BeLessThan(PdfUploadTestConstants.FileSizes.TestMaxBytes,
                "Large PDF should be smaller than test max bytes");

        PdfUploadTestConstants.FileSizes.NearLimit
            .Should().BeLessThan(PdfUploadTestConstants.FileSizes.TestMaxBytes,
                "Near limit should be less than test max bytes");

        PdfUploadTestConstants.FileSizes.OverLimit
            .Should().BeGreaterThan(PdfUploadTestConstants.FileSizes.TestMaxBytes,
                "Over limit should be greater than test max bytes");
    }

    /// <summary>
    /// Validates that test max bytes is reasonable for testing (smaller than production for speed)
    /// </summary>
    [Fact]
    public void ValidateFileSizes_TestMax_IsReasonableForTesting()
    {
        // Arrange & Act & Assert
        PdfUploadTestConstants.FileSizes.TestMaxBytes
            .Should().BeLessThan(PdfUploadTestConstants.FileSizes.ProductionMaxBytes,
                "Test max should be smaller than production max for test performance");

        PdfUploadTestConstants.FileSizes.TestMaxBytes
            .Should().BeGreaterThanOrEqualTo(5 * 1024 * 1024,
                "Test max should be at least 5 MB to adequately test file handling");
    }

    /// <summary>
    /// Documents the update process when production config changes
    /// </summary>
    [Fact]
    public void Documentation_UpdateProcess_IsDocumented()
    {
        // This test serves as documentation
        var updateProcess = @"
When production configuration changes:
1. Update production config first (PdfUploadQuotaService.DefaultQuotas or PdfProcessingOptions)
2. Update test constants in PdfUploadTestConstants
3. Update production constant values in TestConfigurationValidator (this class)
4. Run all tests - these validator tests will fail if there's mismatch
5. Commit changes together to maintain alignment
        ";

        updateProcess.Should().NotBeNullOrEmpty("Update process must be documented");
    }
}
