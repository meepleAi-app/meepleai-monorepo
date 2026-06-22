namespace Api.Services.Email;

/// <summary>
/// Abstraction over the raw email transport. Implementations: <see cref="SmtpEmailSender"/>
/// (legacy Gmail/standard SMTP) and <see cref="ResendEmailSender"/> (transactional API).
/// </summary>
/// <remarks>
/// Issue #1629: introduced to decouple <see cref="EmailService"/> from the underlying
/// transport so we can switch providers via configuration (<c>EMAIL_PROVIDER</c>) without
/// touching the template-building code paths.
/// </remarks>
internal interface IEmailSender
{
    /// <summary>
    /// Sends a single email. Throws <see cref="InvalidOperationException"/> on transport
    /// failure (callers decide whether to swallow or rethrow).
    /// </summary>
    Task SendAsync(EmailRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifier of the active transport (used for logging / diagnostics).
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Provider-agnostic email payload. Built by <see cref="EmailService"/> and passed to
/// the active <see cref="IEmailSender"/>.
/// </summary>
internal sealed class EmailRequest
{
    public required string FromEmail { get; init; }
    public required string FromName { get; init; }
    public required string ToEmail { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }

    /// <summary>
    /// Optional plain-text fallback. Falls back to a stripped version of <see cref="HtmlBody"/>
    /// at transport time if null.
    /// </summary>
    public string? TextBody { get; init; }

    /// <summary>
    /// Optional Reply-To header. Useful when the FROM is a no-reply alias but replies
    /// should be routed to a monitored mailbox.
    /// </summary>
    public string? ReplyTo { get; init; }
}
