using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Command and Result are related
namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Lists the PDF files under <c>pdfs/</c> in blob storage that no row references, and with
/// <paramref name="DryRun"/> false deletes them (#4514). An admin runs it, first as a dry run.
/// </summary>
internal sealed record CleanupOrphanPdfFilesCommand(bool DryRun) : ICommand<CleanupOrphanPdfFilesResult>;

/// <param name="DryRun">Whether this run only listed.</param>
/// <param name="Scanned">Objects found under <c>pdfs/</c>.</param>
/// <param name="Referenced">Objects some row points at; never touched.</param>
/// <param name="TooRecent">Unreferenced objects younger than the threshold; left alone, an upload may still be writing its row.</param>
/// <param name="OrphanKeys">Unreferenced objects older than the threshold.</param>
/// <param name="Deleted">Orphans deleted; 0 on a dry run.</param>
/// <param name="Errors">What stopped the run or a deletion; never an exception message.</param>
internal sealed record CleanupOrphanPdfFilesResult(
    bool DryRun,
    int Scanned,
    int Referenced,
    int TooRecent,
    IReadOnlyList<string> OrphanKeys,
    int Deleted,
    IReadOnlyList<string> Errors);
