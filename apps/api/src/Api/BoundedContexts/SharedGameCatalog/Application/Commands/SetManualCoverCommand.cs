using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 3a — sets an admin-supplied manual cover from a URL: SSRF-pinned fetch (#3534) →
/// license-gate (whitelist) → WebP re-encode → R2 raw-key put (<c>CoverKeyBuilder.ForManual</c>) →
/// persist on the aggregate with the license attestation.
/// </summary>
internal record SetManualCoverCommand(
    Guid GameId,
    string SourceUrl,
    string License,
    string? Attribution,
    Guid AdminId) : ICommand<ManualCoverResult>;

/// <summary>
/// The persisted manual cover: the suffix-free DB key stored on the game and a presigned URL for the
/// freshly-written R2 object (so the picker can preview it immediately).
/// </summary>
internal record ManualCoverResult(string DbKey, string PresignedUrl);
