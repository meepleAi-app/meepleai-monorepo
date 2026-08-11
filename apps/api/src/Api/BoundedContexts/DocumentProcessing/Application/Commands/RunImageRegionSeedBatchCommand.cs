using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #3435 (SP1): runs one batch of the automatic image-region seed. Selects Ready PDFs that
/// have never been seeded (<c>ImageRegionsSeededAt == null</c>), runs a dedicated hi_res pass
/// per PDF, and persists the Image/FigureCaption regions via <see cref="SeedPdfImageRegionsCommand"/>.
/// Gated by config flag <c>PdfProcessing:ImageRegionSeeding:Enabled</c> (default false).
/// Admin-triggered for now; the periodic Quartz trigger is the next slice.
/// </summary>
internal sealed record RunImageRegionSeedBatchCommand(int? BatchSize = null)
    : ICommand<RunImageRegionSeedBatchResult>;

/// <summary>Outcome of one seed batch. <see cref="Enabled"/> is false when the feature flag is off.</summary>
internal sealed record RunImageRegionSeedBatchResult(
    bool Enabled,
    int Processed,
    int TotalRegionsSeeded,
    int Failed);
