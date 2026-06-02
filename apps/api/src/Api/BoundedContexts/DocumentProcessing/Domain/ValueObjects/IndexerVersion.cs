// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersion.cs
namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Identifies a pipeline indexer version. Code-resident registry per design doc D-B
/// (2026-06-01-sp5-admin-kb-fu4-spinouts-design.md §3.2): ≤3 versioni concorrenti, nessuna
/// container infrastructure. Issue #1673.
/// </summary>
/// <remarks>
/// <para>
/// <b>IsSelectable</b>: <c>false</c> per il marker storico <c>v0</c> (pre-versioning,
/// usato dal backfill della migration). <c>true</c> per ogni versione effettivamente
/// invocabile da `/admin/pdfs/{id}/reindex`.
/// </para>
/// </remarks>
internal sealed record IndexerVersion(string Version, string DisplayName, bool IsSelectable);
