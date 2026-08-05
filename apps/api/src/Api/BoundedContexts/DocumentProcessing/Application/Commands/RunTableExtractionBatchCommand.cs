using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #3435 (SP4): runs one batch of the async VLM table-extraction pass. Consumes the SP2 router
/// (<c>GetTableRegionCandidatesQuery</c>) to pick candidate PDFs, then for each pending image region
/// renders + crops the page, calls the smoldocling crop-discriminator (<c>/api/v1/extract-image</c>),
/// and — for tables — persists a retrievable table chunk. Gated by config flag
/// <c>PdfProcessing:TableExtraction:Enabled</c> (default false). <see cref="BatchSize"/> bounds the
/// number of REGIONS processed per run.
/// </summary>
internal sealed record RunTableExtractionBatchCommand(int? BatchSize = null)
    : ICommand<RunTableExtractionBatchResult>;

/// <summary>Outcome of one table-extraction batch. <see cref="Enabled"/> is false when the flag is off.</summary>
internal sealed record RunTableExtractionBatchResult(
    bool Enabled,
    int Processed,
    int Extracted,
    int NotTable,
    int Failed);
