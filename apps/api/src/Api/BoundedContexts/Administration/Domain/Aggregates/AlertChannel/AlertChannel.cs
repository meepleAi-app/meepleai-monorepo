namespace Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;

/// <summary>
/// AlertChannel aggregate root — one row per channel <see cref="Type"/>
/// (Issue #1840 SP5 F4-C7).
///
/// <para>Each channel is uniquely keyed by its <see cref="AlertChannelType"/>:
/// there is at most one Email config and at most one Slack config per
/// environment. The aggregate carries the channel's transport config (JSON
/// blob keyed by channel type — webhook URL for Slack, SMTP details for
/// Email) plus the most recent test-connection outcome surfaced in the
/// admin Canali drawer.</para>
///
/// <para>RowVersion enables optimistic-concurrency protection for the
/// admin-edit flow: two simultaneous admins editing the same channel will
/// get a 409 ConflictException via <c>DbUpdateConcurrencyException</c>
/// translation in the upsert command handler.</para>
/// </summary>
internal sealed class AlertChannel
{
    public AlertChannelType Type { get; private set; }

    /// <summary>JSON payload (UTF-8) carrying the channel's transport config.
    /// Shape depends on <see cref="Type"/>:
    /// <list type="bullet">
    ///   <item><c>email</c>: {smtpHost, smtpPort, useTls, username, password, fromAddress, toAddresses[]}</item>
    ///   <item><c>slack</c>: {webhookUrl, channel}</item>
    /// </list>
    /// Deliberately kept as raw JSON so the schema can evolve without DB
    /// migrations and so secrets at rest can be re-encrypted in a follow-up.</summary>
    public string ConfigJson { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    /// <summary>Timestamp of the most recent test-connection probe; null
    /// until the admin has clicked "Test Connection" at least once.</summary>
    public DateTime? LastTestedAt { get; private set; }

    /// <summary>"ok" | "error" — null when never tested.</summary>
    public string? LastTestStatus { get; private set; }

    /// <summary>Human-readable diagnostic from the most recent probe.</summary>
    public string? LastTestMessage { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>SQL Server / Postgres optimistic concurrency token.
    /// Repository populates this from <c>xmin</c> via EF's <c>[Timestamp]</c>
    /// equivalent (<see cref="Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.IsRowVersion"/>).</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private AlertChannel() { /* EF Core / reconstitution */ }

    private AlertChannel(
        AlertChannelType type,
        string configJson,
        bool isEnabled,
        DateTime createdAt,
        DateTime updatedAt,
        string? createdBy,
        string? updatedBy)
    {
        Type = type;
        ConfigJson = configJson;
        IsEnabled = isEnabled;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedBy = createdBy;
        UpdatedBy = updatedBy;
    }

    /// <summary>Creates a brand-new channel config (use UpsertCommand). The
    /// caller's identity becomes both <see cref="CreatedBy"/> and the initial
    /// <see cref="UpdatedBy"/>.</summary>
    public static AlertChannel Create(
        AlertChannelType type,
        string configJson,
        bool isEnabled,
        string createdBy)
    {
        ValidateConfigJson(configJson);
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("createdBy is required", nameof(createdBy));

        var now = DateTime.UtcNow;
        return new AlertChannel(type, configJson, isEnabled, now, now, createdBy, createdBy);
    }

    /// <summary>Repository-only reconstitution; preserves stored RowVersion and timestamps.</summary>
    public static AlertChannel Reconstitute(
        AlertChannelType type,
        string configJson,
        bool isEnabled,
        DateTime? lastTestedAt,
        string? lastTestStatus,
        string? lastTestMessage,
        DateTime createdAt,
        DateTime updatedAt,
        string? createdBy,
        string? updatedBy,
        byte[] rowVersion)
    {
        return new AlertChannel(type, configJson, isEnabled, createdAt, updatedAt, createdBy, updatedBy)
        {
            LastTestedAt = lastTestedAt,
            LastTestStatus = lastTestStatus,
            LastTestMessage = lastTestMessage,
            RowVersion = rowVersion ?? Array.Empty<byte>(),
        };
    }

    /// <summary>Replaces the transport config (used by the upsert PUT endpoint).</summary>
    public void UpdateConfig(string configJson, bool isEnabled, string updatedBy)
    {
        ValidateConfigJson(configJson);
        if (string.IsNullOrWhiteSpace(updatedBy)) throw new ArgumentException("updatedBy is required", nameof(updatedBy));

        ConfigJson = configJson;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>Records the outcome of a test-connection probe.</summary>
    public void RecordTestResult(bool success, string message, DateTime probedAt)
    {
        LastTestedAt = probedAt;
        LastTestStatus = success ? "ok" : "error";
        LastTestMessage = string.IsNullOrWhiteSpace(message)
            ? (success ? "Connection OK" : "Unknown error")
            : message;
    }

    private static void ValidateConfigJson(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            throw new ArgumentException("Channel configuration JSON cannot be empty", nameof(configJson));

        // Light validation: ensure it's a JSON object (minimum '{}'). Schema
        // validation per type is the command handler's responsibility — keeping
        // the aggregate transport-agnostic so new channel types can land
        // without rewriting this guard.
        var trimmed = configJson.AsSpan().TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '{')
            throw new ArgumentException("Channel configuration must be a JSON object", nameof(configJson));
    }
}
