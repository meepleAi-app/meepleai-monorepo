namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Configuration for PII detection behavior.
/// Issue #5510: Configurable PII detection types and enable/disable.
/// </summary>
public sealed class PiiDetectorOptions
{
    public const string SectionName = "Gdpr:PiiDetection";

    /// <summary>
    /// Master toggle for PII detection. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Detect email addresses. Default: true.
    /// </summary>
    public bool DetectEmails { get; set; } = true;

    /// <summary>
    /// Detect phone numbers (Italian and international). Default: true.
    /// </summary>
    public bool DetectPhones { get; set; } = true;

    /// <summary>
    /// Detect Italian fiscal codes (codice fiscale). Default: true.
    /// </summary>
    public bool DetectFiscalCodes { get; set; } = true;

    /// <summary>
    /// Detect credit card numbers. Default: true.
    /// </summary>
    public bool DetectCreditCards { get; set; } = true;
}
