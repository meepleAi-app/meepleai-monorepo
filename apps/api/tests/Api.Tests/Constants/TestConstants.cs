namespace Api.Tests.Constants;

/// <summary>
/// Centralized test constants for PDF upload and quota testing.
///
/// IMPORTANT: These values MUST match production configuration to prevent test drift.
/// Source of truth:
/// - Quota limits: Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PdfUploadQuotaService.cs (DefaultQuotas)
/// - File size limits: Api/BoundedContexts/DocumentProcessing/Infrastructure/Configuration/PdfProcessingOptions.cs
///
/// To update: Change production config first, then update these constants, then run TestConfigurationValidator tests.
/// </summary>
public static class PdfUploadTestConstants
{
    /// <summary>
    /// Upload quota limits per user tier (must match PdfUploadQuotaService.DefaultQuotas).
    /// Validation: See TestConfigurationValidator.ValidateQuotaLimits_MatchProductionDefaults
    /// </summary>
    public static class QuotaLimits
    {
        /// <summary>
        /// Free tier quota limits
        /// </summary>
        public static class FreeTier
        {
            /// <summary>
            /// Daily upload limit for free tier users (default: 5)
            /// Source: PdfUploadQuotaService.DefaultQuotas.FreeDailyLimit
            /// </summary>
            public const int DailyLimit = 5;

            /// <summary>
            /// Weekly upload limit for free tier users (default: 20)
            /// Source: PdfUploadQuotaService.DefaultQuotas.FreeWeeklyLimit
            /// </summary>
            public const int WeeklyLimit = 20;
        }

        /// <summary>
        /// Normal tier quota limits
        /// </summary>
        public static class NormalTier
        {
            /// <summary>
            /// Daily upload limit for normal tier users (default: 20)
            /// Source: PdfUploadQuotaService.DefaultQuotas.NormalDailyLimit
            /// </summary>
            public const int DailyLimit = 20;

            /// <summary>
            /// Weekly upload limit for normal tier users (default: 100)
            /// Source: PdfUploadQuotaService.DefaultQuotas.NormalWeeklyLimit
            /// </summary>
            public const int WeeklyLimit = 100;
        }

        /// <summary>
        /// Premium tier quota limits
        /// </summary>
        public static class PremiumTier
        {
            /// <summary>
            /// Daily upload limit for premium tier users (default: 100)
            /// Source: PdfUploadQuotaService.DefaultQuotas.PremiumDailyLimit
            /// </summary>
            public const int DailyLimit = 100;

            /// <summary>
            /// Weekly upload limit for premium tier users (default: 500)
            /// Source: PdfUploadQuotaService.DefaultQuotas.PremiumWeeklyLimit
            /// </summary>
            public const int WeeklyLimit = 500;
        }
    }

    /// <summary>
    /// File size constants for PDF upload testing.
    /// Validation: See TestConfigurationValidator.ValidateFileSizes_MatchProductionDefaults
    /// </summary>
    public static class FileSizes
    {
        /// <summary>
        /// Production maximum file size: 100 MB (104,857,600 bytes)
        /// Source: PdfProcessingOptions.MaxFileSizeBytes default value
        /// </summary>
        public const long ProductionMaxBytes = 104_857_600; // 100 MB

        /// <summary>
        /// Test maximum file size: 10 MB (10,485,760 bytes)
        /// Used in tests for performance/speed. Smaller than production but adequate for testing.
        /// </summary>
        public const long TestMaxBytes = 10 * 1024 * 1024; // 10 MB

        /// <summary>
        /// Small PDF file size for quick tests: 50 KB (51,200 bytes)
        /// </summary>
        public const long SmallPdf = 50 * 1024; // 50 KB

        /// <summary>
        /// Medium PDF file size for standard tests: 1 MB (1,048,576 bytes)
        /// </summary>
        public const long MediumPdf = 1024 * 1024; // 1 MB

        /// <summary>
        /// Large PDF file size for capacity tests: 5 MB (5,242,880 bytes)
        /// </summary>
        public const long LargePdf = 5 * 1024 * 1024; // 5 MB

        /// <summary>
        /// File size just under test maximum: TestMaxBytes - 1 KB
        /// Used to test successful upload near size limit
        /// </summary>
        public const long NearLimit = TestMaxBytes - 1024;

        /// <summary>
        /// File size over test maximum: TestMaxBytes + 1 KB
        /// Used to test size limit enforcement
        /// </summary>
        public const long OverLimit = TestMaxBytes + 1024;

        /// <summary>
        /// Large PDF threshold for temp file strategy: 50 MB (52,428,800 bytes)
        /// Source: PdfProcessingOptions.LargePdfThresholdBytes
        /// </summary>
        public const long LargePdfThresholdBytes = 52_428_800; // 50 MB
    }

    /// <summary>
    /// Common test data values for PDF upload scenarios
    /// </summary>
    public static class TestData
    {
        /// <summary>
        /// Standard test user email for upload tests
        /// </summary>
        public const string TestUserEmail = "test@uploadtest.com";

        /// <summary>
        /// Standard test game name for PDF upload tests
        /// </summary>
        public const string TestGameName = "Test Game for PDF Upload";
    }

    /// <summary>
    /// PDF processing timeout constants.
    /// </summary>
    public static class ProcessingTimeouts
    {
        /// <summary>
        /// VLM processing timeout (120 seconds for SmolDocling/Unstructured operations)
        /// </summary>
        public static readonly TimeSpan VlmProcessing = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Standard extraction timeout (30 seconds)
        /// </summary>
        public static readonly TimeSpan StandardExtraction = TimeSpan.FromSeconds(30);
    }
}

