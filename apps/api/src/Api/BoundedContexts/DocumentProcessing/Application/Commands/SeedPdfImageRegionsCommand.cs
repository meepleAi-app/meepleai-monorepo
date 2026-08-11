using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #3447 slice: seed image-table regions for a PDF from a raw Unstructured hi_res JSON.
/// Idempotent (replace-by-pdf). Reused by both the single-PDF admin endpoint and the SP1 batch runner
/// (RunImageRegionSeedBatchCommand, #3435) that runs hi_res on its own; only the periodic Quartz
/// trigger for that batch is still deferred (slice 2). Returns the count inserted.
/// </summary>
internal sealed record SeedPdfImageRegionsCommand(Guid PdfId, string HiResJson, double? MinAreaFraction = null) : ICommand<int>;
