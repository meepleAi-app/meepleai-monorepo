using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to export multiple rule specifications as a ZIP archive.
/// Maximum 100 rule specs per export.
/// </summary>
internal record ExportRuleSpecsCommand(
    IReadOnlyList<Guid> GameIds
) : ICommand<byte[]>;
