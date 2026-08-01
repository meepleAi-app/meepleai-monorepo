using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #3447 slice: seed image-table regions for a PDF from a raw Unstructured hi_res JSON.
/// Idempotent (replace-by-pdf). Invoked by an admin endpoint; the productionized async trigger
/// that runs hi_res on its own is the full feature (deferred, #3435). Returns the count inserted.
/// </summary>
internal sealed record SeedPdfImageRegionsCommand(Guid PdfId, string HiResJson) : ICommand<int>;
