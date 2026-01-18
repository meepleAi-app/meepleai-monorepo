namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// DTO for BGG game extracted from PDF
/// </summary>
public sealed record BggGameDto(
    string GameName,
    int BggId
);
