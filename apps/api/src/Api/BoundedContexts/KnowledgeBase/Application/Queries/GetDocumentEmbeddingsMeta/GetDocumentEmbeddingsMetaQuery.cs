using Api.BoundedContexts.Administration.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;

/// <summary>
/// Issue #1674: returns embeddings metadata for the per-doc admin viewer.
/// Audit Level 1 (read-only forensic trail).
/// </summary>
[AuditableAction("EmbeddingsMetaView", "Document", Level = 1, UserIdSource = AuditUserIdSource.Caller)]
internal sealed record GetDocumentEmbeddingsMetaQuery(Guid DocId)
    : IQuery<DocumentEmbeddingsMetaDto>;
